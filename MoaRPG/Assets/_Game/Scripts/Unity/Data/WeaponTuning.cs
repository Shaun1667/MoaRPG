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
            [Tooltip("Poise added to the target per hit (2-3, groggy gauge)")]
            public float poise;
        }

        [Tooltip("Weapon base attack; the character base attack is added to it (CharacterData)")]
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

        [Header("Charged attack (2-2, hold Attack)")]
        [Tooltip("Stamina spent at once; not enough = no charged attack")]
        [SerializeField] float chargedStaminaCost;
        [Tooltip("Charged attack slashes, one row per slash (inputOpen is not used)")]
        [SerializeField] Step[] chargedSlashes;
        [Tooltip("m from the body center to the target surface")]
        [SerializeField] float chargedHitRange;
        [Tooltip("Full fan angle, degrees")]
        [SerializeField] float chargedHitAngle;

        [Header("Plunge attack (2-2, Attack in the air)")]
        [Tooltip("Damage multiplier when the fall is lower than CombatTuning plungeHighHeight")]
        [SerializeField] float plungeLowMultiplier;
        [Tooltip("Damage multiplier when the fall is at least CombatTuning plungeHighHeight")]
        [SerializeField] float plungeHighMultiplier;
        [Tooltip("Landing hit radius (m) around feet + hitHeight, all directions")]
        [SerializeField] float plungeRange;
        [Tooltip("Poise added per target by a low plunge (2-3)")]
        [SerializeField] float plungeLowPoise;
        [Tooltip("Poise added per target by a high plunge (2-3)")]
        [SerializeField] float plungeHighPoise;

        public float BaseAttack => baseAttack;
        public int StepCount => steps != null ? steps.Length : 0;
        public Step GetStep(int i) => steps[i];
        public float ComboResetTime => comboResetTime;
        public float AttackMoveFactor => attackMoveFactor;
        public float HitRange => hitRange;
        public float HitAngle => hitAngle;
        public float HitHeight => hitHeight;
        public float ChargedStaminaCost => chargedStaminaCost;
        public int ChargedSlashCount => chargedSlashes != null ? chargedSlashes.Length : 0;
        public Step GetChargedSlash(int i) => chargedSlashes[i];
        public float ChargedHitRange => chargedHitRange;
        public float ChargedHitAngle => chargedHitAngle;
        public float PlungeLowMultiplier => plungeLowMultiplier;
        public float PlungeHighMultiplier => plungeHighMultiplier;
        public float PlungeRange => plungeRange;
        public float PlungeLowPoise => plungeLowPoise;
        public float PlungeHighPoise => plungeHighPoise;

        /// <summary>콤보 단계 · 강공격 베기 · 낙하의 경직 수치 (2-3). index는 0부터, 낙하는 high로 고른다.</summary>
        public float ComboPoise(int index) => index >= 0 && index < StepCount ? steps[index].poise : 0f;
        public float ChargedPoise(int index) => index >= 0 && index < ChargedSlashCount ? chargedSlashes[index].poise : 0f;
        public float PlungePoise(bool high) => high ? plungeHighPoise : plungeLowPoise;

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

        /// <summary>강공격 규칙 (2-2).</summary>
        public ChargedRules ToChargedRules()
        {
            var list = new ComboStepRules[ChargedSlashCount];
            for (int i = 0; i < list.Length; i++)
            {
                Step s = chargedSlashes[i];
                list[i] = new ComboStepRules(s.multiplier, s.windup, s.active, s.recovery, s.inputOpen, s.lunge);
            }
            return new ChargedRules(list, chargedStaminaCost);
        }
    }
}
