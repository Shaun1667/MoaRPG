using System;
using Moa.Core.Combat;
using UnityEngine;

namespace Moa.Unity.Data
{
    /// <summary>
    /// 무기 하나의 수치 (2-1). 에셋 Assets/_Game/Data/Weapons/&lt;무기&gt;.asset, 값은 BALANCE.md 6번.
    /// 새 무기 = 이 에셋 하나 (콤보 단계 수 · 배율 · 시간 · 내딛기 · 판정 범위가 모두 여기) — 무기 종류별 분기 코드 없음.
    /// 코드에는 기본값을 두지 않는다 (CLAUDE.md 코딩 규칙).
    /// </summary>
    [CreateAssetMenu(menuName = "MoaRPG/Data/Weapon Tuning", fileName = "WeaponTuning")]
    public sealed class WeaponTuning : ScriptableObject
    {
        [Serializable]
        public struct Step
        {
            [Tooltip("Damage multiplier (attack x this)")]
            public float multiplier;
            [Tooltip("s before the hit window")]
            public float windup;
            [Tooltip("s the hit window lasts")]
            public float active;
            [Tooltip("s after the hit window")]
            public float recovery;
            [Tooltip("s from the step start; a press after this queues the next step (earlier presses are dropped)")]
            public float inputOpen;
            [Tooltip("m moved forward during windup + hit window")]
            public float lunge;
        }

        [Tooltip("Weapon attack (character attack is added later)")]
        [SerializeField] float baseAttack;
        [Tooltip("Normal attack combo, one row per hit")]
        [SerializeField] Step[] steps;
        [Tooltip("s after a step ends (no queued press) before the combo goes back to hit 1")]
        [SerializeField] float comboResetTime;
        [Tooltip("Move speed x this while attacking (0 = stand still)")]
        [SerializeField] float attackMoveFactor;

        [Header("Hit area (forward fan)")]
        [Tooltip("m from the body center to the target surface")]
        [SerializeField] float hitRange;
        [Tooltip("Full fan angle, degrees")]
        [SerializeField] float hitAngle;
        [Tooltip("Fan center height above the feet (m)")]
        [SerializeField] float hitHeight;

        public float BaseAttack => baseAttack;
        public int StepCount => steps != null ? steps.Length : 0;
        public Step GetStep(int i) => steps[i];
        public float ComboResetTime => comboResetTime;
        public float AttackMoveFactor => attackMoveFactor;
        public float HitRange => hitRange;
        public float HitAngle => hitAngle;
        public float HitHeight => hitHeight;

        public ComboRules ToRules()
        {
            var list = new ComboStepRules[StepCount];
            for (int i = 0; i < list.Length; i++)
            {
                Step s = steps[i];
                list[i] = new ComboStepRules(s.multiplier, s.windup, s.active, s.recovery, s.inputOpen, s.lunge);
            }
            return new ComboRules(list, comboResetTime);
        }
    }
}
