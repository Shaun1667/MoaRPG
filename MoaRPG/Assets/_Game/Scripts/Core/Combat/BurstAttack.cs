using Moa.Core.Ids;

namespace Moa.Core.Combat
{
    /// <summary>궁극기(Q) 수치 (SkillTuning 에셋 → PlayerCombat이 넘긴다, BALANCE 10번). 베기 수 = 표의 줄 수.</summary>
    public sealed class BurstRules
    {
        static readonly ComboStepRules[] Empty = new ComboStepRules[0];
        readonly ComboStepRules[] _slashes;

        public BurstRules(ComboStepRules[] slashes, float energyCost, float cooldown, bool invulnerable)
        {
            _slashes = slashes != null ? (ComboStepRules[])slashes.Clone() : Empty;
            EnergyCost = energyCost;
            Cooldown = cooldown;
            Invulnerable = invulnerable;
        }

        public int SlashCount => _slashes.Length;
        public ComboStepRules Slash(int index) => _slashes[index];
        /// <summary>쓰는 에너지 = 에너지 최대 (가득 차야 쓴다).</summary>
        public float EnergyCost { get; }
        /// <summary>쓴 순간부터 다시 쓸 수 있기까지 (초).</summary>
        public float Cooldown { get; }
        /// <summary>쓰는 동안 내내 무적인지.</summary>
        public bool Invulnerable { get; }
        internal ComboStepRules[] Slashes => _slashes;

        public float Total
        {
            get { float t = 0f; foreach (var s in _slashes) t += s.Total; return t; }
        }
    }

    /// <summary>궁극기가 나가지 않은 까닭 (끝에만 추가).</summary>
    public enum BurstBlock
    {
        None,
        NoSlashes,
        Climbing,
        Gliding,
        Dodging,
        Airborne,
        /// <summary>궁극기 · 낙하 공격 중.</summary>
        Busy,
        Cooldown,
        NoEnergy,
    }

    /// <summary>
    /// 궁극기(Q) 순수 규칙 (2-4): 에너지가 가득(EnergyCost)이고 쿨타임이 끝났으면 에너지를 모두 쓰고 베기 표를 차례로.
    /// 쓰는 동안 Invulnerable이면 무적, 회피 · 점프로 끊기지 않는다(입력을 버리는 것은 PlayerMotor). 쿨타임은 쓴 순간부터.
    /// 콤보 · 강공격 · 스킬을 끊는 것은 PlayerAttack, 겹침 검사는 Unity 쪽.
    /// </summary>
    public sealed class BurstAttack
    {
        const float Epsilon = 1e-5f;
        readonly BurstRules _rules;
        readonly SlashSequence _seq = new SlashSequence();

        public BurstAttack(BurstRules rules) { _rules = rules ?? new BurstRules(null, 0f, 0f, false); }

        public BurstRules Rules => _rules;
        public bool Active => _seq.Active;
        /// <summary>지금 무적인지 (쓰는 중 + Invulnerable).</summary>
        public bool Invulnerable => _seq.Active && _rules.Invulnerable;
        public ComboPhase Phase => _seq.Phase;
        public int SlashIndex => _seq.SlashIndex;
        public float Elapsed => _seq.Elapsed;
        public ComboStepRules CurrentSlash => _seq.CurrentSlash;
        public int HitsThisSlash => _seq.HitsThisSlash;
        public float CooldownLeft { get; private set; }
        public bool Ready => CooldownLeft <= 0f;
        public int Count { get; private set; }
        public BurstBlock LastBlock { get; private set; }
        public AttackEnd LastEnd { get; private set; }

        public BurstBlock Check(AttackContext ctx, Energy energy)
        {
            if (_rules.SlashCount == 0) return BurstBlock.NoSlashes;
            if (ctx.Climbing) return BurstBlock.Climbing;
            if (ctx.Gliding) return BurstBlock.Gliding;
            if (ctx.Dodging) return BurstBlock.Dodging;
            if (!ctx.Grounded) return BurstBlock.Airborne;
            if (Active) return BurstBlock.Busy;
            if (CooldownLeft > 0f) return BurstBlock.Cooldown;
            if (energy == null || energy.Current < _rules.EnergyCost - 1e-4f) return BurstBlock.NoEnergy;
            return BurstBlock.None;
        }

        /// <summary>조건이 되면 에너지를 쓰고 첫 베기를 시작한다. 안 되면 false (에너지 그대로, 까닭은 LastBlock).</summary>
        public bool TryStart(AttackContext ctx, Energy energy)
        {
            LastBlock = Check(ctx, energy);
            if (LastBlock != BurstBlock.None) return false;
            if (!energy.TrySpend(_rules.EnergyCost)) { LastBlock = BurstBlock.NoEnergy; return false; }
            Count++;
            CooldownLeft = _rules.Cooldown;
            LastEnd = AttackEnd.None;
            _seq.Start(_rules.Slashes);
            return true;
        }

        /// <summary>한 고정 스텝 진행: 쿨타임 · 베기.</summary>
        public ComboTick Advance(float dt)
        {
            if (CooldownLeft > 0f)
            {
                CooldownLeft -= dt;
                if (CooldownLeft <= Epsilon) CooldownLeft = 0f;
            }
            bool was = _seq.Active;
            ComboTick t = _seq.Advance(dt);
            if (was && !_seq.Active) LastEnd = _seq.LastEnd;
            return t;
        }

        public bool TryHit(EntityId target) => _seq.TryHit(target);

        /// <summary>다른 까닭으로 받지 않았다 (PlayerAttack이 기록, 예: 낙하 중 Busy).</summary>
        public void Refuse(BurstBlock reason) => LastBlock = reason;

        /// <summary>[상태 변경] 끊는다 (순간이동 · 판 다시 시작만 — 회피 · 점프로는 끊기지 않는다). 쿨타임은 그대로.</summary>
        public void Cancel()
        {
            if (_seq.Interrupt(AttackEnd.Cancelled)) LastEnd = AttackEnd.Cancelled;
        }

        /// <summary>[상태 변경] 쿨타임을 없앤다.</summary>
        public void ResetCooldown() => CooldownLeft = 0f;
    }
}
