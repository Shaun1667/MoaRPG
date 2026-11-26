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

    /// <summary>적 AI 수치 (EnemyTuning → 넘긴다, BALANCE 11번). 거리는 발 사이 평면 거리(m), 각도 도, 시간 초, 속력 m/s.</summary>
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
        /// <summary>이 안이면 멈춰 공격을 시작.</summary>
        public readonly float AttackRange;
        /// <summary>공격이 닿는 거리 (닿는 스텝에 이 안 + 앞 부채꼴 + 높이 차 이 안).</summary>
        public readonly float HitRange;
        /// <summary>공격이 닿는 앞 부채꼴 전체 각도.</summary>
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
        /// <summary>맞으면 멈추는 시간 (예고 중에는 안 멈춤).</summary>
        public readonly float HitStunTime;
        /// <summary>쓰러진 뒤 다시 나기까지.</summary>
        public readonly float RespawnDelay;
        /// <summary>공격 주기: Interval = 사거리에 들어와 예고까지 · 공격 사이 쉬기, Telegraph = 예고, Recovery = 공격 뒤 자세.</summary>
        public readonly EnemyAttackRules Attack;

        public EnemyAiRules(float detectRange, float viewAngle, float noticeRange, float loseRange, float loseSightTime, float leashRange,
            float attackRange, float hitRange, float hitAngle, float patrolSpeed, float chaseSpeed, float returnSpeed, float turnSpeed,
            float patrolWait, float arriveDistance, float hitStunTime, float respawnDelay, EnemyAttackRules attack)
        {
            DetectRange = detectRange;
            ViewAngle = viewAngle;
            NoticeRange = noticeRange;
            LoseRange = loseRange;
            LoseSightTime = loseSightTime;
            LeashRange = leashRange;
            AttackRange = attackRange;
            HitRange = hitRange;
            HitAngle = hitAngle;
            PatrolSpeed = patrolSpeed;
            ChaseSpeed = chaseSpeed;
            ReturnSpeed = returnSpeed;
            TurnSpeed = turnSpeed;
            PatrolWait = patrolWait;
            ArriveDistance = arriveDistance;
            HitStunTime = hitStunTime;
            RespawnDelay = respawnDelay;
            Attack = attack;
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

    /// <summary>한 고정 스텝의 결과 — Unity가 이대로 움직이고 돌리고, 닿으면 PlayerDefense에 넘긴다.</summary>
    public readonly struct EnemyTick
    {
        public readonly EnemyState State;
        /// <summary>이번 스텝 수평 속도 (m/s, 방향 × 속력).</summary>
        public readonly Planar Velocity;
        /// <summary>이번 스텝 뒤 몸 방향 (도).</summary>
        public readonly float Yaw;
        /// <summary>이번 스텝에 공격이 닿는 순간인지.</summary>
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

        public EnemyTick(EnemyState state, Planar velocity, float yaw, bool attackLanded, bool attackHits, bool respawned, bool healed)
            : this(state, velocity, yaw, attackLanded, attackHits, false, respawned, healed) { }

        public EnemyTick(EnemyState state, Planar velocity, float yaw, bool attackLanded, bool attackHits, bool attackGrazed, bool respawned, bool healed)
        {
            AttackGrazed = attackGrazed;
            State = state;
            Velocity = velocity;
            Yaw = yaw;
            AttackLanded = attackLanded;
            AttackHits = attackHits;
            Respawned = respawned;
            Healed = healed;
        }
    }

    /// <summary>
    /// 근접 적 AI 상태 기계 (2-5). 엔진을 모르고, 고정 스텝마다 Unity(EnemyAgent)가 잰 <see cref="EnemySense"/>로 <see cref="Step"/>을 부른다.
    /// Idle(순찰이 있으면 PatrolWait 뒤) ↔ Patrol → 알아채면 Chase → AttackRange 안이면 Attack(EnemyAttackCycle: 쉬기 → 예고 → 닿음 → 자세)
    /// → 시야선이 LoseSightTime 막히거나 LoseRange 밖이거나 집에서 LeashRange 밖이면 Return(도착하면 체력 가득 · Idle).
    /// 맞으면(<see cref="NotifyHit"/>) 알아채고 HitStunTime 동안 Stagger — 예고 중에는 멈추지 않는다(예고한 공격은 나간다).
    /// 그로기면 Groggy(행동 없음, 공격 주기 처음으로), 풀리면 Chase. 쓰러지면(<see cref="Die"/>) Dead → RespawnDelay 뒤 집에서 Idle.
    /// 예고가 시작되면 몸을 돌리지 않는다(옆으로 피할 수 있게). 사거리를 벗어나도 예고 · 자세는 끝까지 하고, 쉬는 동안 벗어나면 다시 쫓는다.
    /// </summary>
    public sealed class EnemyBrain
    {
        const float Epsilon = 1e-5f;
        readonly EnemyAiRules _rules;
        readonly EnemyAttackCycle _cycle;
        readonly Planar _patrolOffset;
        bool _patrolOut; // 순찰: 지금 순찰 지점 쪽으로 가는 중
        bool _telegraphInArea; // 이번 예고 중 플레이어가 맞는 자리에 있었던 적이 있는지
        float _lostSight;

        public EnemyBrain(EnemyAiRules rules, Planar home, Planar patrolOffset)
        {
            _rules = rules;
            _cycle = new EnemyAttackCycle(rules.Attack);
            Home = home;
            _patrolOffset = patrolOffset;
        }

        public EnemyAiRules Rules => _rules;
        /// <summary>공격 주기 (Core, 디버그 · 그리기용).</summary>
        public EnemyAttackCycle Cycle => _cycle;
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
                _cycle.Advance(dt, false);
                return Stand(yaw);
            }
            if (State == EnemyState.Groggy) Enter(EnemyState.Chase); // 풀리면 다시 쫓는다
            if (Paused)
            {
                _cycle.Advance(dt, false);
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
            if (dist <= _rules.AttackRange)
            {
                Enter(EnemyState.Attack);
                return AttackStep(dt, s, dist, homeDist);
            }
            return MoveTo(dt, s, s.Target, _rules.ChaseSpeed);
        }

        EnemyTick AttackStep(float dt, in EnemySense s, float dist, float homeDist)
        {
            float yaw = s.Yaw;
            // 쉬는 동안(예고 전)만 몸을 돌리고, 사거리를 벗어나면 다시 쫓는다
            if (_cycle.Phase == EnemyAttackPhase.Idle)
            {
                if (!s.HasTarget || dist > _rules.AttackRange)
                {
                    _cycle.Reset();
                    Enter(EnemyState.Chase);
                    return ChaseStep(dt, s, dist, homeDist);
                }
                yaw = Face(s, s.Target, dt);
            }
            bool wasTelegraph = _cycle.Phase == EnemyAttackPhase.Telegraph;
            bool landed = _cycle.Advance(dt, true);
            bool inArea = s.HasTarget && InHitArea(_rules, s.Self, yaw, s.Target, s.HeightDiff);
            if (_cycle.Phase == EnemyAttackPhase.Telegraph && !wasTelegraph) _telegraphInArea = false; // 예고 시작
            if (_cycle.Phase == EnemyAttackPhase.Telegraph || landed) _telegraphInArea |= inArea;
            bool hits = false, grazed = false;
            if (landed)
            {
                AttackCount++;
                hits = inArea;
                grazed = !hits && _telegraphInArea;
                if (hits) AttackHitCount++;
                else AttackMissCount++;
            }
            return new EnemyTick(State, Planar.Zero, yaw, landed, hits, grazed, false, false);
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
            float len = to.Length;
            if (len <= Epsilon) return Stand(s.Yaw);
            float yaw = Face(s, target, dt);
            // 이번 스텝에 지나치지 않게
            float v = speed * dt > len ? len / dt : speed;
            return new EnemyTick(State, to * (v / len), yaw, false, false, false, false);
        }

        float Face(in EnemySense s, Planar target, float dt)
        {
            Planar to = target - s.Self;
            if (to.Length <= Epsilon) return s.Yaw;
            return MoveMath.TurnTowards(s.Yaw, MoveMath.YawOf(to), _rules.TurnSpeed, dt);
        }

        EnemyTick Stand(float yaw) => new EnemyTick(State, Planar.Zero, yaw, false, false, false, false);

        void Detect()
        {
            DetectCount++;
        }

        void Enter(EnemyState next)
        {
            EnemyState prev = State;
            // 공격 주기는 공격 상태로 들어올 때마다 처음부터 (나갈 때 예고 중이면 취소로 셈다)
            if (next != EnemyState.Attack || prev != EnemyState.Attack) _cycle.Advance(0f, false);
            if (next == EnemyState.Chase && prev != EnemyState.Chase) _lostSight = 0f;
            State = next;
            StateTime = 0f;
            if (prev != next) StateChanged?.Invoke(prev, next);
        }

        /// <summary>
        /// 플레이어에게 맞았다 (피해가 들어간 스텝, 살아 있을 때). 알아채고 HitStunTime 동안 멈춘다 — 경직했으면 true.
        /// 예고 중이면 멈추지 않는다(ArmorCount). 그로기 · 쓰러짐이면 아무것도 안 함.
        /// </summary>
        public bool NotifyHit()
        {
            if (State == EnemyState.Dead || State == EnemyState.Groggy) return false;
            if (State == EnemyState.Attack && _cycle.Phase == EnemyAttackPhase.Telegraph) { ArmorCount++; return false; }
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

        /// <summary>[상태 변경] 바로 다시 난다 (집에서 Idle). Unity는 집으로 옮기고 체력 · 경직을 채운다.</summary>
        public void Respawn()
        {
            bool wasDead = State == EnemyState.Dead;
            _patrolOut = false;
            _lostSight = 0f;
            Enter(EnemyState.Idle);
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

        /// <summary>공격이 닿는 자리: 평면 거리 HitRange 이내 · 앞 부채꼴 HitAngle 안 · 높이 차 HitRange 이내 (아주 가까우면 각도 무시).</summary>
        public static bool InHitArea(EnemyAiRules rules, Planar self, float yaw, Planar target, float heightDiff)
        {
            if (MathF.Abs(heightDiff) > rules.HitRange) return false;
            Planar to = target - self;
            float dist = to.Length;
            if (dist > rules.HitRange + Epsilon) return false;
            if (dist <= Epsilon || rules.HitAngle >= 360f) return true;
            return MathF.Abs(MoveMath.DeltaAngle(yaw, MoveMath.YawOf(to))) <= rules.HitAngle * 0.5f + Epsilon;
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
