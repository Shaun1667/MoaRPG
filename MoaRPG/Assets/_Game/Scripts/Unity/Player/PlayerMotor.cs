using Moa.Core.Combat;
using Moa.Core.Motion;
using Moa.Unity.Cameras;
using Moa.Unity.Combat;
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
    /// 1-3: 등반(벽 쪽으로 움직이면 붙음 · 위로 도약 · 올라서기) · 활공(공중에서 점프로 펴고 접음) — 규칙은 Core의 Climb · Glide,
    /// 수치는 ClimbTuning · GlideTuning 에셋. 벽 · 턱 · 높이 검사 레이는 자기 콜라이더를 뺀다.
    /// </summary>
    /// <remarks>2-1: 일반 공격 — 같은 오브젝트의 PlayerCombat(없으면 공격 없음)을 고정 스텝마다 진행한다. 공격 중에는 이동 × attackMoveFactor · 단계마다 앞으로 내딛기,
    /// 회피 · 점프가 공격을 끊고, 공격 중에는 벽에 붙지 않는다. 등반 · 활공 · 공중에서는 공격 입력을 버린다(Core ComboAttack).</remarks>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMotor : MonoBehaviour
    {
        const int MaxProbeHits = 8;
        const float HeightProbeLength = 1000f; // 높이 검사 레이 길이 (게임 수치 아님 — 충분히 길게)

        [SerializeField] MovementTuning tuning;
        [SerializeField] StaminaTuning staminaTuning;
        [SerializeField] ClimbTuning climbTuning;
        [SerializeField] GlideTuning glideTuning;
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

        Climb _climb;
        Glide _glide;
        Vector3 _wallNormal = Vector3.back;
        Vector3 _mantleStart;
        Vector3 _mantleDir;
        float _mantleRise;
        float _heightAboveGround;
        readonly RaycastHit[] _hits = new RaycastHit[MaxProbeHits];

        PlayerCombat _combat;
        bool _hitPending;
        PlayerVitals _vitals; // 3-1: 피격 경직 · 전멸 연출 (없으면 체력 없음)

        Vector3 _prevPos, _currPos;
        Quaternion _prevRot, _currRot;

        public MovementTuning Tuning => tuning;
        public StaminaTuning StaminaTuning => staminaTuning;
        public ClimbTuning ClimbTuning => climbTuning;
        public GlideTuning GlideTuning => glideTuning;
        /// <summary>스태미나 (Core 규칙, Bind 전에는 null).</summary>
        public Stamina Stamina => _stamina;
        /// <summary>회피 (Core 규칙, Bind 전에는 null).</summary>
        public Dodge Dodge => _dodge;
        /// <summary>등반 · 활공 (Core 규칙, Bind 전에는 null).</summary>
        public Climb Climb => _climb;
        public Glide Glide => _glide;
        /// <summary>지난 고정 스텝에 질주 중이었는지 (땅 위에서 움직이며 소모).</summary>
        public bool Sprinting => _sprinting;
        public bool Dodging => _dodge != null && _dodge.Active;
        public bool Invulnerable => _dodge != null && _dodge.Invulnerable;
        public bool Exhausted => _stamina != null && _stamina.Exhausted;
        public bool Climbing => _climb != null && _climb.Active;
        public bool Gliding => _glide != null && _glide.Active;
        /// <summary>땅 위에서 휘두르는 중인지 (2-1 콤보 · 2-2 강공격, PlayerCombat이 없으면 항상 false).</summary>
        public bool Attacking => _combat != null && _combat.Attacking;
        /// <summary>강공격 중인지 (2-2).</summary>
        public bool ChargedAttacking => _combat != null && _combat.ChargedActive;
        /// <summary>낙하 공격 중인지 (떨어지는 중 + 착지 경직, 2-2).</summary>
        public bool Plunging => _combat != null && _combat.Plunging;
        public bool PlungeFalling => _combat != null && _combat.PlungeFalling;
        /// <summary>스킬(E) 중 — 누르는 중 포함 (2-4).</summary>
        public bool SkillActive => _combat != null && _combat.SkillActive;
        /// <summary>궁극기(Q) 중 (2-4) — 이때 회피 · 점프 입력은 버린다.</summary>
        public bool Bursting => _combat != null && _combat.BurstActive;
        /// <summary>궁극기로 무적 (2-4, 회피 무적 Invulnerable과 따로 — 저스트 회피는 회피 무적만).</summary>
        public bool BurstInvulnerable => _combat != null && _combat.BurstInvulnerable;

        public PlayerCombat Combat => _combat;
        /// <summary>적에게 맞아 경직 중 (3-1) — 이동 · 공격 · 점프 입력을 버리고 밀려난다, 회피로 끊긴다.</summary>
        public bool HitStunned => _vitals != null && _vitals.Stunned;
        /// <summary>전원 쓰러져 어두워졌다 되살아나는 중 (3-1) — 입력을 모두 버린다.</summary>
        public bool Wiped => _vitals != null && _vitals.Wiping;
        public PlayerVitals Vitals => _vitals;
        /// <summary>붙어 있는 벽의 법선 (등반 중일 때만 의미 있음).</summary>
        public Vector3 WallNormal => _wallNormal;
        /// <summary>발 아래 땅까지 높이 (m, 지난 고정 스텝, 땅이 없으면 매우 큼).</summary>
        public float HeightAboveGround => _heightAboveGround;
        public bool Grounded => _grounded;
        public bool OnSteepSlope => _onSteep;
        public bool Walking => _input != null && _input.WalkMode;
        /// <summary>실제로 움직인 속도 (마지막 고정 스텝, m/s).</summary>
        public Vector3 Velocity => _velocity;
        public Vector3 GroundNormal => _groundNormal;
        public float Yaw => _yaw;
        /// <summary>보간된 그리기 위치 (카메라가 따라간다).</summary>
        public Vector3 SmoothedPosition => visual != null ? visual.position : transform.position;

        Vector3 Center => transform.position + Vector3.up * _cc.center.y;
        Vector3 Forward { get { float r = _yaw * Mathf.Deg2Rad; return new Vector3(Mathf.Sin(r), 0f, Mathf.Cos(r)); } }

        public void Bind(IPlayerInput input, OrbitCamera view)
        {
            _input = input;
            _view = view;
            _cc = GetComponent<CharacterController>();
            if (tuning == null) { Debug.LogError("PlayerMotor: MovementTuning is not assigned"); enabled = false; return; }
            if (staminaTuning == null) { Debug.LogError("PlayerMotor: StaminaTuning is not assigned"); enabled = false; return; }
            if (climbTuning == null) { Debug.LogError("PlayerMotor: ClimbTuning is not assigned"); enabled = false; return; }
            if (glideTuning == null) { Debug.LogError("PlayerMotor: GlideTuning is not assigned"); enabled = false; return; }
            _stamina = new Stamina(staminaTuning.ToStaminaRules());
            _dodge = new Dodge(staminaTuning.ToDodgeRules());
            _climb = new Climb(climbTuning.ToRules());
            _glide = new Glide(glideTuning.ToRules());
            _combat = GetComponent<PlayerCombat>();
            _vitals = GetComponent<PlayerVitals>();
            _cc.height = tuning.CapsuleHeight;
            _cc.radius = tuning.CapsuleRadius;
            _cc.center = new Vector3(0f, tuning.CapsuleHeight * 0.5f, 0f);
            _cc.slopeLimit = tuning.SlopeLimit;
            _cc.stepOffset = tuning.StepOffset;
            _cc.skinWidth = tuning.SkinWidth;
            _yaw = MoveMath.Wrap360(transform.eulerAngles.y);
            ResetMotion();
        }

        /// <summary>[상태 변경] 순간이동 — 속도를 0으로 (등반 · 활공도 멈춤).</summary>
        public void Teleport(Vector3 position)
        {
            if (_cc == null) _cc = GetComponent<CharacterController>();
            _cc.enabled = false;
            transform.position = position;
            _cc.enabled = true;
            ResetMotion();
        }

        /// <summary>[상태 변경] 등반 중이면 떨어진다 (다시 붙기 지연 없음).</summary>
        public void DropClimb()
        {
            if (_climb != null && _climb.Active) Drop(ClimbEnd.Cancelled);
        }

        /// <summary>[상태 변경] 활공을 펼친다 (공중 · 높이 · 스태미나 조건은 그대로). 펼쳤으면 true.</summary>
        public bool OpenGlide()
        {
            if (_glide == null || _climb.Active || Plunging) return false;
            _heightAboveGround = MeasureHeight();
            return _glide.TryOpen(_stamina, _grounded, _heightAboveGround);
        }

        /// <summary>[상태 변경] 활공을 접는다.</summary>
        public void CloseGlide() => _glide?.Close(GlideEnd.Toggled);

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
            _climb?.Cancel();
            _glide?.Cancel();
            if (_combat != null) _combat.Cancel();
            _hitPending = false;
            _currPos = _prevPos = transform.position;
            _currRot = _prevRot = transform.rotation;
            if (visual != null) visual.SetPositionAndRotation(_currPos, _currRot);
        }

        void FixedUpdate()
        {
            if (_input == null || tuning == null || _stamina == null) return;
            float dt = Time.fixedDeltaTime;
            if (_vitals != null) _vitals.Step(dt); // 3-1: 경직 · 전멸 연출 (부활하면 이 안에서 부활 지점으로 옮겨진다)
            Vector3 before = transform.position;

            Vector2 move = _input.Move;
            float camYaw = _view != null ? _view.Yaw : _yaw;
            Planar wish = MoveMath.CameraRelative(move.x, move.y, camYaw);

            _heightAboveGround = MeasureHeight();
            if (!_climb.Active && !_dodge.Active && !Attacking && !Plunging && !HitStunned && !Wiped) TryAttachWall(wish); // 공격 · 낙하 중에는 벽에 안 붙는다

            // 등반 중에는 회복하지 않는다 (움직이지 않고 매달려 있어도, 1-3)
            bool climbStep = _climb.Active;
            if (climbStep) StepClimb(move, dt);
            else StepFree(wish, dt);

            _climb.Tick(dt);
            _stamina.Tick(dt, !climbStep);

            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);

            // 판정 구간이면 이동이 끝난 자리에서 겹침 검사 (2-1)
            if (_hitPending) { _hitPending = false; _combat.CheckHits(transform.position, _yaw); }
            _velocity = (transform.position - before) / dt;

            _prevPos = _currPos; _prevRot = _currRot;
            _currPos = transform.position; _currRot = transform.rotation;
        }

        // ---- 땅 · 공중 · 활공 ----

        void StepFree(Planar wish, float dt)
        {
            bool grounded = _grounded;
            // 3-1: 피격 경직 · 전멸 연출 중에는 이동 입력을 버린다 (경직은 회피로 끊을 수 있다)
            bool wiping = Wiped;
            Planar dodgeWish = wish; // 경직 중 회피는 입력 방향으로
            if (HitStunned || wiping) wish = Planar.Zero;
            float wishLen = wish.Length;

            // 회피 (1-2): 입력 방향, 없으면 캐릭터 앞. 시작 조건(쿨 · 공중 · 스태미나)은 Core Dodge
            if (_input.ConsumeDodge() && !Bursting && !wiping && _dodge.TryStart(_stamina, grounded)) // 궁극기 중 회피는 버린다 (2-4)
            {
                if (_combat != null) _combat.Interrupt(AttackEnd.Dodge); // 회피가 공격을 끊는다 (2-1, 원신 방식)
                if (HitStunned) _vitals.EndStun(); // 회피가 피격 경직도 끊는다 (3-1)
                float r = _yaw * Mathf.Deg2Rad;
                _dodgeDir = dodgeWish.Length > 0f ? dodgeWish.Normalized : new Planar(Mathf.Sin(r), Mathf.Cos(r));
                _yaw = MoveMath.YawOf(_dodgeDir);
            }
            bool dodging = _dodge.Active;
            float dashTime = _dodge.Advance(dt);

            // 점프: 땅 위면 점프, 공중이면 활공 펴기 · 접기 (1-3). 낙하 중에는 버린다 (2-2)
            bool jumpPressed = _input.ConsumeJump() && !dodging && !PlungeFalling && !Bursting && !HitStunned && !wiping; // 3-1: 경직 · 전멸 중에도 // 궁극기 중에도 버린다 (2-4)
            if (jumpPressed && grounded && (Attacking || Plunging)) _combat.Interrupt(AttackEnd.Jump); // 점프도 공격 · 착지 경직을 끊는다 (2-1 · 2-2)
            if (jumpPressed && !grounded)
            {
                if (_glide.Active) _glide.Close(GlideEnd.Toggled);
                else _glide.TryOpen(_stamina, false, _heightAboveGround);
            }

            // 공격 (2-1 · 2-2): 입력 → 콤보 · 강공격(길게 누르기) · 낙하(공중) 고르기 → 진행.
            // 새 단계(베기)가 시작되면 몸 방향(이동 입력 → 가장 가까운 적 → 그대로). 낙하가 시작되면 활공을 접는다
            AttackTick attackTick = CombatStep(new AttackContext(grounded && !jumpPressed, dodging, false, _glide.Active, _heightAboveGround, transform.position.y), dt);
            if (attackTick.PlungeStarted)
            {
                if (_glide.Active) _glide.Close(GlideEnd.Plunge);
                _airSprint = false;
            }
            bool gliding = _glide.Active && _glide.Hold(_stamina, dt);
            ComboTick attack = attackTick.Swing;
            if (attack.StepStarted && _combat.PickFacing(transform.position, wish, out float attackYaw)) _yaw = attackYaw;
            bool attacking = Attacking || attack.Lunge > 0f;
            bool plungeFalling = PlungeFalling;
            bool plunging = Plunging;
            _hitPending = attack.HitOpen;

            // 질주 (1-2): 누르는 동안 · 땅 위 · 움직일 때만 소모. 공중에서는 뛰어오를 때의 질주 속력을 소모 없이 유지
            bool wantSprint = _input.SprintHeld && wishLen > 0f;
            _sprinting = false;
            if (grounded && !dodging && !attacking && !plunging && wantSprint && _stamina.CanDrain) // 공격 · 낙하 중에는 질주 없음 (2-1 · 2-2)
            {
                _stamina.Drain(staminaTuning.SprintDrainPerSecond, dt);
                _sprinting = true;
            }
            if (grounded) _airSprint = _sprinting;
            if (gliding) _airSprint = false;
            bool sprintPace = _sprinting || (!grounded && _airSprint && wantSprint);

            // 수평: 회피 중이면 대시, 활공 중이면 입력 방향(없으면 앞)으로 활공 속력, 아니면 카메라 기준 방향 → 목표 속도 → 가속/감속
            if (dodging)
            {
                _planar = _dodgeDir * (_dodge.Rules.Speed * (dashTime / dt));
            }
            else if (HitStunned)
            {
                // 피격 경직 (3-1): 공격한 쪽 반대로 같은 빠르기로 밀려난다 (거리 = knockbackDistance), 몸은 안 돈다
                Vector3 k = _vitals.KnockbackVelocity;
                _planar = new Planar(k.x, k.z);
            }
            else if (plunging)
            {
                // 낙하 공격 (2-2): 떨어지는 동안 · 착지 경직 동안 수평 이동 없음, 몸도 안 돈다
                _planar = Planar.Zero;
            }
            else if (attacking)
            {
                // 공격 중 (2-1): 몸 앞으로 내딛기 + 이동 입력 × attackMoveFactor (0이면 멈춤), 몸은 단계가 시작될 때만 돈다
                Vector3 f = Forward;
                float factor = _combat.Weapon.AttackMoveFactor;
                Planar walk = wishLen > 0f && factor > 0f
                    ? wish.Normalized * (MoveMath.TargetSpeed(false, _input.WalkMode, tuning.WalkSpeed, tuning.RunSpeed, staminaTuning.SprintSpeed, wishLen) * factor)
                    : Planar.Zero;
                _planar = new Planar(f.x, f.z) * (attack.Lunge / dt) + walk;
            }
            else if (gliding)
            {
                if (wishLen > 0f) _yaw = MoveMath.TurnTowards(_yaw, MoveMath.YawOf(wish), tuning.TurnSpeed, dt);
                Vector3 f = Forward;
                Planar dir = wishLen > 0f ? wish.Normalized : new Planar(f.x, f.z);
                _planar = _glide.Horizontal(_planar, dir, dt);
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

            // 수직: 점프 · 중력 · 활공 낙하 (회피 중에는 점프 안 함)
            bool jumped = false;
            if (grounded)
            {
                if (jumpPressed) { _vertical = MoveMath.JumpVelocity(tuning.JumpHeight, tuning.Gravity); jumped = true; }
                else _vertical = -tuning.GroundStickSpeed;
            }
            else if (plungeFalling)
            {
                // 낙하 속력으로 곧장 아래로 (이미 더 빠르게 떨어지고 있으면 그 속력)
                _vertical = Mathf.Min(_vertical, -_combat.Plunge.Rules.FallSpeed);
            }
            else if (gliding)
            {
                _vertical = _glide.Vertical(_vertical, dt);
            }
            else
            {
                _vertical = MoveMath.ApplyGravity(_vertical, tuning.Gravity, tuning.MaxFallSpeed, dt);
            }

            // 이동 (CharacterController가 경사 한계 · 계단 높이 · 충돌을 처리)
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
            if (below && _glide.Active) _glide.Close(GlideEnd.Landed);

            // 낙하 공격 (2-2): 떨어지다 무언가에 닿으면(가파른 면 포함) 착지 — 배율을 정하고 그 자리에서 범위 판정 한 번
            if (plungeFalling && below) { _combat.Land(transform.position); _planar = Planar.Zero; }

            // 휘두르는 중 발밑이 사라지면(내딛다 가장자리를 넘음) 공격을 끊는다 (2-1)
            if (Attacking && !_grounded && !jumped) { _combat.InterruptSwing(AttackEnd.Airborne); _hitPending = false; }
            if (attacking && !Attacking) _planar = Planar.Zero; // 내딛기 속도를 다음 스텝으로 넘기지 않는다

            // 회피가 이번 스텝에 끝났으면 지금 입력이 바라는 속력보다 빠른 부분을 버린다 (거리가 정확히 dodgeDistance)
            if (dodging && !_dodge.Active)
            {
                bool sprintNext = wantSprint && _stamina.CanDrain;
                float after = MoveMath.TargetSpeed(sprintNext, _input.WalkMode, tuning.WalkSpeed, tuning.RunSpeed, staminaTuning.SprintSpeed, wishLen);
                if (_planar.Length > after) _planar = _planar.Normalized * after;
            }
        }

        /// <summary>공격 입력을 꺼내 콤보를 한 스텝 진행 (PlayerCombat이 없거나 연결 전이면 입력만 버린다, 2-1).</summary>
        AttackTick CombatStep(AttackContext ctx, float dt)
        {
            if (_combat == null || !_combat.Bound) { _input.ConsumeAttack(); return default; }
            return _combat.Step(ctx, _stamina, dt);
        }

        // ---- 등반 (1-3) ----

        /// <summary>입력 방향(활공 중이면 입력이 없어도 앞)으로 벽이 가까우면 붙는다. 조건은 Core Climb.</summary>
        void TryAttachWall(Planar wish)
        {
            Vector3 dir;
            if (wish.Length > 0f) dir = new Vector3(wish.X, 0f, wish.Z).normalized;
            else if (_glide.Active) dir = Forward;
            else return;
            if (!Probe(Center, dir, _cc.radius + climbTuning.ProbeDistance, out RaycastHit hit)) return;
            if (hit.collider.GetComponentInParent<Moa.Unity.Combat.IHitTarget>() != null) return; // 2-5: 적 · 허수아비 몸에는 붙지 않는다
            float inputAngle = Climb.InputAngle(dir.x, dir.z, hit.normal.x, hit.normal.z);
            if (!_climb.TryAttach(_stamina, hit.normal.y, inputAngle)) return;
            if (_glide.Active) _glide.Close(GlideEnd.Wall);
            _sprinting = false;
            _airSprint = false;
            _planar = Planar.Zero;
            _vertical = 0f;
            _grounded = false;
            _onSteep = false;
            SetWall(hit.normal);
        }

        void SetWall(Vector3 normal)
        {
            _wallNormal = normal;
            if (normal.x * normal.x + normal.z * normal.z > 1e-6f) _yaw = MoveMath.YawOf(new Planar(-normal.x, -normal.z));
        }

        /// <summary>벽 위: 입력 x = 벽을 따라 오른쪽, y = 위. 움직일 때만 소모 · 점프 = 위로 도약 · 회피 = 떨어짐 · 턱 = 올라서기.</summary>
        void StepClimb(Vector2 move, float dt)
        {
            _dodge.Advance(dt); // 쿨만 줄어든다
            CombatStep(new AttackContext(false, false, true, false), dt); // 등반 중 공격 입력은 버린다 (2-1)
            _sprinting = false;
            _grounded = false;
            _onSteep = false;

            if (_climb.Mantling) { StepMantle(dt); return; }
            if (_input.ConsumeDodge()) { _input.ConsumeJump(); Drop(ClimbEnd.Dodge); return; }

            Vector3 into = new Vector3(-_wallNormal.x, 0f, -_wallNormal.z);
            if (into.sqrMagnitude < 1e-6f) into = Forward;
            into.Normalize();
            float reach = _cc.radius + climbTuning.ProbeDistance;
            bool body = Probe(Center, into, reach, out RaycastHit bodyHit);
            bool ledge = Probe(transform.position + Vector3.up * (_cc.height - climbTuning.LedgeHeight), into, reach, out _);
            if (body && !ledge && TryStartMantle(bodyHit, into)) { StepMantle(dt); return; }
            if (!body || !_climb.IsClimbableWall(bodyHit.normal.y)) { Drop(ClimbEnd.WallEnded); return; }
            SetWall(bodyHit.normal);

            if (_input.ConsumeJump() && !_climb.Leaping) _climb.TryLeap(_stamina);

            Vector3 right = Vector3.Cross(_wallNormal, Vector3.up).normalized;
            Vector3 up = Vector3.Cross(right, _wallNormal).normalized;
            Vector3 delta;
            bool moving = false;
            float upInput = 0f;
            if (_climb.Leaping)
            {
                delta = up * (_climb.Rules.JumpSpeed * _climb.AdvanceLeap(dt));
            }
            else
            {
                Vector2 m = move.sqrMagnitude > 1f ? move.normalized : move;
                moving = m.sqrMagnitude > 0f;
                upInput = m.y;
                delta = (right * m.x + up * m.y) * (climbTuning.ClimbSpeed * dt);
            }
            if (!_climb.Hold(_stamina, moving, dt)) { Drop(ClimbEnd.Exhausted); return; }

            // 벽에 붙여 둔다 (몸 레이가 잰 틈만큼 벽 쪽으로)
            delta += into * Mathf.Max(0f, bodyHit.distance - _cc.radius);
            _bestGroundNormalY = -1f;
            _groundNormal = Vector3.up;
            CollisionFlags flags = _cc.Move(delta);
            _planar = Planar.Zero;
            _vertical = 0f;

            // 내려가다 땅에 닿으면 끝
            if (upInput < 0f && (flags & CollisionFlags.Below) != 0 && MoveMath.IsWalkable(_groundNormal.y, tuning.SlopeLimit))
            {
                Drop(ClimbEnd.Landed);
                _grounded = true;
            }
        }

        /// <summary>턱 레이에 벽이 없으면 벽 너머 위에서 아래로 쏘아 디딜 면을 찾는다. 찾으면 올라서기 시작.</summary>
        bool TryStartMantle(RaycastHit bodyHit, Vector3 into)
        {
            float feet = transform.position.y;
            Vector3 origin = new Vector3(bodyHit.point.x, feet + _cc.height + _cc.skinWidth, bodyHit.point.z) + into * _cc.radius;
            if (!Probe(origin, Vector3.down, _cc.height + _cc.skinWidth, out RaycastHit top)) return false;
            if (!MoveMath.IsWalkable(top.normal.y, tuning.SlopeLimit) || top.point.y <= feet) return false;
            if (!_climb.StartMantle()) return false;
            _mantleStart = transform.position;
            _mantleRise = top.point.y - feet + _cc.skinWidth;
            _mantleDir = into;
            return true;
        }

        void StepMantle(float dt)
        {
            _input.ConsumeJump();
            _input.ConsumeDodge();
            float progress = _climb.AdvanceMantle(dt);
            Climb.MantlePath(progress, out float up01, out float forward01);
            Vector3 target = _mantleStart + Vector3.up * (_mantleRise * up01) + _mantleDir * (climbTuning.MantleForward * forward01);
            _bestGroundNormalY = -1f;
            _groundNormal = Vector3.up;
            _cc.Move(target - transform.position);
            _planar = Planar.Zero;
            _vertical = 0f;
        }

        void Drop(ClimbEnd reason)
        {
            _climb.End(reason);
            _planar = Planar.Zero;
            _vertical = 0f;
        }

        // ---- 검사 레이 (자기 콜라이더 제외) ----

        float MeasureHeight()
        {
            float lift = _cc.radius;
            return Probe(transform.position + Vector3.up * lift, Vector3.down, HeightProbeLength, out RaycastHit hit)
                ? Mathf.Max(0f, hit.distance - lift)
                : HeightProbeLength;
        }

        bool Probe(Vector3 origin, Vector3 direction, float distance, out RaycastHit best)
        {
            best = default;
            int n = Physics.RaycastNonAlloc(origin, direction, _hits, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            bool found = false;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                Collider c = _hits[i].collider;
                if (c == null || c == _cc || c.transform.IsChildOf(transform)) continue;
                if (_hits[i].distance < bestDistance) { bestDistance = _hits[i].distance; best = _hits[i]; found = true; }
            }
            return found;
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
