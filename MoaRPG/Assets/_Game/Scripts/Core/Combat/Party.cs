namespace Moa.Core.Combat
{
    /// <summary>파티 교체 수치 (PartyTuning 에셋 → PlayerCombat이 넘긴다, BALANCE 12번).</summary>
    public sealed class PartyRules
    {
        public PartyRules(float switchCooldown, float benchEnergyRatio)
        {
            SwitchCooldown = switchCooldown > 0f ? switchCooldown : 0f;
            BenchEnergyRatio = benchEnergyRatio < 0f ? 0f : benchEnergyRatio > 1f ? 1f : benchEnergyRatio;
        }

        /// <summary>교체한 순간부터 다음 교체까지 (초, 원신 1초).</summary>
        public float SwitchCooldown { get; }
        /// <summary>처치 에너지를 대기 캐릭터가 받는 비율 (0 ~ 1, 현재 캐릭터는 그대로 1).</summary>
        public float BenchEnergyRatio { get; }
    }

    /// <summary>교체가 받아지지 않은 까닭 (끝에만 추가).</summary>
    public enum SwitchBlock
    {
        None,
        /// <summary>그 칸에 캐릭터가 없다.</summary>
        NoMember,
        /// <summary>이미 그 캐릭터다.</summary>
        SameMember,
        Cooldown,
        /// <summary>궁극기 중 (원신: 궁극기 연출 중에는 못 바꾼다).</summary>
        Burst,
        /// <summary>낙하 공격으로 떨어지는 중.</summary>
        Plunge,
        Gliding,
        /// <summary>그 칸 캐릭터가 쓰러져 있다 (3-1).</summary>
        Downed,
    }

    /// <summary>
    /// 파티 4인 교체 순수 규칙 (2-6). 캐릭터마다 공격 조정(PlayerAttack — 콤보 · 스킬 쿨 · 궁극기 쿨 · 에너지)을 따로 들고, 한 명만 필드에 있다.
    /// - 교체(<see cref="TrySwitch"/>): 쿨(SwitchCooldown)이 끝났고 궁극기 · 낙하(떨어지는 중) · 활공 중이 아니면 바꾼다.
    ///   지금 캐릭터의 공격 · 스킬(누르는 중 포함) · 강공격 · 낙하 착지 경직은 끊는다(AttackEnd.Switch, 원신처럼 끊고 바꾼다).
    ///   회피 · 등반 · 공중은 막지 않는다 — 이동 상태는 몸 하나(PlayerMotor)라 그대로 이어진다.
    /// - 대기 캐릭터도 스킬 · 궁극기 쿨이 돈다(<see cref="Advance"/>). 현재 캐릭터는 PlayerAttack.Step이 진행하므로 여기서는 건드리지 않는다.
    /// - 처치 에너지(<see cref="GainDefeatEnergy"/>): 현재 캐릭터는 그대로, 대기 캐릭터는 × BenchEnergyRatio.
    /// 스태미나는 파티 공용(PlayerMotor 하나), 캐릭터 몸 색 · 화면은 Unity 쪽.
    /// </summary>
    public sealed class Party
    {
        const float Epsilon = 1e-5f;
        readonly PlayerAttack[] _members;
        bool[] _downed = new bool[0]; // 3-1: 쓰러진 칸 (PartyVitals가 정한다)

        /// <summary>members: 칸 순서대로 (null 칸 없이, 1명 이상). 처음 현재 캐릭터는 0번.</summary>
        public Party(PartyRules rules, PlayerAttack[] members)
        {
            Rules = rules ?? new PartyRules(0f, 0f);
            _members = members != null ? (PlayerAttack[])members.Clone() : new PlayerAttack[0];
            for (int i = 0; i < _members.Length; i++)
                if (_members[i] == null) throw new System.ArgumentException("party member " + i + " is null");
            if (_members.Length == 0) throw new System.ArgumentException("party needs at least one member");
            _downed = new bool[_members.Length];
        }

        public PartyRules Rules { get; }
        public int Count => _members.Length;
        public int ActiveIndex { get; private set; }
        public PlayerAttack Active => _members[ActiveIndex];
        public PlayerAttack Member(int index) => _members[index];
        /// <summary>다음 교체까지 남은 초.</summary>
        public float CooldownLeft { get; private set; }
        public bool Ready => CooldownLeft <= 0f;
        /// <summary>교체한 수 · 마지막으로 나간 칸 (-1 = 아직 없음) · 마지막 막힌 까닭 (디버그 · 확인용).</summary>
        public int SwitchCount { get; private set; }
        public int LastFrom { get; private set; } = -1;
        public SwitchBlock LastBlock { get; private set; }
        /// <summary>교체로 끊은 공격 모드 (마지막 교체, None = 끊은 것 없음).</summary>
        public AttackMode LastInterrupted { get; private set; }

        public SwitchBlock Check(int index, bool gliding)
        {
            if (index < 0 || index >= _members.Length) return SwitchBlock.NoMember;
            if (index == ActiveIndex) return SwitchBlock.SameMember;
            if (_downed[index]) return SwitchBlock.Downed;
            if (Active.Burst.Active) return SwitchBlock.Burst;
            if (Active.Plunge.Falling) return SwitchBlock.Plunge;
            if (gliding) return SwitchBlock.Gliding;
            if (CooldownLeft > 0f) return SwitchBlock.Cooldown;
            return SwitchBlock.None;
        }

        /// <summary>index 칸으로 바꾼다. 받아지면 true (안 되면 까닭은 LastBlock). 같은 스텝의 공격 진행 전에 부른다.</summary>
        public bool TrySwitch(int index, bool gliding)
        {
            LastBlock = Check(index, gliding);
            if (LastBlock != SwitchBlock.None) return false;
            LastInterrupted = Active.Mode;
            Active.Interrupt(AttackEnd.Switch);
            LastFrom = ActiveIndex;
            ActiveIndex = index;
            Active.Cancel(); // 들어오는 캐릭터는 처음 자세 (쿨 · 에너지는 그대로)
            CooldownLeft = Rules.SwitchCooldown;
            SwitchCount++;
            return true;
        }

        /// <summary>한 고정 스텝: 교체 쿨 · 대기 캐릭터의 스킬 · 궁극기 쿨. 현재 캐릭터의 공격 진행(PlayerAttack.Step) 뒤에 부른다.</summary>
        public void Advance(float dt)
        {
            if (CooldownLeft > 0f)
            {
                CooldownLeft -= dt;
                if (CooldownLeft <= Epsilon) CooldownLeft = 0f;
            }
            for (int i = 0; i < _members.Length; i++)
                if (i != ActiveIndex) _members[i].AdvanceBench(dt);
        }

        /// <summary>
        /// 적 처치 에너지: 현재 캐릭터는 amount 그대로(궁극기 중이면 0 — PlayerAttack.GainEnergy), 대기 캐릭터는 amount × BenchEnergyRatio.
        /// 현재 캐릭터가 실제로 받은 양을 돌려주고, 대기 캐릭터가 받은 합은 bench.
        /// </summary>
        public float GainDefeatEnergy(float amount, out float bench)
        {
            bench = 0f;
            if (amount <= 0f) return 0f;
            float active = Active.GainEnergy(amount);
            float share = amount * Rules.BenchEnergyRatio;
            for (int i = 0; i < _members.Length; i++)
                if (i != ActiveIndex) bench += _members[i].GainEnergy(share);
            return active;
        }

        /// <summary>그 칸이 쓰러져 있는지 (3-1).</summary>
        public bool IsDowned(int index) => index >= 0 && index < _downed.Length && _downed[index];

        /// <summary>쓰러짐 표시 (3-1, PartyVitals가 부른다). 쓰러진 칸으로는 교체할 수 없다(SwitchBlock.Downed).</summary>
        public void SetDowned(int index, bool downed)
        {
            if (index >= 0 && index < _downed.Length) _downed[index] = downed;
        }

        /// <summary>
        /// 강제 교체 (3-1): 필드 캐릭터가 쓰러졌을 때 다음 캐릭터로. 교체 쿨 · 궁극기 · 낙하 · 활공 막힘을 보지 않고,
        /// 새 쿨도 시작하지 않는다(남은 쿨은 그대로 돈다). 지금 캐릭터의 공격은 끊는다. 바뀌었으면 true.
        /// </summary>
        public bool ForceSwitch(int index)
        {
            if (index < 0 || index >= _members.Length || index == ActiveIndex) return false;
            LastInterrupted = Active.Mode;
            Active.Interrupt(AttackEnd.Switch);
            LastFrom = ActiveIndex;
            ActiveIndex = index;
            Active.Cancel();
            SwitchCount++;
            LastBlock = SwitchBlock.None;
            return true;
        }

        /// <summary>[상태 변경] 교체 쿨을 없앤다.</summary>
        public void ResetCooldown() => CooldownLeft = 0f;

        /// <summary>[상태 변경] 판을 다시 시작할 때 — 0번이 현재, 쿨 0 (쿨 · 에너지 · 기록은 각 캐릭터가 그대로).</summary>
        public void Reset()
        {
            Active.Interrupt(AttackEnd.Cancelled);
            ActiveIndex = 0;
            Active.Cancel();
            CooldownLeft = 0f;
        }
    }
}
