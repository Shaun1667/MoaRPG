using UnityEngine;

namespace Moa.Unity.Data
{
    /// <summary>
    /// 전투 공통 수치 (2-1). 에셋 Assets/_Game/Data/Combat/CombatTuning.asset, 값은 BALANCE.md 7번.
    /// 무기마다 다른 값은 WeaponTuning, 적마다 다른 값은 DummyTuning(적 표). 코드에는 기본값을 두지 않는다.
    /// </summary>
    [CreateAssetMenu(menuName = "MoaRPG/Data/Combat Tuning", fileName = "CombatTuning")]
    public sealed class CombatTuning : ScriptableObject
    {
        [Tooltip("Defense reduction = def / (def + this). Defense equal to this halves damage")]
        [SerializeField] float defenseConstant;
        [Tooltip("With no move input, turn to the nearest target within this many meters (else keep facing)")]
        [SerializeField] float autoAimRange;

        [Header("Charged attack (2-2)")]
        [Tooltip("s the Attack button must stay held after the press for a charged attack")]
        [SerializeField] float chargeHoldTime;

        [Header("Plunge attack (2-2)")]
        [Tooltip("m above the ground needed to start a plunge (a flat-ground jump apex reaches it)")]
        [SerializeField] float plungeMinHeight;
        [Tooltip("m fallen (start feet - landing feet) at which the plunge counts as high")]
        [SerializeField] float plungeHighHeight;
        [Tooltip("m/s straight down while plunging (no horizontal move)")]
        [SerializeField] float plungeFallSpeed;
        [Tooltip("s of recovery after landing (dodge or jump cancels it)")]
        [SerializeField] float plungeLandRecovery;

        [Header("Just dodge (2-3)")]
        [Tooltip("s: an enemy hit that lands within this long after the dodge started (while invulnerable) is a just dodge")]
        [SerializeField] float justDodgeWindow;
        [Tooltip("Time.timeScale during the just-dodge slow motion")]
        [SerializeField] float justSlowScale;
        [Tooltip("Real-time seconds of slow motion")]
        [SerializeField] float justSlowDuration;
        [Tooltip("Game seconds of counter bonus after a just dodge")]
        [SerializeField] float justCounterTime;
        [Tooltip("Poise of the player's hits x this during the counter bonus")]
        [SerializeField] float justCounterPoiseMultiplier;

        [Header("Damage numbers (screen)")]
        [Tooltip("Font size, 1920x1080 reference pixels")]
        [SerializeField] int damageFontSize;
        [Tooltip("s a number stays")]
        [SerializeField] float damageLifetime;
        [Tooltip("m the number rises over its lifetime")]
        [SerializeField] float damageRise;
        [Tooltip("s of fade at the end")]
        [SerializeField] float damageFadeTime;
        [Tooltip("Random-free spread: each new number is shifted sideways by this many reference pixels, cycling")]
        [SerializeField] float damageSpread;

        public float DefenseConstant => defenseConstant;
        public float AutoAimRange => autoAimRange;
        public float ChargeHoldTime => chargeHoldTime;
        public float PlungeMinHeight => plungeMinHeight;
        public float PlungeHighHeight => plungeHighHeight;
        public float PlungeFallSpeed => plungeFallSpeed;
        public float PlungeLandRecovery => plungeLandRecovery;
        public float JustDodgeWindow => justDodgeWindow;
        public float JustSlowScale => justSlowScale;
        public float JustSlowDuration => justSlowDuration;
        public float JustCounterTime => justCounterTime;
        public float JustCounterPoiseMultiplier => justCounterPoiseMultiplier;

        /// <summary>저스트 회피 규칙 (2-3).</summary>
        public Moa.Core.Combat.JustDodgeRules ToJustDodgeRules() =>
            new Moa.Core.Combat.JustDodgeRules(justDodgeWindow, justSlowScale, justSlowDuration, justCounterTime, justCounterPoiseMultiplier);
        public int DamageFontSize => damageFontSize;
        public float DamageLifetime => damageLifetime;
        public float DamageRise => damageRise;
        public float DamageFadeTime => damageFadeTime;
        public float DamageSpread => damageSpread;
    }
}
