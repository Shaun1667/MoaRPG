using Moa.Core.Motion;
using Moa.Unity.Cameras;
using Moa.Unity.Data;
using UnityEngine;

namespace Moa.Unity.Player
{
    /// <summary>
    /// 3인칭 이동 (1-1) — CharacterController로 FixedUpdate에서 움직인다 (CLAUDE.md 핵심 규칙 1 · 2: 이동 · 물리는 Unity).
    /// 계산(카메라 기준 방향 · 가속/감속 · 회전 · 점프 속도 · 경사 판정)은 Core의 MoveMath, 수치는 MovementTuning 에셋.
    /// 비주얼(자식 visual)은 매 프레임 고정 스텝 사이를 보간해 그린다 — 모델을 바꿔도 이 스크립트는 그대로.
    /// GameRoot가 Bind로 연결한다.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMotor : MonoBehaviour
    {
        [SerializeField] MovementTuning tuning;
        [Tooltip("Visual root (graybox now, character model later). Interpolated between physics steps.")]
        [SerializeField] Transform visual;

        CharacterController _cc;
        IPlayerInput _input;
        OrbitCamera _view;

        Planar _planar;
        float _vertical;
        float _yaw;
        bool _grounded;
        bool _onSteep;
        float _bestGroundNormalY;
        Vector3 _groundNormal = Vector3.up;
        Vector3 _velocity;

        Vector3 _prevPos, _currPos;
        Quaternion _prevRot, _currRot;

        public MovementTuning Tuning => tuning;
        public bool Grounded => _grounded;
        public bool OnSteepSlope => _onSteep;
        public bool Walking => _input != null && _input.WalkMode;
        /// <summary>실제로 움직인 속도 (마지막 고정 스텝, m/s).</summary>
        public Vector3 Velocity => _velocity;
        public Vector3 GroundNormal => _groundNormal;
        public float Yaw => _yaw;
        /// <summary>보간된 그리기 위치 (카메라가 따라간다).</summary>
        public Vector3 SmoothedPosition => visual != null ? visual.position : transform.position;

        public void Bind(IPlayerInput input, OrbitCamera view)
        {
            _input = input;
            _view = view;
            _cc = GetComponent<CharacterController>();
            if (tuning == null) { Debug.LogError("PlayerMotor: MovementTuning is not assigned"); enabled = false; return; }
            _cc.height = tuning.CapsuleHeight;
            _cc.radius = tuning.CapsuleRadius;
            _cc.center = new Vector3(0f, tuning.CapsuleHeight * 0.5f, 0f);
            _cc.slopeLimit = tuning.SlopeLimit;
            _cc.stepOffset = tuning.StepOffset;
            _cc.skinWidth = tuning.SkinWidth;
            _yaw = MoveMath.Wrap360(transform.eulerAngles.y);
            ResetMotion();
        }

        /// <summary>[상태 변경] 순간이동 — 속도를 0으로.</summary>
        public void Teleport(Vector3 position)
        {
            if (_cc == null) _cc = GetComponent<CharacterController>();
            _cc.enabled = false;
            transform.position = position;
            _cc.enabled = true;
            ResetMotion();
        }

        void ResetMotion()
        {
            _planar = Planar.Zero;
            _vertical = 0f;
            _velocity = Vector3.zero;
            _grounded = false;
            _onSteep = false;
            _currPos = _prevPos = transform.position;
            _currRot = _prevRot = transform.rotation;
            if (visual != null) visual.SetPositionAndRotation(_currPos, _currRot);
        }

        void FixedUpdate()
        {
            if (_input == null || tuning == null) return;
            float dt = Time.fixedDeltaTime;

            // 수평: 카메라 기준 방향 → 목표 속도 → 가속/감속
            Vector2 move = _input.Move;
            float camYaw = _view != null ? _view.Yaw : _yaw;
            Planar wish = MoveMath.CameraRelative(move.x, move.y, camYaw);
            float wishLen = wish.Length;
            float speed = MoveMath.TargetSpeed(_input.WalkMode, tuning.WalkSpeed, tuning.RunSpeed, wishLen);
            Planar target = wish.Normalized * speed;
            bool grounded = _grounded;
            _planar = MoveMath.Approach(_planar, target,
                grounded ? tuning.GroundAcceleration : tuning.AirAcceleration,
                grounded ? tuning.GroundDeceleration : tuning.AirDeceleration, dt);
            if (wishLen > 0f) _yaw = MoveMath.TurnTowards(_yaw, MoveMath.YawOf(wish), tuning.TurnSpeed, dt);

            // 수직: 점프 · 중력
            bool jump = _input.ConsumeJump();
            bool jumped = false;
            if (grounded)
            {
                if (jump) { _vertical = MoveMath.JumpVelocity(tuning.JumpHeight, tuning.Gravity); jumped = true; }
                else _vertical = -tuning.GroundStickSpeed;
            }
            else
            {
                _vertical = MoveMath.ApplyGravity(_vertical, tuning.Gravity, tuning.MaxFallSpeed, dt);
            }

            // 이동 (CharacterController가 경사 한계 · 계단 높이 · 충돌을 처리)
            Vector3 before = transform.position;
            _bestGroundNormalY = -1f;
            _groundNormal = Vector3.up;
            CollisionFlags flags = _cc.Move(new Vector3(_planar.X, _vertical, _planar.Z) * dt);
            bool below = (flags & CollisionFlags.Below) != 0;
            if ((flags & CollisionFlags.Above) != 0 && _vertical > 0f) _vertical = 0f;

            bool walkable = below && MoveMath.IsWalkable(_groundNormal.y, tuning.SlopeLimit);
            _onSteep = below && !walkable;
            if (grounded && !below && !jumped) _vertical = 0f; // 가장자리에서 떨어질 때 붙어 있던 속도를 버린다
            if (_onSteep && _vertical < 0f && grounded) _vertical = 0f; // 가파른 면에 막 닿음 → 중력으로 미끄러진다
            _grounded = walkable && !jumped;

            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            _velocity = (transform.position - before) / dt;

            _prevPos = _currPos; _prevRot = _currRot;
            _currPos = transform.position; _currRot = transform.rotation;
        }

        void Update()
        {
            if (visual == null) return;
            float alpha = Time.fixedDeltaTime > 0f ? Mathf.Clamp01((Time.time - Time.fixedTime) / Time.fixedDeltaTime) : 1f;
            visual.SetPositionAndRotation(Vector3.Lerp(_prevPos, _currPos, alpha), Quaternion.Slerp(_prevRot, _currRot, alpha));
        }

        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            // 캡슐 아래 반구에 닿은 면 중 가장 평평한 것을 바닥 법선으로
            float bottomSphereY = transform.position.y + _cc.center.y - _cc.height * 0.5f + _cc.radius;
            if (hit.point.y > bottomSphereY) return;
            if (hit.normal.y > _bestGroundNormalY)
            {
                _bestGroundNormalY = hit.normal.y;
                _groundNormal = hit.normal;
            }
        }
    }
}
