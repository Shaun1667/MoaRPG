using System;

namespace Moa.Core.Combat
{
    /// <summary>
    /// 피해 공식 (2-1): 공격력 × 배율 → 방어 반영 → 정수 피해.
    /// 방어 반영 = × (1 − 방어 ÷ (방어 + 방어 상수)) — 방어가 방어 상수와 같으면 절반. 방어 상수는 CombatTuning (BALANCE 7번).
    /// 반올림은 0.5에서 올림(AwayFromZero), 맞으면 최소 1. 속성 · 치명타 없음 (치명타는 나중에 Combat 난수 스트림으로).
    /// </summary>
    public static class DamageMath
    {
        /// <summary>방어로 줄어드는 비율 (0 ~ 1 미만).</summary>
        public static float DefenseReduction(float defense, float defenseConstant)
        {
            if (defense <= 0f) return 0f;
            float k = defenseConstant > 0f ? defenseConstant : 0f;
            return defense / (defense + k);
        }

        /// <summary>방어 반영 전 피해 (실수).</summary>
        public static float Raw(float attack, float multiplier) => attack * multiplier;

        /// <summary>최종 정수 피해. 공격력이나 배율이 0 이하면 0.</summary>
        public static int Compute(float attack, float multiplier, float defense, float defenseConstant)
        {
            float raw = Raw(attack, multiplier);
            if (raw <= 0f) return 0;
            double value = raw * (1.0 - DefenseReduction(defense, defenseConstant));
            int result = (int)Math.Round(value, MidpointRounding.AwayFromZero);
            return result < 1 ? 1 : result;
        }
    }
}
