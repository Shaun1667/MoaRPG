using Moa.Core.Motion;
using NUnit.Framework;

namespace Moa.Tests
{
    /// <summary>1-1 궤도 카메라 순수 계산 (Moa.Core.Motion.OrbitMath).</summary>
    public class OrbitMathTests
    {
        const float Eps = 1e-4f;

        [Test]
        public void ApplyLook_MouseUp_LooksUp_PitchDecreases()
        {
            var a = OrbitMath.ApplyLook(new OrbitAngles(0f, 10f), 0f, 5f, -30f, 70f, false);
            Assert.That(a.Pitch, Is.EqualTo(5f).Within(Eps));
            var inv = OrbitMath.ApplyLook(new OrbitAngles(0f, 10f), 0f, 5f, -30f, 70f, true);
            Assert.That(inv.Pitch, Is.EqualTo(15f).Within(Eps));
        }

        [Test]
        public void ApplyLook_PitchClamped_YawWraps()
        {
            var down = OrbitMath.ApplyLook(new OrbitAngles(350f, 60f), 20f, -100f, -30f, 70f, false);
            Assert.That(down.Pitch, Is.EqualTo(70f).Within(Eps));
            Assert.That(down.Yaw, Is.EqualTo(10f).Within(Eps));
            var up = OrbitMath.ApplyLook(new OrbitAngles(0f, 0f), -20f, 100f, -30f, 70f, false);
            Assert.That(up.Pitch, Is.EqualTo(-30f).Within(Eps));
            Assert.That(up.Yaw, Is.EqualTo(340f).Within(Eps));
        }

        [Test]
        public void Zoom_UsesSignOnly_AndClamps()
        {
            Assert.That(OrbitMath.Zoom(5f, 120f, 1f, 2f, 10f), Is.EqualTo(4f).Within(Eps));
            Assert.That(OrbitMath.Zoom(5f, -0.1f, 1f, 2f, 10f), Is.EqualTo(6f).Within(Eps));
            Assert.That(OrbitMath.Zoom(2.5f, 1f, 1f, 2f, 10f), Is.EqualTo(2f).Within(Eps));
            Assert.That(OrbitMath.Zoom(9.5f, -1f, 1f, 2f, 10f), Is.EqualTo(10f).Within(Eps));
            Assert.That(OrbitMath.Zoom(5f, 0f, 1f, 2f, 10f), Is.EqualTo(5f).Within(Eps));
        }

        [Test]
        public void CollisionDistance_PullsInFrontOfWall()
        {
            Assert.That(OrbitMath.CollisionDistance(5f, false, 0f, 0.1f, 0.5f), Is.EqualTo(5f));
            Assert.That(OrbitMath.CollisionDistance(5f, true, 2f, 0.1f, 0.5f), Is.EqualTo(1.9f).Within(Eps));
            Assert.That(OrbitMath.CollisionDistance(5f, true, 0.2f, 0.1f, 0.5f), Is.EqualTo(0.5f).Within(Eps));
            Assert.That(OrbitMath.CollisionDistance(5f, true, 9f, 0.1f, 0.5f), Is.EqualTo(5f).Within(Eps));
        }

        [Test]
        public void SmoothDistance_PullInIsInstant_ReleaseIsGradual()
        {
            Assert.That(OrbitMath.SmoothDistance(5f, 2f, 5f, 0.016f), Is.EqualTo(2f));
            float back = OrbitMath.SmoothDistance(2f, 5f, 5f, 0.016f);
            Assert.That(back, Is.GreaterThan(2f));
            Assert.That(back, Is.LessThan(5f));
        }

        [Test]
        public void SmoothFactor_IsFrameRateIndependent()
        {
            float one = OrbitMath.SmoothFactor(10f, 0.02f);
            float twoHalves = 1f - (1f - OrbitMath.SmoothFactor(10f, 0.01f)) * (1f - OrbitMath.SmoothFactor(10f, 0.01f));
            Assert.That(twoHalves, Is.EqualTo(one).Within(Eps));
            Assert.That(OrbitMath.SmoothFactor(0f, 0.02f), Is.EqualTo(0f));
            Assert.That(OrbitMath.SmoothFactor(10f, 100f), Is.EqualTo(1f).Within(Eps));
        }

        [Test]
        public void Offset_BehindAndAbove()
        {
            OrbitMath.Offset(new OrbitAngles(0f, 0f), 5f, out float x, out float y, out float z);
            Assert.That(x, Is.EqualTo(0f).Within(Eps));
            Assert.That(y, Is.EqualTo(0f).Within(Eps));
            Assert.That(z, Is.EqualTo(-5f).Within(Eps));

            OrbitMath.Offset(new OrbitAngles(90f, 30f), 4f, out x, out y, out z);
            Assert.That(x, Is.EqualTo(-4f * System.MathF.Cos(30f * System.MathF.PI / 180f)).Within(Eps));
            Assert.That(y, Is.EqualTo(2f).Within(Eps));
            Assert.That(z, Is.EqualTo(0f).Within(Eps));
        }
    }
}
