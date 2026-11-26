namespace Moa.Core.Combat
{
    /// <summary>
    /// 스킬 직후 교체 강화 한 캐릭터 몫의 수치 (3-2, SkillTuning 에셋 → PlayerCombat이 넘긴다, BALANCE 14번).
    /// 이 캐릭터가 스킬(E)을 쓴 뒤 PartyRules.HandoffWindow 안에 교체하면, 들어온 캐릭터가 Duration 동안 받는다.
    /// 캐릭터별 분기 코드 없음 — 버프의 차이는 이 수치뿐.
    /// </summary>
    public sealed class HandoffRules
    {
        public static readonly HandoffRules None = new HandoffRules(0f, 0f, 0, 0f, 0f);

        public HandoffRules(float damageBonus, float poiseBonus, int shield, float energy, float duration)
        {
            DamageBonus = damageBonus > 0f ? damageBonus : 0f;
            PoiseBonus = poiseBonus > 0f ? poiseBonus : 0f;
            Shield = shield > 0 ? shield : 0;
            Energy = energy > 0f ? energy : 0f;
            Duration = duration > 0f ? duration : 0f;
        }

        /// <summary>주는 피해 + 이 비율 (0.25 = +25%).</summary>
        public float DamageBonus { get; }
        /// <summary>주는 경직 + 이 비율.</summary>
        public float PoiseBonus { get; }
        /// <summary>보호막 (받는 피해를 먼저 이만큼 막는다, 버프가 끝나면 사라진다).</summary>
        public int Shield { get; }
        /// <summary>들어온 캐릭터가 교체 순간 한 번 받는 궁극기 에너지.</summary>
        public float Energy { get; }
        /// <summary>버프가 이어지는 시간 (초). 0이면 지속 효과 없음(에너지만).</summary>
        public float Duration { get; }

        public bool HasEffect => Energy > 0f || (Duration > 0f && (DamageBonus > 0f || PoiseBonus > 0f || Shield > 0));
    }

    /// <summary>버프가 끝난 까닭 (끝에만 추가, 3-2).</summary>
    public enum HandoffEnd
    {
        None,
        /// <summary>시간이 다 됐다.</summary>
        Expired,
        /// <summary>받은 캐릭터가 필드에서 나갔다 (교체 · 쓰러짐).</summary>
        Left,
        /// <summary>다른 버프로 바뀌었다.</summary>
        Replaced,
        /// <summary>[상태 변경] 디버그 · 판 다시 시작.</summary>
        Cleared,
    }

    /// <summary>
    /// 지금 필드 캐릭터에게 걸린 교체 강화 (3-2, Party가 하나만 든다). 받은 캐릭터가 필드에 있는 동안만 — 나가면 끝난다.
    /// </summary>
    public sealed class HandoffBuff
    {
        const float Epsilon = 1e-5f;

        public HandoffRules Rules { get; private set; } = HandoffRules.None;
        /// <summary>준 칸 · 받은 칸 (-1 = 없음).</summary>
        public int From { get; private set; } = -1;
        public int To { get; private set; } = -1;
        public float TimeLeft { get; private set; }
        public float Duration => Rules.Duration;
        public int ShieldLeft { get; private set; }
        public bool Active => TimeLeft > 0f;
        public HandoffEnd LastEnd { get; private set; }
        /// <summary>보호막이 막은 합 (디버그 · 확인용).</summary>
        public int AbsorbedTotal { get; private set; }

        /// <summary>to 칸이 필드에 있을 때 주는 피해 배율 (버프 없으면 정확히 1).</summary>
        public float DamageMultiplier(int active) => Active && active == To && Rules.DamageBonus > 0f ? 1f + Rules.DamageBonus : 1f;
        public float PoiseMultiplier(int active) => Active && active == To && Rules.PoiseBonus > 0f ? 1f + Rules.PoiseBonus : 1f;

        /// <summary>버프를 건다 (있던 것은 Replaced로 끝). 지속 시간이 0이면 걸지 않는다.</summary>
        public void Start(HandoffRules rules, int from, int to)
        {
            if (Active) End(HandoffEnd.Replaced);
            if (rules == null || rules.Duration <= 0f) return;
            Rules = rules;
            From = from;
            To = to;
            TimeLeft = rules.Duration;
            ShieldLeft = rules.Shield;
            LastEnd = HandoffEnd.None;
        }

        public void End(HandoffEnd reason)
        {
            if (!Active) return;
            TimeLeft = 0f;
            ShieldLeft = 0;
            LastEnd = reason;
        }

        /// <summary>한 고정 스텝: 시간이 줄고 다 되면 끝 (보호막도 사라진다).</summary>
        public void Advance(float dt)
        {
            if (!Active) return;
            TimeLeft -= dt;
            if (TimeLeft <= Epsilon) End(HandoffEnd.Expired);
        }

        /// <summary>보호막이 damage에서 막은 양 (to 칸이 필드에 있을 때만). 막은 만큼 보호막이 준다.</summary>
        public int Absorb(int active, int damage)
        {
            if (!Active || active != To || ShieldLeft <= 0 || damage <= 0) return 0;
            int a = damage < ShieldLeft ? damage : ShieldLeft;
            ShieldLeft -= a;
            AbsorbedTotal += a;
            return a;
        }
    }
}
