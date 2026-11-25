namespace Moa.Core.Combat
{
    /// <summary>저스트 회피 수치 (CombatTuning → 넘긴다, BALANCE 7번).</summary>
    public readonly struct JustDodgeRules
    {
        /// <summary>적 공격이 닿는 순간, 회피가 시작된 지 이 시간 안이면 저스트 (초). 회피 무적 시간보다 짧게.</summary>
        public readonly float Window;
        /// <summary>성공하면 거는 슬로모션 배속 (Time.timeScale).</summary>
        public readonly float SlowScale;
        /// <summary>슬로모션 길이 (실제 시간, 초).</summary>
        public readonly float SlowDuration;
        /// <summary>성공 뒤 반격 보너스가 이어지는 시간 (게임 시간, 초).</summary>
        public readonly float CounterTime;
        /// <summary>반격 보너스 동안 플레이어 공격의 경직 수치 배율.</summary>
        public readonly float CounterPoiseMultiplier;

        public JustDodgeRules(float window, float slowScale, float slowDuration, float counterTime, float counterPoiseMultiplier)
        {
            Window = window;
            SlowScale = slowScale;
            SlowDuration = slowDuration;
            CounterTime = counterTime;
            CounterPoiseMultiplier = counterPoiseMultiplier;
        }
    }

    /// <summary>적 공격이 닿는 순간의 판정 (끝에만 추가).</summary>
    public enum DodgeJudgement
    {
        /// <summary>맞음 (회피 무적이 아님).</summary>
        Hit,
        /// <summary>회피 무적으로 피했지만 저스트 창 밖 (보상 없음).</summary>
        Avoided,
        /// <summary>저스트 회피 — 슬로모션 + 반격 보너스.</summary>
        Just,
    }

    /// <summary>
    /// 저스트 회피 (2-3). 적 공격이 닿는 순간(<see cref="Judge"/>), 플레이어가 회피 무적 중이고 회피가 시작된 지 Window 안이면 성공:
    /// SlowDuration(실제 시간) 동안 <see cref="TimeScale"/> = SlowScale, CounterTime(게임 시간) 동안 플레이어 공격의 경직 × CounterPoiseMultiplier.
    /// 무적이지만 창 밖이면 피하기만(Avoided), 무적이 아니면 맞음(Hit). 피해 · 넉백은 플레이어 체력이 생길 때.
    /// 게임 시간은 고정 스텝마다 <see cref="Advance"/>, 슬로모션은 실제 시간으로 <see cref="AdvanceReal"/> (Unity 쪽이 Time.timeScale에 넣는다).
    /// </summary>
    public sealed class JustDodge
    {
        const float Epsilon = 1e-5f;
        readonly JustDodgeRules _rules;

        public JustDodge(JustDodgeRules rules) { _rules = rules; }

        public JustDodgeRules Rules => _rules;
        /// <summary>슬로모션이 끝나기까지 남은 실제 초.</summary>
        public float SlowLeft { get; private set; }
        /// <summary>반격 보너스가 끝나기까지 남은 게임 초.</summary>
        public float CounterLeft { get; private set; }
        public bool Slowing => SlowLeft > 0f;
        public bool CounterActive => CounterLeft > 0f;
        /// <summary>지금 걸어야 할 배속 (슬로모션 중이면 SlowScale, 아니면 1).</summary>
        public float TimeScale => Slowing ? _rules.SlowScale : 1f;
        /// <summary>지금 플레이어 공격의 경직 배율 (반격 보너스 중이면 CounterPoiseMultiplier, 아니면 1).</summary>
        public float PoiseMultiplier => CounterActive ? _rules.CounterPoiseMultiplier : 1f;
        public DodgeJudgement Last { get; private set; }
        /// <summary>판정 수 (디버그 · 확인용).</summary>
        public int JustCount { get; private set; }
        public int AvoidCount { get; private set; }
        public int HitCount { get; private set; }

        /// <summary>판정만 (상태 없음): 무적이 아니면 Hit, 회피가 시작된 지 window 안이면 Just, 아니면 Avoided.</summary>
        public static DodgeJudgement Classify(bool invulnerable, float dodgeElapsed, float window)
        {
            if (!invulnerable) return DodgeJudgement.Hit;
            return dodgeElapsed <= window + Epsilon ? DodgeJudgement.Just : DodgeJudgement.Avoided;
        }

        /// <summary>적 공격이 닿는 순간에 부른다. Just면 슬로모션 · 반격 보너스를 (다시) 시작한다.</summary>
        public DodgeJudgement Judge(bool invulnerable, float dodgeElapsed)
        {
            DodgeJudgement j = Classify(invulnerable, dodgeElapsed, _rules.Window);
            Last = j;
            switch (j)
            {
                case DodgeJudgement.Just:
                    JustCount++;
                    SlowLeft = _rules.SlowDuration;
                    CounterLeft = _rules.CounterTime;
                    break;
                case DodgeJudgement.Avoided: AvoidCount++; break;
                default: HitCount++; break;
            }
            return j;
        }

        /// <summary>한 고정 스텝 (게임 시간): 반격 보너스.</summary>
        public void Advance(float dt)
        {
            if (CounterLeft <= 0f) return;
            CounterLeft -= dt;
            if (CounterLeft <= Epsilon) CounterLeft = 0f;
        }

        /// <summary>실제 시간 진행 (슬로모션은 실제 시간으로 잰다 — 느려진 게임 시간으로 재면 길어진다).</summary>
        public void AdvanceReal(float realDt)
        {
            if (SlowLeft <= 0f) return;
            SlowLeft -= realDt;
            if (SlowLeft <= Epsilon) SlowLeft = 0f;
        }

        /// <summary>[상태 변경] 슬로모션 · 보너스를 끈다 (순간이동 · 판 다시 시작). 수는 그대로.</summary>
        public void Cancel()
        {
            SlowLeft = 0f;
            CounterLeft = 0f;
        }
    }
}
