using System;

namespace Moa.Core.Combat
{
    /// <summary>경직 게이지 수치 (적 표 DummyTuning → 넘긴다, BALANCE 8번).</summary>
    public readonly struct PoiseRules
    {
        /// <summary>게이지 최대 — 쌓인 경직이 이만큼이면 그로기.</summary>
        public readonly float Max;
        /// <summary>그로기 시간 (초).</summary>
        public readonly float GroggyDuration;
        /// <summary>그로기 중 받는 피해 배율 (1.3 = +30%).</summary>
        public readonly float GroggyDamageTaken;
        /// <summary>마지막으로 맞은 뒤 게이지가 줄기 시작하기까지 (초).</summary>
        public readonly float RegenDelay;
        /// <summary>그 뒤 1초에 줄어드는 양.</summary>
        public readonly float RegenPerSecond;

        public PoiseRules(float max, float groggyDuration, float groggyDamageTaken, float regenDelay, float regenPerSecond)
        {
            Max = max;
            GroggyDuration = groggyDuration;
            GroggyDamageTaken = groggyDamageTaken;
            RegenDelay = regenDelay;
            RegenPerSecond = regenPerSecond;
        }
    }

    /// <summary>
    /// 경직 게이지 · 그로기 (2-3). 공격마다 경직 수치가 쌓이고(<see cref="Apply"/>), Max에 닿으면 그로기 —
    /// GroggyDuration 동안 행동 불가(부르는 쪽이 <see cref="Groggy"/>를 본다) · 받는 피해 × GroggyDamageTaken.
    /// 그로기 중에는 쌓이지 않고, 끝나면 게이지는 0. 맞지 않으면 RegenDelay 뒤부터 RegenPerSecond로 줄어든다.
    /// 고정 스텝마다 <see cref="Advance"/> (핵심 설계 규칙 2). 적 · 허수아비가 같은 것을 쓴다.
    /// </summary>
    public sealed class Poise
    {
        const float Epsilon = 1e-5f;
        readonly PoiseRules _rules;

        public Poise(PoiseRules rules)
        {
            if (rules.Max <= 0f) throw new ArgumentOutOfRangeException(nameof(rules), "poise max must be > 0");
            _rules = rules;
        }

        public PoiseRules Rules => _rules;
        /// <summary>쌓인 경직 (0 ~ Max).</summary>
        public float Current { get; private set; }
        public float Ratio => Current / _rules.Max;
        public bool Groggy { get; private set; }
        /// <summary>그로기가 끝나기까지 남은 초.</summary>
        public float GroggyLeft { get; private set; }
        /// <summary>게이지가 줄기 시작하기까지 남은 초.</summary>
        public float RegenWait { get; private set; }
        /// <summary>지금 받는 피해 배율 (그로기면 GroggyDamageTaken, 아니면 1).</summary>
        public float DamageTakenMultiplier => Groggy ? _rules.GroggyDamageTaken : 1f;
        /// <summary>그로기가 된 수 · 풀린 수 (디버그 · 확인용).</summary>
        public int BreakCount { get; private set; }
        public int RecoverCount { get; private set; }

        /// <summary>그로기가 됐을 때.</summary>
        public event Action Broken;
        /// <summary>그로기가 풀렸을 때.</summary>
        public event Action Recovered;

        /// <summary>경직을 쌓는다. 이번에 그로기가 됐으면 true. 그로기 중이거나 0 이하면 아무것도 안 함.</summary>
        public bool Apply(float amount)
        {
            if (Groggy || amount <= 0f) return false;
            Current += amount;
            RegenWait = _rules.RegenDelay;
            if (Current < _rules.Max - Epsilon) return false;
            Current = _rules.Max;
            Groggy = true;
            GroggyLeft = _rules.GroggyDuration;
            BreakCount++;
            Broken?.Invoke();
            return true;
        }

        /// <summary>한 고정 스텝: 그로기 시간 · 게이지 회복.</summary>
        public void Advance(float dt)
        {
            if (Groggy)
            {
                GroggyLeft -= dt;
                if (GroggyLeft > Epsilon) return;
                GroggyLeft = 0f;
                Groggy = false;
                Current = 0f;
                RegenWait = 0f;
                RecoverCount++;
                Recovered?.Invoke();
                return;
            }
            if (Current <= 0f) return;
            if (RegenWait > 0f)
            {
                float used = RegenWait < dt ? RegenWait : dt;
                RegenWait -= used;
                if (RegenWait <= Epsilon) RegenWait = 0f;
                dt -= used;
                if (dt <= 0f) return;
            }
            Current -= _rules.RegenPerSecond * dt;
            if (Current <= Epsilon) Current = 0f;
        }

        /// <summary>[상태 변경] 처음으로 (되살아남 · 디버그). 수는 그대로.</summary>
        public void Reset()
        {
            Current = 0f;
            Groggy = false;
            GroggyLeft = 0f;
            RegenWait = 0f;
        }
    }
}
