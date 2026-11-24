using Moa.Unity.Data;
using NUnit.Framework;
using UnityEditor;

namespace Moa.Tests
{
    /// <summary>
    /// 1-1 수치 에셋 검산: 에셋이 있고 값이 BALANCE.md 1 · 2번과 같으며 서로 맞는지
    /// (실수로 바뀐 값을 찾는다 — 값을 바꾸면 BALANCE.md와 이 테스트도 함께 고친다).
    /// </summary>
    public class PlayerTuningAssetTests
    {
        const string MovementPath = "Assets/_Game/Data/Player/MovementTuning.asset";
        const string CameraPath = "Assets/_Game/Data/Player/CameraTuning.asset";
        const float Eps = 1e-4f;

        static MovementTuning Movement()
        {
            var t = AssetDatabase.LoadAssetAtPath<MovementTuning>(MovementPath);
            Assert.That(t, Is.Not.Null, "missing " + MovementPath);
            return t;
        }

        static CameraTuning Camera()
        {
            var t = AssetDatabase.LoadAssetAtPath<CameraTuning>(CameraPath);
            Assert.That(t, Is.Not.Null, "missing " + CameraPath);
            return t;
        }

        [Test]
        public void Movement_MatchesBalance1()
        {
            var t = Movement();
            Assert.That(t.WalkSpeed, Is.EqualTo(2f).Within(Eps));
            Assert.That(t.RunSpeed, Is.EqualTo(5.5f).Within(Eps));
            Assert.That(t.GroundAcceleration, Is.EqualTo(30f).Within(Eps));
            Assert.That(t.GroundDeceleration, Is.EqualTo(40f).Within(Eps));
            Assert.That(t.AirAcceleration, Is.EqualTo(10f).Within(Eps));
            Assert.That(t.AirDeceleration, Is.EqualTo(4f).Within(Eps));
            Assert.That(t.TurnSpeed, Is.EqualTo(720f).Within(Eps));
            Assert.That(t.JumpHeight, Is.EqualTo(1.2f).Within(Eps));
            Assert.That(t.Gravity, Is.EqualTo(25f).Within(Eps));
            Assert.That(t.MaxFallSpeed, Is.EqualTo(40f).Within(Eps));
            Assert.That(t.GroundStickSpeed, Is.EqualTo(8f).Within(Eps));
            Assert.That(t.SlopeLimit, Is.EqualTo(45f).Within(Eps));
            Assert.That(t.StepOffset, Is.EqualTo(0.35f).Within(Eps));
            Assert.That(t.CapsuleHeight, Is.EqualTo(1.8f).Within(Eps));
            Assert.That(t.CapsuleRadius, Is.EqualTo(0.35f).Within(Eps));
            Assert.That(t.SkinWidth, Is.EqualTo(0.05f).Within(Eps));
        }

        [Test]
        public void Movement_IsConsistent()
        {
            var t = Movement();
            Assert.That(t.RunSpeed, Is.GreaterThan(t.WalkSpeed));
            Assert.That(t.WalkSpeed, Is.GreaterThan(0f));
            // 시험 지형: 35°는 오르고 50°는 못 오른다
            Assert.That(t.SlopeLimit, Is.GreaterThan(35f).And.LessThan(50f));
            // CharacterController 조건: stepOffset <= height, radius*2 <= height
            Assert.That(t.StepOffset, Is.LessThanOrEqualTo(t.CapsuleHeight));
            Assert.That(t.CapsuleRadius * 2f, Is.LessThanOrEqualTo(t.CapsuleHeight));
            Assert.That(t.Gravity, Is.GreaterThan(0f));
        }

        [Test]
        public void Camera_MatchesBalance2()
        {
            var t = Camera();
            Assert.That(t.PivotHeight, Is.EqualTo(1.5f).Within(Eps));
            Assert.That(t.FollowSharpness, Is.EqualTo(20f).Within(Eps));
            Assert.That(t.StartPitch, Is.EqualTo(15f).Within(Eps));
            Assert.That(t.MinPitch, Is.EqualTo(-30f).Within(Eps));
            Assert.That(t.MaxPitch, Is.EqualTo(70f).Within(Eps));
            Assert.That(t.MouseSensitivity, Is.EqualTo(0.12f).Within(Eps));
            Assert.That(t.StickSpeed, Is.EqualTo(180f).Within(Eps));
            Assert.That(t.InvertY, Is.False);
            Assert.That(t.StartDistance, Is.EqualTo(5f).Within(Eps));
            Assert.That(t.MinDistance, Is.EqualTo(2f).Within(Eps));
            Assert.That(t.MaxDistance, Is.EqualTo(10f).Within(Eps));
            Assert.That(t.ZoomStep, Is.EqualTo(1f).Within(Eps));
            Assert.That(t.ZoomSharpness, Is.EqualTo(10f).Within(Eps));
            Assert.That(t.CollisionRadius, Is.EqualTo(0.25f).Within(Eps));
            Assert.That(t.CollisionPadding, Is.EqualTo(0.1f).Within(Eps));
            Assert.That(t.CollisionMinDistance, Is.EqualTo(0.5f).Within(Eps));
            Assert.That(t.CollisionReleaseSharpness, Is.EqualTo(5f).Within(Eps));
            Assert.That(t.FieldOfView, Is.EqualTo(60f).Within(Eps));
            Assert.That(t.NearClip, Is.EqualTo(0.1f).Within(Eps));
        }

        [Test]
        public void Camera_IsConsistent()
        {
            var t = Camera();
            Assert.That(t.MinPitch, Is.LessThan(t.MaxPitch));
            Assert.That(t.StartPitch, Is.InRange(t.MinPitch, t.MaxPitch));
            Assert.That(t.MinDistance, Is.LessThan(t.MaxDistance));
            Assert.That(t.StartDistance, Is.InRange(t.MinDistance, t.MaxDistance));
            Assert.That(t.CollisionMinDistance, Is.LessThanOrEqualTo(t.MinDistance));
        }
    }
}
