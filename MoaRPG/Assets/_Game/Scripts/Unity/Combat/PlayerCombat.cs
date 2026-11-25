using System;
using Moa.Core.Combat;
using Moa.Core.Ids;
using Moa.Core.Motion;
using Moa.Unity.Data;
using Moa.Unity.Player;
using UnityEngine;
using EntityId = Moa.Core.Ids.EntityId; // UnityEngine.EntityId와 같은 이름

namespace Moa.Unity.Combat
{
    /// <summary>
    /// 플레이어 일반 공격 (2-1). 규칙은 Core ComboAttack · DamageMath, 수치는 WeaponTuning(무기 하나 = 에셋 하나) · CombatTuning.
    /// PlayerMotor가 고정 스텝마다 <see cref="Step"/>(입력 → 콤보 진행)을 부르고, 이동이 끝난 뒤 판정 구간이면 <see cref="CheckHits"/>.
    /// 판정 = 몸 앞 부채꼴 겹침 검사(OverlapSphere + 각도), 레이어 추가 없이 자기 콜라이더는 뺀다. 같은 휘두르기에 같은 대상은 한 번만(ID 기준).
    /// 몸 방향: 단계가 시작될 때 이동 입력이 있으면 그쪽, 없으면 autoAimRange 안의 가장 가까운 적, 그것도 없으면 지금 방향.
    /// GameRoot가 Bind · SetId로 연결한다.
    /// </summary>
    [RequireComponent(typeof(PlayerMotor))]
    public sealed class PlayerCombat : MonoBehaviour
    {
        const int MaxOverlap = 32;

        [SerializeField] WeaponTuning weapon;
        [SerializeField] CombatTuning tuning;

        readonly Collider[] _overlap = new Collider[MaxOverlap];
        IPlayerInput _input;
        ComboAttack _combo;
        EntityId _id;
        float _attackScale = 1f; // 디버그 공격력 배수 (1 = 그대로)

        public WeaponTuning Weapon => weapon;
        public CombatTuning Tuning => tuning;
        public ComboAttack Combo => _combo;
        public bool Bound => _combo != null && _input != null;
        public bool Attacking => _combo != null && _combo.Attacking;
        public EntityId Id => _id;
        /// <summary>지금 공격력 = 무기 공격력 × 디버그 배수.</summary>
        public float Attack => weapon != null ? weapon.BaseAttack * _attackScale : 0f;
        public float AttackScale { get => _attackScale; set => _attackScale = value > 0f ? value : 0f; }
        public HitReport LastHit { get; private set; }
        public int HitCount { get; private set; }

        /// <summary>한 대상을 맞힐 때마다 (피해 숫자 · 디버그 기록).</summary>
        public event Action<HitReport> HitLanded;

        public void Bind(IPlayerInput input)
        {
            _input = input;
            if (weapon == null) { Debug.LogError("PlayerCombat: WeaponTuning is not assigned"); enabled = false; return; }
            if (tuning == null) { Debug.LogError("PlayerCombat: CombatTuning is not assigned"); enabled = false; return; }
            if (weapon.StepCount == 0) Debug.LogError("PlayerCombat: weapon " + weapon.name + " has no combo steps");
            _combo = new ComboAttack(weapon.ToRules());
        }

        public void SetId(EntityId id) => _id = id;

        /// <summary>입력을 꺼내 콤보에 넣고 한 스텝 진행 (PlayerMotor가 부른다). 막힌 입력은 버린다.</summary>
        public ComboTick Step(AttackContext ctx, float dt)
        {
            if (!Bound) return default;
            if (_input.ConsumeAttack()) _combo.Press(ctx);
            return _combo.Advance(dt);
        }

        public void Interrupt(AttackEnd reason) => _combo?.Interrupt(reason);

        /// <summary>[상태 변경] 콤보를 처음으로 (순간이동 등).</summary>
        public void Cancel() => _combo?.Cancel();

        /// <summary>새 단계의 몸 방향: 이동 입력 → 가장 가까운 적 → 그대로(false).</summary>
        public bool PickFacing(Vector3 feet, Planar wish, out float yaw)
        {
            yaw = 0f;
            if (wish.Length > 0f) { yaw = MoveMath.YawOf(wish); return true; }
            if (tuning == null || tuning.AutoAimRange <= 0f) return false;
            Vector3 center = feet + Vector3.up * weapon.HitHeight;
            int n = Physics.OverlapSphereNonAlloc(center, tuning.AutoAimRange, _overlap, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            bool found = false;
            for (int i = 0; i < n; i++)
            {
                IHitTarget t = TargetOf(_overlap[i]);
                if (t == null || !t.Alive) continue;
                Vector3 d = t.Position - feet;
                d.y = 0f;
                float sq = d.sqrMagnitude;
                if (sq < 1e-6f || sq >= best) continue;
                best = sq;
                yaw = MoveMath.YawOf(new Planar(d.x, d.z));
                found = true;
            }
            return found;
        }

        /// <summary>판정 구간인 스텝에 부른다: 몸 앞 부채꼴 안의 대상을 이번 휘두르기에 한 번씩 맞힌다. 이번에 맞힌 수.</summary>
        public int CheckHits(Vector3 feet, float yawDegrees)
        {
            if (!Bound || !_combo.Attacking) return 0;
            Vector3 center = feet + Vector3.up * weapon.HitHeight;
            float r = yawDegrees * Mathf.Deg2Rad;
            Vector3 forward = new Vector3(Mathf.Sin(r), 0f, Mathf.Cos(r));
            float halfAngle = weapon.HitAngle * 0.5f;
            int n = Physics.OverlapSphereNonAlloc(center, weapon.HitRange, _overlap, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            int hits = 0;
            for (int i = 0; i < n; i++)
            {
                Collider c = _overlap[i];
                IHitTarget t = TargetOf(c);
                if (t == null || !t.Alive) continue;
                Vector3 to = c.bounds.center - center;
                to.y = 0f;
                if (to.sqrMagnitude > 1e-6f && Vector3.Angle(forward, to) > halfAngle) continue;
                if (!_combo.TryHit(t.Id)) continue; // 이번 휘두르기에 이미 맞음
                ComboStepRules step = _combo.CurrentStep;
                int damage = DamageMath.Compute(Attack, step.Multiplier, t.Defense, tuning.DefenseConstant);
                int applied = t.ReceiveHit(damage, feet);
                var report = new HitReport(t.Id, (t as Component) != null ? ((Component)t).name : "-", damage, applied,
                    _combo.StepIndex + 1, _combo.SwingCount, t.NumberPosition);
                LastHit = report;
                HitCount++;
                hits++;
                HitLanded?.Invoke(report);
            }
            return hits;
        }

        IHitTarget TargetOf(Collider c)
        {
            if (c == null || c.transform.IsChildOf(transform)) return null; // 자기 콜라이더 제외
            return c.GetComponentInParent<IHitTarget>();
        }
    }
}
