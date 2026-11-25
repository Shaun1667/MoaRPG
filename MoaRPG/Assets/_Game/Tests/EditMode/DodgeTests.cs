using Moa.Core.Motion;
using NUnit.Framework;

namespace Moa.Tests
{
    /// <summary>1-2 회피 순수 규칙 (Moa.Core.Motion.Dodge). 값은 BALANCE 3번 시작 값.</summary>
    public class DodgeTests
    {
        const float Eps = 1e-4f;
        const float Dt = 0.02f;
        static readonly DodgeRules Rules = new DodgeRules(3.5f, 0.3f, 0.2f, 0.2f, 20f, true);
        static readonly StaminaRules StaminaRules = new StaminaRules(240f, 25f, 1f, 60f);

        [Test]
        public void Speed_IsDistanceOverDuration()
        {
            Assert.That(Rules.Speed, Is.EqualTo(3.5f / 0.3f).Within(Eps));
        }

        [Test]
        public void Dash_CoversExactDistance_InDuration()
        {
            var d = new Dodge(Rules);
            var s = new Stamina(StaminaRules);
            Assert.That(d.TryStart(s, true), Is.True);
            float dashTime = 0f;
            int steps = 0;
            while (d.Active && steps < 100) { dashTime += d.Advance(Dt); steps++; }
            Assert.That(steps, Is.EqualTo(15));
            Assert.That(dashTime, Is.EqualTo(0.3f).Within(Eps));
            Assert.That(dashTime * Rules.Speed, Is.EqualTo(3.5f).Within(1e-3f));
        }

        [Test]
        public void Dash_OddStep_StillExactDistance()
        {
            var d = new Dodge(Rules);
            Assert.That(d.TryStart(null, true), Is.True);
            float dashTime = 0f;
            int guard = 0;
            while (d.Active && guard++ < 100) dashTime += d.Advance(0.07f);
            Assert.That(dashTime, Is.EqualTo(0.3f).Within(Eps));
        }

        [Test]
        public void Invulnerable_ForFirst02Seconds()
        {
            var d = new Dodge(Rules);
            d.TryStart(null, true);
            Assert.That(d.Invulnerable, Is.True);
            int invSteps = 0;
            while (d.Active)
            {
                bool invDuringStep = d.Invulnerable; // 이 스텝이 시작될 때
                d.Advance(Dt);
                if (invDuringStep) invSteps++;
            }
            Assert.That(invSteps * Dt, Is.EqualTo(0.2f).Within(Eps));
            Assert.That(d.Invulnerable, Is.False);
        }

        [Test]
        public void SpendsCost()
        {
            var d = new Dodge(Rules);
            var s = new Stamina(StaminaRules);
            d.TryStart(s, true);
            Assert.That(s.Current, Is.EqualTo(220f).Within(Eps));
        }

        [Test]
        public void NotEnoughStamina_DoesNotStart_NorSpend()
        {
            var d = new Dodge(Rules);
            var s = new Stamina(StaminaRules);
            s.Set(19f);
            Assert.That(d.TryStart(s, true), Is.False);
            Assert.That(d.LastBlock, Is.EqualTo(DodgeBlock.NoStamina));
            Assert.That(d.Active, Is.False);
            Assert.That(s.Current, Is.EqualTo(19f));
        }

        [Test]
        public void Airborne_Blocked_WhenGroundOnly()
        {
            var d = new Dodge(Rules);
            var s = new Stamina(StaminaRules);
            Assert.That(d.TryStart(s, false), Is.False);
            Assert.That(d.LastBlock, Is.EqualTo(DodgeBlock.Airborne));
            Assert.That(s.Current, Is.EqualTo(240f));
        }

        [Test]
        public void Busy_And_Cooldown_ThenAllowed()
        {
            var d = new Dodge(Rules);
            var s = new Stamina(StaminaRules);
            d.TryStart(s, true);
            d.Advance(Dt);
            Assert.That(d.TryStart(s, true), Is.False);
            Assert.That(d.LastBlock, Is.EqualTo(DodgeBlock.Busy));
            while (d.Active) d.Advance(Dt);
            Assert.That(d.CooldownLeft, Is.EqualTo(0.2f).Within(Eps));
            Assert.That(d.TryStart(s, true), Is.False);
            Assert.That(d.LastBlock, Is.EqualTo(DodgeBlock.Cooldown));
            for (int i = 0; i < 10; i++) d.Advance(Dt);
            Assert.That(d.TryStart(s, true), Is.True);
            Assert.That(d.Count, Is.EqualTo(2));
            Assert.That(s.Current, Is.EqualTo(200f).Within(Eps));
        }

        [Test]
        public void Cancel_StopsWithoutCooldown()
        {
            var d = new Dodge(Rules);
            d.TryStart(null, true);
            d.Advance(Dt);
            d.Cancel();
            Assert.That(d.Active, Is.False);
            Assert.That(d.CooldownLeft, Is.EqualTo(0f));
        }
    }
}
