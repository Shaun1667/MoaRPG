using Moa.Core.Motion;
using UnityEngine;

namespace Moa.Unity.Data
{
    /// <summary>
    /// 활공 수치 (1-3). 에셋 Assets/_Game/Data/Player/GlideTuning.asset, 값은 BALANCE.md 5번.
    /// 코드에는 기본값을 두지 않는다 (CLAUDE.md 코딩 규칙).
    /// </summary>
    [CreateAssetMenu(menuName = "MoaRPG/Data/Glide Tuning", fileName = "GlideTuning")]
    public sealed class GlideTuning : ScriptableObject
    {
        [Tooltip("Minimum height above the ground to open the glider (m)")]
        [SerializeField] float minHeight;
        [Tooltip("Horizontal target speed (m/s)")]
        [SerializeField] float horizontalSpeed;
        [Tooltip("Horizontal acceleration / deceleration (m/s^2)")]
        [SerializeField] float horizontalAcceleration;
        [Tooltip("Fall target speed (m/s, positive)")]
        [SerializeField] float fallSpeed;
        [Tooltip("Vertical acceleration towards the fall speed (m/s^2)")]
        [SerializeField] float verticalAcceleration;
        [Tooltip("Stamina per second while gliding")]
        [SerializeField] float drainPerSecond;

        public float MinHeight => minHeight;
        public float HorizontalSpeed => horizontalSpeed;
        public float HorizontalAcceleration => horizontalAcceleration;
        public float FallSpeed => fallSpeed;
        public float VerticalAcceleration => verticalAcceleration;
        public float DrainPerSecond => drainPerSecond;

        public GlideRules ToRules() => new GlideRules(minHeight, horizontalSpeed, horizontalAcceleration, fallSpeed, verticalAcceleration, drainPerSecond);
    }
}
