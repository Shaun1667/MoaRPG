using Moa.Core.Combat;
using Moa.Core.Motion;
using Moa.Unity.Data;
using UnityEngine;
using EntityId = Moa.Core.Ids.EntityId; // UnityEngine.EntityId와 같은 이름

namespace Moa.Unity.Combat
{
    /// <summary>
    /// 근접 적 (2-5). 상태 기계는 Core EnemyBrain(대기 · 순찰 · 추적 · 공격 · 복귀 · 경직 · 그로기 · 쓰러짐), 공격 주기는 그 안의 EnemyAttackCycle,
    /// 체력 · 경직은 Core Health · Poise, 수치는 EnemyTuning(적 한 종류 = 에셋 하나, BALANCE 11번).
    /// 여기는 Unity 쪽만: 고정 스텝마다 플레이어까지 거리 · 시야선(레이)을 재서 Brain에 넘기고, 나온 속도로 CharacterController를 움직인다(NavMesh 없음 — 곧장 쫓고 벽은 미끄러진다).
    /// 공격이 닿는 스텝에 플레이어가 앞 부채꼴 안이면 PlayerDefense.ReceiveEnemyAttack → 저스트 · 피함 · 맞음 (2-3과 같은 창구).
    /// 맞는 쪽은 IHitTarget · IPoiseTarget · IEnergyDrop (PlayerCombat · 피해 숫자 · 경직 막대 · 처치 에너지가 그대로 붙는다).
    /// 실행 순서를 플레이어(PlayerMotor) 뒤로 둔다 — 같은 스텝의 회피 상태를 본다. GameRoot가 Bind · SetId로 연결한다.
    /// 예고 = 앞 부채꼴 선(LineRenderer, 반지름 hitRange)이 warnStart → warnEnd 색, 닿는 순간 hitColor. 색 · 두께 · 기울기는 Setup 표.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(CharacterController))]
    public sealed class EnemyAgent : MonoBehaviour, IHitTarget, IPoiseTarget, IEnergyDrop
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        const int MaxRayHits = 16;
        const int FanSegments = 24;

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
        CharacterController _cc;
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
        public EnemyState State => _brain != null ? _brain.State : EnemyState.Idle;
        public Vector3 Home => _home;
        public bool Bound => _target != null;
        /// <summary>[상태 변경] 멈춤 (디버그) — 서 있고 공격 주기도 처음으로.</summary>
        public bool Paused { get => _brain != null && _brain.Paused; set { if (_brain != null) _brain.Paused = value; } }
        /// <summary>마지막으로 닿은 공격의 판정 · 맞힌 수 · 빗나간 수 (디버그 · 확인용).</summary>
        public DodgeJudgement LastJudgement { get; private set; }
        public int JudgedCount { get; private set; }
        public int HitCount { get; private set; }
        public bool Flashing => _flashLeft > 0f;

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
            _home = transform.position;
            _homeYaw = transform.eulerAngles.y;
            _yaw = _homeYaw;
            _brain = new EnemyBrain(tuning.ToAiRules(), new Planar(_home.x, _home.z), new Planar(patrolOffset.x, patrolOffset.z));
            if (visual != null) { _visualHome = visual.localPosition; _visualHomeRot = visual.localRotation; }
            if (warningFan != null) { warningFan.positionCount = 0; warningFan.enabled = false; }
            _prevPos = _currPos = transform.position;
            _prevRot = _currRot = transform.rotation;
        }

        /// <summary>쫓고 칠 대상 (플레이어의 적 공격 창구).</summary>
        public void Bind(PlayerDefense target) => _target = target;

        public void SetId(EntityId id) => _id = id;

        void FixedUpdate()
        {
            if (_brain == null) return;
            float dt = Time.fixedDeltaTime;
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
            _currPos = transform.position;
            _currRot = transform.rotation;

            if (!t.AttackLanded) return;
            _hitFlashLeft = hitFlashTime;
            if (_target == null) return;
            // 맞는 자리면 판정, 예고 중 그 자리에 있다 회피로 빠져나갔으면 무적일 때만 판정(저스트 · 피함)
            if (!t.AttackHits && !(t.AttackGrazed && _target.Invulnerable)) return;
            JudgedCount++;
            LastJudgement = _target.ReceiveEnemyAttack(transform.position, tuning.Attack, tuning.AttackMultiplier); // 3-1: Hit이면 필드 캐릭터 피해
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

        /// <summary>집에서 다시 난다 (체력 · 경직 가득 · 집 방향).</summary>
        void PlaceAtHome()
        {
            _cc.enabled = false;
            transform.SetPositionAndRotation(_home, Quaternion.Euler(0f, _homeYaw, 0f));
            _yaw = _homeYaw;
            _vertical = 0f;
            _health.Restore();
            _poise?.Reset();
            _knockAge = -1f;
            _flashLeft = 0f;
            _hitFlashLeft = 0f;
            _prevPos = _currPos = _home;
            _prevRot = _currRot = transform.rotation;
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

            // 몸 색: 번쩍임 > 그로기 > 예고
            bool telegraph = alive && _brain.State == EnemyState.Attack && _brain.Cycle.Phase == EnemyAttackPhase.Telegraph;
            bool flash = _flashLeft > 0f;
            if (flash) _flashLeft -= dt;
            int want = flash ? 1 : Groggy ? 2 : telegraph ? 3 : 0;
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

            // 예고 부채꼴
            if (warningFan == null) return;
            bool hitFlash = _hitFlashLeft > 0f;
            bool show = alive && (telegraph || hitFlash);
            warningFan.enabled = show;
            if (!show) return;
            DrawFan(pos, _yaw);
            Color c = hitFlash && !telegraph ? hitColor : Color.Lerp(warnStartColor, warnEndColor, _brain.Cycle.TelegraphRatio);
            warningFan.startColor = c;
            warningFan.endColor = c;
        }

        void DrawFan(Vector3 feet, float yaw)
        {
            float range = tuning.HitRange;
            float half = Mathf.Min(tuning.HitAngle, 360f) * 0.5f;
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
