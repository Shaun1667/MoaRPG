using Moa.Core.Combat;
using UnityEngine;

namespace Moa.Unity.Data
{
    /// <summary>
    /// 예고 공격 수치 (2-3, 저스트 회피 시험용 공격 허수아비). 에셋 Assets/_Game/Data/Enemies/AttackingDummy.asset, 값은 BALANCE.md 9번.
    /// 체력 · 방어 · 경직은 DummyTuning(같은 허수아비 표). 코드에는 기본값을 두지 않는다. 예고 색은 Setup 표.
    /// </summary>
    [CreateAssetMenu(menuName = "MoaRPG/Data/Enemy Attack Tuning", fileName = "EnemyAttackTuning")]
    public sealed class EnemyAttackTuning : ScriptableObject
    {
        [Tooltip("s of rest before each warning (also before the first one)")]
        [SerializeField] float attackInterval;
        [Tooltip("s of warning; the hit lands on the step it ends")]
        [SerializeField] float telegraphTime;
        [Tooltip("s of rest pose after the hit")]
        [SerializeField] float recoveryTime;
        [Tooltip("m: the hit reaches the player when the feet are this close (flat distance, all around)")]
        [SerializeField] float attackRange;

        public float AttackInterval => attackInterval;
        public float TelegraphTime => telegraphTime;
        public float RecoveryTime => recoveryTime;
        public float AttackRange => attackRange;

        public EnemyAttackRules ToRules() => new EnemyAttackRules(attackInterval, telegraphTime, recoveryTime);
    }
}
