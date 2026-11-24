using UnityEngine;

namespace Moa.Unity.Data
{
    /// <summary>
    /// 플레이어 이동 수치 (1-1). 에셋 Assets/_Game/Data/Player/MovementTuning.asset, 값은 BALANCE.md 1번.
    /// 코드에는 기본값을 두지 않는다 (CLAUDE.md 코딩 규칙).
    /// </summary>
    [CreateAssetMenu(menuName = "MoaRPG/Data/Movement Tuning", fileName = "MovementTuning")]
    public sealed class MovementTuning : ScriptableObject
    {
        [Header("Speed (m/s)")]
        [SerializeField] float walkSpeed;
        [SerializeField] float runSpeed;

        [Header("Acceleration (m/s^2)")]
        [SerializeField] float groundAcceleration;
        [SerializeField] float groundDeceleration;
        [SerializeField] float airAcceleration;
        [SerializeField] float airDeceleration;

        [Header("Turn")]
        [Tooltip("Degrees per second toward the move direction")]
        [SerializeField] float turnSpeed;

        [Header("Jump / Gravity")]
        [Tooltip("Jump apex height (m)")]
        [SerializeField] float jumpHeight;
        [Tooltip("Gravity (m/s^2, positive)")]
        [SerializeField] float gravity;
        [Tooltip("Max fall speed (m/s, positive)")]
        [SerializeField] float maxFallSpeed;
        [Tooltip("Downward speed kept while grounded so the body sticks to slopes and stairs (m/s)")]
        [SerializeField] float groundStickSpeed;

        [Header("Character Controller")]
        [Tooltip("Steepest walkable slope (degrees)")]
        [SerializeField] float slopeLimit;
        [Tooltip("Highest step climbed without jumping (m)")]
        [SerializeField] float stepOffset;
        [SerializeField] float capsuleHeight;
        [SerializeField] float capsuleRadius;
        [SerializeField] float skinWidth;

        public float WalkSpeed => walkSpeed;
        public float RunSpeed => runSpeed;
        public float GroundAcceleration => groundAcceleration;
        public float GroundDeceleration => groundDeceleration;
        public float AirAcceleration => airAcceleration;
        public float AirDeceleration => airDeceleration;
        public float TurnSpeed => turnSpeed;
        public float JumpHeight => jumpHeight;
        public float Gravity => gravity;
        public float MaxFallSpeed => maxFallSpeed;
        public float GroundStickSpeed => groundStickSpeed;
        public float SlopeLimit => slopeLimit;
        public float StepOffset => stepOffset;
        public float CapsuleHeight => capsuleHeight;
        public float CapsuleRadius => capsuleRadius;
        public float SkinWidth => skinWidth;
    }
}
