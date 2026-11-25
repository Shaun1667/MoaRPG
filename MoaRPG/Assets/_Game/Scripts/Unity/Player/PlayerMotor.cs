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
    /// 1-2: 질주(누르는 동안, 땅 위에서 움직일 때만 스태미나 소모) · 회피(입력 방향, 없으면 앞) — 규칙은 Core의 Stamina · Dodge, 수치는 StaminaTuning 에셋.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMotor : MonoBehaviour
    {
        [SerializeField] MovementTuning tuning;
        [SerializeField] StaminaTuning staminaTuning;
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

        Stamina _stamina;
        Dodge _dodge;
        bool _sprinting;
        bool _airSprint;
        Planar _dodgeDir;

        Vector3 _prevPos, _currPos;
        Quaternion _prevRot, _currRot;

        public MovementTuning Tuning => tuning;
        public StaminaTuning StaminaTuning => staminaTuning;
        /// <summary>스태미나 (Core 규칙, Bind 전에는 null).</summary>
        public Stamina Stamina => _stamina;
        /// <summary>회피 (Core 규칙, Bind 전에는 null).</summary>
        public Dodge Dodge => _dodge;
        /// <summary>지난 고정 스텝에 질주 중이었는지 (땅 위에서 움직이며 소모).</summary>
        public bool Sprinting => _sprinting;
        public bool Dodging => _dodge != null && _dodge.Active;
        public bool Invulnerable => _dodge != null && _dodge.Invulnerable;
        public bool Exhausted => _stamina != null && _stamina.Exhausted;
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
            if (staminaTuning == null) { Debug.LogError("PlayerMotor: StaminaTuning is not assigned"); enabled = false; return; }
            _stamina = new Stamina(staminaTuning.ToStaminaRules());
            _dodge = new Dodge(staminaTuning.ToDodgeRules());
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
            _sprinting = false;
            _airSprint = false;
            _dodge?.Cancel();
            _currPos = _prevPos = transform.position;
            _currRot = _prevRot = transform.rotation;
            if (visual != null) visual.SetPositionAndRotation(_currPos, _currRot);
        }

        void FixedUpdate()
        {
            if (_input == null || tuning == null || _stamina == null) return;
            float dt = Time.fixedDeltaTime;
            bool grounded = _grounded;

            Vector2 move = _input.Move;
            float camYaw = _view != null ? _view.Yaw : _yaw;
            Planar wish = MoveMath.CameraRelative(move.x, move.y, camYaw);
            float wishLen = wish.Length;

            // 회피 (1-2): 입력 방향, 없으면 캐릭터 앞. 시작 조건(쿨 · 공중 · 스태미나)은 Core Dodge
            if (_input.ConsumeDodge() && _dodge.TryStart(_stamina, grounded))
            {
                float r = _yaw * Mathf.Deg2Rad;
                _dodgeDir = wishLen > 0f ? wish.Normalized : new Planar(Mathf.Sin(r), Mathf.Cos(r));
                _yaw = MoveMath.YawOf(_dodgeDir);
            }
            bool dodging = _dodge.Active;
            float dashTime = _dodge.Advance(dt);

            // 질주 (1-2): 누르는 동안 · 땅 위 · 움직일 때만 소모. 공중에서는 뛰어오를 때의 질주 속력을 소모 없이 유지
            bool wantSprint = _input.SprintHeld && wishLen > 0f;
            _sprinting = false;
            if (grounded && !dodging && wantSprint && _stamina.CanDrain)
            {
                _stamina.Drain(staminaTuning.SprintDrainPerSecond, dt);
                _sprinting = true;
            }
            if (grounded) _airSprint = _sprinting;
            bool sprintPace = _sprinting || (!grounded && _airSprint && wantSprint);

            // 수평: 회피 중이면 대시, 아니면 카메라 기준 방향 → 목표 속도 → 가속/감속
            if (dodging)
            {
                _planar = _dodgeDir * (_dodge.Rules.Speed * (dashTime / dt));
            }
            else
            {
                float speed = MoveMath.TargetSpeed(sprintPace, _input.WalkMode, tuning.WalkSpeed, tuning.RunSpeed, staminaTuning.SprintSpeed, wishLen);
                Planar target = wish.Normalized * speed;
                _planar = MoveMath.Approach(_planar, target,
                    grounded ? tuning.GroundAcceleration : tuning.AirAcceleration,
                    grounded ? tuning.GroundDeceleration : tuning.AirDeceleration, dt);
                if (wishLen > 0f) _yaw = MoveMath.TurnTowards(_yaw, MoveMath.YawOf(wish), tuning.TurnSpeed, dt);
            }

            // 수직: 점프 · 중력 (회피 중에는 점프 안 함)
            bool jump = _input.ConsumeJump() && !dodging;
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

            // 회피가 이번 스텝에 끝났으면 지금 입력이 바라는 속력보다 빠른 부분을 버린다 (거리가 정확히 dodgeDistance)
            if (dodging && !_dodge.Active)
            {
                bool sprintNext = wantSprint && _stamina.CanDrain;
                float after = MoveMath.TargetSpeed(sprintNext, _input.WalkMode, tuning.WalkSpeed, tuning.RunSpeed, staminaTuning.SprintSpeed, wishLen);
                if (_planar.Length > after) _planar = _planar.Normalized * after;
            }

            _stamina.Tick(dt);

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
