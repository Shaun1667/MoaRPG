using Moa.Core.Motion;
using Moa.Unity.Data;
using Moa.Unity.Player;
using UnityEngine;

namespace Moa.Unity.Cameras
{
    /// <summary>
    /// 3인칭 궤도 카메라 (1-1) — 패키지 없이 직접 작성. 마우스 · 스틱 궤도 회전(피치 제한) · 휠 줌(거리 제한) ·
    /// 벽에 가리면 SphereCast로 앞으로 당김 · 부드러운 따라가기. 계산은 Core의 OrbitMath, 수치는 CameraTuning 에셋.
    /// GameRoot가 Bind로 연결한다.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class OrbitCamera : MonoBehaviour
    {
        [SerializeField] CameraTuning tuning;

        const int MaxHits = 16;
        readonly RaycastHit[] _hits = new RaycastHit[MaxHits];

        IPlayerInput _input;
        PlayerMotor _target;
        OrbitAngles _angles;
        float _desiredDistance;
        float _zoomDistance;
        float _currentDistance;
        Vector3 _pivot;
        bool _blocked;

        public CameraTuning Tuning => tuning;
        public float Yaw => _angles.Yaw;
        public float Pitch => _angles.Pitch;
        /// <summary>지금 카메라 거리 (벽 당김 반영).</summary>
        public float Distance => _currentDistance;
        /// <summary>휠로 정한 거리.</summary>
        public float DesiredDistance => _desiredDistance;
        /// <summary>벽에 가려 당겨졌는지.</summary>
        public bool Blocked => _blocked;

        public void Bind(IPlayerInput input, PlayerMotor target)
        {
            _input = input;
            _target = target;
            if (tuning == null) { Debug.LogError("OrbitCamera: CameraTuning is not assigned"); enabled = false; return; }
            var cam = GetComponent<Camera>();
            cam.fieldOfView = tuning.FieldOfView;
            cam.nearClipPlane = tuning.NearClip;
            float yaw = target != null ? target.transform.eulerAngles.y : transform.eulerAngles.y;
            _angles = new OrbitAngles(MoveMath.Wrap360(yaw), OrbitMath.ClampPitch(tuning.StartPitch, tuning.MinPitch, tuning.MaxPitch));
            _desiredDistance = _zoomDistance = OrbitMath.Clamp(tuning.StartDistance, tuning.MinDistance, tuning.MaxDistance);
            SnapToTarget();
        }

        /// <summary>[보기] 각도를 정한다 (피치는 제한 안으로).</summary>
        public void SetAngles(float yaw, float pitch)
        {
            if (tuning == null) return;
            _angles = new OrbitAngles(MoveMath.Wrap360(yaw), OrbitMath.ClampPitch(pitch, tuning.MinPitch, tuning.MaxPitch));
        }

        /// <summary>따라가기 보간 없이 바로 대상 위치로 (순간이동 뒤).</summary>
        public void SnapToTarget()
        {
            if (_target == null || tuning == null) return;
            _pivot = PivotTarget();
            _zoomDistance = _desiredDistance;
            _currentDistance = AllowedDistance(_pivot, Rotation() * Vector3.back, _zoomDistance);
            Apply();
        }

        void LateUpdate()
        {
            if (_target == null || tuning == null) return;
            float dt = Time.deltaTime;

            if (_input != null)
            {
                Vector2 look = _input.LookMouseDelta * tuning.MouseSensitivity + _input.LookStick * (tuning.StickSpeed * dt);
                _angles = OrbitMath.ApplyLook(_angles, look.x, look.y, tuning.MinPitch, tuning.MaxPitch, tuning.InvertY);
                float scroll = _input.ZoomScroll;
                if (scroll != 0f) _desiredDistance = OrbitMath.Zoom(_desiredDistance, scroll, tuning.ZoomStep, tuning.MinDistance, tuning.MaxDistance);
            }

            _zoomDistance = Mathf.Lerp(_zoomDistance, _desiredDistance, OrbitMath.SmoothFactor(tuning.ZoomSharpness, dt));
            _pivot = Vector3.Lerp(_pivot, PivotTarget(), OrbitMath.SmoothFactor(tuning.FollowSharpness, dt));

            float allowed = AllowedDistance(_pivot, Rotation() * Vector3.back, _zoomDistance);
            _currentDistance = OrbitMath.SmoothDistance(_currentDistance, allowed, tuning.CollisionReleaseSharpness, dt);
            Apply();
        }

        Quaternion Rotation() => Quaternion.Euler(_angles.Pitch, _angles.Yaw, 0f);

        Vector3 PivotTarget() => _target.SmoothedPosition + Vector3.up * tuning.PivotHeight;

        void Apply()
        {
            OrbitMath.Offset(_angles, _currentDistance, out float x, out float y, out float z);
            transform.SetPositionAndRotation(_pivot + new Vector3(x, y, z), Rotation());
        }

        float AllowedDistance(Vector3 pivot, Vector3 back, float desired)
        {
            int n = Physics.SphereCastNonAlloc(pivot, tuning.CollisionRadius, back, _hits, desired, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            bool hit = false;
            float nearest = desired;
            for (int i = 0; i < n; i++)
            {
                var h = _hits[i];
                if (h.collider == null) continue;
                if (_target != null && h.collider.transform.IsChildOf(_target.transform)) continue; // 플레이어 자신은 무시
                // distance 0 = 시작부터 겹침 (피벗이 벽 안) → 가장 가까이
                if (!hit || h.distance < nearest) { nearest = h.distance; hit = true; }
            }
            _blocked = hit;
            return OrbitMath.CollisionDistance(desired, hit, nearest, tuning.CollisionPadding, tuning.CollisionMinDistance);
        }
    }
}
