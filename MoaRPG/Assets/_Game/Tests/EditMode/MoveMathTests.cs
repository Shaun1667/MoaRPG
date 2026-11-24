using Moa.Core.Motion;
using NUnit.Framework;

namespace Moa.Tests
{
    /// <summary>1-1 이동 순수 계산 (Moa.Core.Motion.MoveMath).</summary>
    public class MoveMathTests
    {
        const float Eps = 1e-4f;

        [Test]
        public void CameraRelative_Yaw0_ForwardIsPlusZ_RightIsPlusX()
        {
            var f = MoveMath.CameraRelative(0f, 1f, 0f);
            Assert.That(f.X, Is.EqualTo(0f).Within(Eps));
            Assert.That(f.Z, Is.EqualTo(1f).Within(Eps));
            var r = MoveMath.CameraRelative(1f, 0f, 0f);
            Assert.That(r.X, Is.EqualTo(1f).Within(Eps));
            Assert.That(r.Z, Is.EqualTo(0f).Within(Eps));
        }

        [Test]
        public void CameraRelative_Yaw90_ForwardIsPlusX_RightIsMinusZ()
        {
            var f = MoveMath.CameraRelative(0f, 1f, 90f);
            Assert.That(f.X, Is.EqualTo(1f).Within(Eps));
            Assert.That(f.Z, Is.EqualTo(0f).Within(Eps));
            var r = MoveMath.CameraRelative(1f, 0f, 90f);
            Assert.That(r.X, Is.EqualTo(0f).Within(Eps));
            Assert.That(r.Z, Is.EqualTo(-1f).Within(Eps));
        }

        [Test]
        public void CameraRelative_DiagonalKeyboard_IsNotFaster()
        {
            var d = MoveMath.CameraRelative(1f, 1f, 37f);
            Assert.That(d.Length, Is.EqualTo(1f).Within(Eps));
        }

        [Test]
        public void CameraRelative_HalfStick_KeepsMagnitude()
        {
            var d = MoveMath.CameraRelative(0f, 0.5f, 200f);
            Assert.That(d.Length, Is.EqualTo(0.5f).Within(Eps));
        }

        [Test]
        public void TargetSpeed_WalkOrRun_ScaledByInput()
        {
            Assert.That(MoveMath.TargetSpeed(true, 2f, 6f, 1f), Is.EqualTo(2f).Within(Eps));
            Assert.That(MoveMath.TargetSpeed(false, 2f, 6f, 1f), Is.EqualTo(6f).Within(Eps));
            Assert.That(MoveMath.TargetSpeed(false, 2f, 6f, 0.5f), Is.EqualTo(3f).Within(Eps));
            Assert.That(MoveMath.TargetSpeed(false, 2f, 6f, 3f), Is.EqualTo(6f).Within(Eps));
        }

        [Test]
        public void Approach_Accelerates_ByRateTimesDt_ThenStopsAtTarget()
        {
            var v = MoveMath.Approach(Planar.Zero, new Planar(0f, 6f), 20f, 40f, 0.1f);
            Assert.That(v.Z, Is.EqualTo(2f).Within(Eps));
            for (int i = 0; i < 10; i++) v = MoveMath.Approach(v, new Planar(0f, 6f), 20f, 40f, 0.1f);
            Assert.That(v.Z, Is.EqualTo(6f).Within(Eps));
        }

        [Test]
        public void Approach_Decelerates_WithDecelerationRate()
        {
            var v = MoveMath.Approach(new Planar(6f, 0f), Planar.Zero, 20f, 40f, 0.1f);
            Assert.That(v.X, Is.EqualTo(2f).Within(Eps));
            v = MoveMath.Approach(v, Planar.Zero, 20f, 40f, 0.1f);
            Assert.That(v.Length, Is.EqualTo(0f).Within(Eps));
        }

        [Test]
        public void DeltaAngle_TakesShortestWay()
        {
            Assert.That(MoveMath.DeltaAngle(350f, 10f), Is.EqualTo(20f).Within(Eps));
            Assert.That(MoveMath.DeltaAngle(10f, 350f), Is.EqualTo(-20f).Within(Eps));
            Assert.That(MoveMath.DeltaAngle(0f, 180f), Is.EqualTo(180f).Within(Eps));
            Assert.That(MoveMath.DeltaAngle(90f, -270f), Is.EqualTo(0f).Within(Eps));
        }

        [Test]
        public void TurnTowards_LimitedStep_AndSnapsWhenClose()
        {
            Assert.That(MoveMath.TurnTowards(0f, 90f, 360f, 0.1f), Is.EqualTo(36f).Within(Eps));
            Assert.That(MoveMath.TurnTowards(10f, 350f, 360f, 0.1f), Is.EqualTo(350f).Within(Eps));
            Assert.That(MoveMath.TurnTowards(0f, 270f, 360f, 0.1f), Is.EqualTo(324f).Within(Eps));
        }

        [Test]
        public void YawOf_MatchesUnityConvention()
        {
            Assert.That(MoveMath.YawOf(new Planar(0f, 1f)), Is.EqualTo(0f).Within(Eps));
            Assert.That(MoveMath.YawOf(new Planar(1f, 0f)), Is.EqualTo(90f).Within(Eps));
            Assert.That(MoveMath.YawOf(new Planar(-1f, 0f)), Is.EqualTo(270f).Within(Eps));
        }

        [Test]
        public void JumpVelocity_ReachesHeight()
        {
            float v = MoveMath.JumpVelocity(1.2f, 25f);
            Assert.That(v, Is.EqualTo(System.MathF.Sqrt(60f)).Within(Eps));
            // 꼭대기 높이 v²/2g = h
            Assert.That(v * v / (2f * 25f), Is.EqualTo(1.2f).Within(Eps));
            Assert.That(MoveMath.JumpVelocity(0f, 25f), Is.EqualTo(0f));
        }

        [Test]
        public void ApplyGravity_CapsAtMaxFall()
        {
            Assert.That(MoveMath.ApplyGravity(0f, 20f, 50f, 0.5f), Is.EqualTo(-10f).Within(Eps));
            Assert.That(MoveMath.ApplyGravity(-45f, 20f, 50f, 0.5f), Is.EqualTo(-50f).Within(Eps));
        }

        [Test]
        public void IsWalkable_35Yes_50No_AtLimit45()
        {
            float n35 = System.MathF.Cos(35f * System.MathF.PI / 180f);
            float n50 = System.MathF.Cos(50f * System.MathF.PI / 180f);
            Assert.That(MoveMath.IsWalkable(n35, 45f), Is.True);
            Assert.That(MoveMath.IsWalkable(n50, 45f), Is.False);
            Assert.That(MoveMath.IsWalkable(1f, 45f), Is.True);
            Assert.That(MoveMath.IsWalkable(0f, 45f), Is.False);
            Assert.That(MoveMath.SlopeAngle(n35), Is.EqualTo(35f).Within(1e-3f));
        }
    }
}
