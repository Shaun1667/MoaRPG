using Moa.Core.Motion;
using NUnit.Framework;

namespace Moa.Tests
{
    /// <summary>1-2 스태미나 순수 규칙 (Moa.Core.Motion.Stamina). 값은 BALANCE 3번 시작 값.</summary>
    public class StaminaTests
    {
        const float Eps = 1e-3f;
        const float Dt = 0.02f;
        static readonly StaminaRules Rules = new StaminaRules(240f, 25f, 1f, 60f);

        static void Run(Stamina s, float seconds, float drainPerSecond = 0f)
        {
            int steps = (int)System.Math.Round(seconds / Dt);
            for (int i = 0; i < steps; i++)
            {
                if (drainPerSecond > 0f) s.Drain(drainPerSecond, Dt);
                s.Tick(Dt);
            }
        }

        [Test]
        public void StartsFull_NotExhausted()
        {
            var s = new Stamina(Rules);
            Assert.That(s.Current, Is.EqualTo(240f).Within(Eps));
            Assert.That(s.IsFull, Is.True);
            Assert.That(s.Exhausted, Is.False);
            Assert.That(s.CanDrain, Is.True);
        }

        [Test]
        public void Sprint_Drains18PerSecond()
        {
            var s = new Stamina(Rules);
            Run(s, 2f, 18f);
            Assert.That(s.Current, Is.EqualTo(240f - 36f).Within(Eps));
        }

        [Test]
        public void NoRegen_DuringDelay_ThenRegen25PerSecond()
        {
            var s = new Stamina(Rules);
            Run(s, 1f, 18f); // 222
            float after = s.Current;
            Run(s, 1f); // 회복 지연 1초
            Assert.That(s.Current, Is.EqualTo(after).Within(Eps));
            Run(s, 0.4f); // 최대치(240)에 닿기 전 구간
            Assert.That(s.Current, Is.EqualTo(after + 10f).Within(0.01f));
        }

        [Test]
        public void SpendingResetsRegenDelay()
        {
            var s = new Stamina(Rules);
            s.Set(100f);
            Run(s, 0.8f);
            Assert.That(s.TrySpend(20f), Is.True);
            s.Tick(Dt);
            Run(s, 0.9f); // 마지막으로 쓴 뒤 1초가 안 됨
            Assert.That(s.Current, Is.EqualTo(80f).Within(Eps));
        }

        [Test]
        public void FromEmpty_FullAfter10Point6Seconds()
        {
            var s = new Stamina(Rules);
            s.Set(0f);
            Run(s, 10.58f);
            Assert.That(s.IsFull, Is.False);
            Run(s, 0.04f);
            Assert.That(s.IsFull, Is.True);
            Assert.That(s.Current, Is.EqualTo(240f).Within(Eps));
        }

        [Test]
        public void FullSprint_LastsAbout13Seconds()
        {
            var s = new Stamina(Rules);
            int steps = 0;
            while (s.CanDrain && steps < 10000) { s.Drain(18f, Dt); s.Tick(Dt); steps++; }
            Assert.That(steps * Dt, Is.EqualTo(240f / 18f).Within(Dt));
            Assert.That(s.Exhausted, Is.True);
            Assert.That(s.Current, Is.EqualTo(0f));
        }

        [Test]
        public void Exhausted_BlocksSprint_Until60()
        {
            var s = new Stamina(Rules);
            s.Set(1f);
            Assert.That(s.Drain(18f, 1f), Is.False); // 바닥남
            s.Tick(Dt);
            Assert.That(s.Exhausted, Is.True);
            Assert.That(s.CanDrain, Is.False);
            Assert.That(s.Drain(18f, Dt), Is.False);
            Run(s, 1f + 2.38f); // 59.5
            Assert.That(s.Exhausted, Is.True);
            Run(s, 0.04f); // 60.5
            Assert.That(s.Exhausted, Is.False);
            Assert.That(s.CanDrain, Is.True);
        }

        [Test]
        public void TrySpend_NotEnough_DoesNothing()
        {
            var s = new Stamina(Rules);
            s.Set(19f);
            Run(s, 0.1f);
            float before = s.Current;
            float since = s.SinceSpend;
            Assert.That(s.TrySpend(20f), Is.False);
            Assert.That(s.Current, Is.EqualTo(before));
            Assert.That(s.SinceSpend, Is.EqualTo(since));
        }

        [Test]
        public void TrySpend_ToZero_Exhausts()
        {
            var s = new Stamina(Rules);
            s.Set(20f);
            Assert.That(s.TrySpend(20f), Is.True);
            Assert.That(s.Current, Is.EqualTo(0f));
            Assert.That(s.Exhausted, Is.True);
        }

        [Test]
        public void Infinite_NeverDecreases()
        {
            var s = new Stamina(Rules) { Infinite = true };
            Run(s, 5f, 18f);
            Assert.That(s.TrySpend(1000f), Is.True);
            Assert.That(s.Current, Is.EqualTo(240f));
            Assert.That(s.CanDrain, Is.True);
        }

        [Test]
        public void Fill_RestoresAndClearsExhaustion()
        {
            var s = new Stamina(Rules);
            s.Set(0f);
            s.Fill();
            Assert.That(s.Current, Is.EqualTo(240f));
            Assert.That(s.Exhausted, Is.False);
        }

        [Test]
        public void TargetSpeed_SprintOverridesWalk()
        {
            Assert.That(MoveMath.TargetSpeed(true, true, 2f, 5.5f, 8f, 1f), Is.EqualTo(8f).Within(Eps));
            Assert.That(MoveMath.TargetSpeed(false, false, 2f, 5.5f, 8f, 1f), Is.EqualTo(5.5f).Within(Eps));
            Assert.That(MoveMath.TargetSpeed(false, true, 2f, 5.5f, 8f, 0.5f), Is.EqualTo(1f).Within(Eps));
            Assert.That(MoveMath.TargetSpeed(true, false, 2f, 5.5f, 8f, 0f), Is.EqualTo(0f).Within(Eps));
        }
    }
}
