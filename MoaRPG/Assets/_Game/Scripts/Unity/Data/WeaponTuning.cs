using System;
using Moa.Core.Combat;
using UnityEngine;

namespace Moa.Unity.Data
{
    /// <summary>
    /// 무기 하나의 수치 (2-1). 에셋 Assets/_Game/Data/Weapons/&lt;무기&gt;.asset, 값은 BALANCE.md 6번.
    /// 3-3: 무기 종류(kind)는 보이는 것(그레이박스 모델 · 디버그)만 고르고, 규칙은 값으로 — 동작마다 판정 방식(근접 / 투사체),
    /// 강공격의 조준 시간 · 회전 소모, 투사체 수치, 자동 조준 거리 (BALANCE 15번). 새 종류(창 · 법구) = enum 끝에 한 줄 + 에셋 하나.
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

        [Tooltip("Weapon kind (3-3): only picks the graybox model and debug label - the rules come from the values below")]
        [SerializeField] WeaponKind kind;
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

        [Header("Switch entry attack (3-2, Attack right after switching in)")]
        [Tooltip("The one strong slash (dash slash) the incoming character makes instead of hit 1 (inputOpen is not used). multiplier 0 = no entry attack")]
        [SerializeField] Step entrySlash;
        [Tooltip("m from the body center to the target surface")]
        [SerializeField] float entryHitRange;
        [Tooltip("Full fan angle, degrees (360 = all around)")]
        [SerializeField] float entryHitAngle;

        [Header("Hit delivery (3-3): Melee = forward fan, Projectile = one shot when the hit window opens")]
        [SerializeField] HitDelivery comboDelivery;
        [SerializeField] HitDelivery chargedDelivery;
        [SerializeField] HitDelivery entryDelivery;
        [Tooltip("m/s (horizontal)")]
        [SerializeField] float projectileSpeed;
        [Tooltip("s a projectile flies (range = speed x this)")]
        [SerializeField] float projectileLifetime;
        [Tooltip("m, radius of the swept hit sphere")]
        [SerializeField] float projectileRadius;
        [Tooltip("m, nearest target within this is faced when a step starts without move input (0 = CombatTuning autoAimRange)")]
        [SerializeField] float autoAimRange;

        [Header("Charged attack style (3-3): both 0 = the slashes in order")]
        [Tooltip("s of aiming before a release fires (hold = aim, release = shoot; released earlier = nothing). 0 = no aiming")]
        [SerializeField] float chargedAimTime;
        [Tooltip("Stamina per second while spinning: rows before the last repeat while held, the last row is the finisher. 0 = no spin")]
        [SerializeField] float chargedSpinDrainPerSecond;

        public WeaponKind Kind => kind;
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
        /// <summary>교체 등장 공격 (3-2): 베기 한 줄 · 범위 · 각도. 배율이 0이면 없음.</summary>
        public bool HasEntry => entrySlash.multiplier > 0f;
        public Step EntrySlash => entrySlash;
        public float EntryHitRange => entryHitRange;
        public float EntryHitAngle => entryHitAngle;
        public float EntryPoise => HasEntry ? entrySlash.poise : 0f;

        // ---- 3-3 무기 종류 ----
        public HitDelivery ComboDelivery => comboDelivery;
        public HitDelivery ChargedDelivery => chargedDelivery;
        public HitDelivery EntryDelivery => entryDelivery;
        public float ProjectileSpeed => projectileSpeed;
        public float ProjectileLifetime => projectileLifetime;
        public float ProjectileRadius => projectileRadius;
        /// <summary>자동 조준 거리 (0 = CombatTuning 것을 쓴다).</summary>
        public float AutoAimRange => autoAimRange;
        public float ChargedAimTime => chargedAimTime;
        public float ChargedSpinDrainPerSecond => chargedSpinDrainPerSecond;
        /// <summary>하나라도 투사체로 맞히는 동작이 있는지.</summary>
        public bool UsesProjectiles => comboDelivery == HitDelivery.Projectile || chargedDelivery == HitDelivery.Projectile || entryDelivery == HitDelivery.Projectile;

        /// <summary>공격 동작의 판정 방식 (콤보 · 강공격 · 등장만 무기가 정하고, 낙하 · 스킬 · 궁극기는 근접 범위).</summary>
        public HitDelivery Delivery(AttackMode mode) =>
            mode == AttackMode.Combo ? comboDelivery
            : mode == AttackMode.Charged ? chargedDelivery
            : mode == AttackMode.Entry ? entryDelivery
            : HitDelivery.Melee;

        /// <summary>투사체 수치 (3-3).</summary>
        public ProjectileRules ToProjectileRules() => new ProjectileRules(projectileSpeed, projectileLifetime, projectileRadius);

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

        /// <summary>교체 등장 공격 베기 표 (3-2, 없으면 빈 표).</summary>
        public ComboStepRules[] ToEntrySlashes()
        {
            if (!HasEntry) return new ComboStepRules[0];
            Step s = entrySlash;
            return new[] { new ComboStepRules(s.multiplier, s.windup, s.active, s.recovery, s.inputOpen, s.lunge) };
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
            return new ChargedRules(list, chargedStaminaCost, chargedAimTime, chargedSpinDrainPerSecond);
        }
    }

    /// <summary>무기 종류 (3-3, GDD 4번 5종) — 그레이박스 모델 · 디버그 표시만 고른다. 규칙은 WeaponTuning의 값. 끝에만 추가.</summary>
    public enum WeaponKind
    {
        Sword,
        Claymore,
        Polearm,
        Bow,
        Catalyst,
    }
}
