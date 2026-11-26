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
    /// 2-6 파티: party(PartyTuning)가 있으면 캐릭터마다 PlayerAttack을 따로 두고(Core Party), 교체 입력(1 ~ 4)으로 현재 캐릭터를 바꾼다 —
    /// 무기 · 스킬 · 공격력은 현재 캐릭터 것(<see cref="Weapon"/> · <see cref="Skills"/> · <see cref="Attack"/>). 없으면 weapon · skills 칸 하나로(2-5까지와 같음) + character 칸의 캐릭터 기본 공격력.
    /// GameRoot가 Bind · SetId로 연결한다.
    /// </summary>
    [RequireComponent(typeof(PlayerMotor))]
    public sealed class PlayerCombat : MonoBehaviour
    {
        const int MaxOverlap = 32;

        [SerializeField] WeaponTuning weapon;
        [SerializeField] CombatTuning tuning;
        [Tooltip("Skill (E) / burst (Q) of this character (2-4). Empty = no skill or burst")]
        [SerializeField] SkillTuning skills;
        [Tooltip("Party of up to 4 characters (2-6). Empty = one character with the weapon / skills above")]
        [SerializeField] PartyTuning party;
        [Tooltip("Character whose base attack is added to the weapon above when there is no party (2-5 way). Only its base attack is used")]
        [SerializeField] CharacterData character;

        readonly Collider[] _overlap = new Collider[MaxOverlap];
        IPlayerInput _input;
        PlayerAttack _attack;
        PlayerDefense _defense; // 2-3: 저스트 회피 반격 보너스 (없으면 ×1)
        EntityId _id;
        float _attackScale = 1f; // 디버그 공격력 배수 (1 = 그대로)

        // 2-6: 현재 캐릭터 (파티가 없으면 weapon · skills 칸)
        Party _party;
        CharacterData[] _characters = new CharacterData[0];
        WeaponTuning _weapon;
        SkillTuning _skills;
        float _baseAttack;

        /// <summary>현재 캐릭터의 무기 (Bind 전에는 weapon 칸).</summary>
        public WeaponTuning Weapon => _weapon != null ? _weapon : weapon;
        public CombatTuning Tuning => tuning;
        /// <summary>현재 캐릭터의 스킬 · 궁극기 표 (Bind 전에는 skills 칸).</summary>
        public SkillTuning Skills => _attack != null ? _skills : skills;
        /// <summary>스킬(E) · 궁극기(Q) · 에너지 (2-4, 현재 캐릭터 것, Bind 전에는 null).</summary>
        public SkillAttack Skill => _attack?.Skill;
        public BurstAttack Burst => _attack?.Burst;
        public Energy Energy => _attack?.Energy;
        /// <summary>스킬 중 (누르는 중 포함).</summary>
        public bool SkillActive => _attack != null && _attack.Skill.Active;
        public bool BurstActive => _attack != null && _attack.Burst.Active;
        /// <summary>궁극기로 무적인지.</summary>
        public bool BurstInvulnerable => _attack != null && _attack.Invulnerable;
        /// <summary>마지막으로 찬 에너지 · 스킬로 찬 합 · 처치로 찬 합 (현재 캐릭터 기준, 디버그 · 확인용).</summary>
        public float LastEnergyGain { get; private set; }
        public float EnergyFromSkill { get; private set; }
        public float EnergyFromDefeat { get; private set; }
        /// <summary>처치 에너지로 대기 캐릭터들이 받은 합 · 마지막에 받은 합 (2-6).</summary>
        public float BenchEnergyFromDefeat { get; private set; }
        public float LastBenchEnergyGain { get; private set; }

        /// <summary>공격 조정 (Core, 현재 캐릭터 것, Bind 전에는 null).</summary>
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
        /// <summary>지금 공격력 = (캐릭터 기본 공격력 + 무기 공격력) × 디버그 배수.</summary>
        public float Attack => Weapon != null ? (_baseAttack + Weapon.BaseAttack) * _attackScale : 0f;
        public float AttackScale { get => _attackScale; set => _attackScale = value > 0f ? value : 0f; }
        /// <summary>현재 캐릭터 기본 공격력 (파티가 없으면 character 칸, Bind 전에는 0).</summary>
        public float CharacterAttack => _baseAttack;
        /// <summary>현재 무기 기본 공격력.</summary>
        public float WeaponAttack => Weapon != null ? Weapon.BaseAttack : 0f;
        public HitReport LastHit { get; private set; }
        public int HitCount { get; private set; }
        /// <summary>마지막 낙하 착지 자리 (발) · 그때 맞힌 수.</summary>
        public Vector3 LastLandPosition { get; private set; }
        public int LastLandHits { get; private set; }

        // ---- 2-6 파티 ----

        /// <summary>파티 규칙 (Core, 파티가 없거나 Bind 전이면 null).</summary>
        public Party Party => _party;
        public PartyTuning PartyTuning => party;
        /// <summary>캐릭터 수 (파티가 없으면 0).</summary>
        public int CharacterCount => _party != null ? _party.Count : 0;
        /// <summary>현재 칸 (0부터, 파티가 없으면 0).</summary>
        public int ActiveIndex => _party != null ? _party.ActiveIndex : 0;
        public CharacterData ActiveCharacter => GetCharacter(ActiveIndex);
        public CharacterData GetCharacter(int index) => index >= 0 && index < _characters.Length ? _characters[index] : null;
        /// <summary>칸의 공격 조정 (대기 캐릭터 포함, 없으면 null).</summary>
        public PlayerAttack MemberAttacks(int index) => _party != null && index >= 0 && index < _party.Count ? _party.Member(index) : null;

        /// <summary>한 대상을 맞힐 때마다 (피해 숫자 · 디버그 기록).</summary>
        public event Action<HitReport> HitLanded;
        /// <summary>낙하 공격이 착지했을 때 (발 위치, 판정 반지름) — 바닥 원 표현용.</summary>
        public event Action<Vector3, float> PlungeLanded;
        /// <summary>궁극기가 나갔을 때 (발 위치, 판정 반지름) — 바닥 원 표현용 (2-4).</summary>
        public event Action<Vector3, float> BurstCast;
        /// <summary>에너지가 찬 때 (찬 양, 스킬이면 true · 처치면 false) (2-4).</summary>
        public event Action<float, bool> EnergyGained;
        /// <summary>캐릭터를 바꿨을 때 (나간 칸, 들어온 칸) (2-6).</summary>
        public event Action<int, int> Switched;

        public void Bind(IPlayerInput input)
        {
            _input = input;
            if (tuning == null) { Debug.LogError("PlayerCombat: CombatTuning is not assigned"); enabled = false; return; }
            _defense = GetComponent<PlayerDefense>();

            if (party != null && party.MemberCount > 0)
            {
                int n = party.MemberCount;
                var characters = new CharacterData[n];
                var attacks = new PlayerAttack[n];
                for (int i = 0; i < n; i++)
                {
                    CharacterData c = party.GetMember(i);
                    if (c == null) { Debug.LogError("PlayerCombat: party " + party.name + " slot " + (i + 1) + " is empty"); enabled = false; return; }
                    WeaponTuning w = c.Weapon != null ? c.Weapon : weapon;
                    if (w == null) { Debug.LogError("PlayerCombat: character " + c.name + " has no weapon"); enabled = false; return; }
                    characters[i] = c;
                    attacks[i] = Build(w, c.Skills, c.name);
                }
                _characters = characters;
                _party = new Party(party.ToRules(), attacks);
                ApplyActive();
                return;
            }

            if (weapon == null) { Debug.LogError("PlayerCombat: WeaponTuning is not assigned"); enabled = false; return; }
            _party = null;
            _characters = new CharacterData[0];
            _weapon = weapon;
            _skills = skills;
            if (character == null) Debug.LogWarning("PlayerCombat: no party and no character - attack is the weapon attack only");
            _baseAttack = character != null ? character.BaseAttack : 0f;
            _attack = Build(weapon, skills, name);
        }

        PlayerAttack Build(WeaponTuning w, SkillTuning s, string who)
        {
            if (w.StepCount == 0) Debug.LogError("PlayerCombat: weapon " + w.name + " has no combo steps");
            if (w.ChargedSlashCount == 0) Debug.LogWarning("PlayerCombat: weapon " + w.name + " has no charged attack slashes");
            if (s == null) Debug.LogWarning("PlayerCombat: " + who + " has no SkillTuning - no skill (E) / burst (Q)");
            var plunge = new PlungeRules(tuning.PlungeMinHeight, tuning.PlungeHighHeight, w.PlungeLowMultiplier, w.PlungeHighMultiplier,
                tuning.PlungeFallSpeed, tuning.PlungeLandRecovery);
            return new PlayerAttack(w.ToRules(), w.ToChargedRules(), plunge, tuning.ChargeHoldTime,
                s != null ? s.ToSkillRules() : null, s != null ? s.ToBurstRules() : null);
        }

        /// <summary>현재 캐릭터의 공격 · 무기 · 스킬 · 공격력으로 바꾼다 (2-6).</summary>
        void ApplyActive()
        {
            CharacterData c = _characters[_party.ActiveIndex];
            _attack = _party.Active;
            _weapon = c.Weapon != null ? c.Weapon : weapon;
            _skills = c.Skills;
            _baseAttack = c.BaseAttack;
        }

        public void SetId(EntityId id) => _id = id;

        /// <summary>입력을 꺼내 공격을 고르고 한 스텝 진행 (PlayerMotor가 부른다). 막힌 입력은 버린다. stamina = 강공격 비용. 2-4: 스킬 · 궁극기 입력도.
        /// 2-6: 먼저 교체 입력(바꾸면 이 스텝부터 새 캐릭터), 끝에 교체 쿨 · 대기 캐릭터 쿨.</summary>
        public AttackTick Step(AttackContext ctx, Stamina stamina, float dt)
        {
            if (!Bound) return default;
            int slot = _input.ConsumeSwitch();
            if (slot > 0 && _party != null) TrySwitch(slot - 1, ctx.Gliding);
            bool pressed = _input.ConsumeAttack();
            bool skill = _input.ConsumeSkill();
            bool burst = _input.ConsumeBurst();
            AttackTick tick = _attack.Step(new AttackInput(pressed, _input.AttackHeld, skill, _input.SkillHeld, burst), ctx, stamina, dt);
            if (tick.BurstStarted && _skills != null) BurstCast?.Invoke(transform.position, _skills.BurstHitRange);
            _party?.Advance(dt);
            return tick;
        }

        /// <summary>index 칸(0부터)으로 바꾼다 (2-6). 받아지면 true, 안 되면 까닭은 Party.LastBlock. 보통은 고정 스텝 안(Step)에서 입력으로.</summary>
        public bool TrySwitch(int index, bool gliding)
        {
            if (_party == null) return false;
            int from = _party.ActiveIndex;
            if (!_party.TrySwitch(index, gliding)) return false;
            ApplyActive();
            Switched?.Invoke(from, index);
            return true;
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
            Vector3 center = feet + Vector3.up * Weapon.HitHeight;
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

        /// <summary>
        /// 판정 구간인 스텝에 부른다: 몸 앞 부채꼴 안의 대상을 이번 휘두르기(콤보 단계 · 강공격 · 스킬 · 궁극기 베기)에 한 번씩 맞힌다. 이번에 맞힌 수.
        /// 범위 · 각도: 콤보 = 무기 hitRange · hitAngle, 강공격 = charged*, 스킬 = 그 형태의 hitRange · hitAngle, 궁극기 = burstHit* (360도면 방향 없음).
        /// </summary>
        public int CheckHits(Vector3 feet, float yawDegrees)
        {
            if (!Bound || !_attack.Swinging) return 0;
            AttackMode kind = _attack.Burst.Active ? AttackMode.Burst
                : _attack.Skill.Casting ? AttackMode.Skill
                : _attack.Charged.Active ? AttackMode.Charged
                : _attack.Combo.Attacking ? AttackMode.Combo
                : AttackMode.None;
            if (kind == AttackMode.None) return 0; // 스킬을 누르는 중 (아직 베지 않음)
            WeaponTuning w = Weapon;
            float range, angle;
            switch (kind)
            {
                case AttackMode.Burst: range = _skills.BurstHitRange; angle = _skills.BurstHitAngle; break;
                case AttackMode.Skill: { var f = _skills.GetForm(_attack.Skill.Form); range = f.hitRange; angle = f.hitAngle; break; }
                case AttackMode.Charged: range = w.ChargedHitRange; angle = w.ChargedHitAngle; break;
                default: range = w.HitRange; angle = w.HitAngle; break;
            }
            Vector3 center = feet + Vector3.up * w.HitHeight;
            float r = yawDegrees * Mathf.Deg2Rad;
            Vector3 forward = new Vector3(Mathf.Sin(r), 0f, Mathf.Cos(r));
            float halfAngle = angle * 0.5f;
            int n = Physics.OverlapSphereNonAlloc(center, range, _overlap, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            int hits = 0;
            for (int i = 0; i < n; i++)
            {
                Collider c = _overlap[i];
                IHitTarget t = TargetOf(c);
                if (t == null || !t.Alive) continue;
                Vector3 to = c.bounds.center - center;
                to.y = 0f;
                if (halfAngle < 180f && to.sqrMagnitude > 1e-6f && Vector3.Angle(forward, to) > halfAngle) continue;
                if (!_attack.TryHitSwing(t.Id)) continue; // 이번 휘두르기에 이미 맞음
                float multiplier = _attack.CurrentSwing.Multiplier;
                switch (kind)
                {
                    case AttackMode.Burst:
                        Report(t, multiplier, _skills.BurstPoise(_attack.Burst.SlashIndex), feet, kind, _attack.Burst.SlashIndex + 1, _attack.Burst.Count);
                        break;
                    case AttackMode.Skill:
                        Report(t, multiplier, _skills.SkillPoise(_attack.Skill.Form, _attack.Skill.SlashIndex), feet, kind, _attack.Skill.SlashIndex + 1, _attack.Skill.Count);
                        float e = _attack.TakeSkillEnergy(); // 이번 스킬이 처음 맞힌 순간 한 번
                        if (e > 0f) { EnergyFromSkill += e; OnEnergy(e, true); }
                        break;
                    case AttackMode.Charged:
                        Report(t, multiplier, w.ChargedPoise(_attack.Charged.SlashIndex), feet, kind, _attack.Charged.SlashIndex + 1, _attack.Charged.Count);
                        break;
                    default:
                        Report(t, multiplier, w.ComboPoise(_attack.Combo.StepIndex), feet, AttackMode.Combo, _attack.Combo.StepIndex + 1, _attack.Combo.SwingCount);
                        break;
                }
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
            WeaponTuning w = Weapon;
            Vector3 center = feet + Vector3.up * w.HitHeight;
            int n = Physics.OverlapSphereNonAlloc(center, w.PlungeRange, _overlap, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            int hits = 0;
            PlungeAttack p = _attack.Plunge;
            for (int i = 0; i < n; i++)
            {
                IHitTarget t = TargetOf(_overlap[i]);
                if (t == null || !t.Alive) continue;
                if (!p.TryHit(t.Id)) continue; // 낙하 한 번에 대상마다 한 번 (콜라이더가 여럿이어도)
                Report(t, p.Multiplier, w.PlungePoise(p.High), feet, AttackMode.Plunge, p.High ? 2 : 1, p.Count);
                hits++;
            }
            LastLandPosition = feet;
            LastLandHits = hits;
            PlungeLanded?.Invoke(feet, w.PlungeRange);
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
            bool wasAlive = t.Alive;
            int applied = t.ReceiveHit(damage, feet);
            // 2-4: 이 한 방으로 쓰러뜨렸으면 적 표의 처치 에너지 (궁극기 중에는 안 찬다)
            // 2-6: 파티면 현재 캐릭터는 그대로, 대기 캐릭터는 × benchEnergyRatio
            if (wasAlive && !t.Alive && t is IEnergyDrop drop)
            {
                float bench = 0f;
                float gained = _party != null ? _party.GainDefeatEnergy(drop.EnergyOnDefeat, out bench) : _attack.GainEnergy(drop.EnergyOnDefeat);
                if (gained > 0f) { EnergyFromDefeat += gained; OnEnergy(gained, false); }
                LastBenchEnergyGain = bench;
                BenchEnergyFromDefeat += bench;
            }
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

        void OnEnergy(float amount, bool fromSkill)
        {
            LastEnergyGain = amount;
            EnergyGained?.Invoke(amount, fromSkill);
        }
    }
}
