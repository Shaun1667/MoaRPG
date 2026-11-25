using Moa.Core.Motion;
using NUnit.Framework;

namespace Moa.Tests
{
    /// <summary>1-3 활공 순수 규칙 (Moa.Core.Motion.Glide). 값은 BALANCE 5번 시작 값.</summary>
    public class GlideTests
    {
        const float Eps = 1e-3f;
        const float Dt = 0.02f;
        static readonly GlideRules Rules = new GlideRules(1.5f, 6f, 8f, 2f, 30f, 5f);
        static readonly StaminaRules StaminaRules = new StaminaRules(240f, 25f, 1f, 60f);

        [Test]
        public void Open_OnlyInAir_AboveMinHeight_WithStamina()
        {
            var g = new Glide(Rules);
            var s = new Stamina(StaminaRules);
            Assert.That(g.Check(s, true, 10f), Is.EqualTo(GlideBlock.Grounded));
            Assert.That(g.Check(s, false, 1.49f), Is.EqualTo(GlideBlock.TooLow));
            Assert.That(g.Check(s, false, 1.5f), Is.EqualTo(GlideBlock.None));
            s.Set(0f);
            Assert.That(g.Check(s, false, 10f), Is.EqualTo(GlideBlock.NoStamina));
        }

        [Test]
        public void JumpApex_IsBelowMinHeight()
        {
            // 평지 점프 정점(1.2m, BALANCE 1번)에서는 펼 수 없다
            var g = new Glide(Rules);
            Assert.That(g.Check(null, false, 1.2f), Is.EqualTo(GlideBlock.TooLow));
        }

        [Test]
        public void Open_DoesNotSpend_Hold_Drains5PerSecond()
        {
            var g = new Glide(Rules);
            var s = new Stamina(StaminaRules);
            Assert.That(g.TryOpen(s, false, 10f), Is.True);
            Assert.That(s.Current, Is.EqualTo(240f));
            for (int i = 0; i < 200; i++) { g.Hold(s, Dt); s.Tick(Dt); }
            Assert.That(s.Current, Is.EqualTo(220f).Within(0.01f)); // float 200번 누적 오차(약 0.0012)
            Assert.That(g.Active, Is.True);
        }

        [Test]
        public void FullStamina_Glides48Seconds()
        {
            var g = new Glide(Rules);
            var s = new Stamina(StaminaRules);
            g.TryOpen(s, false, 100f);
            int steps = 0;
            while (g.Active && steps < 10000) { g.Hold(s, Dt); s.Tick(Dt); steps++; }
            Assert.That(steps * Dt, Is.EqualTo(48f).Within(0.05f));
            Assert.That(g.LastEnd, Is.EqualTo(GlideEnd.Exhausted));
            Assert.That(s.Exhausted, Is.True);
        }

        [Test]
        public void Vertical_ApproachesFallSpeed_At30()
        {
            var g = new Glide(Rules);
            // 빠르게 떨어지던 중(-10) → 한 스텝에 0.6씩 줄어 -2에서 멈춤
            float v = -10f;
            v = g.Vertical(v, Dt);
            Assert.That(v, Is.EqualTo(-9.4f).Within(Eps));
            for (int i = 0; i < 100; i++) v = g.Vertical(v, Dt);
            Assert.That(v, Is.EqualTo(-2f).Within(Eps));
            // 올라가던 중(+5)도 -2로
            v = 5f;
            for (int i = 0; i < 100; i++) v = g.Vertical(v, Dt);
            Assert.That(v, Is.EqualTo(-2f).Within(Eps));
        }

        [Test]
        public void Horizontal_ReachesSixInThreeQuarterSecond()
        {
            var g = new Glide(Rules);
            var p = Planar.Zero;
            var forward = new Planar(0f, 1f);
            for (int i = 0; i < 37; i++) p = g.Horizontal(p, forward, Dt);
            Assert.That(p.Length, Is.LessThan(6f));
            p = g.Horizontal(p, forward, Dt);
            Assert.That(p.Length, Is.EqualTo(6f).Within(Eps));
            Assert.That(p.Z, Is.EqualTo(6f).Within(Eps));
        }

        [Test]
        public void Close_RecordsReason()
        {
            var g = new Glide(Rules);
            g.TryOpen(null, false, 10f);
            g.Close(GlideEnd.Wall);
            Assert.That(g.Active, Is.False);
            Assert.That(g.LastEnd, Is.EqualTo(GlideEnd.Wall));
            Assert.That(g.TryOpen(null, false, 10f), Is.True);
            Assert.That(g.Count, Is.EqualTo(2));
            Assert.That(g.TryOpen(null, false, 10f), Is.False);
            Assert.That(g.LastBlock, Is.EqualTo(GlideBlock.Active));
        }

        [Test]
        public void Cancel_Stops()
        {
            var g = new Glide(Rules);
            g.TryOpen(null, false, 10f);
            g.Cancel();
            Assert.That(g.Active, Is.False);
            Assert.That(g.LastEnd, Is.EqualTo(GlideEnd.Cancelled));
        }
    }
}
