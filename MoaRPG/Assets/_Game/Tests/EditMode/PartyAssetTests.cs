using System.Collections.Generic;
using Moa.Core.Combat;
using Moa.Unity.Data;
using NUnit.Framework;
using UnityEditor;

namespace Moa.Tests
{
    /// <summary>
    /// 2-6 수치 에셋 검산: 파티(Data/Characters/Party.asset) · 캐릭터 4명이 BALANCE.md 12번과 같고,
    /// 1번 캐릭터가 2-1 ~ 2-5의 한손검 영웅과 같은지 (콤보 36 · 36 · 48 · 52 · 68, E 136, Q 320). 값을 바꾸면 BALANCE.md와 이 테스트도 함께 고친다.
    /// </summary>
    public class PartyAssetTests
    {
        const string PartyPath = "Assets/_Game/Data/Characters/Party.asset";
        const string SwordPath = "Assets/_Game/Data/Weapons/Sword.asset";
        const string HeroSkillsPath = "Assets/_Game/Data/Skills/SwordHero.asset";
        const string CombatPath = "Assets/_Game/Data/Combat/CombatTuning.asset";
        const string DummyPath = "Assets/_Game/Data/Enemies/TrainingDummy.asset";
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
        public void Party_MatchesBalance12()
        {
            var p = Load<PartyTuning>(PartyPath);
            Assert.That(p.SwitchCooldown, Is.EqualTo(1.0f).Within(Eps), "Genshin: 1 s");
            Assert.That(WholeSteps(p.SwitchCooldown), Is.True);
            Assert.That(p.BenchEnergyRatio, Is.EqualTo(0.6f).Within(Eps));
            Assert.That(p.MemberCount, Is.EqualTo(PartyTuning.MaxMembers));
            var seen = new HashSet<CharacterData>();
            for (int i = 0; i < p.MemberCount; i++)
            {
                Assert.That(p.GetMember(i), Is.Not.Null, "slot " + (i + 1));
                Assert.That(seen.Add(p.GetMember(i)), Is.True, "the same character twice in the party");
            }
            Assert.That(Load<DummyTuning>(DummyPath).EnergyOnDefeat * p.BenchEnergyRatio, Is.EqualTo(3f).Within(Eps), "off-field members get 3 of 5");
        }

        [Test]
        public void Characters_AreComplete_AndLookDifferent()
        {
            var p = Load<PartyTuning>(PartyPath);
            var colors = new List<UnityEngine.Color>();
            var keys = new HashSet<string>();
            for (int i = 0; i < p.MemberCount; i++)
            {
                var c = p.GetMember(i);
                string at = c.name;
                Assert.That(AssetDatabase.GetAssetPath(c), Does.StartWith("Assets/_Game/Data/Characters/"), at);
                Assert.That(c.NameKey, Does.StartWith("character."), at + " name key");
                Assert.That(keys.Add(c.NameKey), Is.True, at + " name key is unique");
                Assert.That(c.Weapon, Is.Not.Null, at + " weapon");
                Assert.That(c.Skills, Is.Not.Null, at + " skills");
                Assert.That(c.BaseAttack, Is.GreaterThanOrEqualTo(0f), at);
                Assert.That(c.Color.a, Is.EqualTo(1f).Within(Eps), at + " body color is opaque");
                foreach (var other in colors)
                {
                    float d = UnityEngine.Mathf.Abs(other.r - c.Color.r) + UnityEngine.Mathf.Abs(other.g - c.Color.g) + UnityEngine.Mathf.Abs(other.b - c.Color.b);
                    Assert.That(d, Is.GreaterThan(0.3f), at + " color is close to another member");
                }
                colors.Add(c.Color);

                SkillTuning s = c.Skills;
                Assert.That(s.BurstEnergyCost, Is.GreaterThan(0f), at + " burst cost");
                Assert.That(WholeSteps(s.SkillHoldTime) && WholeSteps(s.Tap.cooldown) && WholeSteps(s.Hold.cooldown) && WholeSteps(s.BurstCooldown), Is.True, at + " times = whole fixed steps");
                Assert.That(s.Tap.energy * 7f, Is.GreaterThanOrEqualTo(s.BurstEnergyCost), at + ": at most seven taps fill the burst");
            }
        }

        [Test]
        public void Slot1_IsTheSwordHero_Of2_1To2_5()
        {
            var p = Load<PartyTuning>(PartyPath);
            var hero = p.GetMember(0);
            Assert.That(hero.Weapon, Is.SameAs(Load<WeaponTuning>(SwordPath)));
            Assert.That(hero.Skills, Is.SameAs(Load<SkillTuning>(HeroSkillsPath)));
            Assert.That(hero.BaseAttack, Is.EqualTo(0f), "attack = 0 + weapon 100, as before");
            Assert.That(hero.TotalAttack, Is.EqualTo(100f).Within(Eps));
            var w = hero.Weapon;
            var combo = new int[w.StepCount];
            for (int i = 0; i < combo.Length; i++) combo[i] = Hit(hero.TotalAttack, w.GetStep(i).multiplier);
            Assert.That(combo, Is.EqualTo(new[] { 36, 36, 48, 52, 68 }));
            Assert.That(Hit(hero.TotalAttack, hero.Skills.Tap.slashes[0].multiplier), Is.EqualTo(136));
            Assert.That(Hit(hero.TotalAttack, hero.Skills.GetBurstSlash(0).multiplier), Is.EqualTo(320));
        }

        [Test]
        public void OtherMembers_MatchBalance12()
        {
            var p = Load<PartyTuning>(PartyPath);
            // 칸: 공격력 · E 짧게 쿨 · 에너지 · 궁극기 비용 · 쿨 → 허수아비에 E · Q
            var want = new[]
            {
                new { attack = 100f, tapCd = 4f, tapEnergy = 10f, cost = 60f, burstCd = 15f, e = 112, q = 272 },
                new { attack = 110f, tapCd = 6f, tapEnergy = 8f, cost = 50f, burstCd = 12f, e = 176, q = 405 },
                new { attack = 100f, tapCd = 5f, tapEnergy = 14f, cost = 40f, burstCd = 15f, e = 120, q = 240 },
            };
            for (int i = 0; i < want.Length; i++)
            {
                var c = p.GetMember(i + 1);
                string at = c.name;
                Assert.That(c.TotalAttack, Is.EqualTo(want[i].attack).Within(Eps), at);
                Assert.That(c.Skills.Tap.cooldown, Is.EqualTo(want[i].tapCd).Within(Eps), at);
                Assert.That(c.Skills.Tap.energy, Is.EqualTo(want[i].tapEnergy).Within(Eps), at);
                Assert.That(c.Skills.BurstEnergyCost, Is.EqualTo(want[i].cost).Within(Eps), at);
                Assert.That(c.Skills.BurstCooldown, Is.EqualTo(want[i].burstCd).Within(Eps), at);
                Assert.That(Hit(c.TotalAttack, c.Skills.Tap.slashes[0].multiplier), Is.EqualTo(want[i].e), at + " E");
                Assert.That(Hit(c.TotalAttack, c.Skills.GetBurstSlash(0).multiplier), Is.EqualTo(want[i].q), at + " Q");
            }
            // 떠돌이(공격력 110) 콤보
            var wanderer = p.GetMember(2);
            var combo = new int[wanderer.Weapon.StepCount];
            for (int i = 0; i < combo.Length; i++) combo[i] = Hit(wanderer.TotalAttack, wanderer.Weapon.GetStep(i).multiplier);
            Assert.That(combo, Is.EqualTo(new[] { 40, 40, 53, 57, 75 }));
        }
    }
}
