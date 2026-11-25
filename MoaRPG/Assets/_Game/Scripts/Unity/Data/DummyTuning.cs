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

        [Header("Poise gauge / groggy (2-3)")]
        [Tooltip("Poise built up by hits; reaching this makes the dummy groggy")]
        [SerializeField] float maxPoise;
        [Tooltip("s the dummy stays groggy (cannot act)")]
        [SerializeField] float groggyDuration;
        [Tooltip("Damage taken x this while groggy (1.3 = +30%)")]
        [SerializeField] float groggyDamageTaken;
        [Tooltip("s after the last hit before the gauge starts to drain")]
        [SerializeField] float poiseRegenDelay;
        [Tooltip("Gauge drained per second after the delay")]
        [SerializeField] float poiseRegenPerSecond;

        [Header("Burst energy (2-4)")]
        [Tooltip("Burst (Q) energy the player gains when this enemy is defeated")]
        [SerializeField] float energyOnDefeat;


        public int MaxHealth => maxHealth;
        public float Defense => defense;
        public float RespawnDelay => respawnDelay;
        public float KnockbackDistance => knockbackDistance;
        public float KnockbackTime => knockbackTime;
        public float FlashTime => flashTime;
        public float NumberHeight => numberHeight;
        public float MaxPoise => maxPoise;
        public float GroggyDuration => groggyDuration;
        public float GroggyDamageTaken => groggyDamageTaken;
        public float PoiseRegenDelay => poiseRegenDelay;
        public float PoiseRegenPerSecond => poiseRegenPerSecond;
        /// <summary>처치하면 플레이어가 얻는 궁극기 에너지 (2-4, BALANCE 10번).</summary>
        public float EnergyOnDefeat => energyOnDefeat;


        /// <summary>경직 게이지 규칙 (2-3).</summary>
        public Moa.Core.Combat.PoiseRules ToPoiseRules() => new Moa.Core.Combat.PoiseRules(maxPoise, groggyDuration, groggyDamageTaken, poiseRegenDelay, poiseRegenPerSecond);
    }
}
