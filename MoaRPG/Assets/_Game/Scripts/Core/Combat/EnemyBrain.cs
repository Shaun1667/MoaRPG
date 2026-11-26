using System;
using Moa.Core.Motion;

namespace Moa.Core.Combat
{
    /// <summary>적 AI 상태 (2-5, 끝에만 추가).</summary>
    public enum EnemyState
    {
        /// <summary>제자리에서 기다림 (순찰이 있으면 PatrolWait 뒤 순찰).</summary>
        Idle,
        /// <summary>집 ↔ 순찰 지점을 천천히 오감.</summary>
        Patrol,
        /// <summary>플레이어를 쫓음.</summary>
        Chase,
        /// <summary>사거리 안: 예고 → 공격 (EnemyAttackCycle).</summary>
        Attack,
        /// <summary>시야를 잃었거나 너무 멀어 집으로 돌아감 (도착하면 체력 가득).</summary>
        Return,
        /// <summary>맞아서 잠깐 경직.</summary>
        Stagger,
        /// <summary>경직 게이지가 차서 그로기 (행동 없음).</summary>
        Groggy,
        /// <summary>쓰러짐 (RespawnDelay 뒤 집에서 다시 남).</summary>
        Dead,
    }

    /// <summary>
    /// 적 공격 한 줄 (3-4, EnemyTuning 공격 표 한 줄 → 넘긴다, BALANCE 11 · 16번): 배율 · 범위 · 예고 · 넘어짐 · 투사체 여부.
    /// 적 종류별 분기 코드 없이 값으로만 달라진다 — 판정 방식(근접 / 투사체) 하나와 이어지는 시간(Active, 돌진)이 있는지.
    /// </summary>
    public readonly struct EnemyAttackSpec
    {
        /// <summary>피해 배율 (적 공격력 × 이것, 그 뒤 캐릭터 방어).</summary>
        public readonly float Multiplier;
        /// <summary>이 안이면 멈춰 이 공격을 시작 (발 사이 평면 거리 m).</summary>
        public readonly float StartRange;
        /// <summary>근접: 닿는 거리 · 앞 부채꼴 전체 각도. 투사체: 예고 선 길이(표현)만.</summary>
        public readonly float HitRange;
        public readonly float HitAngle;
        /// <summary>쉬기 · 예고 · 이어짐(돌진) · 자세.</summary>
        public readonly EnemyAttackRules Timing;
        /// <summary>이어지는 동안(Timing.Active) 몸 앞으로 나가는 거리 (m, 돌진). 0이면 제자리.</summary>
        public readonly float LungeDistance;
        /// <summary>맞으면 플레이어가 넘어져 행동 불가인 시간 (초, 회피로 못 끊음). 0이면 넘어짐 없음(보통 피격 경직).</summary>
        public readonly float KnockdownTime;
        /// <summary>판정 방식 (3-3과 같은 값) — 투사체면 예고가 끝나는 스텝에 한 발을 쏜다.</summary>
        public readonly HitDelivery Delivery;
        public readonly ProjectileRules Projectile;

        public EnemyAttackSpec(float multiplier, float startRange, float hitRange, float hitAngle, EnemyAttackRules timing,
            float lungeDistance, float knockdownTime, HitDelivery delivery, ProjectileRules projectile)
        {
            Multiplier = multiplier;
            StartRange = startRange;
            HitRange = hitRange;
            HitAngle = hitAngle;
            Timing = timing;
            LungeDistance = lungeDistance > 0f ? lungeDistance : 0f;
            KnockdownTime = knockdownTime > 0f ? knockdownTime : 0f;
            Delivery = delivery;
            Projectile = projectile;
        }

        public bool Knockdown => KnockdownTime > 0f;
        public bool IsProjectile => Delivery == HitDelivery.Projectile;
        /// <summary>이어지는 공격인지 (돌진 처럼 Active 동안 매 스텝 닿음 검사, 한 번만 맞힘).</summary>
        public bool Sustained => !IsProjectile && Timing.Active > 0f;
        /// <summary>돌진 빠르기 (m/s) = 거리 ÷ 이어지는 시간.</summary>
        public float LungeSpeed => Timing.Active > 0f ? LungeDistance / Timing.Active : 0f;
    }

    /// <summary>
    /// 길찾기 (3-4) — Core는 어디로 갈지(목적지)만 정하고, 그리로 가는 다음 지점은 Unity(NavMesh)가 돌려준다.
    /// 길이 없거나 NavMesh가 없으면 목적지를 그대로 돌려준다(곧장).
    /// </summary>
    public interface IEnemyPath
    {
        /// <summary>self에서 destination으로 가는 길의 다음 지점 (평면).</summary>
        Planar Steer(Planar self, Planar destination);
    }

    /// <summary>적 AI 수치 (EnemyTuning → 넘긴다, BALANCE 11 · 16번). 거리는 발 사이 평면 거리(m), 각도 도, 시간 초, 속력 m/s.</summary>
    public readonly struct EnemyAiRules
    {
        /// <summary>앞 시야 안에서 플레이어를 알아채는 거리.</summary>
        public readonly float DetectRange;
        /// <summary>시야 전체 각도 (좌우 절반씩).</summary>
        public readonly float ViewAngle;
        /// <summary>이 안이면 등 뒤여도 알아챔 (시야선은 필요).</summary>
        public readonly float NoticeRange;
        /// <summary>쫓다가 플레이어가 이보다 멀면 복귀.</summary>
        public readonly float LoseRange;
        /// <summary>쫓다가 시야선이 이만큼 계속 막히면 복귀.</summary>
        public readonly float LoseSightTime;
        /// <summary>쫓다가 집에서 이보다 멀어지면 복귀.</summary>
        public readonly float LeashRange;
        /// <summary>첫 공격 줄의 시작 거리 · 닿는 거리 · 각도 · 주기 (2-5 한 줄짜리 적과 같은 이름).</summary>
        public readonly float AttackRange;
        public readonly float HitRange;
        public readonly float HitAngle;
        public readonly float PatrolSpeed;
        public readonly float ChaseSpeed;
        public readonly float ReturnSpeed;
        /// <summary>몸을 돌리는 빠르기 (도/초).</summary>
        public readonly float TurnSpeed;
        /// <summary>순찰 지점 · 집에서 쉬는 시간.</summary>
        public readonly float PatrolWait;
        /// <summary>지점에 닿았다고 보는 거리.</summary>
        public readonly float ArriveDistance;
        /// <summary>맞으면 멈추는 시간 (예고 · 돌진 중에는 안 멈춤). 0이면 맞아도 안 멈춤(3-4 정예).</summary>
        public readonly float HitStunTime;
        /// <summary>쓰러진 뒤 다시 나기까지.</summary>
        public readonly float RespawnDelay;
        /// <summary>첫 공격 줄의 주기: Interval = 사거리에 들어와 예고까지 · 공격 사이 쉬기, Telegraph = 예고, Recovery = 공격 뒤 자세.</summary>
        public readonly EnemyAttackRules Attack;
        /// <summary>3-4: 공격 표 (한 줄 = 공격 하나, 차례로 돌아가며 쓴다). 비지 않다.</summary>
        public readonly EnemyAttackSpec[] Attacks;
        /// <summary>3-4: 공격 사이(쉬는 동안) 플레이어가 이보다 가까우면 물러난다 (원거리). 0이면 물러나지 않음.</summary>
        public readonly float RetreatRange;
        public readonly float RetreatSpeed;

        /// <summary>2-5 한 줄짜리 근접 적 (배율은 0 — 피해는 Unity가 따로 넣던 방식, 시험용).</summary>
        public EnemyAiRules(float detectRange, float viewAngle, float noticeRange, float loseRange, float loseSightTime, float leashRange,
            float attackRange, float hitRange, float hitAngle, float patrolSpeed, float chaseSpeed, float returnSpeed, float turnSpeed,
            float patrolWait, float arriveDistance, float hitStunTime, float respawnDelay, EnemyAttackRules attack)
            : this(detectRange, viewAngle, noticeRange, loseRange, loseSightTime, leashRange, patrolSpeed, chaseSpeed, returnSpeed, turnSpeed,
                patrolWait, arriveDistance, hitStunTime, respawnDelay, 0f, 0f,
                new[] { new EnemyAttackSpec(0f, attackRange, hitRange, hitAngle, attack, 0f, 0f, HitDelivery.Melee, default) })
        {
        }

        /// <summary>3-4: 공격 표가 있는 적 (원거리면 물러나기 거리 · 빠르기).</summary>
        public EnemyAiRules(float detectRange, float viewAngle, float noticeRange, float loseRange, float loseSightTime, float leashRange,
            float patrolSpeed, float chaseSpeed, float returnSpeed, float turnSpeed, float patrolWait, float arriveDistance,
            float hitStunTime, float respawnDelay, float retreatRange, float retreatSpeed, EnemyAttackSpec[] attacks)
        {
            DetectRange = detectRange;
            ViewAngle = viewAngle;
            NoticeRange = noticeRange;
            LoseRange = loseRange;
            LoseSightTime = loseSightTime;
            LeashRange = leashRange;
            PatrolSpeed = patrolSpeed;
            ChaseSpeed = chaseSpeed;
            ReturnSpeed = returnSpeed;
            TurnSpeed = turnSpeed;
            PatrolWait = patrolWait;
            ArriveDistance = arriveDistance;
            HitStunTime = hitStunTime;
            RespawnDelay = respawnDelay;
            RetreatRange = retreatRange > 0f ? retreatRange : 0f;
            RetreatSpeed = retreatSpeed > 0f ? retreatSpeed : 0f;
            Attacks = attacks != null && attacks.Length > 0 ? (EnemyAttackSpec[])attacks.Clone() : new[] { default(EnemyAttackSpec) };
            AttackRange = Attacks[0].StartRange;
            HitRange = Attacks[0].HitRange;
            HitAngle = Attacks[0].HitAngle;
            Attack = Attacks[0].Timing;
        }
    }

    /// <summary>한 고정 스텝에 Unity가 잰 것 (적 쪽에서 본 플레이어).</summary>
    public readonly struct EnemySense
    {
        /// <summary>대상(플레이어)이 있는지.</summary>
        public readonly bool HasTarget;
        /// <summary>적 발 (평면).</summary>
        public readonly Planar Self;
        /// <summary>적 몸 방향 (도, 0 = +Z).</summary>
        public readonly float Yaw;
        /// <summary>플레이어 발 (평면).</summary>
        public readonly Planar Target;
        /// <summary>플레이어 발 높이 − 적 발 높이 (m).</summary>
        public readonly float HeightDiff;
        /// <summary>눈에서 플레이어까지 가리는 것이 없는지 (Unity 레이).</summary>
        public readonly bool LineOfSight;

        public EnemySense(Planar self, float yaw, Planar target, float heightDiff, bool lineOfSight)
        {
            HasTarget = true;
            Self = self;
            Yaw = yaw;
            Target = target;
            HeightDiff = heightDiff;
            LineOfSight = lineOfSight;
        }

        /// <summary>대상이 없는 스텝 (플레이어가 없는 씬 · 디버그).</summary>
        public static EnemySense NoTarget(Planar self, float yaw) => new EnemySense(false, self, yaw);

        EnemySense(bool hasTarget, Planar self, float yaw)
        {
            HasTarget = hasTarget;
            Self = self;
            Yaw = yaw;
            Target = Planar.Zero;
            HeightDiff = 0f;
            LineOfSight = false;
        }

        /// <summary>발 사이 평면 거리 (대상이 없으면 무한).</summary>
        public float Distance => HasTarget ? (Target - Self).Length : float.PositiveInfinity;
    }

    /// <summary>한 고정 스텝의 결과 — Unity가 이대로 움직이고 돌리고, 닿으면 PlayerDefense에 넘기고, 쏘면 투사체를 낸다.</summary>
    public readonly struct EnemyTick
    {
        public readonly EnemyState State;
        /// <summary>이번 스텝 수평 속도 (m/s, 방향 × 속력).</summary>
        public readonly Planar Velocity;
        /// <summary>이번 스텝 뒤 몸 방향 (도).</summary>
        public readonly float Yaw;
        /// <summary>이번 스텝에 근접 공격이 닿는 순간인지 (돌진은 맞힌 스텝 또는 끝난 스텝).</summary>
        public readonly bool AttackLanded;
        /// <summary>닿는 순간 플레이어가 맞는 자리에 있었는지 (거리 · 부채꼴 · 높이).</summary>
        public readonly bool AttackHits;
        /// <summary>
        /// 닿는 순간 맞는 자리는 아니지만 이번 예고 중 한 번이라도 그 자리에 있었다 (회피로 빠져나감).
        /// Unity는 이때 플레이어가 무적(회피)이면 저스트 판정을 맡긴다 — 저스트 회피가 빠져나간 거리 때문에 없어지지 않게.
        /// </summary>
        public readonly bool AttackGrazed;
        /// <summary>이번 스텝에 다시 났다 (Unity는 집으로 옮기고 체력 · 경직을 채운다).</summary>
        public readonly bool Respawned;
        /// <summary>이번 스텝에 집에 돌아왔다 (체력 가득).</summary>
        public readonly bool Healed;
        /// <summary>3-4: 이번 스텝에 투사체를 쏜다 (Unity가 플레이어 쪽으로 한 발).</summary>
        public readonly bool FireProjectile;
        /// <summary>3-4: 이 스텝의 공격 줄 번호 (0부터) — 배율 · 넘어짐 · 투사체 수치는 이 줄에서.</summary>
        public readonly int AttackIndex;

        public EnemyTick(EnemyState state, Planar velocity, float yaw, bool attackLanded, bool attackHits, bool respawned, bool healed)
            : this(state, velocity, yaw, attackLanded, attackHits, false, respawned, healed) { }

        public EnemyTick(EnemyState state, Planar velocity, float yaw, bool attackLanded, bool attackHits, bool attackGrazed, bool respawned, bool healed)
            : this(state, velocity, yaw, attackLanded, attackHits, attackGrazed, respawned, healed, false, 0) { }

        public EnemyTick(EnemyState state, Planar velocity, float yaw, bool attackLanded, bool attackHits, bool attackGrazed, bool respawned, bool healed,
            bool fireProjectile, int attackIndex)
        {
            AttackGrazed = attackGrazed;
            State = state;
            Velocity = velocity;
            Yaw = yaw;
            AttackLanded = attackLanded;
            AttackHits = attackHits;
            Respawned = respawned;
            Healed = healed;
            FireProjectile = fireProjectile;
            AttackIndex = attackIndex;
        }
    }

    /// <summary>
    /// 적 AI 상태 기계 (2-5, 3-4 공격 표 · 길찾기). 엔진을 모르고, 고정 스텝마다 Unity(EnemyAgent)가 잰 <see cref="EnemySense"/>로 <see cref="Step"/>을 부른다.
    /// Idle(순찰이 있으면 PatrolWait 뒤) ↔ Patrol → 알아채면 Chase → 지금 공격 줄의 StartRange 안이면 Attack(EnemyAttackCycle: 쉬기 → 예고 → [돌진] → 자세)
    /// → 시야선이 LoseSightTime 막히거나 LoseRange 밖이거나 집에서 LeashRange 밖이면 Return(도착하면 체력 가득 · Idle).
    /// 공격 표(3-4)는 한 줄씩 차례로 쓴다(자세가 끝나면 다음 줄). 근접 — 예고가 끝나는 스텝에 닿음(2-5 그대로), 돌진 — Active 동안 앞으로 나가며 처음 닿은 스텝에 한 번,
    /// 투사체 — 예고 중 플레이어 쪽으로 돌고 끝나는 스텝에 한 발(FireProjectile). RetreatRange가 있으면 쉬는 동안 플레이어가 가까우면 마주본 채 물러난다.
    /// 이동은 목적지만 정하고 다음 지점은 <see cref="Path"/>(Unity NavMesh)에 묻는다 — 없으면 곧장.
    /// 맞으면(<see cref="NotifyHit"/>) 알아채고 HitStunTime 동안 Stagger — 예고 · 돌진 중에는 멈추지 않는다(예고한 공격은 나간다), HitStunTime 0이면 아예 안 멈춤.
    /// 그로기면 Groggy(행동 없음, 공격 주기 처음으로), 풀리면 Chase. 쓰러지면(<see cref="Die"/>) Dead → RespawnDelay 뒤 집에서 Idle.
    /// 근접 예고가 시작되면 몸을 돌리지 않는다(옆으로 피할 수 있게). 사거리를 벗어나도 예고 · 자세는 끝까지 하고, 쉬는 동안 벗어나면 다시 쫓는다.
    /// </summary>
    public sealed class EnemyBrain
    {
        const float Epsilon = 1e-5f;
        readonly EnemyAiRules _rules;
        readonly EnemyAttackCycle[] _cycles;
        readonly Planar _patrolOffset;
        int _current; // 지금 공격 줄
        bool _activeHit; // 이번 돌진이 이미 맞혔는지 (한 번만)
        bool _patrolOut; // 순찰: 지금 순찰 지점 쪽으로 가는 중
        bool _telegraphInArea; // 이번 예고 중 플레이어가 맞는 자리에 있었던 적이 있는지
        float _lostSight;

        public EnemyBrain(EnemyAiRules rules, Planar home, Planar patrolOffset)
        {
            _rules = rules;
            EnemyAttackSpec[] attacks = rules.Attacks ?? new[] { new EnemyAttackSpec(0f, rules.AttackRange, rules.HitRange, rules.HitAngle, rules.Attack, 0f, 0f, HitDelivery.Melee, default) };
            _cycles = new EnemyAttackCycle[attacks.Length];
            for (int i = 0; i < attacks.Length; i++) _cycles[i] = new EnemyAttackCycle(attacks[i].Timing);
            Home = home;
            _patrolOffset = patrolOffset;
        }

        public EnemyAiRules Rules => _rules;
        /// <summary>지금 공격 줄의 공격 주기 (Core, 디버그 · 그리기용).</summary>
        public EnemyAttackCycle Cycle => _cycles[_current];
        /// <summary>3-4: 공격 표 줄 수 · 지금 줄 번호 · 그 줄.</summary>
        public int AttackKinds => _cycles.Length;
        public int CurrentAttackIndex => _current;
        public EnemyAttackSpec CurrentAttack => Attack(_current);
        public EnemyAttackSpec Attack(int index) => _rules.Attacks != null && index >= 0 && index < _rules.Attacks.Length ? _rules.Attacks[index] : default;
        /// <summary>3-4: 길찾기 (Unity NavMesh). null이면 곧장.</summary>
        public IEnemyPath Path { get; set; }
        public EnemyState State { get; private set; }
        /// <summary>지금 상태가 된 뒤 지난 초.</summary>
        public float StateTime { get; private set; }
        public Planar Home { get; }
        public Planar PatrolOffset => _patrolOffset;
        public bool HasPatrol => _patrolOffset.Length > Epsilon;
        /// <summary>지금 순찰 목적지 (집 또는 집 + 순찰 오프셋).</summary>
        public Planar PatrolTarget => _patrolOut ? Home + _patrolOffset : Home;
        /// <summary>[상태 변경] 멈춤 (디버그) — 서 있고 공격 주기도 처음으로.</summary>
        public bool Paused { get; set; }
        /// <summary>쫓는 중 시야선이 막혀 있던 초.</summary>
        public float LostSightTime => _lostSight;
        /// <summary>마지막 스텝에 잰 플레이어 거리 · 집까지 거리 · 시야선 · 알아챌 수 있었는지 (디버그).</summary>
        public float LastDistance { get; private set; } = float.PositiveInfinity;
        public float LastHomeDistance { get; private set; }
        public bool LastLineOfSight { get; private set; }
        public bool LastCanSee { get; private set; }
        /// <summary>마지막 복귀 까닭 (디버그).</summary>
        public EnemyReturnReason LastReturnReason { get; private set; }
        /// <summary>3-4: 마지막으로 움직인 스텝의 목적지 · 길의 다음 지점 (디버그).</summary>
        public Planar LastDestination { get; private set; }
        public Planar LastWaypoint { get; private set; }
        public bool Retreating { get; private set; }

        /// <summary>디버그 · 확인용 수.</summary>
        public int DetectCount { get; private set; }
        public int AttackCount { get; private set; }
        public int AttackHitCount { get; private set; }
        public int AttackMissCount { get; private set; }
        public int StaggerCount { get; private set; }
        public int ArmorCount { get; private set; }
        public int ReturnCount { get; private set; }
        public int DeathCount { get; private set; }
        public int RespawnCount { get; private set; }
        /// <summary>3-4: 쏜 투사체 수 · 돌진 수 · 물러난 스텝 수.</summary>
        public int FireCount { get; private set; }
        public int LungeCount { get; private set; }
        public int RetreatSteps { get; private set; }

        /// <summary>상태가 바뀔 때 (전, 후).</summary>
        public event Action<EnemyState, EnemyState> StateChanged;

        public bool Alive => State != EnemyState.Dead;
        public float RespawnLeft => State == EnemyState.Dead ? MathF.Max(0f, _rules.RespawnDelay - StateTime) : 0f;
        /// <summary>쫓거나 싸우는 중 (알아챈 상태).</summary>
        public bool Engaged => State == EnemyState.Chase || State == EnemyState.Attack || State == EnemyState.Stagger || State == EnemyState.Groggy;

        /// <summary>한 고정 스텝. groggy = 경직 게이지가 그로기인지 (Unity의 Poise).</summary>
        public EnemyTick Step(float dt, in EnemySense s, bool groggy)
        {
            float dist = s.Distance;
            float homeDist = (Home - s.Self).Length;
            LastDistance = dist;
            LastHomeDistance = homeDist;
            LastLineOfSight = s.HasTarget && s.LineOfSight;
            LastCanSee = CanSee(_rules, s);
            Retreating = false;
            float yaw = s.Yaw;

            if (State == EnemyState.Dead)
            {
                StateTime += dt;
                if (StateTime < _rules.RespawnDelay - Epsilon) return Stand(yaw);
                Respawn();
                return new EnemyTick(State, Planar.Zero, yaw, false, false, true, false);
            }
            if (groggy)
            {
                if (State != EnemyState.Groggy) Enter(EnemyState.Groggy);
                else StateTime += dt;
                Cycle.Advance(dt, false);
                return Stand(yaw);
            }
            if (State == EnemyState.Groggy) Enter(EnemyState.Chase); // 풀리면 다시 쫓는다
            if (Paused)
            {
                Cycle.Advance(dt, false);
                return Stand(yaw);
            }

            StateTime += dt;
            switch (State)
            {
                case EnemyState.Idle:
                    if (LastCanSee) { Detect(); return ChaseStep(dt, s, dist, homeDist); }
                    if (HasPatrol && StateTime >= _rules.PatrolWait - Epsilon)
                    {
                        _patrolOut = !_patrolOut;
                        Enter(EnemyState.Patrol);
                        return MoveTo(dt, s, PatrolTarget, _rules.PatrolSpeed);
                    }
                    return Stand(yaw);

                case EnemyState.Patrol:
                    if (LastCanSee) { Detect(); return ChaseStep(dt, s, dist, homeDist); }
                    if ((PatrolTarget - s.Self).Length <= _rules.ArriveDistance) { Enter(EnemyState.Idle); return Stand(yaw); }
                    return MoveTo(dt, s, PatrolTarget, _rules.PatrolSpeed);

                case EnemyState.Chase:
                    return ChaseStep(dt, s, dist, homeDist);

                case EnemyState.Attack:
                    return AttackStep(dt, s, dist, homeDist);

                case EnemyState.Return:
                    if (homeDist <= _rules.ArriveDistance)
                    {
                        Enter(EnemyState.Idle);
                        _patrolOut = false;
                        ReturnCount++;
                        return new EnemyTick(State, Planar.Zero, yaw, false, false, false, true);
                    }
                    return MoveTo(dt, s, Home, _rules.ReturnSpeed);

                case EnemyState.Stagger:
                    if (StateTime >= _rules.HitStunTime - Epsilon) { Enter(EnemyState.Chase); return ChaseStep(dt, s, dist, homeDist); }
                    return Stand(yaw);

                default:
                    return Stand(yaw);
            }
        }

        EnemyTick ChaseStep(float dt, in EnemySense s, float dist, float homeDist)
        {
            if (State != EnemyState.Chase) Enter(EnemyState.Chase);
            if (!s.HasTarget) return BeginReturn(EnemyReturnReason.NoTarget, s.Yaw);
            _lostSight = s.LineOfSight ? 0f : _lostSight + dt;
            if (dist > _rules.LoseRange) return BeginReturn(EnemyReturnReason.TooFar, s.Yaw);
            if (_lostSight >= _rules.LoseSightTime - Epsilon) return BeginReturn(EnemyReturnReason.LostSight, s.Yaw);
            if (homeDist > _rules.LeashRange) return BeginReturn(EnemyReturnReason.Leash, s.Yaw);
            if (dist <= CurrentAttack.StartRange)
            {
                Enter(EnemyState.Attack);
                return AttackStep(dt, s, dist, homeDist);
            }
            return MoveTo(dt, s, s.Target, _rules.ChaseSpeed);
        }

        EnemyTick AttackStep(float dt, in EnemySense s, float dist, float homeDist)
        {
            EnemyAttackSpec spec = CurrentAttack;
            EnemyAttackCycle cycle = Cycle;
            float yaw = s.Yaw;
            Planar velocity = Planar.Zero;
            // 쉬는 동안(예고 전)만 몸을 돌리고, 사거리를 벗어나면 다시 쫓는다. 원거리는 너무 가까우면 마주본 채 물러난다 (3-4)
            if (cycle.Phase == EnemyAttackPhase.Idle)
            {
                if (!s.HasTarget || dist > spec.StartRange)
                {
                    cycle.Reset();
                    Enter(EnemyState.Chase);
                    return ChaseStep(dt, s, dist, homeDist);
                }
                yaw = Face(s, s.Target, dt);
                if (_rules.RetreatRange > 0f && dist < _rules.RetreatRange - Epsilon && homeDist < _rules.LeashRange)
                    velocity = RetreatVelocity(dt, s);
            }
            else if (cycle.Phase == EnemyAttackPhase.Telegraph && spec.IsProjectile && s.HasTarget)
            {
                yaw = Face(s, s.Target, dt); // 3-4: 투사체는 예고 중에도 조준 (쏜 뒤 옆으로 비켜 피한다)
            }
            bool wasTelegraph = cycle.Phase == EnemyAttackPhase.Telegraph;
            bool wasActive = cycle.Phase == EnemyAttackPhase.Active;
            bool wasRecovery = cycle.Phase == EnemyAttackPhase.Recovery;
            bool started = cycle.Advance(dt, true);
            bool inArea = s.HasTarget && InHitArea(spec, s.Self, yaw, s.Target, s.HeightDiff);
            if (cycle.Phase == EnemyAttackPhase.Telegraph && !wasTelegraph) _telegraphInArea = false; // 예고 시작
            if (cycle.Phase == EnemyAttackPhase.Telegraph || started) _telegraphInArea |= inArea;
            bool landed = false, hits = false, grazed = false, fire = false;
            int index = _current;
            if (started)
            {
                AttackCount++;
                if (spec.IsProjectile) { fire = true; FireCount++; }
                else if (spec.Sustained) { _activeHit = false; LungeCount++; }
                else
                {
                    landed = true;
                    hits = inArea;
                    grazed = !hits && _telegraphInArea;
                    if (hits) AttackHitCount++;
                    else AttackMissCount++;
                }
            }
            if (spec.Sustained)
            {
                bool activeNow = cycle.Phase == EnemyAttackPhase.Active;
                bool activeEnded = wasActive && !activeNow;
                if ((activeNow || activeEnded) && !_activeHit && inArea)
                {
                    landed = true;
                    hits = true;
                    _activeHit = true;
                    AttackHitCount++;
                }
                else if (activeEnded && !_activeHit)
                {
                    landed = true;
                    grazed = _telegraphInArea;
                    AttackMissCount++;
                }
                if (activeNow) velocity = Forward(yaw) * spec.LungeSpeed;
            }
            if (wasRecovery && cycle.Phase == EnemyAttackPhase.Idle) NextAttack();
            return new EnemyTick(State, velocity, yaw, landed, hits, grazed, false, false, fire, index);
        }

        /// <summary>다음 공격 줄로 (한 줄이면 그대로).</summary>
        void NextAttack()
        {
            if (_cycles.Length < 2) return;
            _current = (_current + 1) % _cycles.Length;
            _cycles[_current].Reset();
        }

        /// <summary>플레이어에서 RetreatRange만큼 떨어진 자리로 (길찾기), 빠르기 RetreatSpeed. 몸은 플레이어를 본 채.</summary>
        Planar RetreatVelocity(float dt, in EnemySense s)
        {
            Planar away = s.Self - s.Target;
            Planar dir = away.Length > Epsilon ? away.Normalized : Forward(s.Yaw + 180f);
            Planar dest = s.Target + dir * _rules.RetreatRange;
            Planar wp = Steer(s.Self, dest);
            Planar to = wp - s.Self;
            float len = to.Length;
            if (len <= Epsilon) return Planar.Zero;
            Retreating = true;
            RetreatSteps++;
            float v = _rules.RetreatSpeed * dt > len ? len / dt : _rules.RetreatSpeed;
            return to * (v / len);
        }

        EnemyTick BeginReturn(EnemyReturnReason reason, float yaw)
        {
            LastReturnReason = reason;
            Enter(EnemyState.Return);
            return Stand(yaw);
        }

        EnemyTick MoveTo(float dt, in EnemySense s, Planar target, float speed)
        {
            Planar to = target - s.Self;
            if (to.Length <= Epsilon) return Stand(s.Yaw);
            // 3-4: 길의 다음 지점으로 (길찾기가 없거나 길이 없으면 목적지 그대로 = 2-5)
            Planar wp = Steer(s.Self, target);
            Planar step = wp - s.Self;
            if (step.Length <= Epsilon) { wp = target; step = to; }
            float len = step.Length;
            float yaw = Face(s, wp, dt);
            // 이번 스텝에 지나치지 않게
            float v = speed * dt > len ? len / dt : speed;
            return new EnemyTick(State, step * (v / len), yaw, false, false, false, false);
        }

        Planar Steer(Planar self, Planar destination)
        {
            LastDestination = destination;
            Planar wp = Path != null ? Path.Steer(self, destination) : destination;
            LastWaypoint = wp;
            return wp;
        }

        float Face(in EnemySense s, Planar target, float dt)
        {
            Planar to = target - s.Self;
            if (to.Length <= Epsilon) return s.Yaw;
            return MoveMath.TurnTowards(s.Yaw, MoveMath.YawOf(to), _rules.TurnSpeed, dt);
        }

        static Planar Forward(float yaw)
        {
            float r = yaw * (MathF.PI / 180f);
            return new Planar(MathF.Sin(r), MathF.Cos(r));
        }

        EnemyTick Stand(float yaw) => new EnemyTick(State, Planar.Zero, yaw, false, false, false, false, false, false, _current);

        void Detect()
        {
            DetectCount++;
        }

        void Enter(EnemyState next)
        {
            EnemyState prev = State;
            // 공격 주기는 공격 상태로 들어올 때마다 처음부터 (나갈 때 예고 중이면 취소로 센다)
            if (next != EnemyState.Attack || prev != EnemyState.Attack) Cycle.Advance(0f, false);
            if (next == EnemyState.Chase && prev != EnemyState.Chase) _lostSight = 0f;
            State = next;
            StateTime = 0f;
            if (prev != next) StateChanged?.Invoke(prev, next);
        }

        /// <summary>
        /// 플레이어에게 맞았다 (피해가 들어간 스텝, 살아 있을 때). 알아채고 HitStunTime 동안 멈춘다 — 경직했으면 true.
        /// 예고 · 돌진 중이면 멈추지 않는다(ArmorCount). HitStunTime 0(3-4 정예)이면 알아채기만 하고 안 멈춘다. 그로기 · 쓰러짐이면 아무것도 안 함.
        /// </summary>
        public bool NotifyHit()
        {
            if (State == EnemyState.Dead || State == EnemyState.Groggy) return false;
            if (State == EnemyState.Attack && (Cycle.Phase == EnemyAttackPhase.Telegraph || Cycle.Phase == EnemyAttackPhase.Active)) { ArmorCount++; return false; }
            if (_rules.HitStunTime <= 0f)
            {
                ArmorCount++;
                if (!Engaged) { DetectCount++; Enter(EnemyState.Chase); }
                return false;
            }
            if (!Engaged && State != EnemyState.Stagger) DetectCount++;
            StaggerCount++;
            Enter(EnemyState.Stagger);
            return true;
        }

        /// <summary>쓰러졌다 (체력 0). RespawnDelay 뒤 다시 난다.</summary>
        public void Die()
        {
            if (State == EnemyState.Dead) return;
            DeathCount++;
            Enter(EnemyState.Dead);
        }

        /// <summary>[상태 변경] 바로 다시 난다 (집에서 Idle, 공격 표 처음 줄). Unity는 집으로 옮기고 체력 · 경직을 채운다.</summary>
        public void Respawn()
        {
            bool wasDead = State == EnemyState.Dead;
            _patrolOut = false;
            _lostSight = 0f;
            Enter(EnemyState.Idle);
            foreach (var c in _cycles) c.Reset();
            _current = 0;
            if (wasDead) RespawnCount++;
        }

        /// <summary>
        /// [상태 변경] 상태를 바로 바꾼다 (디버그). Idle · Patrol · Chase · Return · Stagger만 — Attack은 Chase에서 사거리로,
        /// Groggy는 경직 게이지로, Dead는 체력으로만 된다. 쓰러져 있으면 false.
        /// </summary>
        public bool ForceState(EnemyState state)
        {
            if (State == EnemyState.Dead) return false;
            switch (state)
            {
                case EnemyState.Idle:
                case EnemyState.Chase:
                case EnemyState.Stagger:
                    Enter(state);
                    return true;
                case EnemyState.Patrol:
                    if (!HasPatrol) return false;
                    _patrolOut = !_patrolOut;
                    Enter(state);
                    return true;
                case EnemyState.Return:
                    LastReturnReason = EnemyReturnReason.Forced;
                    Enter(state);
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>앞 시야(ViewAngle) 안 DetectRange 이내, 또는 NoticeRange 이내 — 둘 다 시야선이 있어야.</summary>
        public static bool CanSee(EnemyAiRules rules, in EnemySense s)
        {
            if (!s.HasTarget || !s.LineOfSight) return false;
            float dist = s.Distance;
            if (dist <= rules.NoticeRange) return true;
            if (dist > rules.DetectRange) return false;
            return MathF.Abs(MoveMath.DeltaAngle(s.Yaw, MoveMath.YawOf(s.Target - s.Self))) <= rules.ViewAngle * 0.5f + Epsilon;
        }

        /// <summary>공격이 닿는 자리 (첫 공격 줄): 평면 거리 HitRange 이내 · 앞 부채꼴 HitAngle 안 · 높이 차 HitRange 이내 (아주 가까우면 각도 무시).</summary>
        public static bool InHitArea(EnemyAiRules rules, Planar self, float yaw, Planar target, float heightDiff)
            => InHitArea(rules.HitRange, rules.HitAngle, self, yaw, target, heightDiff);

        /// <summary>3-4: 공격 줄 하나의 닿는 자리 (투사체 줄은 늘 false — 투사체가 맞힌다).</summary>
        public static bool InHitArea(in EnemyAttackSpec spec, Planar self, float yaw, Planar target, float heightDiff)
            => !spec.IsProjectile && InHitArea(spec.HitRange, spec.HitAngle, self, yaw, target, heightDiff);

        static bool InHitArea(float hitRange, float hitAngle, Planar self, float yaw, Planar target, float heightDiff)
        {
            if (MathF.Abs(heightDiff) > hitRange) return false;
            Planar to = target - self;
            float dist = to.Length;
            if (dist > hitRange + Epsilon) return false;
            if (dist <= Epsilon || hitAngle >= 360f) return true;
            return MathF.Abs(MoveMath.DeltaAngle(yaw, MoveMath.YawOf(to))) <= hitAngle * 0.5f + Epsilon;
        }
    }

    /// <summary>복귀한 까닭 (디버그, 끝에만 추가).</summary>
    public enum EnemyReturnReason
    {
        None,
        LostSight,
        TooFar,
        Leash,
        NoTarget,
        Forced,
    }
}
