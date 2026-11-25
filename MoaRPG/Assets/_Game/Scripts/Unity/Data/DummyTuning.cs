using UnityEngine;

namespace Moa.Unity.Data
{
    /// <summary>
    /// 허수아비(시험용 적) 수치 (2-1). 에셋 Assets/_Game/Data/Enemies/TrainingDummy.asset, 값은 BALANCE.md 8번.
    /// 코드에는 기본값을 두지 않는다. 색은 Setup 표(표현 값).
    /// </summary>
    [CreateAssetMenu(menuName = "MoaRPG/Data/Dummy Tuning", fileName = "TrainingDummy")]
    public sealed class DummyTuning : ScriptableObject
    {
        [SerializeField] int maxHealth;
        [SerializeField] float defense;
        [Tooltip("s after death before it stands up again (full health)")]
        [SerializeField] float respawnDelay;
        [Tooltip("m the body is pushed back on a hit (visual only, the dummy is rooted)")]
        [SerializeField] float knockbackDistance;
        [Tooltip("s to reach the push; coming back takes twice this")]
        [SerializeField] float knockbackTime;
        [Tooltip("s the body flashes on a hit")]
        [SerializeField] float flashTime;
        [Tooltip("Damage number height above the feet (m)")]
        [SerializeField] float numberHeight;

        public int MaxHealth => maxHealth;
        public float Defense => defense;
        public float RespawnDelay => respawnDelay;
        public float KnockbackDistance => knockbackDistance;
        public float KnockbackTime => knockbackTime;
        public float FlashTime => flashTime;
        public float NumberHeight => numberHeight;
    }
}
