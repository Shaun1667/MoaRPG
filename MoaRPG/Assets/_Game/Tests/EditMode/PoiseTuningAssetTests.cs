using Moa.Core.Combat;
using Moa.Unity.Data;
using NUnit.Framework;
using UnityEditor;

namespace Moa.Tests
{
    /// <summary>
    /// 2-3 수치 에셋 검산: 경직(Sword.asset · TrainingDummy.asset) · 저스트 회피(CombatTuning.asset) · 예고 공격(AttackingDummy.asset)이
    /// BALANCE.md 6 · 7 · 8 · 9번과 같고 서로 맞는지 (값을 바꾸면 BALANCE.md와 이 테스트도 함께 고친다).
    /// </summary>
    public class PoiseTuningAssetTests
    {
        const string SwordPath = "Assets/_Game/Data/Weapons/Sword.asset";
        const string CombatPath = "Assets/_Game/Data/Combat/CombatTuning.asset";
        const string DummyPath = "Assets/_Game/Data/Enemies/TrainingDummy.asset";
        const string AttackPath = "Assets/_Game/Data/Enemies/AttackingDummy.asset";
        const string StaminaPath = "Assets/_Game/Data/Player/StaminaTuning.asset";
        const float Eps = 1e-4f;
        const float FixedStep = 0.02f;

        // BALANCE 6번 경직: 콤보 1 ~ 5타 · 강공격 1 · 2베기
        static readonly float[] ComboPoise = { 8f, 8f, 10f, 12f, 16f };
        static readonly float[] ChargedPoise = { 15f, 20f };

        static T Load<T>(string path) where T : UnityEngine.Object
        {
            var t = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.That(t, Is.Not.Null, "missing " + path);
            return t;
        }

        static bool WholeSteps(float seconds)
        {
            float steps = seconds / FixedStep;
            return System.Math.Abs(steps - System.Math.Round(steps)) < 1e-3;
        }

        [Test]
        public void Sword_Poise_MatchesBalance6()
        {
            var w = Load<WeaponTuning>(SwordPath);
            Assert.That(w.StepCount, Is.EqualTo(ComboPoise.Length));
            for (int i = 0; i < ComboPoise.Length; i++) Assert.That(w.ComboPoise(i), Is.EqualTo(ComboPoise[i]).Within(Eps), "hit " + (i + 1));
            Assert.That(w.ChargedSlashCount, Is.EqualTo(ChargedPoise.Length));
            for (int i = 0; i < ChargedPoise.Length; i++) Assert.That(w.ChargedPoise(i), Is.EqualTo(ChargedPoise[i]).Within(Eps), "charged slash " + (i + 1));
            Assert.That(w.PlungeLowPoise, Is.EqualTo(20f).Within(Eps));
            Assert.That(w.PlungeHighPoise, Is.EqualTo(35f).Within(Eps));
            Assert.That(w.PlungePoise(false), Is.EqualTo(20f).Within(Eps));
            Assert.That(w.PlungePoise(true), Is.EqualTo(35f).Within(Eps));
        }

        [Test]
        public void Dummy_Poise_MatchesBalance8()
        {
            var d = Load<DummyTuning>(DummyPath);
            Assert.That(d.MaxPoise, Is.EqualTo(100f).Within(Eps));
            Assert.That(d.GroggyDuration, Is.EqualTo(5f).Within(Eps), "GDD: groggy 5 s");
            Assert.That(d.GroggyDamageTaken, Is.EqualTo(1.3f).Within(Eps), "GDD: +30% damage");
            Assert.That(d.PoiseRegenDelay, Is.EqualTo(2f).Within(Eps));
            Assert.That(d.PoiseRegenPerSecond, Is.EqualTo(25f).Within(Eps));
        }

        [Test]
        public void Combat_JustDodge_MatchesBalance7()
        {
            var c = Load<CombatTuning>(CombatPath);
            Assert.That(c.JustDodgeWindow, Is.EqualTo(0.16f).Within(Eps));
            Assert.That(c.JustSlowScale, Is.EqualTo(0.3f).Within(Eps));
            Assert.That(c.JustSlowDuration, Is.EqualTo(0.8f).Within(Eps));
            Assert.That(c.JustCounterTime, Is.EqualTo(3f).Within(Eps));
            Assert.That(c.JustCounterPoiseMultiplier, Is.EqualTo(2f).Within(Eps));
        }

        [Test]
        public void AttackingDummy_MatchesBalance9()
        {
            var a = Load<EnemyAttackTuning>(AttackPath);
            Assert.That(a.AttackInterval, Is.EqualTo(2f).Within(Eps));
            Assert.That(a.TelegraphTime, Is.EqualTo(0.8f).Within(Eps));
            Assert.That(a.RecoveryTime, Is.EqualTo(0.6f).Within(Eps));
            Assert.That(a.AttackRange, Is.EqualTo(3f).Within(Eps));
        }

        [Test]
        public void PoiseAndJustDodge_AreConsistent()
        {
            var w = Load<WeaponTuning>(SwordPath);
            var d = Load<DummyTuning>(DummyPath);
            var c = Load<CombatTuning>(CombatPath);
            var a = Load<EnemyAttackTuning>(AttackPath);
            var s = Load<StaminaTuning>(StaminaPath);

            float combo = 0f;
            for (int i = 0; i < w.StepCount; i++) combo += w.ComboPoise(i);
            Assert.That(combo, Is.EqualTo(54f).Within(Eps));
            Assert.That(combo, Is.LessThan(d.MaxPoise), "one combo does not break the dummy");
            Assert.That(combo * 2f, Is.GreaterThanOrEqualTo(d.MaxPoise), "two combos do");
            Assert.That(combo * c.JustCounterPoiseMultiplier, Is.GreaterThanOrEqualTo(d.MaxPoise), "one combo during the counter bonus does");
            Assert.That(w.PlungeHighPoise, Is.GreaterThan(w.PlungeLowPoise));

            // 저스트 창은 회피 무적 안 (창 밖 무적 구간 = 피하기만), 시간은 고정 스텝 배수
            Assert.That(c.JustDodgeWindow, Is.LessThan(s.DodgeInvulnerableTime));
            Assert.That(WholeSteps(c.JustDodgeWindow), Is.True, "window = whole fixed steps");
            Assert.That(c.JustSlowScale, Is.InRange(0.05f, 1f));
            Assert.That(c.JustCounterTime, Is.LessThan(d.GroggyDuration));
            // 반격 보너스 안에 콤보 5타가 닿는다: 슬로모션 동안 흐른 게임 시간 + 1 ~ 4타 길이 + 5타 선딜
            float fifthHit = c.JustSlowDuration * c.JustSlowScale;
            for (int i = 0; i < w.StepCount - 1; i++) { var st = w.GetStep(i); fifthHit += st.windup + st.active + st.recovery; }
            fifthHit += w.GetStep(w.StepCount - 1).windup;
            Assert.That(fifthHit, Is.EqualTo(2.18f).Within(1e-3f));
            Assert.That(c.JustCounterTime, Is.GreaterThan(fifthHit), "a full combo right after the slow motion gets the bonus");
            foreach (float v in new[] { a.AttackInterval, a.TelegraphTime, a.RecoveryTime })
                Assert.That(WholeSteps(v), Is.True, v + " s = whole fixed steps");
            Assert.That(a.TelegraphTime, Is.GreaterThan(c.JustDodgeWindow), "the warning is longer than the window");
            Assert.That(WholeSteps(d.GroggyDuration) && WholeSteps(d.PoiseRegenDelay), Is.True);

            // 그로기 허수아비에 한손검 1타: 36 → 47
            Assert.That(DamageMath.Compute(w.BaseAttack, w.GetStep(0).multiplier * d.GroggyDamageTaken, d.Defense, c.DefenseConstant), Is.EqualTo(47));
        }
    }
}
