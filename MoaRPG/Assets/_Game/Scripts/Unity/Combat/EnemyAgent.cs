using Moa.Core.Combat;
using Moa.Core.Motion;
using Moa.Unity.Data;
using UnityEngine;
using UnityEngine.AI;
using EntityId = Moa.Core.Ids.EntityId; // UnityEngine.EntityId와 같은 이름

namespace Moa.Unity.Combat
{
    /// <summary>길찾기 상태 (3-4, 디버그 · 확인용, 끝에만 추가).</summary>
    public enum EnemyPathState
    {
        /// <summary>아직 움직이지 않음.</summary>
        None,
        /// <summary>NavMesh 길 전체를 따라감.</summary>
        Complete,
        /// <summary>목적지까지 못 가는 길 — 갈 수 있는 데까지.</summary>
        Partial,
        /// <summary>NavMesh가 없거나 길이 없어 곧장 (2-5 방식).</summary>
        Straight,
    }

    /// <summary>
    /// 적 (2-5 근접 · 3-4 원거리 · 정예). 상태 기계는 Core EnemyBrain(대기 · 순찰 · 추적 · 공격 · 복귀 · 경직 · 그로기 · 쓰러짐, 공격 표를 차례로),
    /// 체력 · 경직은 Core Health · Poise, 수치는 EnemyTuning(적 한 종류 = 에셋 하나, BALANCE 11 · 16번). 적 종류별 분기 코드 없음 — 공격 줄의 값으로만.
    /// 여기는 Unity 쪽만: 고정 스텝마다 플레이어까지 거리 · 시야선(레이)을 재서 Brain에 넘기고, 나온 속도로 CharacterController를 움직인다.
    /// 3-4 길찾기: Brain이 목적지를 정하면 <see cref="Steer"/>가 NavMeshAgent로 길을 재서 다음 꺾임 지점을 돌려준다(이동 · 회전 수치는 EnemyTuning 그대로).
    /// NavMeshAgent는 길 계산만(updatePosition · updateRotation 끔) — 움직이는 것은 CharacterController. NavMesh가 없으면 곧장(2-5와 같음).
    /// 근접 공격이 닿는 스텝에 플레이어가 맞는 자리면 PlayerDefense.ReceiveEnemyAttack → 저스트 · 피함 · 맞음 (2-3과 같은 창구).
    /// 투사체 줄은 예고가 끝나는 스텝에 EnemyProjectiles로 한 발 — 닿으면 같은 창구. 넘어짐은 줄의 knockdownTime.
    /// 맞는 쪽은 IHitTarget · IPoiseTarget · IEnergyDrop (PlayerCombat · 피해 숫자 · 경직 막대 · 처치 에너지가 그대로 붙는다).
    /// 실행 순서를 플레이어(PlayerMotor) 뒤로 둔다 — 같은 스텝의 회피 상태를 본다. GameRoot가 Bind · SetId로 연결한다.
    /// 예고 = 앞 부채꼴 선(LineRenderer)이 warnStart → warnEnd 색, 닿는 순간 hitColor. 돌진은 돌진 길이만큼 좁은 길, 투사체는 조준선. 색 · 두께 · 기울기는 Setup 표.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(CharacterController))]
    public sealed class EnemyAgent : MonoBehaviour, IHitTarget, IPoiseTarget, IEnergyDrop, IEnemyPath, IRewardDrop
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        const int MaxRayHits = 16;
        const int FanSegments = 24;
        const int MaxCorners = 32;
        const float CornerReached = 0.01f; // m: 이보다 가까운 꺾임은 지난 것으로 (숫자 오차)

        [SerializeField] EnemyTuning tuning;
        [Tooltip("Drawn body (hidden while dead, pushed on a hit)")]
        [SerializeField] Transform visual;
        [SerializeField] Renderer[] flashRenderers;
        [SerializeField] Color flashColor;
        [SerializeField] Color groggyColor;
        [Tooltip("Body color while warning an attack")]
        [SerializeField] Color telegraphColor;
        [Tooltip("Degrees the body leans back while groggy (visual only)")]
        [SerializeField] float groggyTilt;
        [Tooltip("Patrol point relative to home (m, world). Zero = stands still")]
        [SerializeField] Vector3 patrolOffset;
        [Tooltip("3-4: shoots the projectile rows (empty = melee only)")]
        [SerializeField] EnemyProjectiles projectiles;
        [Header("Warning fan")]
        [SerializeField] LineRenderer warningFan;
        [SerializeField] Color warnStartColor;
        [SerializeField] Color warnEndColor;
        [SerializeField] Color hitColor;
        [Tooltip("s the fan stays in hitColor after the hit lands (visual only)")]
        [SerializeField] float hitFlashTime;
        [Tooltip("m above the feet the fan is drawn")]
        [SerializeField] float fanHeight;

        readonly RaycastHit[] _rayHits = new RaycastHit[MaxRayHits];
        readonly Vector3[] _corners = new Vector3[MaxCorners];
        CharacterController _cc;
        NavMeshAgent _nav;
        NavMeshPath _path;
        bool _navChecked, _navOn;
        Health _health;
        Poise _poise;
        EnemyBrain _brain;
        PlayerDefense _target;
        EntityId _id;
        MaterialPropertyBlock _block;
        Vector3 _home;
        float _homeYaw;
        float _yaw;
        float _vertical;
        Vector3 _prevPos, _currPos;
        Quaternion _prevRot, _currRot;
        Vector3 _visualHome;
        Quaternion _visualHomeRot = Quaternion.identity;
        Vector3 _knockDir;
        float _knockAge = -1f;
        float _flashLeft;
        float _hitFlashLeft;
        int _fanAttack; // 닿은 색을 보일 때의 공격 줄 (자세가 끝나 다음 줄로 넘어가도 그 줄 모양)
        int _shownColor = -1; // 0 = 그대로, 1 = 번쩍임, 2 = 그로기, 3 = 예고

        public EnemyTuning Tuning => tuning;
        /// <summary>상태 기계 (Core, Awake 전에는 null).</summary>
        public EnemyBrain Brain => _brain;
        public Health Health => _health;
        public EntityId Id => _id;
        public bool Alive => _health != null && !_health.IsDead;
        public float Defense => tuning != null ? tuning.Defense : 0f;
        public Vector3 Position => transform.position;
        public Vector3 NumberPosition => transform.position + Vector3.up * (tuning != null ? tuning.NumberHeight : 0f);
        public Poise Poise => _poise;
        public bool Groggy => _poise != null && _poise.Groggy;
        public float DamageTakenMultiplier => _poise != null ? _poise.DamageTakenMultiplier : 1f;
        public float EnergyOnDefeat => tuning != null ? tuning.EnergyOnDefeat : 0f;
        /// <summary>3-5: 처치 보상 (적 표).</summary>
        public int GoldOnDefeat => tuning != null ? tuning.GoldOnDefeat : 0;
        public int ExpItemsOnDefeat => tuning != null ? tuning.ExpItemsOnDefeat : 0;
        public EnemyState State => _brain != null ? _brain.State : EnemyState.Idle;
        public Vector3 Home => _home;
        public bool Bound => _target != null;
        public EnemyProjectiles Projectiles => projectiles;
        /// <summary>[상태 변경] 멈춤 (디버그) — 서 있고 공격 주기도 처음으로.</summary>
        public bool Paused { get => _brain != null && _brain.Paused; set { if (_brain != null) _brain.Paused = value; } }
        /// <summary>마지막으로 닿은 공격의 판정 · 맞힌 수 · 빗나간 수 (디버그 · 확인용).</summary>
        public DodgeJudgement LastJudgement { get; private set; }
        public int JudgedCount { get; private set; }
        public int HitCount { get; private set; }
        public bool Flashing => _flashLeft > 0f;

        /// <summary>3-4 길찾기 (디버그 · 확인용): NavMesh 위인지 · 마지막 길 상태 · 꺾임 수 · 남은 길 · 다음 지점.</summary>
        public bool UsesNavMesh => _navOn;
        public bool OnNavMesh => _navOn && _nav != null && _nav.isOnNavMesh;
        public EnemyPathState PathState { get; private set; }
        public int PathCorners { get; private set; }
        public float PathRemaining { get; private set; }
        public Vector3 PathNext { get; private set; }
        /// <summary>길이 곧장과 다른 쪽(꺾임)을 가리킨 스텝 수 — 벽을 돌아간 흔적.</summary>
        public int DetourSteps { get; private set; }

        void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _block = new MaterialPropertyBlock();
            if (tuning == null) { Debug.LogError("EnemyAgent: EnemyTuning is not assigned"); enabled = false; return; }
            _cc.height = tuning.CapsuleHeight;
            _cc.radius = tuning.CapsuleRadius;
            _cc.center = new Vector3(0f, tuning.CapsuleHeight * 0.5f, 0f);
            _health = new Health(tuning.MaxHealth);
            _health.Died += OnDied;
            if (tuning.MaxPoise > 0f) _poise = new Poise(tuning.ToPoiseRules());
            if (tuning.AttackCount == 0) Debug.LogWarning("EnemyAgent: " + tuning.name + " has no attack rows - it never attacks");
            _home = transform.position;
            _homeYaw = transform.eulerAngles.y;
            _yaw = _homeYaw;
            _brain = new EnemyBrain(tuning.ToAiRules(), new Planar(_home.x, _home.z), new Planar(patrolOffset.x, patrolOffset.z));
            _brain.Path = this;
            if (visual != null) { _visualHome = visual.localPosition; _visualHomeRot = visual.localRotation; }
            if (warningFan != null) { warningFan.positionCount = 0; warningFan.enabled = false; }
            if (projectiles != null) projectiles.PlayerHit += OnProjectileHit;
            _prevPos = _currPos = transform.position;
            _prevRot = _currRot = transform.rotation;
        }

        /// <summary>쫓고 칠 대상 (플레이어의 적 공격 창구).</summary>
        public void Bind(PlayerDefense target) => _target = target;

        public void SetId(EntityId id) => _id = id;

        /// <summary>
        /// NavMesh가 있는지 본다 (첫 고정 스텝 — NavMeshSurface가 자기 데이터를 올린 뒤). 있으면 NavMeshAgent를 켜서 길 계산에만 쓴다.
        /// 컴포넌트는 Setup이 꺼 둔 채로 넣는다 — NavMesh가 없는 씬에서 켜지며 오류를 내지 않게.
        /// </summary>
        void CheckNavMesh()
        {
            _navChecked = true;
            _navOn = false;
            if (_nav == null) _nav = GetComponent<NavMeshAgent>();
            if (_nav == null) return;
            _nav.updatePosition = false;
            _nav.updateRotation = false;
            _nav.radius = tuning.CapsuleRadius;
            _nav.height = tuning.CapsuleHeight;
            if (!NavMesh.SamplePosition(transform.position, out NavMeshHit hit, tuning.CapsuleHeight, _nav.areaMask))
            {
                _nav.enabled = false;
                PathState = EnemyPathState.Straight;
                return;
            }
            _nav.enabled = true;
            _nav.Warp(transform.position);
            _nav.isStopped = true; // 스스로 가지 않는다 (길만 잰다)
            if (_path == null) _path = new NavMeshPath();
            _navOn = _nav.isOnNavMesh;
        }

        /// <summary>
        /// IEnemyPath (3-4): self에서 destination으로 가는 NavMesh 길의 다음 꺾임 지점. 목적지가 NavMesh 밖이면 가까운 NavMesh 점까지,
        /// 길이 없거나 NavMesh가 없으면 destination 그대로(곧장).
        /// </summary>
        public Planar Steer(Planar self, Planar destination)
        {
            if (!OnNavMesh) { SetStraight(destination); return destination; }
            Vector3 from = transform.position;
            _nav.nextPosition = from;
            Vector3 to = new Vector3(destination.X, from.y, destination.Z);
            if (!NavMesh.SamplePosition(to, out NavMeshHit hit, tuning.CapsuleHeight * 2f, _nav.areaMask)) { SetStraight(destination); return destination; }
            if (!_nav.CalculatePath(hit.position, _path) || _path.status == NavMeshPathStatus.PathInvalid) { SetStraight(destination); return destination; }
            int n = _path.GetCornersNonAlloc(_corners);
            if (n < 2) { SetStraight(destination); return destination; }
            PathState = _path.status == NavMeshPathStatus.PathComplete ? EnemyPathState.Complete : EnemyPathState.Partial;
            PathCorners = n;
            float remaining = 0f;
            for (int i = 1; i < n; i++) remaining += Vector3.Distance(_corners[i - 1], _corners[i]);
            PathRemaining = remaining;
            Vector3 next = _corners[n - 1];
            for (int i = 1; i < n; i++)
            {
                Vector3 d = _corners[i] - from;
                d.y = 0f;
                if (d.sqrMagnitude > CornerReached * CornerReached) { next = _corners[i]; break; }
            }
            PathNext = next;
            if (n > 2) DetourSteps++;
            return new Planar(next.x, next.z);
        }

        void SetStraight(Planar destination)
        {
            PathState = EnemyPathState.Straight;
            PathCorners = 0;
            PathRemaining = (destination - new Planar(transform.position.x, transform.position.z)).Length;
            PathNext = new Vector3(destination.X, transform.position.y, destination.Z);
        }

        void FixedUpdate()
        {
            if (_brain == null) return;
            float dt = Time.fixedDeltaTime;
            if (!_navChecked) CheckNavMesh();
            if (projectiles != null) projectiles.Step(dt); // 날아가는 것은 쓰러져도 끝까지
            if (_hitFlashLeft > 0f) _hitFlashLeft -= dt;
            if (Alive) _poise?.Advance(dt);

            _prevPos = _currPos;
            _prevRot = _currRot;
            EnemyTick t = _brain.Step(dt, Sense(), Groggy);
            if (t.Respawned) PlaceAtHome();
            if (t.Healed) { _health.Restore(); _poise?.Reset(); }
            if (!Alive) { _currPos = transform.position; _currRot = transform.rotation; return; }

            _yaw = t.Yaw;
            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            if (_cc.enabled)
            {
                _vertical -= tuning.Gravity * dt;
                CollisionFlags flags = _cc.Move(new Vector3(t.Velocity.X, _vertical, t.Velocity.Z) * dt);
                if ((flags & CollisionFlags.Below) != 0) _vertical = -tuning.Gravity * dt; // 땅에 붙여 둔다 (한 스텝 중력만큼)
            }
            if (OnNavMesh) _nav.nextPosition = transform.position;
            _currPos = transform.position;
            _currRot = transform.rotation;

            if (t.FireProjectile) { Fire(t.AttackIndex); _hitFlashLeft = hitFlashTime; _fanAttack = t.AttackIndex; }
            if (!t.AttackLanded) return;
            _hitFlashLeft = hitFlashTime;
            _fanAttack = t.AttackIndex;
            if (_target == null) return;
            // 맞는 자리면 판정, 예고 중 그 자리에 있다 회피로 빠져나갔으면 무적일 때만 판정(저스트 · 피함)
            if (!t.AttackHits && !(t.AttackGrazed && _target.Invulnerable)) return;
            var row = tuning.GetAttack(t.AttackIndex);
            JudgedCount++;
            LastJudgement = _target.ReceiveEnemyAttack(transform.position, tuning.Attack, row != null ? row.multiplier : 0f, row != null ? row.knockdownTime : 0f); // 3-1 피해 · 3-4 넘어짐
        }

        /// <summary>투사체 줄 한 발 (3-4): 눈 높이에서 플레이어 쪽(없으면 몸 앞)으로 수평. 배율 · 줄 번호는 쏠 때 값.</summary>
        void Fire(int attackIndex)
        {
            var row = tuning.GetAttack(attackIndex);
            if (row == null || projectiles == null) { Debug.LogWarning("EnemyAgent: " + name + " has a projectile row but no EnemyProjectiles"); return; }
            Vector3 p = transform.position;
            Vector3 dir = transform.forward;
            if (_target != null)
            {
                Vector3 d = _target.transform.position - p;
                d.y = 0f;
                if (d.sqrMagnitude > 1e-6f) dir = d.normalized;
            }
            var shot = new ProjectileShot(tuning.Attack, row.multiplier, 0f, AttackMode.Combo, attackIndex + 1, 1);
            projectiles.Fire(row.ToProjectileRules(), shot, p + Vector3.up * tuning.EyeHeight, dir, p);
        }

        void OnProjectileHit(PlayerDefense player, ProjectileShot shot, Vector3 from)
        {
            var row = tuning.GetAttack(shot.Step - 1);
            JudgedCount++;
            LastJudgement = player.ReceiveEnemyAttack(from, shot.Attack, shot.Multiplier, row != null ? row.knockdownTime : 0f);
        }

        /// <summary>이번 스텝에 본 것: 플레이어 발 · 높이 차 · 눈에서 플레이어 가슴까지 시야선.</summary>
        EnemySense Sense()
        {
            Vector3 p = transform.position;
            var self = new Planar(p.x, p.z);
            if (_target == null || !_target.isActiveAndEnabled) return EnemySense.NoTarget(self, _yaw);
            Vector3 tp = _target.transform.position;
            return new EnemySense(self, _yaw, new Planar(tp.x, tp.z), tp.y - p.y, LineOfSight(p, tp));
        }

        bool LineOfSight(Vector3 feet, Vector3 targetFeet)
        {
            Vector3 eye = feet + Vector3.up * tuning.EyeHeight;
            Vector3 aim = targetFeet + Vector3.up * tuning.EyeHeight;
            Vector3 d = aim - eye;
            float len = d.magnitude;
            if (len < 1e-4f) return true;
            int n = Physics.RaycastNonAlloc(eye, d / len, _rayHits, len, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                Collider c = _rayHits[i].collider;
                if (c == null || c == _cc || c.transform.IsChildOf(transform)) continue;
                if (c.transform.IsChildOf(_target.transform)) continue;
                if (c.GetComponentInParent<IHitTarget>() != null) continue; // 다른 적 · 허수아비는 가리지 않는다
                return false;
            }
            return true;
        }

        public int ReceiveHit(int damage, Vector3 from)
        {
            if (!Alive) return 0;
            HitCount++;
            Vector3 d = transform.position - from;
            d.y = 0f;
            _knockDir = d.sqrMagnitude > 1e-6f ? d.normalized : -transform.forward;
            _knockAge = 0f;
            _flashLeft = tuning.FlashTime;
            int applied = _health.TakeDamage(damage);
            if (Alive) _brain.NotifyHit();
            return applied;
        }

        public bool ReceivePoise(float amount)
        {
            if (!Alive || _poise == null) return false;
            return _poise.Apply(amount);
        }

        void OnDied()
        {
            _brain.Die();
            _poise?.Reset();
            SetShown(false);
        }

        /// <summary>집에서 다시 난다 (체력 · 경직 가득 · 집 방향 · 날아가는 투사체 지움).</summary>
        void PlaceAtHome()
        {
            _cc.enabled = false;
            transform.SetPositionAndRotation(_home, Quaternion.Euler(0f, _homeYaw, 0f));
            if (OnNavMesh) _nav.Warp(_home);
            _yaw = _homeYaw;
            _vertical = 0f;
            _health.Restore();
            _poise?.Reset();
            _knockAge = -1f;
            _flashLeft = 0f;
            _hitFlashLeft = 0f;
            _prevPos = _currPos = _home;
            _prevRot = _currRot = transform.rotation;
            if (projectiles != null) projectiles.Clear();
            SetShown(true);
        }

        void SetShown(bool shown)
        {
            if (visual != null) visual.gameObject.SetActive(shown);
            if (_cc != null) _cc.enabled = shown;
            if (!shown && warningFan != null) warningFan.enabled = false;
        }

        // ---- 디버그 진입점 (GameRoot.DebugXxx가 부른다) ----

        /// <summary>[상태 변경] 바로 집에서 다시 난다.</summary>
        public void Respawn()
        {
            if (_brain == null) return;
            _brain.Respawn();
            PlaceAtHome();
        }

        /// <summary>[상태 변경] 쓰러뜨린다 (체력 0 — 처치 에너지는 안 줌).</summary>
        public void Kill()
        {
            if (!Alive) return;
            bool inv = _health.Invulnerable;
            _health.Invulnerable = false;
            _health.TakeDamage(_health.Current);
            _health.Invulnerable = inv;
        }

        /// <summary>[상태 변경] 상태를 바꾼다 (Idle · Patrol · Chase · Return · Stagger).</summary>
        public bool ForceState(EnemyState state) => _brain != null && Alive && _brain.ForceState(state);

        public void SetInvulnerable(bool on) { if (_health != null) _health.Invulnerable = on; }

        /// <summary>[상태 변경] 경직 게이지를 비운다 (그로기도 풀림).</summary>
        public void ResetPoise() => _poise?.Reset();

        // ---- 그리기 ----

        void Update()
        {
            if (_brain == null || visual == null) return;
            float dt = Time.deltaTime;
            bool alive = Alive;

            // 스텝 사이 보간 + 넉백(비주얼만) + 그로기 기울기
            float alpha = Time.fixedDeltaTime > 0f ? Mathf.Clamp01((Time.time - Time.fixedTime) / Time.fixedDeltaTime) : 1f;
            Vector3 pos = Vector3.Lerp(_prevPos, _currPos, alpha);
            Quaternion rot = Quaternion.Slerp(_prevRot, _currRot, alpha);
            float k = 0f;
            if (_knockAge >= 0f && tuning.KnockbackTime > 0f)
            {
                _knockAge += dt;
                float kt = tuning.KnockbackTime;
                if (_knockAge < kt) k = _knockAge / kt;
                else if (_knockAge < 3f * kt) k = 1f - (_knockAge - kt) / (2f * kt);
                else _knockAge = -1f;
            }
            visual.SetPositionAndRotation(pos + rot * _visualHome + _knockDir * (tuning.KnockbackDistance * k),
                rot * _visualHomeRot * (Groggy ? Quaternion.Euler(-groggyTilt, 0f, 0f) : Quaternion.identity));

            // 몸 색: 번쩍임 > 그로기 > 예고 (돌진 중에도 예고 색)
            var phase = _brain.Cycle.Phase;
            bool warning = alive && _brain.State == EnemyState.Attack && phase == EnemyAttackPhase.Telegraph;
            bool charging = alive && _brain.State == EnemyState.Attack && phase == EnemyAttackPhase.Active;
            bool flash = _flashLeft > 0f;
            if (flash) _flashLeft -= dt;
            int want = flash ? 1 : Groggy ? 2 : warning || charging ? 3 : 0;
            if (want != _shownColor && flashRenderers != null)
            {
                _shownColor = want;
                foreach (var r in flashRenderers)
                {
                    if (r == null) continue;
                    if (want == 0) { r.SetPropertyBlock(null); continue; }
                    r.GetPropertyBlock(_block);
                    _block.SetColor(BaseColorId, want == 1 ? flashColor : want == 2 ? groggyColor : telegraphColor);
                    r.SetPropertyBlock(_block);
                }
            }

            // 예고 부채꼴 (예고 · 돌진 중 · 닿은 뒤 잠깐)
            if (warningFan == null) return;
            bool hitFlash = _hitFlashLeft > 0f;
            bool show = alive && (warning || charging || hitFlash);
            warningFan.enabled = show;
            if (!show) return;
            int attackIndex = warning || charging ? _brain.CurrentAttackIndex : _fanAttack;
            DrawFan(warning ? pos : charging ? pos : pos, _yaw, tuning.GetAttack(attackIndex));
            Color c = hitFlash && !warning ? hitColor : Color.Lerp(warnStartColor, warnEndColor, _brain.Cycle.TelegraphRatio);
            warningFan.startColor = c;
            warningFan.endColor = c;
        }

        /// <summary>앞 부채꼴 (근접), 돌진 길이만큼 좁은 길 (돌진), 조준선 (투사체 — 각도 0).</summary>
        void DrawFan(Vector3 feet, float yaw, EnemyTuning.AttackRow row)
        {
            if (row == null) { warningFan.positionCount = 0; return; }
            float range = row.hitRange;
            float half = Mathf.Min(row.hitAngle, 360f) * 0.5f;
            if (row.delivery == HitDelivery.Melee && row.activeTime > 0f && row.lungeDistance > 0f)
            {
                range = row.lungeDistance + row.hitRange;
                half = Mathf.Atan2(row.hitRange * 0.5f, range) * Mathf.Rad2Deg;
            }
            Vector3 center = feet + Vector3.up * fanHeight;
            warningFan.positionCount = FanSegments + 2;
            warningFan.SetPosition(0, center);
            for (int i = 0; i <= FanSegments; i++)
            {
                float a = (yaw - half + 2f * half * i / FanSegments) * Mathf.Deg2Rad;
                warningFan.SetPosition(i + 1, center + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * range);
            }
        }
    }
}
