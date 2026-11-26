using Moa.Core.Combat;
using UnityEngine;

namespace Moa.Unity.Data
{
    /// <summary>
    /// 적 한 종류의 수치 (2-5). 새 적 = Assets/_Game/Data/Enemies/에 에셋 하나 (첫 적: Grunt.asset, 근접 졸병), 값은 BALANCE.md 11번.
    /// 체력 · 방어 · 경직 · 처치 에너지 · 피격 연출 · AI(감지 · 추적 · 공격 · 복귀 · 다시 나기) · 몸 크기를 한 곳에. 코드에는 기본값을 두지 않는다. 색은 Setup 표.
    /// </summary>
    [CreateAssetMenu(menuName = "MoaRPG/Data/Enemy Tuning", fileName = "Enemy")]
    public sealed class EnemyTuning : ScriptableObject
    {
        [Header("Body / health")]
        [SerializeField] int maxHealth;
        [SerializeField] float defense;
        [Tooltip("Burst (Q) energy the player gains when this enemy is defeated")]
        [SerializeField] float energyOnDefeat;
        [Tooltip("m, capsule height (feet to top)")]
        [SerializeField] float capsuleHeight;
        [Tooltip("m, capsule radius")]
        [SerializeField] float capsuleRadius;
        [Tooltip("m above the feet the enemy looks from (line of sight ray)")]
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

        [Header("Attack")]
        [Tooltip("m: stops and starts attacking this close")]
        [SerializeField] float attackRange;
        [Tooltip("m: the hit reaches the player this close (flat, and height difference)")]
        [SerializeField] float hitRange;
        [Tooltip("Degrees, whole front cone the hit covers")]
        [SerializeField] float hitAngle;
        [Tooltip("s of rest in range before each warning")]
        [SerializeField] float attackInterval;
        [Tooltip("s of warning; the hit lands on the step it ends (hits do not stagger during it)")]
        [SerializeField] float telegraphTime;
        [Tooltip("s of rest pose after the hit")]
        [SerializeField] float recoveryTime;
        [Tooltip("Attack of this enemy against the player (3-1)")]
        [SerializeField] float attack;
        [Tooltip("Damage multiplier of its hit (3-1): damage = attack x this, then the character's defense")]
        [SerializeField] float attackMultiplier;

        [Header("Hit stun / respawn")]
        [Tooltip("s the enemy stops when hit (not during a warning)")]
        [SerializeField] float hitStunTime;
        [Tooltip("s after death before it appears again at home")]
        [SerializeField] float respawnDelay;

        public int MaxHealth => maxHealth;
        public float Defense => defense;
        public float EnergyOnDefeat => energyOnDefeat;
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
        public float AttackRange => attackRange;
        public float HitRange => hitRange;
        public float HitAngle => hitAngle;
        public float AttackInterval => attackInterval;
        public float TelegraphTime => telegraphTime;
        public float RecoveryTime => recoveryTime;
        public float Attack => attack;
        public float AttackMultiplier => attackMultiplier;
        public float HitStunTime => hitStunTime;
        public float RespawnDelay => respawnDelay;

        public PoiseRules ToPoiseRules() => new PoiseRules(maxPoise, groggyDuration, groggyDamageTaken, poiseRegenDelay, poiseRegenPerSecond);

        public EnemyAttackRules ToAttackRules() => new EnemyAttackRules(attackInterval, telegraphTime, recoveryTime);

        public EnemyAiRules ToAiRules() => new EnemyAiRules(detectRange, viewAngle, noticeRange, loseRange, loseSightTime, leashRange,
            attackRange, hitRange, hitAngle, patrolSpeed, chaseSpeed, returnSpeed, turnSpeed, patrolWait, arriveDistance, hitStunTime, respawnDelay,
            ToAttackRules());
    }
}
