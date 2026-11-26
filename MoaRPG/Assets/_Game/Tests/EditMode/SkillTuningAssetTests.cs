using Moa.Core.Combat;
using Moa.Unity.Data;
using NUnit.Framework;
using UnityEditor;

namespace Moa.Tests
{
    /// <summary>
    /// 2-4 수치 에셋 검산: 스킬 · 궁극기(SwordHero.asset) · 처치 에너지(TrainingDummy.asset)가 BALANCE.md 10번과 같고 서로 맞는지
    /// (값을 바꾸면 BALANCE.md와 이 테스트도 함께 고친다).
    /// </summary>
    public class SkillTuningAssetTests
    {
        const string SkillsPath = "Assets/_Game/Data/Skills/SwordHero.asset";
        const string SwordPath = "Assets/_Game/Data/Weapons/Sword.asset";
        const string HeroPath = "Assets/_Game/Data/Characters/SwordHero.asset"; // 2-6: 공격력 = 캐릭터 + 무기
        const string CombatPath = "Assets/_Game/Data/Combat/CombatTuning.asset";
        const string DummyPath = "Assets/_Game/Data/Enemies/TrainingDummy.asset";
        const float Eps = 1e-4f;
        const float FixedStep = 0.02f;

        // BALANCE 10번: 배율 · 선딜 · 판정 · 후딜 · 내딛기 · 경직
        static readonly float[][] Tap = { new[] { 1.70f, 0.12f, 0.10f, 0.30f, 0.40f, 30f } };
        static readonly float[][] Hold = { new[] { 1.20f, 0.10f, 0.08f, 0.04f, 0.50f, 20f }, new[] { 2.00f, 0.10f, 0.12f, 0.40f, 1.00f, 40f } };
        static readonly float[][] Burst = { new[] { 4.00f, 0.60f, 0.10f, 0.50f, 0f, 60f } };

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

        static void AssertRows(WeaponTuning.Step[] rows, float[][] want, string what)
        {
            Assert.That(rows, Is.Not.Null, what);
            Assert.That(rows.Length, Is.EqualTo(want.Length), what + " rows");
            for (int i = 0; i < want.Length; i++)
            {
                var r = rows[i];
                string at = what + " " + (i + 1);
                Assert.That(r.multiplier, Is.EqualTo(want[i][0]).Within(Eps), at + " multiplier");
                Assert.That(r.windup, Is.EqualTo(want[i][1]).Within(Eps), at + " windup");
                Assert.That(r.active, Is.EqualTo(want[i][2]).Within(Eps), at + " active");
                Assert.That(r.recovery, Is.EqualTo(want[i][3]).Within(Eps), at + " recovery");
                Assert.That(r.lunge, Is.EqualTo(want[i][4]).Within(Eps), at + " lunge");
                Assert.That(r.poise, Is.EqualTo(want[i][5]).Within(Eps), at + " poise");
                Assert.That(WholeSteps(r.windup) && WholeSteps(r.active) && WholeSteps(r.recovery), Is.True, at + " = whole fixed steps");
                Assert.That(r.recovery, Is.GreaterThan(0f), at + ": the hit window closes before the slash ends (hits land while it is active)");
            }
        }

        static WeaponTuning.Step[] BurstRows(SkillTuning t)
        {
            var list = new WeaponTuning.Step[t.BurstSlashCount];
            for (int i = 0; i < list.Length; i++) list[i] = t.GetBurstSlash(i);
            return list;
        }

        [Test]
        public void Skill_MatchesBalance10()
        {
            var t = Load<SkillTuning>(SkillsPath);
            Assert.That(t.SkillHoldTime, Is.EqualTo(0.5f).Within(Eps));
            Assert.That(WholeSteps(t.SkillHoldTime), Is.True);
            AssertRows(t.Tap.slashes, Tap, "tap");
            Assert.That(t.Tap.cooldown, Is.EqualTo(5f).Within(Eps));
            Assert.That(t.Tap.energy, Is.EqualTo(10f).Within(Eps));
            Assert.That(t.Tap.hitRange, Is.EqualTo(3.0f).Within(Eps));
            Assert.That(t.Tap.hitAngle, Is.EqualTo(360f).Within(Eps));
            AssertRows(t.Hold.slashes, Hold, "hold");
            Assert.That(t.Hold.cooldown, Is.EqualTo(8f).Within(Eps));
            Assert.That(t.Hold.energy, Is.EqualTo(15f).Within(Eps));
            Assert.That(t.Hold.hitRange, Is.EqualTo(3.5f).Within(Eps));
            Assert.That(t.Hold.hitAngle, Is.EqualTo(180f).Within(Eps));
        }

        [Test]
        public void Burst_MatchesBalance10()
        {
            var t = Load<SkillTuning>(SkillsPath);
            Assert.That(t.BurstEnergyCost, Is.EqualTo(40f).Within(Eps));
            Assert.That(t.BurstCooldown, Is.EqualTo(12f).Within(Eps));
            Assert.That(t.BurstInvulnerable, Is.True);
            AssertRows(BurstRows(t), Burst, "burst");
            Assert.That(t.BurstHitRange, Is.EqualTo(5.0f).Within(Eps));
            Assert.That(t.BurstHitAngle, Is.EqualTo(360f).Within(Eps));
            Assert.That(Load<DummyTuning>(DummyPath).EnergyOnDefeat, Is.EqualTo(5f).Within(Eps));
        }

        [Test]
        public void SkillAndBurst_AreConsistent()
        {
            var t = Load<SkillTuning>(SkillsPath);
            var hero = Load<CharacterData>(HeroPath);
            var w = Load<WeaponTuning>(SwordPath);
            Assert.That(hero.Weapon, Is.SameAs(w));
            var c = Load<CombatTuning>(CombatPath);
            var d = Load<DummyTuning>(DummyPath);
            SkillRules sr = t.ToSkillRules();
            BurstRules br = t.ToBurstRules();

            // 시간: 짧게 0.52초 · 길게 0.84초(+ 누르기 0.5초) · 궁극기 1.2초
            Assert.That(sr.Tap.Total, Is.EqualTo(0.52f).Within(1e-3f));
            Assert.That(sr.Hold.Total, Is.EqualTo(0.84f).Within(1e-3f));
            Assert.That(br.Total, Is.EqualTo(1.20f).Within(1e-3f));
            Assert.That(sr.Tap.Cooldown, Is.LessThan(sr.Hold.Cooldown), "the hold form is stronger and waits longer");
            Assert.That(sr.HoldTime, Is.GreaterThan(c.ChargeHoldTime), "skill hold is longer than the charged-attack hold");
            Assert.That(br.Cooldown, Is.GreaterThan(sr.Hold.Cooldown));

            // 허수아비(방어 100)에 피해: 짧게 136 · 길게 96 + 160 · 궁극기 320
            int Hit(float m) => DamageMath.Compute(hero.TotalAttack, m, d.Defense, c.DefenseConstant);
            Assert.That(Hit(sr.Tap.Slash(0).Multiplier), Is.EqualTo(136));
            Assert.That(Hit(sr.Hold.Slash(0).Multiplier) + Hit(sr.Hold.Slash(1).Multiplier), Is.EqualTo(256));
            Assert.That(Hit(br.Slash(0).Multiplier), Is.EqualTo(320));

            // 에너지: 짧게 4번 · 길게 3번이면 가득 (40), 허수아비 처치 5
            Assert.That(sr.Tap.Energy * 4f, Is.GreaterThanOrEqualTo(br.EnergyCost));
            Assert.That(sr.Tap.Energy * 3f, Is.LessThan(br.EnergyCost));
            Assert.That(sr.Hold.Energy * 3f, Is.GreaterThanOrEqualTo(br.EnergyCost));
            Assert.That(d.EnergyOnDefeat, Is.GreaterThan(0f).And.LessThan(sr.Tap.Energy));

            // 경직: 궁극기 한 방(60)으로는 그로기가 안 되고, 궁극기 + 짧게(30) + 콤보 1 · 2타(16)면 된다
            Assert.That(t.BurstPoise(0), Is.LessThan(d.MaxPoise));
            Assert.That(t.BurstPoise(0) + t.SkillPoise(SkillForm.Tap, 0) + w.ComboPoise(0) + w.ComboPoise(1), Is.GreaterThanOrEqualTo(d.MaxPoise));
            Assert.That(t.SkillPoise(SkillForm.Hold, 0) + t.SkillPoise(SkillForm.Hold, 1), Is.EqualTo(60f).Within(Eps));
        }
    }
}
