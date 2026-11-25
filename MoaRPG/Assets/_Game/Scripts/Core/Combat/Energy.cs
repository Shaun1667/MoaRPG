namespace Moa.Core.Combat
{
    /// <summary>
    /// 궁극기(Q) 에너지 (2-4). 최대 = 궁극기 비용(BurstRules.EnergyCost) — 가득 차야 궁극기를 쓴다.
    /// 채우는 쪽: 스킬(E)이 처음 맞힌 순간 한 번(SkillFormRules.Energy) · 적 처치(적 표의 처치 에너지). 궁극기 중에는 차지 않는다(PlayerAttack.GainEnergy).
    /// 속성이 없으므로 원소 입자 대신 바로 채운다 (핵심 설계 규칙 6).
    /// </summary>
    public sealed class Energy
    {
        const float Epsilon = 1e-4f;

        public Energy(float max) { Max = max > 0f ? max : 0f; }

        public float Max { get; }
        public float Current { get; private set; }
        public float Ratio => Max > 0f ? Current / Max : 0f;
        public bool Full => Max > 0f && Current >= Max - Epsilon;
        /// <summary>지금까지 실제로 찬 양 · 채운 수 (디버그 · 확인용).</summary>
        public float TotalGained { get; private set; }
        public int GainCount { get; private set; }

        /// <summary>amount만큼 채운다 (최대에서 멈춘다). 실제로 찬 양.</summary>
        public float Add(float amount)
        {
            if (amount <= 0f || Max <= 0f) return 0f;
            float before = Current;
            Current = Current + amount >= Max - Epsilon ? Max : Current + amount;
            float gained = Current - before;
            if (gained > 0f) { TotalGained += gained; GainCount++; }
            return gained;
        }

        /// <summary>amount가 있으면 쓰고 true, 모자라면 그대로 false.</summary>
        public bool TrySpend(float amount)
        {
            if (Current < amount - Epsilon) return false;
            Current = Current - amount > 0f ? Current - amount : 0f;
            return true;
        }

        /// <summary>[상태 변경] 가득 채운다.</summary>
        public void Fill() => Current = Max;

        /// <summary>[상태 변경] 값을 정한다 (0 ~ 최대).</summary>
        public void Set(float value) => Current = value < 0f ? 0f : value > Max ? Max : value;
    }
}
