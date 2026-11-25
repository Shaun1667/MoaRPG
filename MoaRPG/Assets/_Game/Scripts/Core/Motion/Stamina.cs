namespace Moa.Core.Motion
{
    /// <summary>스태미나 수치 (StaminaTuning 에셋 → PlayerMotor가 넘긴다, BALANCE 3번).</summary>
    public readonly struct StaminaRules
    {
        /// <summary>최대치.</summary>
        public readonly float Max;
        /// <summary>초당 회복량.</summary>
        public readonly float RegenPerSecond;
        /// <summary>마지막으로 쓴 뒤 회복이 시작되기까지 (초).</summary>
        public readonly float RegenDelay;
        /// <summary>바닥나서 지친 뒤 이만큼 차야 지침이 풀린다.</summary>
        public readonly float ExhaustRecover;

        public StaminaRules(float max, float regenPerSecond, float regenDelay, float exhaustRecover)
        {
            Max = max;
            RegenPerSecond = regenPerSecond;
            RegenDelay = regenDelay;
            ExhaustRecover = exhaustRecover;
        }
    }

    /// <summary>
    /// 스태미나 순수 규칙 (1-2) — 이동(질주, 1-3 등반 · 활공)과 회피가 이것 하나를 같이 쓴다 (GDD 4장).
    /// 고정 스텝마다: 쓸 것을 먼저 쓰고(<see cref="Drain"/> · <see cref="TrySpend"/>) 마지막에 <see cref="Tick"/>.
    /// 쓴 스텝에는 회복하지 않고, 마지막으로 쓴 뒤 RegenDelay초가 지나면 초당 RegenPerSecond씩 찬다.
    /// 0이 되면 지침(Exhausted) — 계속 쓰기(질주)는 ExhaustRecover까지 찰 때까지 안 된다. 한 번에 쓰기(회피)는 양만 있으면 된다.
    /// </summary>
    public sealed class Stamina
    {
        readonly StaminaRules _rules;
        bool _spentThisStep;

        public Stamina(StaminaRules rules)
        {
            _rules = rules;
            Current = rules.Max;
            SinceSpend = rules.RegenDelay;
        }

        public StaminaRules Rules => _rules;
        public float Max => _rules.Max;
        public float Current { get; private set; }
        public float Ratio => _rules.Max > 0f ? Current / _rules.Max : 0f;
        public bool IsFull => Current >= _rules.Max;

        /// <summary>바닥난 뒤 ExhaustRecover까지 차지 않은 상태.</summary>
        public bool Exhausted { get; private set; }

        /// <summary>마지막으로 쓴 뒤 지난 시간 (초).</summary>
        public float SinceSpend { get; private set; }

        /// <summary>회복 지연이 끝나 차는 중.</summary>
        public bool Regenerating => !IsFull && SinceSpend >= _rules.RegenDelay;

        /// <summary>회복 시작까지 남은 시간 (초, 0 = 차는 중이거나 가득).</summary>
        public float RegenWait => IsFull || SinceSpend >= _rules.RegenDelay ? 0f : _rules.RegenDelay - SinceSpend;

        /// <summary>디버그: 켜면 쓰기가 언제나 되고 줄지 않는다.</summary>
        public bool Infinite { get; set; }

        /// <summary>계속 쓰기(질주)를 시작 · 유지할 수 있는지.</summary>
        public bool CanDrain => Infinite || (!Exhausted && Current > 0f);

        /// <summary>한 번에 cost를 쓸 수 있는지.</summary>
        public bool CanAfford(float cost) => Infinite || Current >= cost;

        /// <summary>
        /// 계속 쓰기: 초당 perSecond를 dt만큼. 쓸 수 없으면 false (쓰지 않음).
        /// 이번에 바닥나면 쓰고 나서 false (다음 스텝부터 못 씀).
        /// </summary>
        public bool Drain(float perSecond, float dt)
        {
            if (Infinite) return true;
            if (!CanDrain) return false;
            Spend(perSecond * dt);
            return !Exhausted;
        }

        /// <summary>한 번에 cost를 쓴다. 모자라면 쓰지 않고 false.</summary>
        public bool TrySpend(float cost)
        {
            if (Infinite) return true;
            if (Current < cost) return false;
            Spend(cost);
            return true;
        }

        void Spend(float amount)
        {
            if (amount <= 0f) return;
            Current -= amount;
            if (Current <= 0f) { Current = 0f; Exhausted = true; }
            _spentThisStep = true;
            SinceSpend = 0f;
        }

        /// <summary>고정 스텝 끝에 한 번: 회복 지연 · 회복 · 지침 풀림.</summary>
        public void Tick(float dt)
        {
            if (_spentThisStep) { _spentThisStep = false; return; }
            float before = SinceSpend;
            SinceSpend += dt;
            if (Current < _rules.Max && SinceSpend > _rules.RegenDelay)
            {
                // 지연이 이 스텝 안에서 끝났으면 끝난 뒤의 시간만큼만 찬다
                float regenTime = before >= _rules.RegenDelay ? dt : SinceSpend - _rules.RegenDelay;
                Current += _rules.RegenPerSecond * regenTime;
                if (Current > _rules.Max) Current = _rules.Max;
            }
            if (Exhausted && Current >= _rules.ExhaustRecover) Exhausted = false;
        }

        /// <summary>[상태 변경] 가득 채우기 (지침도 풀림).</summary>
        public void Fill()
        {
            Current = _rules.Max;
            Exhausted = false;
            SinceSpend = _rules.RegenDelay;
            _spentThisStep = false;
        }

        /// <summary>[상태 변경] 값을 정한다 (0 이하 = 바닥 · 지침). 회복 지연은 처음부터.</summary>
        public void Set(float value)
        {
            Current = value < 0f ? 0f : (value > _rules.Max ? _rules.Max : value);
            if (Current <= 0f) Exhausted = true;
            else if (Current >= _rules.ExhaustRecover) Exhausted = false;
            SinceSpend = 0f;
            _spentThisStep = false;
        }
    }
}
