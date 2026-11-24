using UnityEngine;

namespace Moa.Unity.Data
{
    /// <summary>
    /// 3인칭 궤도 카메라 수치 (1-1). 에셋 Assets/_Game/Data/Player/CameraTuning.asset, 값은 BALANCE.md 2번.
    /// 코드에는 기본값을 두지 않는다 (CLAUDE.md 코딩 규칙).
    /// </summary>
    [CreateAssetMenu(menuName = "MoaRPG/Data/Camera Tuning", fileName = "CameraTuning")]
    public sealed class CameraTuning : ScriptableObject
    {
        [Header("Pivot / Follow")]
        [Tooltip("Pivot height above the player's feet (m)")]
        [SerializeField] float pivotHeight;
        [Tooltip("Follow smoothing (higher = tighter)")]
        [SerializeField] float followSharpness;

        [Header("Angles (degrees, pitch + = looking down)")]
        [SerializeField] float startPitch;
        [SerializeField] float minPitch;
        [SerializeField] float maxPitch;
        [Tooltip("Degrees per mouse pixel")]
        [SerializeField] float mouseSensitivity;
        [Tooltip("Degrees per second at full gamepad stick")]
        [SerializeField] float stickSpeed;
        [SerializeField] bool invertY;

        [Header("Distance (m)")]
        [SerializeField] float startDistance;
        [SerializeField] float minDistance;
        [SerializeField] float maxDistance;
        [Tooltip("Distance change per wheel notch")]
        [SerializeField] float zoomStep;
        [Tooltip("Zoom smoothing (higher = faster)")]
        [SerializeField] float zoomSharpness;

        [Header("Wall pull-in")]
        [Tooltip("SphereCast radius")]
        [SerializeField] float collisionRadius;
        [Tooltip("Gap kept in front of the hit wall")]
        [SerializeField] float collisionPadding;
        [Tooltip("Closest the camera gets when pulled in")]
        [SerializeField] float collisionMinDistance;
        [Tooltip("How fast the camera returns after the wall is gone (higher = faster)")]
        [SerializeField] float collisionReleaseSharpness;

        [Header("Lens")]
        [SerializeField] float fieldOfView;
        [SerializeField] float nearClip;

        public float PivotHeight => pivotHeight;
        public float FollowSharpness => followSharpness;
        public float StartPitch => startPitch;
        public float MinPitch => minPitch;
        public float MaxPitch => maxPitch;
        public float MouseSensitivity => mouseSensitivity;
        public float StickSpeed => stickSpeed;
        public bool InvertY => invertY;
        public float StartDistance => startDistance;
        public float MinDistance => minDistance;
        public float MaxDistance => maxDistance;
        public float ZoomStep => zoomStep;
        public float ZoomSharpness => zoomSharpness;
        public float CollisionRadius => collisionRadius;
        public float CollisionPadding => collisionPadding;
        public float CollisionMinDistance => collisionMinDistance;
        public float CollisionReleaseSharpness => collisionReleaseSharpness;
        public float FieldOfView => fieldOfView;
        public float NearClip => nearClip;
    }
}
