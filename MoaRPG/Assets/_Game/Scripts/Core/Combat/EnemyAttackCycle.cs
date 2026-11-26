namespace Moa.Core.Combat
{
    /// <summary>예고 공격 주기 수치 (EnemyAttackTuning · EnemyTuning 공격 줄 → 넘긴다, BALANCE 9 · 11 · 16번).</summary>
    public readonly struct EnemyAttackRules
    {
        /// <summary>쉬는 시간 — 공격이 끝난 뒤(또는 처음) 다음 예고까지 (초).</summary>
        public readonly float Interval;
        /// <summary>예고 시간 — 이것이 끝나는 스텝에 공격이 나간다 (초).</summary>
        public readonly float Telegraph;
        /// <summary>공격 뒤 쉬는 자세 (초).</summary>
        public readonly float Recovery;
        /// <summary>3-4: 공격이 이어지는 시간 (초, 돌진처럼 여러 스텝에 걸쳐 닿는 공격). 0이면 예고가 끝나는 한 스텝에 닿는다(2-3 · 2-5 그대로).</summary>
        public readonly float Active;

        public EnemyAttackRules(float interval, float telegraph, float recovery) : this(interval, telegraph, recovery, 0f) { }

        public EnemyAttackRules(float interval, float telegraph, float recovery, float active)
        {
            Interval = interval;
            Telegraph = telegraph;
            Recovery = recovery;
            Active = active > 0f ? active : 0f;
        }

        /// <summary>한 주기 (초).</summary>
        public float Period => Interval + Telegraph + Active + Recovery;
    }

    /// <summary>예고 공격 단계 (끝에만 추가).</summary>
    public enum EnemyAttackPhase
    {
        Idle,
        Telegraph,
        Recovery,
        /// <summary>3-4: 공격이 이어지는 중 (Active가 0보다 큰 공격만 — 돌진).</summary>
        Active,
    }

    /// <summary>
    /// 일정 간격으로 예고한 뒤 공격하는 주기 (2-3, 저스트 회피 시험용 허수아비 — 2-5 적 AI의 출발점).
    /// Idle(Interval) → Telegraph(Telegraph, 끝나는 스텝에 공격이 나간다) → [Active(Active, 3-4 돌진)] → Recovery → Idle …
    /// 행동할 수 없으면(그로기 · 쓰러짐) 처음(Idle 0초)으로 돌아간다 — 예고하던 공격도 취소.
    /// 고정 스텝마다 <see cref="Advance"/>. 범위 검사 · 그리기는 Unity 쪽(DummyAttacker).
    /// </summary>
    public sealed class EnemyAttackCycle
    {
        const float Epsilon = 1e-5f;
        readonly EnemyAttackRules _rules;

        public EnemyAttackCycle(EnemyAttackRules rules) { _rules = rules; }

        public EnemyAttackRules Rules => _rules;
        public EnemyAttackPhase Phase { get; private set; }
        /// <summary>지금 단계가 시작된 뒤 지난 초.</summary>
        public float Elapsed { get; private set; }
        /// <summary>예고 진행 0 ~ 1 (예고 중이 아니면 0).</summary>
        public float TelegraphRatio => Phase == EnemyAttackPhase.Telegraph && _rules.Telegraph > 0f ? Elapsed / _rules.Telegraph : 0f;
        /// <summary>공격이 닿기까지 남은 초 (예고 중이 아니면 0).</summary>
        public float TimeToHit => Phase == EnemyAttackPhase.Telegraph ? _rules.Telegraph - Elapsed : 0f;
        /// <summary>닿은 공격 수 · 취소된 수 (디버그 · 확인용).</summary>
        public int AttackCount { get; private set; }
        public int CancelCount { get; private set; }

        /// <summary>한 고정 스텝. 이번 스텝에 공격이 나가면(예고가 끝나면) true. canAct가 false면 처음으로 돌아가고 false.</summary>
        public bool Advance(float dt, bool canAct)
        {
            if (!canAct)
            {
                if (Phase != EnemyAttackPhase.Idle) CancelCount++;
                Phase = EnemyAttackPhase.Idle;
                Elapsed = 0f;
                return false;
            }
            Elapsed += dt;
            switch (Phase)
            {
                case EnemyAttackPhase.Idle:
                    if (Elapsed >= _rules.Interval - Epsilon) { Phase = EnemyAttackPhase.Telegraph; Elapsed = 0f; }
                    return false;
                case EnemyAttackPhase.Telegraph:
                    if (Elapsed < _rules.Telegraph - Epsilon) return false;
                    Phase = _rules.Active > 0f ? EnemyAttackPhase.Active : EnemyAttackPhase.Recovery;
                    Elapsed = 0f;
                    AttackCount++;
                    return true;
                case EnemyAttackPhase.Active:
                    if (Elapsed >= _rules.Active - Epsilon) { Phase = EnemyAttackPhase.Recovery; Elapsed = 0f; }
                    return false;
                default:
                    if (Elapsed >= _rules.Recovery - Epsilon) { Phase = EnemyAttackPhase.Idle; Elapsed = 0f; }
                    return false;
            }
        }

        /// <summary>[상태 변경] 처음으로 (Idle 0초). 수는 그대로.</summary>
        public void Reset()
        {
            Phase = EnemyAttackPhase.Idle;
            Elapsed = 0f;
        }
    }
}
