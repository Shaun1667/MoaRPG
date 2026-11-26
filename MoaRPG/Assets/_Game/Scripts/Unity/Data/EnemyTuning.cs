using System;
using Moa.Core.Combat;
using UnityEngine;

namespace Moa.Unity.Data
{
    /// <summary>적 등급 (3-4, GDD 5번 일반 · 정예 · 보스 — 표시 · 디버그만, 규칙은 값으로, 끝에만 추가).</summary>
    public enum EnemyRank
    {
        Common,
        Elite,
        Boss,
    }

    /// <summary>
    /// 적 한 종류의 수치 (2-5). 새 적 = Assets/_Game/Data/Enemies/에 에셋 하나 (Grunt · Archer · Brute), 값은 BALANCE.md 11 · 16번.
    /// 체력 · 방어 · 경직 · 처치 에너지 · 피격 연출 · AI(감지 · 추적 · 물러나기 · 복귀 · 다시 나기) · 몸 크기 · 공격 표를 한 곳에. 코드에는 기본값을 두지 않는다. 색은 Setup 표.
    /// 3-4: 공격은 <see cref="attacks"/> 표 한 줄 = 공격 하나(배율 · 범위 · 예고 · 돌진 · 넘어짐 · 투사체), 차례로 돌아가며 쓴다. 적 종류별 분기 코드 없음.
    /// </summary>
    [CreateAssetMenu(menuName = "MoaRPG/Data/Enemy Tuning", fileName = "Enemy")]
    public sealed class EnemyTuning : ScriptableObject
    {
        /// <summary>공격 한 줄 (3-4). 시간은 0.02초 배수.</summary>
        [Serializable]
        public sealed class AttackRow
        {
            [Tooltip("Name shown in the debug window only (not game text)")]
            public string debugName;
            [Tooltip("Damage = enemy attack x this, then the character's defense")]
            public float multiplier;
            [Tooltip("m: stops and starts this attack this close")]
            public float startRange;
            [Tooltip("Melee: m the hit reaches (flat, and height difference). Projectile: length of the aim line (visual)")]
            public float hitRange;
            [Tooltip("Degrees, whole front cone the melee hit covers (360 = all around)")]
            public float hitAngle;
            [Tooltip("s of rest in range before the warning")]
            public float interval;
            [Tooltip("s of warning; the attack goes out on the step it ends (hits do not stagger during it)")]
            public float telegraph;
            [Tooltip("s the attack lasts after the warning (charge). 0 = lands on one step")]
            public float activeTime;
            [Tooltip("m moved forward during activeTime (charge)")]
            public float lungeDistance;
            [Tooltip("s of rest pose after the attack")]
            public float recovery;
            [Tooltip("s the player lies knocked down when hit on the ground (dodge cannot end it). 0 = normal hit stun")]
            public float knockdownTime;
            [Tooltip("Melee = front cone check, Projectile = one shot toward the player when the warning ends")]
            public HitDelivery delivery;
            [Tooltip("Projectile only: m/s")]
            public float projectileSpeed;
            [Tooltip("Projectile only: s it flies (range = speed x lifetime)")]
            public float projectileLifetime;
            [Tooltip("Projectile only: m, hit sphere radius")]
            public float projectileRadius;

            public ProjectileRules ToProjectileRules() => new ProjectileRules(projectileSpeed, projectileLifetime, projectileRadius);

            public EnemyAttackSpec ToSpec() => new EnemyAttackSpec(multiplier, startRange, hitRange, hitAngle,
                new EnemyAttackRules(interval, telegraph, recovery, activeTime), lungeDistance, knockdownTime, delivery, ToProjectileRules());
        }

        [Tooltip("Common / Elite / Boss (display and debug only)")]
        [SerializeField] EnemyRank rank;

        [Header("Body / health")]
        [SerializeField] int maxHealth;
        [SerializeField] float defense;
        [Tooltip("Burst (Q) energy the player gains when this enemy is defeated")]
        [SerializeField] float energyOnDefeat;
        [Tooltip("Gold the player gains when this enemy is defeated (3-5)")]
        [SerializeField] int goldOnDefeat;
        [Tooltip("Exp material (Adventure EXP) the player gains when this enemy is defeated (3-5)")]
        [SerializeField] int expItemsOnDefeat;
        [Tooltip("Wish stones (gacha) the player gains when this enemy is defeated (3-7)")]
        [SerializeField] int wishesOnDefeat;
        [Tooltip("m, capsule height (feet to top)")]
        [SerializeField] float capsuleHeight;
        [Tooltip("m, capsule radius")]
        [SerializeField] float capsuleRadius;
        [Tooltip("m above the feet the enemy looks from (line of sight ray, projectile height)")]
        [SerializeField] float eyeHeight;
        [Tooltip("m/s^2 while not grounded")]
        [SerializeField] float gravity;

        [Header("Hit feedback (visual only)")]
        [Tooltip("s the body flashes on a hit")]
        [SerializeField] float flashTime;
        [Tooltip("m the body is pushed back on a hit (visual only)")]
        [SerializeField] float knockbackDistance;
        [Tooltip("s to reach the push; coming back takes twice this")]
        [SerializeField] float knockbackTime;
        [Tooltip("Damage number height above the feet (m)")]
        [SerializeField] float numberHeight;

        [Header("Poise gauge / groggy")]
        [SerializeField] float maxPoise;
        [SerializeField] float groggyDuration;
        [Tooltip("Damage taken x this while groggy (1.3 = +30%)")]
        [SerializeField] float groggyDamageTaken;
        [SerializeField] float poiseRegenDelay;
        [SerializeField] float poiseRegenPerSecond;

        [Header("Senses")]
        [Tooltip("m: notices the player inside the view cone this close")]
        [SerializeField] float detectRange;
        [Tooltip("Degrees, whole view cone")]
        [SerializeField] float viewAngle;
        [Tooltip("m: notices the player this close even from behind")]
        [SerializeField] float noticeRange;
        [Tooltip("m: gives up the chase when the player is farther than this")]
        [SerializeField] float loseRange;
        [Tooltip("s: gives up the chase after the line of sight is blocked this long")]
        [SerializeField] float loseSightTime;
        [Tooltip("m: gives up the chase when this far from home")]
        [SerializeField] float leashRange;

        [Header("Movement")]
        [SerializeField] float patrolSpeed;
        [SerializeField] float chaseSpeed;
        [SerializeField] float returnSpeed;
        [Tooltip("Degrees per second")]
        [SerializeField] float turnSpeed;
        [Tooltip("s resting at each end of the patrol")]
        [SerializeField] float patrolWait;
        [Tooltip("m: a point counts as reached this close")]
        [SerializeField] float arriveDistance;
        [Tooltip("m (3-4, ranged): between attacks, backs away while the player is closer than this. 0 = never")]
        [SerializeField] float retreatRange;
        [Tooltip("m/s backing away")]
        [SerializeField] float retreatSpeed;

        [Header("Attack")]
        [Tooltip("Attack of this enemy against the player (3-1)")]
        [SerializeField] float attack;
        [Tooltip("3-4: one row = one attack, used in turn (row 1, row 2, row 1 ...)")]
        [SerializeField] AttackRow[] attacks;

        [Header("Hit stun / respawn")]
        [Tooltip("s the enemy stops when hit (not during a warning or charge). 0 = never stops (elite)")]
        [SerializeField] float hitStunTime;
        [Tooltip("s after death before it appears again at home")]
        [SerializeField] float respawnDelay;

        public EnemyRank Rank => rank;
        public int MaxHealth => maxHealth;
        public float Defense => defense;
        public float EnergyOnDefeat => energyOnDefeat;
        public int GoldOnDefeat => goldOnDefeat;
        public int ExpItemsOnDefeat => expItemsOnDefeat;
        public int WishesOnDefeat => wishesOnDefeat;
        public float CapsuleHeight => capsuleHeight;
        public float CapsuleRadius => capsuleRadius;
        public float EyeHeight => eyeHeight;
        public float Gravity => gravity;
        public float FlashTime => flashTime;
        public float KnockbackDistance => knockbackDistance;
        public float KnockbackTime => knockbackTime;
        public float NumberHeight => numberHeight;
        public float MaxPoise => maxPoise;
        public float GroggyDuration => groggyDuration;
        public float GroggyDamageTaken => groggyDamageTaken;
        public float PoiseRegenDelay => poiseRegenDelay;
        public float PoiseRegenPerSecond => poiseRegenPerSecond;
        public float DetectRange => detectRange;
        public float ViewAngle => viewAngle;
        public float NoticeRange => noticeRange;
        public float LoseRange => loseRange;
        public float LoseSightTime => loseSightTime;
        public float LeashRange => leashRange;
        public float PatrolSpeed => patrolSpeed;
        public float ChaseSpeed => chaseSpeed;
        public float ReturnSpeed => returnSpeed;
        public float TurnSpeed => turnSpeed;
        public float PatrolWait => patrolWait;
        public float ArriveDistance => arriveDistance;
        public float RetreatRange => retreatRange;
        public float RetreatSpeed => retreatSpeed;
        public float Attack => attack;
        public float HitStunTime => hitStunTime;
        public float RespawnDelay => respawnDelay;

        /// <summary>3-4: 공격 표 줄 수 · 한 줄 (없으면 null).</summary>
        public int AttackCount => attacks != null ? attacks.Length : 0;
        public AttackRow GetAttack(int index) => attacks != null && index >= 0 && index < attacks.Length ? attacks[index] : null;

        // 첫 공격 줄 (2-5 한 줄짜리 적과 같은 이름 — Grunt는 줄이 하나)
        AttackRow First => GetAttack(0);
        public float AttackRange => First != null ? First.startRange : 0f;
        public float HitRange => First != null ? First.hitRange : 0f;
        public float HitAngle => First != null ? First.hitAngle : 0f;
        public float AttackInterval => First != null ? First.interval : 0f;
        public float TelegraphTime => First != null ? First.telegraph : 0f;
        public float RecoveryTime => First != null ? First.recovery : 0f;
        public float AttackMultiplier => First != null ? First.multiplier : 0f;

        public PoiseRules ToPoiseRules() => new PoiseRules(maxPoise, groggyDuration, groggyDamageTaken, poiseRegenDelay, poiseRegenPerSecond);

        /// <summary>첫 공격 줄의 주기.</summary>
        public EnemyAttackRules ToAttackRules() => First != null ? First.ToSpec().Timing : default;

        public EnemyAttackSpec[] ToAttackSpecs()
        {
            var specs = new EnemyAttackSpec[AttackCount];
            for (int i = 0; i < specs.Length; i++) specs[i] = attacks[i] != null ? attacks[i].ToSpec() : default;
            return specs;
        }

        public EnemyAiRules ToAiRules() => new EnemyAiRules(detectRange, viewAngle, noticeRange, loseRange, loseSightTime, leashRange,
            patrolSpeed, chaseSpeed, returnSpeed, turnSpeed, patrolWait, arriveDistance, hitStunTime, respawnDelay, retreatRange, retreatSpeed,
            ToAttackSpecs());
    }
}
