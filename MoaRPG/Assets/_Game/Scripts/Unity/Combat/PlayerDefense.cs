using System;
using Moa.Core.Combat;
using Moa.Unity.Data;
using Moa.Unity.Player;
using UnityEngine;

namespace Moa.Unity.Combat
{
    /// <summary>
    /// 플레이어가 적 공격을 받는 창구 (2-3) — 지금은 저스트 회피 판정만 (플레이어 체력 · 피해는 파티 · 적 AI 때).
    /// 규칙은 Core JustDodge, 수치는 CombatTuning(BALANCE 7번). 적이 공격이 닿는 스텝에 <see cref="ReceiveEnemyAttack"/>을 부른다
    /// (회피 상태는 PlayerMotor의 Dodge — 적 쪽은 실행 순서를 플레이어 뒤로 둔다).
    /// 저스트면 슬로모션: 실제 시간으로 재며 Time.timeScale을 바꾸고, 끝나면 1로 돌린다(이 컴포넌트가 바꾼 때만). 반격 보너스는 게임 시간(고정 스텝).
    /// GameRoot가 Bind로 연결한다.
    /// </summary>
    [RequireComponent(typeof(PlayerMotor))]
    public sealed class PlayerDefense : MonoBehaviour
    {
        [SerializeField] CombatTuning tuning;

        PlayerMotor _motor;
        PlayerVitals _vitals; // 3-1: Hit이면 피해 (없으면 맞은 수만)
        JustDodge _just;
        bool _scaling; // 이 컴포넌트가 Time.timeScale을 바꿔 둔 상태

        public CombatTuning Tuning => tuning;
        public JustDodge Just => _just;
        public bool Bound => _just != null && _motor != null;
        /// <summary>지금 플레이어 공격의 경직 배율 (반격 보너스 중이면 1보다 큼).</summary>
        public float PoiseMultiplier => _just != null ? _just.PoiseMultiplier : 1f;
        /// <summary>지금 무적인지 (회피 · 궁극기) — 2-5: 예고 중 맞는 자리에 있다 회피로 빠져나간 공격도 판정을 받는다.</summary>
        public bool Invulnerable => _motor != null && (_motor.Invulnerable || _motor.BurstInvulnerable);
        /// <summary>마지막으로 공격이 온 자리 · 그때 플레이어 발 (디버그 · 확인용).</summary>
        public Vector3 LastAttackFrom { get; private set; }
        public Vector3 LastJudgedAt { get; private set; }

        /// <summary>적 공격이 닿을 때마다 (판정, 플레이어 발 위치) — 화면 글 · 연출용.</summary>
        public event Action<DodgeJudgement, Vector3> Judged;

        public void Bind(PlayerMotor motor)
        {
            _motor = motor;
            _vitals = GetComponent<PlayerVitals>();
            if (tuning == null) { Debug.LogError("PlayerDefense: CombatTuning is not assigned"); enabled = false; return; }
            _just = new JustDodge(tuning.ToJustDodgeRules());
        }

        /// <summary>적 공격이 이 스텝에 플레이어에게 닿았다 (범위 검사는 적 쪽). 판정을 돌려준다. 피해 없음 (2-3 공격 허수아비 — 저스트 연습용).</summary>
        public DodgeJudgement ReceiveEnemyAttack(Vector3 from) => ReceiveEnemyAttack(from, 0f, 0f);

        /// <summary>마지막으로 받은 피해 (3-1, 디버그 · 확인용).</summary>
        public PlayerHit LastHit { get; private set; } = PlayerHit.None(0);

        /// <summary>
        /// 적 공격이 닿았다 (3-1): 판정이 Hit이면 필드 캐릭터가 attack × multiplier 피해(방어 반영, PlayerVitals). 저스트 · 피함이면 피해 없음.
        /// 전멸 연출 중에는 판정하지 않고 Avoided.
        /// </summary>
        public DodgeJudgement ReceiveEnemyAttack(Vector3 from, float attack, float multiplier)
        {
            if (!Bound) return DodgeJudgement.Hit;
            if (_vitals != null && _vitals.Wiping) return DodgeJudgement.Avoided;
            bool invulnerable = _motor.Invulnerable;
            float elapsed = _motor.Dodge != null ? _motor.Dodge.Elapsed : 0f;
            // 2-4: 궁극기 무적이면 피하기만 (저스트 보상은 회피 무적일 때만)
            DodgeJudgement j = !invulnerable && _motor.BurstInvulnerable
                ? _just.Judge(true, float.MaxValue)
                : _just.Judge(invulnerable, elapsed);
            LastAttackFrom = from;
            LastJudgedAt = transform.position;
            if (j == DodgeJudgement.Just) ApplyTimeScale();
            if (j == DodgeJudgement.Hit && _vitals != null && attack > 0f && multiplier > 0f) LastHit = _vitals.TakeEnemyHit(from, attack, multiplier);
            Judged?.Invoke(j, transform.position);
            return j;
        }

        /// <summary>[상태 변경] 슬로모션 · 반격 보너스를 끈다.</summary>
        public void Cancel()
        {
            _just?.Cancel();
            ApplyTimeScale();
        }

        void FixedUpdate()
        {
            if (_just != null) _just.Advance(Time.fixedDeltaTime);
        }

        void Update()
        {
            if (_just == null) return;
            _just.AdvanceReal(Time.unscaledDeltaTime);
            ApplyTimeScale();
        }

        void ApplyTimeScale()
        {
            if (_just == null) return;
            if (_just.Slowing)
            {
                Time.timeScale = _just.TimeScale;
                _scaling = true;
            }
            else if (_scaling)
            {
                Time.timeScale = 1f;
                _scaling = false;
            }
        }

        void OnDisable()
        {
            if (_scaling) { Time.timeScale = 1f; _scaling = false; }
        }
    }
}
