using Moa.Core.Combat;
using Moa.Unity.Data;
using NUnit.Framework;
using UnityEditor;

namespace Moa.Tests
{
    /// <summary>
    /// 3-2 수치 에셋 검산: 파티(Party.asset) 교체 연계 칸 · 한손검(Sword.asset) 등장 공격 · 캐릭터 스킬 에셋의 교체 강화가 BALANCE.md 14번과 같고,
    /// "서포터 E → 딜러 교체"가 의미 있는지(수호자 → 떠돌이 콤보 한 번에 Grunt 그로기, 현자 → 떠돌이 피해 +25% · 에너지 +15). 값을 바꾸면 BALANCE.md와 이 테스트도 함께 고친다.
    /// </summary>
    public class SwitchComboAssetTests
    {
        const string PartyPath = "Assets/_Game/Data/Characters/Party.asset";
        const string SwordPath = "Assets/_Game/Data/Weapons/Sword.asset";
        const string CombatPath = "Assets/_Game/Data/Combat/CombatTuning.asset";
        const string DummyPath = "Assets/_Game/Data/Enemies/TrainingDummy.asset";
        const string GruntPath = "Assets/_Game/Data/Enemies/Grunt.asset";
        const float Eps = 1e-4f;
        const float FixedStep = 0.02f;

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

        static int Hit(float attack, float multiplier)
        {
            var c = Load<CombatTuning>(CombatPath);
            var d = Load<DummyTuning>(DummyPath);
            return DamageMath.Compute(attack, multiplier, d.Defense, c.DefenseConstant);
        }

        [Test]
        public void Party_SwitchCombo_MatchesBalance14()
        {
            var p = Load<PartyTuning>(PartyPath);
            Assert.That(p.EntryWindow, Is.EqualTo(0.8f).Within(Eps));
            Assert.That(p.HandoffWindow, Is.EqualTo(3.0f).Within(Eps));
            Assert.That(p.EntryGroggyBonus, Is.EqualTo(1.5f).Within(Eps));
            Assert.That(WholeSteps(p.EntryWindow) && WholeSteps(p.HandoffWindow), Is.True);
            Assert.That(p.EntryWindow, Is.LessThan(p.SwitchCooldown), "the entry window ends before the next switch is possible");
            var r = p.ToRules();
            Assert.That(r.EntryWindow, Is.EqualTo(0.8f).Within(Eps));
            Assert.That(r.HandoffWindow, Is.EqualTo(3.0f).Within(Eps));
        }

        [Test]
        public void Sword_EntrySlash_MatchesBalance14()
        {
            var w = Load<WeaponTuning>(SwordPath);
            Assert.That(w.HasEntry, Is.True);
            var s = w.EntrySlash;
            Assert.That(s.multiplier, Is.EqualTo(1.20f).Within(Eps));
            Assert.That(s.windup, Is.EqualTo(0.08f).Within(Eps));
            Assert.That(s.active, Is.EqualTo(0.10f).Within(Eps));
            Assert.That(s.recovery, Is.EqualTo(0.32f).Within(Eps));
            Assert.That(s.lunge, Is.EqualTo(2.0f).Within(Eps));
            Assert.That(s.poise, Is.EqualTo(25f).Within(Eps));
            Assert.That(w.EntryHitRange, Is.EqualTo(2.6f).Within(Eps));
            Assert.That(w.EntryHitAngle, Is.EqualTo(150f).Within(Eps));
            Assert.That(WholeSteps(s.windup) && WholeSteps(s.active) && WholeSteps(s.recovery), Is.True);
            Assert.That(w.ToEntrySlashes().Length, Is.EqualTo(1));
        }

        [Test]
        public void EntryDamage_96_106_Groggy187_206()
        {
            var p = Load<PartyTuning>(PartyPath);
            var w = Load<WeaponTuning>(SwordPath);
            var d = Load<DummyTuning>(DummyPath);
            float m = w.EntrySlash.multiplier;
            Assert.That(Hit(100f, m), Is.EqualTo(96), "hero (100) on the dummy; combo hit 1 is 36");
            Assert.That(Hit(110f, m), Is.EqualTo(106), "wanderer (110)");
            // PlayerCombat.Report와 같은 곱 순서: 배율 × 받는 피해(그로기) × 버프(1) × 그로기 연계
            Assert.That(Hit(100f, m * d.GroggyDamageTaken * 1f * p.EntryGroggyBonus), Is.EqualTo(187));
            Assert.That(Hit(110f, m * d.GroggyDamageTaken * 1f * p.EntryGroggyBonus), Is.EqualTo(206));
            Assert.That(Hit(100f, m * d.GroggyDamageTaken), Is.EqualTo(125), "without the combo bonus");
        }

        [Test]
        public void Handoffs_MatchBalance14()
        {
            var p = Load<PartyTuning>(PartyPath);
            // 칸: 피해 · 경직 · 보호막 · 에너지 · 시간
            var want = new[]
            {
                new { dmg = 0.15f, poise = 0f, shield = 0, energy = 0f, time = 6f },
                new { dmg = 0f, poise = 0.30f, shield = 300, energy = 0f, time = 8f },
                new { dmg = 0.10f, poise = 0f, shield = 0, energy = 0f, time = 5f },
                new { dmg = 0.25f, poise = 0f, shield = 0, energy = 15f, time = 8f },
            };
            for (int i = 0; i < p.MemberCount; i++)
            {
                var s = p.GetMember(i).Skills;
                string at = s.name;
                Assert.That(s.HandoffDamageBonus, Is.EqualTo(want[i].dmg).Within(Eps), at);
                Assert.That(s.HandoffPoiseBonus, Is.EqualTo(want[i].poise).Within(Eps), at);
                Assert.That(s.HandoffShield, Is.EqualTo(want[i].shield), at);
                Assert.That(s.HandoffEnergy, Is.EqualTo(want[i].energy).Within(Eps), at);
                Assert.That(s.HandoffDuration, Is.EqualTo(want[i].time).Within(Eps), at);
                Assert.That(WholeSteps(s.HandoffDuration), Is.True, at);
                Assert.That(s.ToHandoffRules().HasEffect, Is.True, at);
                Assert.That(s.HandoffDuration, Is.GreaterThan(p.HandoffWindow), at + ": the buff outlasts the window");
            }
        }

        [Test]
        public void SupportToDealer_IsWorthIt()
        {
            var p = Load<PartyTuning>(PartyPath);
            var guardian = p.GetMember(1).Skills;
            var sage = p.GetMember(3).Skills;
            var wanderer = p.GetMember(2);
            var w = wanderer.Weapon;
            var grunt = Load<EnemyTuning>(GruntPath);

            // 수호자 E → 떠돌이: 콤보 경직 54 → 70.2 — Grunt(60)가 콤보 한 번에 그로기 (버프 없이는 54로 안 됨)
            float poise = 0f;
            for (int i = 0; i < w.StepCount; i++) poise += w.ComboPoise(i);
            Assert.That(poise, Is.EqualTo(54f).Within(Eps));
            Assert.That(poise, Is.LessThan(grunt.MaxPoise));
            Assert.That(poise * (1f + guardian.HandoffPoiseBonus), Is.GreaterThanOrEqualTo(grunt.MaxPoise));

            // 현자 E → 떠돌이: 콤보 40 · 40 · 53 · 57 · 75 (265) → 49 · 49 · 66 · 71 · 93 (328), 에너지 +15 / 50
            float buff = 1f + sage.HandoffDamageBonus;
            var plain = new int[w.StepCount];
            var buffed = new int[w.StepCount];
            for (int i = 0; i < w.StepCount; i++)
            {
                plain[i] = Hit(wanderer.TotalAttack, w.GetStep(i).multiplier * 1f * 1f * 1f);
                buffed[i] = Hit(wanderer.TotalAttack, w.GetStep(i).multiplier * 1f * buff * 1f);
            }
            Assert.That(plain, Is.EqualTo(new[] { 40, 40, 53, 57, 75 }), "no buff = 2-6 numbers");
            Assert.That(buffed, Is.EqualTo(new[] { 49, 49, 66, 71, 93 }));
            Assert.That(sage.HandoffEnergy / wanderer.Skills.BurstEnergyCost, Is.EqualTo(0.3f).Within(Eps), "15 of 50");
        }
    }
}
