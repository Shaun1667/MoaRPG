using Moa.Core.Combat;
using Moa.Core.Ids;
using Moa.Unity.Data;
using UnityEngine;
using EntityId = Moa.Core.Ids.EntityId; // UnityEngine.EntityId와 같은 이름

namespace Moa.Unity.Combat
{
    /// <summary>
    /// 허수아비 (2-1): 체력(Core Health) · 맞으면 번쩍임 · 넉백 조금(비주얼만 — 몸은 제자리) · 죽으면 사라졌다가 respawnDelay 뒤 가득 찬 채 다시 선다.
    /// 수치는 DummyTuning(BALANCE 8번), 번쩍임 색은 Setup 표. ID는 GameRoot가 판을 시작할 때 준다(SetId).
    /// 되살아나기 시간은 고정 스텝(FixedUpdate), 번쩍임 · 넉백 그리기는 Update.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class TrainingDummy : MonoBehaviour, IHitTarget, IPoiseTarget, IEnergyDrop
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] DummyTuning tuning;
        [Tooltip("Drawn body (hidden while dead, pushed on a hit)")]
        [SerializeField] Transform visual;
        [SerializeField] Renderer[] flashRenderers;
        [SerializeField] Color flashColor;
        [Tooltip("Body color while groggy (2-3)")]
        [SerializeField] Color groggyColor;
        [Tooltip("Degrees the body leans back while groggy (visual only)")]
        [SerializeField] float groggyTilt;

        Health _health;
        Poise _poise;
        Quaternion _visualHomeRot = Quaternion.identity;
        int _shownColor; // 0 = 그대로, 1 = 번쩍임, 2 = 그로기
        EntityId _id;
        Collider _collider;
        MaterialPropertyBlock _block;
        Vector3 _visualHome;
        Vector3 _knockDir;
        float _knockAge = -1f;
        float _flashLeft;
        bool _flashing;
        float _respawnLeft;

        public DummyTuning Tuning => tuning;
        public Health Health => _health;
        public EntityId Id => _id;
        public bool Alive => _health != null && !_health.IsDead;
        public float Defense => tuning != null ? tuning.Defense : 0f;
        public Vector3 Position => transform.position;
        public Vector3 NumberPosition => transform.position + Vector3.up * (tuning != null ? tuning.NumberHeight : 0f);
        /// <summary>맞은 수 · 죽은 수 · 되살아난 수 (디버그 · 확인용).</summary>
        public int HitCount { get; private set; }
        public int DeathCount { get; private set; }
        public int RespawnCount { get; private set; }
        /// <summary>되살아나기까지 남은 초 (죽어 있을 때).</summary>
        public float RespawnLeft => Alive ? 0f : _respawnLeft;
        public bool Flashing => _flashing;
        /// <summary>경직 게이지 (2-3, DummyTuning maxPoise가 0이면 null).</summary>
        public Poise Poise => _poise;
        public bool Groggy => _poise != null && _poise.Groggy;
        public float DamageTakenMultiplier => _poise != null ? _poise.DamageTakenMultiplier : 1f;
        /// <summary>행동할 수 있는지 (살아 있고 그로기가 아님 — 공격 허수아비 DummyAttacker가 본다).</summary>
        public bool CanAct => Alive && !Groggy;
        /// <summary>처치하면 플레이어가 얻는 궁극기 에너지 (2-4).</summary>
        public float EnergyOnDefeat => tuning != null ? tuning.EnergyOnDefeat : 0f;


        public bool ReceivePoise(float amount)
        {
            if (!Alive || _poise == null) return false;
            return _poise.Apply(amount);
        }

        /// <summary>[상태 변경] 경직 게이지를 비운다 (그로기도 풀림).</summary>
        public void ResetPoise() => _poise?.Reset();

        void Awake()
        {
            _collider = GetComponent<Collider>();
            _block = new MaterialPropertyBlock();
            if (tuning == null) { Debug.LogError("TrainingDummy: DummyTuning is not assigned"); enabled = false; return; }
            _health = new Health(tuning.MaxHealth);
            _health.Died += OnDied;
            if (tuning.MaxPoise > 0f) _poise = new Poise(tuning.ToPoiseRules());
            else Debug.LogWarning("TrainingDummy: DummyTuning maxPoise is 0 - no poise gauge");
            if (visual != null) _visualHomeRot = visual.localRotation;
            if (visual != null) _visualHome = visual.localPosition;
        }

        public void SetId(EntityId id) => _id = id;

        public int ReceiveHit(int damage, Vector3 from)
        {
            if (!Alive) return 0;
            HitCount++;
            Vector3 d = transform.position - from;
            d.y = 0f;
            _knockDir = d.sqrMagnitude > 1e-6f ? d.normalized : -transform.forward;
            _knockAge = 0f;
            _flashLeft = tuning.FlashTime;
            return _health.TakeDamage(damage);
        }

        /// <summary>[상태 변경] 바로 다시 세운다 (체력 가득).</summary>
        public void Respawn()
        {
            if (_health == null) return;
            bool wasDead = _health.IsDead;
            _health.Restore();
            _poise?.Reset();
            _respawnLeft = 0f;
            _knockAge = -1f;
            _flashLeft = 0f;
            SetShown(true);
            if (wasDead) RespawnCount++;
        }

        public void SetInvulnerable(bool on) { if (_health != null) _health.Invulnerable = on; }

        void OnDied()
        {
            DeathCount++;
            _poise?.Reset();
            _respawnLeft = tuning.RespawnDelay;
            SetShown(false);
        }

        void SetShown(bool shown)
        {
            if (visual != null) visual.gameObject.SetActive(shown);
            if (_collider != null) _collider.enabled = shown;
        }

        void FixedUpdate()
        {
            if (_health == null) return;
            if (!_health.IsDead) { _poise?.Advance(Time.fixedDeltaTime); return; }
            _respawnLeft -= Time.fixedDeltaTime;
            if (_respawnLeft <= 1e-5f) Respawn();
        }

        void Update()
        {
            if (_health == null) return;
            float dt = Time.deltaTime;

            // 넉백: knockbackTime에 밀려 나가고 그 두 배 시간에 돌아온다
            if (visual != null)
            {
                float k = 0f;
                if (_knockAge >= 0f && tuning.KnockbackTime > 0f)
                {
                    _knockAge += dt;
                    float t = tuning.KnockbackTime;
                    if (_knockAge < t) k = _knockAge / t;
                    else if (_knockAge < 3f * t) k = 1f - (_knockAge - t) / (2f * t);
                    else _knockAge = -1f;
                }
                Vector3 local = transform.InverseTransformDirection(_knockDir) * (tuning.KnockbackDistance * k);
                visual.localPosition = _visualHome + local;
                visual.localRotation = Groggy ? _visualHomeRot * Quaternion.Euler(-groggyTilt, 0f, 0f) : _visualHomeRot;
            }

            // 번쩍임
            // 2-3: 번쩍임이 그로기 색보다 앞선다
            bool flash = _flashLeft > 0f;
            if (flash) _flashLeft -= dt;
            _flashing = flash;
            int want = flash ? 1 : Groggy ? 2 : 0;
            if (want != _shownColor && flashRenderers != null)
            {
                _shownColor = want;
                foreach (var r in flashRenderers)
                {
                    if (r == null) continue;
                    if (want == 0) { r.SetPropertyBlock(null); continue; }
                    r.GetPropertyBlock(_block);
                    _block.SetColor(BaseColorId, want == 1 ? flashColor : groggyColor);
                    r.SetPropertyBlock(_block);
                }
            }
        }
    }
}
