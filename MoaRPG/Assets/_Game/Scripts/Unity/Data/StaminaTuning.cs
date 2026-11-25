using Moa.Core.Motion;
using UnityEngine;

namespace Moa.Unity.Data
{
    /// <summary>
    /// 스태미나 · 질주 · 회피 수치 (1-2). 에셋 Assets/_Game/Data/Player/StaminaTuning.asset, 값은 BALANCE.md 3번.
    /// 코드에는 기본값을 두지 않는다 (CLAUDE.md 코딩 규칙). 걷기 · 뛰기 · 가속은 MovementTuning(BALANCE 1번).
    /// </summary>
    [CreateAssetMenu(menuName = "MoaRPG/Data/Stamina Tuning", fileName = "StaminaTuning")]
    public sealed class StaminaTuning : ScriptableObject
    {
        [Header("Stamina")]
        [SerializeField] float maxStamina;
        [Tooltip("Stamina per second while regenerating")]
        [SerializeField] float regenPerSecond;
        [Tooltip("Seconds after the last spend before regeneration starts")]
        [SerializeField] float regenDelay;
        [Tooltip("After running out, sprint stays locked until stamina is back to this amount")]
        [SerializeField] float exhaustRecover;

        [Header("Sprint")]
        [Tooltip("m/s")]
        [SerializeField] float sprintSpeed;
        [Tooltip("Stamina per second while sprinting on the ground")]
        [SerializeField] float sprintDrainPerSecond;

        [Header("Dodge")]
        [SerializeField] float dodgeCost;
        [Tooltip("m")]
        [SerializeField] float dodgeDistance;
        [Tooltip("s")]
        [SerializeField] float dodgeDuration;
        [Tooltip("s from the start of the dodge")]
        [SerializeField] float dodgeInvulnerableTime;
        [Tooltip("s after a dodge ends before the next one")]
        [SerializeField] float dodgeCooldown;
        [Tooltip("Ground only (Genshin style)")]
        [SerializeField] bool dodgeGroundOnly;

        [Header("Gauge (screen)")]
        [Tooltip("Height above the feet the gauge follows (m)")]
        [SerializeField] float gaugeAnchorHeight;
        [Tooltip("Screen offset from that point, in 1920x1080 reference pixels")]
        [SerializeField] Vector2 gaugeScreenOffset;
        [Tooltip("Gauge diameter, reference pixels")]
        [SerializeField] float gaugeSize;
        [Tooltip("Seconds the gauge stays after it is full")]
        [SerializeField] float gaugeHideDelay;
        [Tooltip("Fade-out seconds")]
        [SerializeField] float gaugeFadeTime;

        public float MaxStamina => maxStamina;
        public float RegenPerSecond => regenPerSecond;
        public float RegenDelay => regenDelay;
        public float ExhaustRecover => exhaustRecover;
        public float SprintSpeed => sprintSpeed;
        public float SprintDrainPerSecond => sprintDrainPerSecond;
        public float DodgeCost => dodgeCost;
        public float DodgeDistance => dodgeDistance;
        public float DodgeDuration => dodgeDuration;
        public float DodgeInvulnerableTime => dodgeInvulnerableTime;
        public float DodgeCooldown => dodgeCooldown;
        public bool DodgeGroundOnly => dodgeGroundOnly;
        public float GaugeAnchorHeight => gaugeAnchorHeight;
        public Vector2 GaugeScreenOffset => gaugeScreenOffset;
        public float GaugeSize => gaugeSize;
        public float GaugeHideDelay => gaugeHideDelay;
        public float GaugeFadeTime => gaugeFadeTime;

        public StaminaRules ToStaminaRules() => new StaminaRules(maxStamina, regenPerSecond, regenDelay, exhaustRecover);
        public DodgeRules ToDodgeRules() => new DodgeRules(dodgeDistance, dodgeDuration, dodgeInvulnerableTime, dodgeCooldown, dodgeCost, dodgeGroundOnly);
    }
}
