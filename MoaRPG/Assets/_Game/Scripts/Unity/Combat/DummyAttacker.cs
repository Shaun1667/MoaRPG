using Moa.Core.Combat;
using Moa.Unity.Data;
using UnityEngine;

namespace Moa.Unity.Combat
{
    /// <summary>
    /// 공격 허수아비 (2-3, 저스트 회피 시험용): 같은 오브젝트의 TrainingDummy(체력 · 경직)에 붙어 일정 간격으로 예고한 뒤 주변을 친다.
    /// 규칙은 Core EnemyAttackCycle, 수치는 EnemyAttackTuning(BALANCE 9번). 그로기 · 쓰러짐이면 행동하지 않는다(예고도 취소).
    /// 공격이 닿는 스텝에 플레이어 발이 attackRange(평면 거리) 안이면 PlayerDefense.ReceiveEnemyAttack → 저스트 · 피함 · 맞음.
    /// 실행 순서를 플레이어(PlayerMotor) 뒤로 둔다 — 같은 스텝의 회피 상태를 본다.
    /// 예고 = 바닥 원(LineRenderer, 반지름 = attackRange)이 warnStart → warnEnd 색으로, 닿는 순간 hitColor로 잠깐. 색 · 두께는 Setup 표.
    /// GameRoot가 Bind로 대상(플레이어)을 준다.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(TrainingDummy))]
    public sealed class DummyAttacker : MonoBehaviour
    {
        const int RingSegments = 48;

        [SerializeField] EnemyAttackTuning tuning;
        [SerializeField] LineRenderer warningRing;
        [SerializeField] Color warnStartColor;
        [SerializeField] Color warnEndColor;
        [SerializeField] Color hitColor;
        [Tooltip("s the ring stays in hitColor after the hit lands (visual only)")]
        [SerializeField] float hitFlashTime;
        [Tooltip("m above the feet the ring is drawn")]
        [SerializeField] float ringHeight;

        TrainingDummy _dummy;
        EnemyAttackCycle _cycle;
        PlayerDefense _target;
        float _hitFlashLeft;
        bool _paused;

        public EnemyAttackTuning Tuning => tuning;
        public EnemyAttackCycle Cycle => _cycle;
        public TrainingDummy Dummy => _dummy;
        /// <summary>[상태 변경] 멈춤 (디버그) — 멈추면 예고도 취소.</summary>
        public bool Paused { get => _paused; set => _paused = value; }
        /// <summary>마지막 공격 결과 (닿은 공격 중 범위 밖이면 Missed = true).</summary>
        public DodgeJudgement LastJudgement { get; private set; }
        public bool LastMissed { get; private set; }
        public int MissCount { get; private set; }
        public int JudgedCount { get; private set; }

        void Awake()
        {
            _dummy = GetComponent<TrainingDummy>();
            if (tuning == null) { Debug.LogError("DummyAttacker: EnemyAttackTuning is not assigned"); enabled = false; return; }
            _cycle = new EnemyAttackCycle(tuning.ToRules());
            if (warningRing != null) { warningRing.positionCount = 0; warningRing.enabled = false; }
        }

        public void Bind(PlayerDefense target) => _target = target;

        /// <summary>[상태 변경] 주기를 처음으로.</summary>
        public void ResetCycle() => _cycle?.Reset();

        void FixedUpdate()
        {
            if (_cycle == null) return;
            float dt = Time.fixedDeltaTime;
            if (_hitFlashLeft > 0f) _hitFlashLeft -= dt;
            if (!_cycle.Advance(dt, _dummy.CanAct && !_paused)) return;

            _hitFlashLeft = hitFlashTime;
            if (_target == null || !InRange(_target.transform.position)) { LastMissed = true; MissCount++; return; }
            LastMissed = false;
            JudgedCount++;
            LastJudgement = _target.ReceiveEnemyAttack(transform.position);
        }

        /// <summary>발 사이 평면 거리가 attackRange 안인지 (높이 차는 허수아비 키 정도까지).</summary>
        public bool InRange(Vector3 feet)
        {
            Vector3 d = feet - transform.position;
            if (Mathf.Abs(d.y) > tuning.AttackRange) return false;
            d.y = 0f;
            return d.sqrMagnitude <= tuning.AttackRange * tuning.AttackRange;
        }

        void Update()
        {
            if (warningRing == null || _cycle == null) return;
            bool telegraph = _cycle.Phase == EnemyAttackPhase.Telegraph;
            bool flash = _hitFlashLeft > 0f;
            bool show = (telegraph || flash) && _dummy.Alive;
            if (show != warningRing.enabled)
            {
                warningRing.enabled = show;
                if (show) DrawRing();
            }
            if (!show) return;
            Color c = flash && !telegraph ? hitColor : Color.Lerp(warnStartColor, warnEndColor, _cycle.TelegraphRatio);
            warningRing.startColor = c;
            warningRing.endColor = c;
        }

        void DrawRing()
        {
            warningRing.positionCount = RingSegments;
            Vector3 center = transform.position + Vector3.up * ringHeight;
            for (int i = 0; i < RingSegments; i++)
            {
                float a = i * Mathf.PI * 2f / RingSegments;
                warningRing.SetPosition(i, center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * tuning.AttackRange);
            }
        }
    }
}
