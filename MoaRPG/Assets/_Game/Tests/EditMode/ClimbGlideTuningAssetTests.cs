using Moa.Unity.Data;
using NUnit.Framework;
using UnityEditor;

namespace Moa.Tests
{
    /// <summary>
    /// 1-3 수치 에셋 검산: ClimbTuning.asset · GlideTuning.asset 값이 BALANCE.md 4 · 5번과 같고 서로 맞는지
    /// (값을 바꾸면 BALANCE.md와 이 테스트도 함께 고친다).
    /// </summary>
    public class ClimbGlideTuningAssetTests
    {
        const string ClimbPath = "Assets/_Game/Data/Player/ClimbTuning.asset";
        const string GlidePath = "Assets/_Game/Data/Player/GlideTuning.asset";
        const string StaminaPath = "Assets/_Game/Data/Player/StaminaTuning.asset";
        const string MovementPath = "Assets/_Game/Data/Player/MovementTuning.asset";
        const float Eps = 1e-4f;

        static T Load<T>(string path) where T : UnityEngine.Object
        {
            var t = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.That(t, Is.Not.Null, "missing " + path);
            return t;
        }

        [Test]
        public void Climb_MatchesBalance4()
        {
            var t = Load<ClimbTuning>(ClimbPath);
            Assert.That(t.MinWallAngle, Is.EqualTo(60f).Within(Eps));
            Assert.That(t.MaxWallAngle, Is.EqualTo(120f).Within(Eps));
            Assert.That(t.AttachInputAngle, Is.EqualTo(50f).Within(Eps));
            Assert.That(t.ReattachDelay, Is.EqualTo(0.5f).Within(Eps));
            Assert.That(t.ProbeDistance, Is.EqualTo(0.3f).Within(Eps));
            Assert.That(t.ClimbSpeed, Is.EqualTo(1.5f).Within(Eps));
            Assert.That(t.DrainPerSecond, Is.EqualTo(10f).Within(Eps));
            Assert.That(t.JumpCost, Is.EqualTo(25f).Within(Eps));
            Assert.That(t.JumpHeight, Is.EqualTo(1.8f).Within(Eps));
            Assert.That(t.JumpDuration, Is.EqualTo(0.35f).Within(Eps));
            Assert.That(t.LedgeHeight, Is.EqualTo(0.6f).Within(Eps));
            Assert.That(t.MantleForward, Is.EqualTo(0.8f).Within(Eps));
            Assert.That(t.MantleDuration, Is.EqualTo(0.35f).Within(Eps));
        }

        [Test]
        public void Glide_MatchesBalance5()
        {
            var t = Load<GlideTuning>(GlidePath);
            Assert.That(t.MinHeight, Is.EqualTo(1.5f).Within(Eps));
            Assert.That(t.HorizontalSpeed, Is.EqualTo(6f).Within(Eps));
            Assert.That(t.HorizontalAcceleration, Is.EqualTo(8f).Within(Eps));
            Assert.That(t.FallSpeed, Is.EqualTo(2f).Within(Eps));
            Assert.That(t.VerticalAcceleration, Is.EqualTo(30f).Within(Eps));
            Assert.That(t.DrainPerSecond, Is.EqualTo(5f).Within(Eps));
        }

        [Test]
        public void ClimbGlide_AreConsistent()
        {
            var c = Load<ClimbTuning>(ClimbPath);
            var g = Load<GlideTuning>(GlidePath);
            var s = Load<StaminaTuning>(StaminaPath);
            var m = Load<MovementTuning>(MovementPath);
            Assert.That(c.MinWallAngle, Is.GreaterThan(m.SlopeLimit), "walkable slopes are not climbed");
            Assert.That(c.MaxWallAngle, Is.GreaterThanOrEqualTo(90f));
            Assert.That(c.JumpCost, Is.LessThanOrEqualTo(s.ExhaustRecover), "a climb jump is possible right after exhaustion recovers");
            Assert.That(c.LedgeHeight, Is.LessThan(m.CapsuleHeight * 0.5f), "ledge probe stays above the body probe (capsule center)");
            Assert.That(c.MantleForward, Is.GreaterThan(m.CapsuleRadius * 2f), "mantle moves the whole capsule past the edge");
            Assert.That(g.MinHeight, Is.GreaterThan(m.JumpHeight), "a flat-ground jump cannot open the glider");
            Assert.That(g.FallSpeed, Is.LessThan(m.MaxFallSpeed));
            // 원신 체감: 가득에서 기둥 20m 오르기(13.3초 · 133) 가능, 활공 48초
            Assert.That(20f / c.ClimbSpeed * c.DrainPerSecond, Is.LessThan(s.MaxStamina));
            Assert.That(s.MaxStamina / g.DrainPerSecond, Is.EqualTo(48f).Within(0.01f));
        }
    }
}
