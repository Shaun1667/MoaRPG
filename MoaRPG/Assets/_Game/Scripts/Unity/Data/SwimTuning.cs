using Moa.Core.Motion;
using UnityEngine;

namespace Moa.Unity.Data
{
    /// <summary>
    /// 수영 · 익사 수치 (3-6). 에셋 Assets/_Game/Data/Player/SwimTuning.asset, 값은 BALANCE.md 18번.
    /// 코드에는 기본값을 두지 않는다 (CLAUDE.md 코딩 규칙).
    /// </summary>
    [CreateAssetMenu(menuName = "MoaRPG/Data/Swim Tuning", fileName = "SwimTuning")]
    public sealed class SwimTuning : ScriptableObject
    {
        [Tooltip("Water depth above the feet that starts swimming (m)")]
        [SerializeField] float enterDepth;
        [Tooltip("Swimming ends when the depth gets below this (m, smaller than enterDepth)")]
        [SerializeField] float exitDepth;
        [Tooltip("While swimming the feet float this far below the surface (m)")]
        [SerializeField] float floatDepth;
        [Tooltip("How fast the float depth is reached (1/s)")]
        [SerializeField] float floatSharpness;
        [Tooltip("Max vertical speed towards the float depth (m/s)")]
        [SerializeField] float floatMaxSpeed;
        [Tooltip("Swim speed (m/s)")]
        [SerializeField] float speed;
        [Tooltip("Sprint swim speed (m/s, sprint key while moving)")]
        [SerializeField] float sprintSpeed;
        [Tooltip("Swim acceleration / deceleration (m/s^2)")]
        [SerializeField] float acceleration;
        [Tooltip("Stamina per second in the water (also when not moving)")]
        [SerializeField] float drainPerSecond;
        [Tooltip("Stamina per second while sprint swimming")]
        [SerializeField] float sprintDrainPerSecond;
        [Tooltip("Health the field character loses when drowning (ratio of max health, 1 HP is kept)")]
        [SerializeField] float drownHealthRatio;
        [Tooltip("How far past the water edge the shore search looks for ground after drowning (m)")]
        [SerializeField] float shoreMargin;

        public float EnterDepth => enterDepth;
        public float ExitDepth => exitDepth;
        public float FloatDepth => floatDepth;
        public float FloatSharpness => floatSharpness;
        public float FloatMaxSpeed => floatMaxSpeed;
        public float Speed => speed;
        public float SprintSpeed => sprintSpeed;
        public float Acceleration => acceleration;
        public float DrainPerSecond => drainPerSecond;
        public float SprintDrainPerSecond => sprintDrainPerSecond;
        public float DrownHealthRatio => drownHealthRatio;
        public float ShoreMargin => shoreMargin;

        public SwimRules ToRules() => new SwimRules(enterDepth, exitDepth, floatDepth, floatSharpness, floatMaxSpeed,
            speed, sprintSpeed, acceleration, drainPerSecond, sprintDrainPerSecond, drownHealthRatio);
    }
}
