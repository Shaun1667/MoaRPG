using Moa.Core.Motion;
using NUnit.Framework;

namespace Moa.Tests
{
    /// <summary>1-3 등반 순수 규칙 (Moa.Core.Motion.Climb) + 등반 중 회복 막기(Stamina.Tick(dt, false)). 값은 BALANCE 4번 시작 값.</summary>
    public class ClimbTests
    {
        const float Eps = 1e-3f;
        const float Dt = 0.02f;
        static readonly ClimbRules Rules = new ClimbRules(60f, 120f, 50f, 1.5f, 10f, 25f, 1.8f, 0.35f, 0.5f, 0.3f, 0.6f, 0.8f, 0.35f);
        static readonly StaminaRules StaminaRules = new StaminaRules(240f, 25f, 1f, 60f);
        const float Vertical = 0f; // 수직 벽 법선 y

        static float NormalY(float wallAngleDeg) => (float)System.Math.Cos(wallAngleDeg * System.Math.PI / 180.0);

        [Test]
        public void WallAngle_VerticalIs90()
        {
            Assert.That(Climb.WallAngle(0f), Is.EqualTo(90f).Within(Eps));
            Assert.That(Climb.WallAngle(1f), Is.EqualTo(0f).Within(Eps));
        }

        [Test]
        public void Attach_WallAngle_60To120Only()
        {
            var c = new Climb(Rules);
            Assert.That(c.Check(null, NormalY(59f), 0f), Is.EqualTo(ClimbBlock.WallAngle));
            Assert.That(c.Check(null, NormalY(60f), 0f), Is.EqualTo(ClimbBlock.None));
            Assert.That(c.Check(null, NormalY(120f), 0f), Is.EqualTo(ClimbBlock.None));
            Assert.That(c.Check(null, NormalY(121f), 0f), Is.EqualTo(ClimbBlock.WallAngle));
            // 시험 지형 50° 경사는 붙지 않는다
            Assert.That(c.Check(null, NormalY(50f), 0f), Is.EqualTo(ClimbBlock.WallAngle));
        }

        [Test]
        public void InputAngle_TowardsWall()
        {
            // 벽 법선 -Z (플레이어 쪽) → 벽 쪽 = +Z
            Assert.That(Climb.InputAngle(0f, 1f, 0f, -1f), Is.EqualTo(0f).Within(Eps));
            Assert.That(Climb.InputAngle(1f, 0f, 0f, -1f), Is.EqualTo(90f).Within(Eps));
            Assert.That(Climb.InputAngle(0f, -1f, 0f, -1f), Is.EqualTo(180f).Within(Eps));
            float a = Climb.InputAngle((float)System.Math.Sin(0.7), (float)System.Math.Cos(0.7), 0f, -1f);
            Assert.That(a, Is.EqualTo(0.7f * 57.29578f).Within(0.01f));
        }

        [Test]
        public void Attach_InputAngle_Within50()
        {
            var c = new Climb(Rules);
            Assert.That(c.Check(null, Vertical, 50f), Is.EqualTo(ClimbBlock.None));
            Assert.That(c.Check(null, Vertical, 51f), Is.EqualTo(ClimbBlock.InputAngle));
        }

        [Test]
        public void Attach_NeedsCanDrain()
        {
            var c = new Climb(Rules);
            var s = new Stamina(StaminaRules);
            s.Set(0f);
            Assert.That(c.TryAttach(s, Vertical, 0f), Is.False);
            Assert.That(c.LastBlock, Is.EqualTo(ClimbBlock.NoStamina));
            Assert.That(c.Active, Is.False);
        }

        [Test]
        public void Attach_DoesNotSpend()
        {
            var c = new Climb(Rules);
            var s = new Stamina(StaminaRules);
            Assert.That(c.TryAttach(s, Vertical, 0f), Is.True);
            Assert.That(c.Phase, Is.EqualTo(ClimbPhase.Climbing));
            Assert.That(s.Current, Is.EqualTo(240f));
            Assert.That(c.Count, Is.EqualTo(1));
        }

        [Test]
        public void Moving_Drains10PerSecond_StillDoesNot()
        {
            var c = new Climb(Rules);
            var s = new Stamina(StaminaRules);
            c.TryAttach(s, Vertical, 0f);
            for (int i = 0; i < 100; i++) { c.Hold(s, true, Dt); s.Tick(Dt, false); }
            Assert.That(s.Current, Is.EqualTo(220f).Within(Eps));
            for (int i = 0; i < 100; i++) { c.Hold(s, false, Dt); s.Tick(Dt, false); }
            Assert.That(s.Current, Is.EqualTo(220f).Within(Eps), "hanging still neither drains nor regenerates");
        }

        [Test]
        public void TickWithoutRegen_RestartsDelay()
        {
            var s = new Stamina(StaminaRules);
            s.Set(100f);
            for (int i = 0; i < 200; i++) s.Tick(Dt, false);
            Assert.That(s.Current, Is.EqualTo(100f).Within(Eps));
            Assert.That(s.SinceSpend, Is.EqualTo(0f));
            // 회복을 다시 허용하면 지연 1초를 처음부터 센다
            for (int i = 0; i < 50; i++) s.Tick(Dt, true);
            Assert.That(s.Current, Is.EqualTo(100f).Within(Eps));
            for (int i = 0; i < 50; i++) s.Tick(Dt, true);
            Assert.That(s.Current, Is.EqualTo(125f).Within(Eps));
        }

        [Test]
        public void OutOfStamina_EndsExhausted_WithReattachDelay()
        {
            var c = new Climb(Rules);
            var s = new Stamina(StaminaRules);
            s.Set(0.9f);
            c.TryAttach(s, Vertical, 0f);
            int steps = 0;
            while (c.Active && steps < 100) { c.Hold(s, true, Dt); s.Tick(Dt, false); steps++; }
            Assert.That(steps, Is.EqualTo(5), "0.9 stamina at 0.2 per step runs out on the 5th step");
            Assert.That(c.LastEnd, Is.EqualTo(ClimbEnd.Exhausted));
            Assert.That(c.ReattachLeft, Is.EqualTo(0.5f).Within(Eps));
            Assert.That(s.Exhausted, Is.True);
        }

        [Test]
        public void ReattachDelay_05Seconds()
        {
            var c = new Climb(Rules);
            c.TryAttach(null, Vertical, 0f);
            c.End(ClimbEnd.Dodge);
            Assert.That(c.Check(null, Vertical, 0f), Is.EqualTo(ClimbBlock.Cooldown));
            for (int i = 0; i < 24; i++) c.Tick(Dt);
            Assert.That(c.Check(null, Vertical, 0f), Is.EqualTo(ClimbBlock.Cooldown));
            c.Tick(Dt);
            Assert.That(c.Check(null, Vertical, 0f), Is.EqualTo(ClimbBlock.None));
        }

        [Test]
        public void LandedAndMantled_NoReattachDelay()
        {
            var c = new Climb(Rules);
            c.TryAttach(null, Vertical, 0f);
            c.End(ClimbEnd.Landed);
            Assert.That(c.ReattachLeft, Is.EqualTo(0f));
            c.TryAttach(null, Vertical, 0f);
            c.StartMantle();
            while (c.Active) c.AdvanceMantle(Dt);
            Assert.That(c.LastEnd, Is.EqualTo(ClimbEnd.Mantled));
            Assert.That(c.ReattachLeft, Is.EqualTo(0f));
        }

        [Test]
        public void WallEnded_HasReattachDelay()
        {
            var c = new Climb(Rules);
            c.TryAttach(null, Vertical, 0f);
            c.End(ClimbEnd.WallEnded);
            Assert.That(c.ReattachLeft, Is.EqualTo(0.5f).Within(Eps));
        }

        [Test]
        public void Leap_Costs25_Covers18m_In035Seconds()
        {
            var c = new Climb(Rules);
            var s = new Stamina(StaminaRules);
            c.TryAttach(s, Vertical, 0f);
            Assert.That(c.TryLeap(s), Is.True);
            Assert.That(s.Current, Is.EqualTo(215f).Within(Eps));
            float time = 0f;
            int steps = 0;
            while (c.Leaping && steps < 100) { time += c.AdvanceLeap(Dt); steps++; }
            Assert.That(steps, Is.EqualTo(18), "0.35 s = 17.5 steps of 0.02");
            Assert.That(time, Is.EqualTo(0.35f).Within(Eps));
            Assert.That(time * Rules.JumpSpeed, Is.EqualTo(1.8f).Within(Eps));
            Assert.That(c.Phase, Is.EqualTo(ClimbPhase.Climbing));
        }

        [Test]
        public void Leap_NotEnoughStamina_DoesNotStart()
        {
            var c = new Climb(Rules);
            var s = new Stamina(StaminaRules);
            s.Set(24f);
            c.TryAttach(s, Vertical, 0f);
            Assert.That(c.TryLeap(s), Is.False);
            Assert.That(c.LastBlock, Is.EqualTo(ClimbBlock.NoStamina));
            Assert.That(s.Current, Is.EqualTo(24f));
            Assert.That(c.Phase, Is.EqualTo(ClimbPhase.Climbing));
        }

        [Test]
        public void Leap_NoDrainWhileLeaping()
        {
            var c = new Climb(Rules);
            var s = new Stamina(StaminaRules);
            c.TryAttach(s, Vertical, 0f);
            c.TryLeap(s);
            for (int i = 0; i < 5; i++) { c.Hold(s, true, Dt); c.AdvanceLeap(Dt); }
            Assert.That(s.Current, Is.EqualTo(215f).Within(Eps));
        }

        [Test]
        public void Mantle_Takes035Seconds()
        {
            var c = new Climb(Rules);
            c.TryAttach(null, Vertical, 0f);
            Assert.That(c.StartMantle(), Is.True);
            int steps = 0;
            float p = 0f;
            while (c.Active && steps < 100) { p = c.AdvanceMantle(Dt); steps++; }
            Assert.That(steps, Is.EqualTo(18));
            Assert.That(p, Is.EqualTo(1f));
            Assert.That(c.MantleCount, Is.EqualTo(1));
        }

        [Test]
        public void MantlePath_RiseFirst_ThenForward()
        {
            Climb.MantlePath(0f, out float u, out float f);
            Assert.That(u, Is.EqualTo(0f)); Assert.That(f, Is.EqualTo(0f));
            Climb.MantlePath(0.25f, out u, out f);
            Assert.That(u, Is.EqualTo(0.5f).Within(Eps)); Assert.That(f, Is.EqualTo(0f));
            Climb.MantlePath(0.5f, out u, out f);
            Assert.That(u, Is.EqualTo(1f)); Assert.That(f, Is.EqualTo(0f));
            Climb.MantlePath(0.75f, out u, out f);
            Assert.That(u, Is.EqualTo(1f)); Assert.That(f, Is.EqualTo(0.5f).Within(Eps));
            Climb.MantlePath(1f, out u, out f);
            Assert.That(u, Is.EqualTo(1f)); Assert.That(f, Is.EqualTo(1f));
        }

        [Test]
        public void Cancel_NoDelay()
        {
            var c = new Climb(Rules);
            c.TryAttach(null, Vertical, 0f);
            c.Cancel();
            Assert.That(c.Active, Is.False);
            Assert.That(c.LastEnd, Is.EqualTo(ClimbEnd.Cancelled));
            Assert.That(c.ReattachLeft, Is.EqualTo(0f));
        }

        [Test]
        public void Busy_WhileActive()
        {
            var c = new Climb(Rules);
            c.TryAttach(null, Vertical, 0f);
            Assert.That(c.TryAttach(null, Vertical, 0f), Is.False);
            Assert.That(c.LastBlock, Is.EqualTo(ClimbBlock.Busy));
        }
    }
}
