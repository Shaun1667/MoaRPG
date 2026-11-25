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
    /// 플레이어 공격 (2-1 일반 공격 · 2-2 강공격 · 낙하 공격). 규칙은 Core PlayerAttack(ComboAttack · ChargedAttack · PlungeAttack) · DamageMath,
    /// 수치는 WeaponTuning(무기 하나 = 에셋 하나) · CombatTuning.
    /// PlayerMotor가 고정 스텝마다 <see cref="Step"/>(입력 → 공격 고르기 → 진행)을 부르고, 이동이 끝난 뒤 판정 구간이면 <see cref="CheckHits"/>,
    /// 낙하 중 땅에 닿으면 <see cref="Land"/>(범위 판정 한 번).
    /// 휘두르기 판정 = 몸 앞 부채꼴 겹침 검사(OverlapSphere + 각도, 강공격은 그 범위 · 각도), 낙하 착지 = 발 + hitHeight에서 반지름 plungeRange 구.
    /// 레이어 추가 없이 자기 콜라이더는 뺀다. 같은 휘두르기 · 같은 낙하에 같은 대상은 한 번만(ID 기준).
    /// 몸 방향: 단계(베기)가 시작될 때 이동 입력이 있으면 그쪽, 없으면 autoAimRange 안의 가장 가까운 적, 그것도 없으면 지금 방향.
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
        PlayerAttack _attack;
        PlayerDefense _defense; // 2-3: 저스트 회피 반격 보너스 (없으면 ×1)
        EntityId _id;
        float _attackScale = 1f; // 디버그 공격력 배수 (1 = 그대로)

        public WeaponTuning Weapon => weapon;
        public CombatTuning Tuning => tuning;
        /// <summary>공격 조정 (Core, Bind 전에는 null).</summary>
        public PlayerAttack Attacks => _attack;
        public ComboAttack Combo => _attack?.Combo;
        public ChargedAttack Charged => _attack?.Charged;
        public PlungeAttack Plunge => _attack?.Plunge;
        public bool Bound => _attack != null && _input != null;
        /// <summary>땅 위에서 휘두르는 중 (콤보 · 강공격).</summary>
        public bool Attacking => _attack != null && _attack.Swinging;
        public bool ChargedActive => _attack != null && _attack.Charged.Active;
        /// <summary>낙하 공격 중 (떨어지는 중 + 착지 경직).</summary>
        public bool Plunging => _attack != null && _attack.Plunge.Active;
        public bool PlungeFalling => _attack != null && _attack.Plunge.Falling;
        public AttackMode Mode => _attack != null ? _attack.Mode : AttackMode.None;
        public EntityId Id => _id;
        /// <summary>지금 공격력 = 무기 공격력 × 디버그 배수.</summary>
        public float Attack => weapon != null ? weapon.BaseAttack * _attackScale : 0f;
        public float AttackScale { get => _attackScale; set => _attackScale = value > 0f ? value : 0f; }
        public HitReport LastHit { get; private set; }
        public int HitCount { get; private set; }
        /// <summary>마지막 낙하 착지 자리 (발) · 그때 맞힌 수.</summary>
        public Vector3 LastLandPosition { get; private set; }
        public int LastLandHits { get; private set; }

        /// <summary>한 대상을 맞힐 때마다 (피해 숫자 · 디버그 기록).</summary>
        public event Action<HitReport> HitLanded;
        /// <summary>낙하 공격이 착지했을 때 (발 위치, 판정 반지름) — 바닥 원 표현용.</summary>
        public event Action<Vector3, float> PlungeLanded;

        public void Bind(IPlayerInput input)
        {
            _input = input;
            if (weapon == null) { Debug.LogError("PlayerCombat: WeaponTuning is not assigned"); enabled = false; return; }
            if (tuning == null) { Debug.LogError("PlayerCombat: CombatTuning is not assigned"); enabled = false; return; }
            if (weapon.StepCount == 0) Debug.LogError("PlayerCombat: weapon " + weapon.name + " has no combo steps");
            if (weapon.ChargedSlashCount == 0) Debug.LogWarning("PlayerCombat: weapon " + weapon.name + " has no charged attack slashes");
            var plunge = new PlungeRules(tuning.PlungeMinHeight, tuning.PlungeHighHeight, weapon.PlungeLowMultiplier, weapon.PlungeHighMultiplier,
                tuning.PlungeFallSpeed, tuning.PlungeLandRecovery);
            _attack = new PlayerAttack(weapon.ToRules(), weapon.ToChargedRules(), plunge, tuning.ChargeHoldTime);
            _defense = GetComponent<PlayerDefense>();
        }

        public void SetId(EntityId id) => _id = id;

        /// <summary>입력을 꺼내 공격을 고르고 한 스텝 진행 (PlayerMotor가 부른다). 막힌 입력은 버린다. stamina = 강공격 비용.</summary>
        public AttackTick Step(AttackContext ctx, Stamina stamina, float dt)
        {
            if (!Bound) return default;
            bool pressed = _input.ConsumeAttack();
            return _attack.Step(pressed, _input.AttackHeld, ctx, stamina, dt);
        }

        /// <summary>공격을 모두 끊는다 (회피 · 점프).</summary>
        public void Interrupt(AttackEnd reason) => _attack?.Interrupt(reason);

        /// <summary>땅 위 휘두르기만 끊는다 (발밑이 사라짐) — 낙하는 그대로.</summary>
        public void InterruptSwing(AttackEnd reason) => _attack?.InterruptSwing(reason);

        /// <summary>[상태 변경] 공격을 처음으로 (순간이동 등).</summary>
        public void Cancel() => _attack?.Cancel();

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

        /// <summary>판정 구간인 스텝에 부른다: 몸 앞 부채꼴 안의 대상을 이번 휘두르기(콤보 단계 또는 강공격 베기)에 한 번씩 맞힌다. 이번에 맞힌 수.</summary>
        public int CheckHits(Vector3 feet, float yawDegrees)
        {
            if (!Bound || !_attack.Swinging) return 0;
            bool charged = _attack.Charged.Active;
            Vector3 center = feet + Vector3.up * weapon.HitHeight;
            float r = yawDegrees * Mathf.Deg2Rad;
            Vector3 forward = new Vector3(Mathf.Sin(r), 0f, Mathf.Cos(r));
            float halfAngle = (charged ? weapon.ChargedHitAngle : weapon.HitAngle) * 0.5f;
            float range = charged ? weapon.ChargedHitRange : weapon.HitRange;
            int n = Physics.OverlapSphereNonAlloc(center, range, _overlap, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            int hits = 0;
            for (int i = 0; i < n; i++)
            {
                Collider c = _overlap[i];
                IHitTarget t = TargetOf(c);
                if (t == null || !t.Alive) continue;
                Vector3 to = c.bounds.center - center;
                to.y = 0f;
                if (to.sqrMagnitude > 1e-6f && Vector3.Angle(forward, to) > halfAngle) continue;
                if (!_attack.TryHitSwing(t.Id)) continue; // 이번 휘두르기에 이미 맞음
                float multiplier = _attack.CurrentSwing.Multiplier;
                if (charged) Report(t, multiplier, weapon.ChargedPoise(_attack.Charged.SlashIndex), feet, AttackMode.Charged, _attack.Charged.SlashIndex + 1, _attack.Charged.Count);
                else Report(t, multiplier, weapon.ComboPoise(_attack.Combo.StepIndex), feet, AttackMode.Combo, _attack.Combo.StepIndex + 1, _attack.Combo.SwingCount);
                hits++;
            }
            return hits;
        }

        /// <summary>
        /// 떨어지던 낙하 공격이 땅에 닿았다 (PlayerMotor가 이동 뒤에 부른다): 떨어진 높이로 배율을 정하고 원형 범위 판정을 한 번.
        /// 낙하 중이 아니면 아무것도 하지 않고 −1, 착지했으면 맞힌 수.
        /// </summary>
        public int Land(Vector3 feet)
        {
            if (!Bound || !_attack.Land(feet.y)) return -1;
            Vector3 center = feet + Vector3.up * weapon.HitHeight;
            int n = Physics.OverlapSphereNonAlloc(center, weapon.PlungeRange, _overlap, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            int hits = 0;
            PlungeAttack p = _attack.Plunge;
            for (int i = 0; i < n; i++)
            {
                IHitTarget t = TargetOf(_overlap[i]);
                if (t == null || !t.Alive) continue;
                if (!p.TryHit(t.Id)) continue; // 낙하 한 번에 대상마다 한 번 (콜라이더가 여럿이어도)
                Report(t, p.Multiplier, weapon.PlungePoise(p.High), feet, AttackMode.Plunge, p.High ? 2 : 1, p.Count);
                hits++;
            }
            LastLandPosition = feet;
            LastLandHits = hits;
            PlungeLanded?.Invoke(feet, weapon.PlungeRange);
            return hits;
        }

        /// <summary>
        /// 한 대상에 피해 · 경직 (2-3): 피해 = 공격력 × 배율 × 대상의 받는 피해 배율(그로기면 큼) → 방어.
        /// 그 뒤 경직 = 표 값 × 반격 보너스(저스트 회피 뒤) — 이 한 방으로 그로기가 되어도 이 피해에는 배율이 안 든다.
        /// </summary>
        void Report(IHitTarget t, float multiplier, float poise, Vector3 feet, AttackMode kind, int step, int swing)
        {
            var pt = t as IPoiseTarget;
            bool onGroggy = pt != null && pt.Groggy;
            float taken = pt != null ? pt.DamageTakenMultiplier : 1f;
            int damage = DamageMath.Compute(Attack, multiplier * taken, t.Defense, tuning.DefenseConstant);
            int applied = t.ReceiveHit(damage, feet);
            float poiseApplied = 0f;
            bool broke = false;
            if (pt != null && pt.Poise != null && !pt.Groggy && t.Alive && poise > 0f)
            {
                poiseApplied = poise * (_defense != null ? _defense.PoiseMultiplier : 1f);
                broke = pt.ReceivePoise(poiseApplied);
            }
            var report = new HitReport(t.Id, (t as Component) != null ? ((Component)t).name : "-", damage, applied, step, swing, t.NumberPosition, kind,
                poiseApplied, broke, onGroggy);
            LastHit = report;
            HitCount++;
            HitLanded?.Invoke(report);
        }

        IHitTarget TargetOf(Collider c)
        {
            if (c == null || c.transform.IsChildOf(transform)) return null; // 자기 콜라이더 제외
            return c.GetComponentInParent<IHitTarget>();
        }
    }
}
