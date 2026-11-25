using Moa.Unity.Data;
using NUnit.Framework;
using UnityEditor;

namespace Moa.Tests
{
    /// <summary>
    /// 1-2 수치 에셋 검산: StaminaTuning.asset 값이 BALANCE.md 3번과 같고 서로 맞는지
    /// (값을 바꾸면 BALANCE.md와 이 테스트도 함께 고친다).
    /// </summary>
    public class StaminaTuningAssetTests
    {
        const string StaminaPath = "Assets/_Game/Data/Player/StaminaTuning.asset";
        const string MovementPath = "Assets/_Game/Data/Player/MovementTuning.asset";
        const float Eps = 1e-4f;

        static StaminaTuning Stamina()
        {
            var t = AssetDatabase.LoadAssetAtPath<StaminaTuning>(StaminaPath);
            Assert.That(t, Is.Not.Null, "missing " + StaminaPath);
            return t;
        }

        [Test]
        public void Stamina_MatchesBalance3()
        {
            var t = Stamina();
            Assert.That(t.MaxStamina, Is.EqualTo(240f).Within(Eps));
            Assert.That(t.RegenPerSecond, Is.EqualTo(25f).Within(Eps));
            Assert.That(t.RegenDelay, Is.EqualTo(1f).Within(Eps));
            Assert.That(t.ExhaustRecover, Is.EqualTo(60f).Within(Eps));
            Assert.That(t.SprintSpeed, Is.EqualTo(8f).Within(Eps));
            Assert.That(t.SprintDrainPerSecond, Is.EqualTo(18f).Within(Eps));
            Assert.That(t.DodgeCost, Is.EqualTo(20f).Within(Eps));
            Assert.That(t.DodgeDistance, Is.EqualTo(3.5f).Within(Eps));
            Assert.That(t.DodgeDuration, Is.EqualTo(0.3f).Within(Eps));
            Assert.That(t.DodgeInvulnerableTime, Is.EqualTo(0.2f).Within(Eps));
            Assert.That(t.DodgeCooldown, Is.EqualTo(0.2f).Within(Eps));
            Assert.That(t.DodgeGroundOnly, Is.True);
            Assert.That(t.GaugeAnchorHeight, Is.EqualTo(1.2f).Within(Eps));
            Assert.That(t.GaugeScreenOffset.x, Is.EqualTo(90f).Within(Eps));
            Assert.That(t.GaugeScreenOffset.y, Is.EqualTo(0f).Within(Eps));
            Assert.That(t.GaugeSize, Is.EqualTo(64f).Within(Eps));
            Assert.That(t.GaugeHideDelay, Is.EqualTo(0.6f).Within(Eps));
            Assert.That(t.GaugeFadeTime, Is.EqualTo(0.3f).Within(Eps));
        }

        [Test]
        public void Stamina_IsConsistent()
        {
            var t = Stamina();
            var move = AssetDatabase.LoadAssetAtPath<MovementTuning>(MovementPath);
            Assert.That(move, Is.Not.Null, "missing " + MovementPath);
            Assert.That(t.SprintSpeed, Is.GreaterThan(move.RunSpeed), "sprint must be faster than run");
            Assert.That(t.DodgeInvulnerableTime, Is.LessThanOrEqualTo(t.DodgeDuration));
            Assert.That(t.DodgeCost, Is.LessThanOrEqualTo(t.MaxStamina));
            Assert.That(t.ExhaustRecover, Is.InRange(t.DodgeCost, t.MaxStamina), "after exhaustion a dodge is possible before sprint unlocks");
            Assert.That(t.RegenPerSecond, Is.GreaterThan(0f));
            // 원신 체감: 가득 찬 상태에서 질주 약 13초, 바닥에서 가득까지 약 10.6초
            Assert.That(t.MaxStamina / t.SprintDrainPerSecond, Is.EqualTo(13.333f).Within(0.01f));
            Assert.That(t.RegenDelay + t.MaxStamina / t.RegenPerSecond, Is.EqualTo(10.6f).Within(0.01f));
        }
    }
}
