using System;
using Moa.Core.Combat;
using Moa.Core.Growth;
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
    /// 3-3 무기 종류: 동작(콤보 · 강공격 · 등장)마다 무기 표의 판정 방식(WeaponTuning.Delivery) — 근접이면 위 부채꼴, 투사체면 판정 구간이 열리는 스텝에
    /// 한 발(같은 ProjectileShooter). 맞으면 같은 Report(피해 · 경직 · 처치 에너지 · 버프). 무기 종류별 분기는 이 판정 방식 하나뿐.
    /// 조준형 강공격 중에는 자동 조준을 하지 않는다(몸은 PlayerMotor가 카메라 쪽으로).
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
        PlayerVitals _vitals;   // 3-1: 피격 경직 · 전멸 연출 중에는 입력을 버린다
        ProjectileShooter _shooter; // 3-3: 투사체 (없으면 투사체 동작은 안 나감)
        WeaponTuning[] _equipped = new WeaponTuning[0]; // 3-3: 칸마다 지금 끼운 무기 (디버그 바꿔 끼기)
        long _lastShotKey = -1;  // 3-3: 이번 휘두르기에 이미 쏘는지
        int _appliedIndex;      // 3-1: ApplyActive를 마지막으로 한 칸 (쓰러져 강제 교체됐는지 SyncActive가 본다)
        EntityId _id;
        float _attackScale = 1f; // 디버그 공격력 배수 (1 = 그대로)
        MemberStats[] _growth = { MemberStats.Identity }; // 3-5: 칸마다 성장 배율 (GameRoot가 SetGrowth, 없으면 1 = 데이터 값 그대로)

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
        public float Attack => Weapon != null ? (_baseAttack * ActiveGrowth.Character + Weapon.BaseAttack * ActiveGrowth.Weapon) * _attackScale : 0f;
        public float AttackScale { get => _attackScale; set => _attackScale = value > 0f ? value : 0f; }
        /// <summary>현재 캐릭터 기본 공격력 (파티가 없으면 character 칸, Bind 전에는 0).</summary>
        public float CharacterAttack => _baseAttack * ActiveGrowth.Character;
        /// <summary>현재 무기 기본 공격력.</summary>
        public float WeaponAttack => Weapon != null ? Weapon.BaseAttack * ActiveGrowth.Weapon : 0f;
        public HitReport LastHit { get; private set; }
        public int HitCount { get; private set; }
        /// <summary>마지막 낙하 착지 자리 (발) · 그때 맞힌 수.</summary>
        public Vector3 LastLandPosition { get; private set; }
        /// <summary>3-2: 등장 공격이 맞힌 수 · 그중 그로기 적에게 추가 배율이 든 수 (디버그 · 확인용).</summary>
        public int EntryHitCount { get; private set; }
        public int EntryGroggyHitCount { get; private set; }
        /// <summary>3-2: 지금 캐릭터의 등장 공격 (Bind 전에는 null).</summary>
        public EntryAttack Entry => _attack?.Entry;
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
        /// <summary>플레이어의 한 방으로 대상이 쓰러졌을 때 (3-5 처치 보상).</summary>
        public event Action<IHitTarget> Defeated;

        // ---- 3-5 성장 ----

        MemberStats ActiveGrowth => ActiveIndex < _growth.Length ? _growth[ActiveIndex] : MemberStats.Identity;

        /// <summary>칸(0부터, 파티가 없으면 0)의 성장 배율.</summary>
        public MemberStats Growth(int slot) => slot >= 0 && slot < _growth.Length ? _growth[slot] : MemberStats.Identity;

        /// <summary>[상태 변경] 칸의 성장 배율을 정한다 (3-5, GameRoot가 성장이 바뀔 때마다) — 캐릭터 공격력 · 무기 공격력 · E · Q 배율.</summary>
        public void SetGrowth(int slot, MemberStats stats)
        {
            if (slot >= 0 && slot < _growth.Length) _growth[slot] = stats;
        }

        /// <summary>칸의 합 공격력 (캐릭터 × 성장 + 지금 끼운 무기 × 성장, 디버그 배수 없이) — 성장 화면용.</summary>
        public float MemberAttack(int slot)
        {
            MemberStats g = Growth(slot);
            if (_party == null) return slot == 0 && Weapon != null ? _baseAttack * g.Character + Weapon.BaseAttack * g.Weapon : 0f;
            CharacterData c = GetCharacter(slot);
            if (c == null) return 0f;
            WeaponTuning w = slot < _equipped.Length && _equipped[slot] != null ? _equipped[slot] : c.Weapon;
            return c.BaseAttack * g.Character + (w != null ? w.BaseAttack * g.Weapon : 0f);
        }

        public void Bind(IPlayerInput input)
        {
            _input = input;
            if (tuning == null) { Debug.LogError("PlayerCombat: CombatTuning is not assigned"); enabled = false; return; }
            _defense = GetComponent<PlayerDefense>();
            _vitals = GetComponent<PlayerVitals>();
            _shooter = GetComponent<ProjectileShooter>();
            if (_shooter != null) { _shooter.Hit -= OnProjectileHit; _shooter.Hit += OnProjectileHit; }

            if (party != null && party.MemberCount > 0)
            {
                int n = party.MemberCount;
                var characters = new CharacterData[n];
                var attacks = new PlayerAttack[n];
                var handoffs = new HandoffRules[n]; // 3-2: 스킬 뒤 교체 강화 (캐릭터의 SkillTuning)
                for (int i = 0; i < n; i++)
                {
                    CharacterData c = party.GetMember(i);
                    if (c == null) { Debug.LogError("PlayerCombat: party " + party.name + " slot " + (i + 1) + " is empty"); enabled = false; return; }
                    WeaponTuning w = c.Weapon != null ? c.Weapon : weapon;
                    if (w == null) { Debug.LogError("PlayerCombat: character " + c.name + " has no weapon"); enabled = false; return; }
                    characters[i] = c;
                    attacks[i] = Build(w, c.Skills, c.name);
                    handoffs[i] = c.Skills != null ? c.Skills.ToHandoffRules() : HandoffRules.None;
                }
                _characters = characters;
                _equipped = new WeaponTuning[n];
                for (int i = 0; i < n; i++) _equipped[i] = characters[i].Weapon != null ? characters[i].Weapon : weapon;
                _party = new Party(party.ToRules(), attacks, handoffs);
                _growth = new MemberStats[n];
                for (int i = 0; i < n; i++) _growth[i] = MemberStats.Identity;
                ApplyActive();
                return;
            }

            if (weapon == null) { Debug.LogError("PlayerCombat: WeaponTuning is not assigned"); enabled = false; return; }
            _party = null;
            _characters = new CharacterData[0];
            _equipped = new[] { weapon };
            _growth = new[] { MemberStats.Identity };
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
            if (w.UsesProjectiles && GetComponent<ProjectileShooter>() == null) Debug.LogWarning("PlayerCombat: weapon " + w.name + " shoots projectiles but there is no ProjectileShooter - those attacks hit nothing");
            var plunge = new PlungeRules(tuning.PlungeMinHeight, tuning.PlungeHighHeight, w.PlungeLowMultiplier, w.PlungeHighMultiplier,
                tuning.PlungeFallSpeed, tuning.PlungeLandRecovery);
            return new PlayerAttack(w.ToRules(), w.ToChargedRules(), plunge, tuning.ChargeHoldTime,
                s != null ? s.ToSkillRules() : null, s != null ? s.ToBurstRules() : null, w.ToEntrySlashes());
        }

        /// <summary>현재 캐릭터의 공격 · 무기 · 스킬 · 공격력으로 바꾼다 (2-6).</summary>
        void ApplyActive()
        {
            CharacterData c = _characters[_party.ActiveIndex];
            _attack = _party.Active;
            _weapon = _party.ActiveIndex < _equipped.Length && _equipped[_party.ActiveIndex] != null ? _equipped[_party.ActiveIndex] : (c.Weapon != null ? c.Weapon : weapon);
            _skills = c.Skills;
            _baseAttack = c.BaseAttack;
            _appliedIndex = _party.ActiveIndex;
        }

        /// <summary>파티가 없을 때의 캐릭터 (character 칸, 3-1 체력 · 방어용).</summary>
        public CharacterData SoloCharacter => character;

        /// <summary>
        /// 파티의 현재 칸이 바깥에서 바뀌었으면(3-1: 필드 캐릭터가 쓰러져 강제 교체) 무기 · 스킬 · 공격력을 맞추고 Switched를 알린다.
        /// 바뀌었으면 true.
        /// </summary>
        public bool SyncActive()
        {
            if (_party == null || _party.ActiveIndex == _appliedIndex) return false;
            int from = _appliedIndex;
            ApplyActive();
            Switched?.Invoke(from, _party.ActiveIndex);
            return true;
        }

        public void SetId(EntityId id) => _id = id;

        /// <summary>입력을 꺼내 공격을 고르고 한 스텝 진행 (PlayerMotor가 부른다). 막힌 입력은 버린다. stamina = 강공격 비용. 2-4: 스킬 · 궁극기 입력도.
        /// 2-6: 먼저 교체 입력(바꾸면 이 스텝부터 새 캐릭터), 끝에 교체 쿨 · 대기 캐릭터 쿨.</summary>
        public AttackTick Step(AttackContext ctx, Stamina stamina, float dt)
        {
            if (!Bound) return default;
            // 3-1: 피격 경직 · 전멸 연출 중에는 교체 · 공격 · 스킬 · 궁극기 입력을 버린다 (쿨은 그대로 돈다)
            bool blocked = _vitals != null && _vitals.InputBlocked;
            int slot = _input.ConsumeSwitch();
            if (slot > 0 && _party != null && !blocked) TrySwitch(slot - 1, ctx.Gliding);
            bool pressed = _input.ConsumeAttack() && !blocked;
            bool skill = _input.ConsumeSkill() && !blocked;
            bool burst = _input.ConsumeBurst() && !blocked;
            AttackTick tick = _attack.Step(new AttackInput(pressed, _input.AttackHeld && !blocked, skill, _input.SkillHeld && !blocked, burst), ctx, stamina, dt);
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
        public void Cancel()
        {
            _attack?.Cancel();
            if (_shooter != null) _shooter.Clear(); // 3-3: 날아가는 투사체도 지운다
        }

        /// <summary>새 단계의 몸 방향: 이동 입력 → 가장 가까운 적 → 그대로(false).</summary>
        public bool PickFacing(Vector3 feet, Planar wish, out float yaw)
        {
            yaw = 0f;
            if (wish.Length > 0f) { yaw = MoveMath.YawOf(wish); return true; }
            if (_attack != null && _attack.Charged.Active && _attack.Charged.Rules.Aimed) return false; // 3-3: 조준해 쏜 한 발은 조준 방향 그대로
            float aimRange = Weapon != null && Weapon.AutoAimRange > 0f ? Weapon.AutoAimRange : (tuning != null ? tuning.AutoAimRange : 0f); // 3-3: 활은 멀리
            if (tuning == null || aimRange <= 0f) return false;
            Vector3 center = feet + Vector3.up * Weapon.HitHeight;
            int n = Physics.OverlapSphereNonAlloc(center, aimRange, _overlap, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
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
                : _attack.Entry.Active ? AttackMode.Entry
                : _attack.Charged.Active ? AttackMode.Charged
                : _attack.Combo.Attacking ? AttackMode.Combo
                : AttackMode.None;
            if (kind == AttackMode.None) return 0; // 스킬을 누르는 중 (아직 베지 않음)
            WeaponTuning w = Weapon;
            // 3-3: 투사체로 맞히는 동작이면 겹침 검사 대신 이번 휘두르기에 한 발
            if (w.Delivery(kind) == HitDelivery.Projectile) { FireOnce(kind, feet, yawDegrees); return 0; }
            float range, angle;
            switch (kind)
            {
                case AttackMode.Burst: range = _skills.BurstHitRange; angle = _skills.BurstHitAngle; break;
                case AttackMode.Skill: { var f = _skills.GetForm(_attack.Skill.Form); range = f.hitRange; angle = f.hitAngle; break; }
                case AttackMode.Charged: range = w.ChargedHitRange; angle = w.ChargedHitAngle; break;
                case AttackMode.Entry: range = w.EntryHitRange; angle = w.EntryHitAngle; break; // 3-2
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
                if (kind == AttackMode.Burst) multiplier *= ActiveGrowth.Burst; // 3-5: 궁극기 · 스킬 레벨 배율 (레벨 1 = 1)
                else if (kind == AttackMode.Skill) multiplier *= ActiveGrowth.Skill;
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
                    case AttackMode.Entry:
                        Report(t, multiplier, w.EntryPoise, feet, kind, 1, _attack.Entry.Count);
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
        void Report(IHitTarget t, float multiplier, float poise, Vector3 feet, AttackMode kind, int step, int swing) =>
            Report(t, Attack, multiplier, poise, feet, kind, step, swing);

        /// <summary>3-3: 공격력을 주는 쪽 (투사체는 쏜 때의 공격력).</summary>
        void Report(IHitTarget t, float attack, float multiplier, float poise, Vector3 feet, AttackMode kind, int step, int swing)
        {
            var pt = t as IPoiseTarget;
            bool onGroggy = pt != null && pt.Groggy;
            float taken = pt != null ? pt.DamageTakenMultiplier : 1f;
            // 3-2: 교체 강화(버프가 없으면 정확히 1) · 그로기 적에게 등장 공격이면 추가 배율
            float buff = _party != null ? _party.DamageMultiplier : 1f;
            bool entryGroggy = kind == AttackMode.Entry && onGroggy && _party != null && _party.Rules.EntryGroggyBonus != 1f;
            float bonus = entryGroggy ? _party.Rules.EntryGroggyBonus : 1f;
            if (kind == AttackMode.Entry) { EntryHitCount++; if (entryGroggy) EntryGroggyHitCount++; }
            int damage = DamageMath.Compute(attack, multiplier * taken * buff * bonus, t.Defense, tuning.DefenseConstant);
            bool wasAlive = t.Alive;
            int applied = t.ReceiveHit(damage, feet);
            // 2-4: 이 한 방으로 쓰러뜨렸으면 적 표의 처치 에너지 (궁극기 중에는 안 찬다)
            // 2-6: 파티면 현재 캐릭터는 그대로, 대기 캐릭터는 × benchEnergyRatio
            if (wasAlive && !t.Alive) Defeated?.Invoke(t); // 3-5: 처치 보상 (GameRoot → 성장 지갑)
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
                poiseApplied = poise * (_defense != null ? _defense.PoiseMultiplier : 1f) * (_party != null ? _party.PoiseMultiplier : 1f);
                broke = pt.ReceivePoise(poiseApplied);
            }
            var report = new HitReport(t.Id, (t as Component) != null ? ((Component)t).name : "-", damage, applied, step, swing, t.NumberPosition, kind,
                poiseApplied, broke, onGroggy);
            LastHit = report;
            HitCount++;
            HitLanded?.Invoke(report);
        }

        // ---- 3-3 투사체 ----

        /// <summary>지금 동작이 맞히는 방식 (휘두르는 중이 아니면 Melee).</summary>
        public HitDelivery CurrentDelivery => _attack != null && Weapon != null ? Weapon.Delivery(_attack.Mode) : HitDelivery.Melee;
        /// <summary>조준형 강공격으로 조준 중 (몸은 카메라 방향).</summary>
        public bool Aiming => _attack != null && _attack.Aiming;
        public ProjectileShooter Shooter => _shooter;
        /// <summary>투사체가 맞힌 수 · 쏜 수 (이 플레이어 기준, 디버그 · 확인용).</summary>
        public int ProjectileHitCount { get; private set; }
        public int ShotsFired { get; private set; }
        public int ActiveProjectiles => _shooter != null ? _shooter.ActiveCount : 0;

        /// <summary>고정 스텝마다 날아가는 투사체를 진행 (PlayerMotor가 이동 · 판정 뒤에 부른다 — 교체 · 등반 중에도).</summary>
        public void AdvanceProjectiles(float dt) { if (_shooter != null) _shooter.Step(dt); }

        /// <summary>이번 휘두르기(콤보 단계 · 강공격 베기 · 등장)에 아직 안 나갔으면 한 발. 몸 가운데 높이에서 몸 앞으로.</summary>
        void FireOnce(AttackMode kind, Vector3 feet, float yawDegrees)
        {
            int counter, step, swing;
            float poise;
            WeaponTuning w = Weapon;
            switch (kind)
            {
                case AttackMode.Charged:
                    counter = _attack.Charged.Count * 1024 + _attack.Charged.SlashIndex;
                    step = _attack.Charged.SlashIndex + 1; swing = _attack.Charged.Count;
                    poise = w.ChargedPoise(_attack.Charged.SlashIndex);
                    break;
                case AttackMode.Entry:
                    counter = _attack.Entry.Count;
                    step = 1; swing = _attack.Entry.Count;
                    poise = w.EntryPoise;
                    break;
                default:
                    counter = _attack.Combo.SwingCount;
                    step = _attack.Combo.StepIndex + 1; swing = _attack.Combo.SwingCount;
                    poise = w.ComboPoise(_attack.Combo.StepIndex);
                    break;
            }
            long key = ((long)kind << 40) ^ ((long)ActiveIndex << 36) ^ counter;
            if (key == _lastShotKey) return; // 판정 구간이 여러 스텝이어도 한 발
            _lastShotKey = key;
            if (_shooter == null) return;
            float r = yawDegrees * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Sin(r), 0f, Mathf.Cos(r));
            var shot = new ProjectileShot(Attack, _attack.CurrentSwing.Multiplier, poise, kind, step, swing);
            if (_shooter.Fire(w.ToProjectileRules(), shot, feet + Vector3.up * w.HitHeight, dir, feet) >= 0) ShotsFired++;
        }

        void OnProjectileHit(IHitTarget t, ProjectileShot shot, Vector3 shooterFeet)
        {
            if (!Bound || t == null || !t.Alive) return;
            ProjectileHitCount++;
            Report(t, shot.Attack, shot.Multiplier, shot.Poise, shooterFeet, shot.Kind, shot.Step, shot.Swing);
        }

        /// <summary>
        /// [상태 변경] 현재 캐릭터의 무기를 바꾼다 (3-3, 디버그 바꿔 끼기 — 에셋은 그대로, 판을 다시 시작하면 원래대로).
        /// 콤보 · 강공격 · 낙하 · 등장 공격 · 무기 공격력이 새 무기 것, 스킬 · 궁극기 · 에너지는 그대로. 바꿨으면 true.
        /// </summary>
        public bool DebugSetWeapon(WeaponTuning w)
        {
            if (!Bound || w == null || tuning == null) return false;
            var plunge = new PlungeRules(tuning.PlungeMinHeight, tuning.PlungeHighHeight, w.PlungeLowMultiplier, w.PlungeHighMultiplier,
                tuning.PlungeFallSpeed, tuning.PlungeLandRecovery);
            _attack.ChangeWeapon(w.ToRules(), w.ToChargedRules(), plunge, w.ToEntrySlashes());
            int slot = ActiveIndex;
            if (slot < _equipped.Length) _equipped[slot] = w;
            _weapon = w;
            _lastShotKey = -1;
            return true;
        }

        /// <summary>[상태 변경] 현재 캐릭터를 데이터의 무기로 되돌린다.</summary>
        public bool DebugResetWeapon()
        {
            CharacterData c = _party != null ? ActiveCharacter : null;
            WeaponTuning w = c != null && c.Weapon != null ? c.Weapon : weapon;
            return DebugSetWeapon(w);
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
