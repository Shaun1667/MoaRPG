using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Moa.Core.Combat;
using Moa.Core.Growth;
using Moa.Unity.Combat;
using Moa.Unity.Data;
using Moa.Unity.Save;
using Moa.Unity.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Moa.Tests
{
    /// <summary>
    /// 3-5 성장 에셋 · 저장 파일 검산: Data/Growth/Growth.asset이 BALANCE 17번과 같고, 새 계정이면 2-1 ~ 3-4 숫자 그대로(배율 1),
    /// 성장하면 피해 · 체력이 오르고, 캐릭터 저장 id · 적 처치 보상이 있고, 저장 파일(SaveStore)이 왕복 · 깨지면 백업 · 새로 시작하는지 — 임시 경로만 쓴다.
    /// 화면(GrowthScreen)은 재화가 모자라면 버튼을 끈다. 값을 바꾸면 BALANCE.md와 이 테스트도 함께 고친다.
    /// </summary>
    public class GrowthAssetTests
    {
        const string GrowthPath = "Assets/_Game/Data/Growth/Growth.asset";
        const string PartyPath = "Assets/_Game/Data/Characters/Party.asset";
        const string CombatPath = "Assets/_Game/Data/Combat/CombatTuning.asset";
        const string DummyPath = "Assets/_Game/Data/Enemies/TrainingDummy.asset";
        const float Eps = 1e-4f;

        static T Load<T>(string path) where T : UnityEngine.Object
        {
            var t = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.That(t, Is.Not.Null, "missing " + path);
            return t;
        }

        static int Hit(float attack, float multiplier)
        {
            var c = Load<CombatTuning>(CombatPath);
            var d = Load<DummyTuning>(DummyPath);
            return DamageMath.Compute(attack, multiplier, d.Defense, c.DefenseConstant);
        }

        static string[] PartyIds()
        {
            var p = Load<PartyTuning>(PartyPath);
            var ids = new string[p.MemberCount];
            for (int i = 0; i < ids.Length; i++) ids[i] = p.GetMember(i).SaveId;
            return ids;
        }

        [Test]
        public void Growth_MatchesBalance17()
        {
            var g = Load<GrowthTuning>(GrowthPath);
            Assert.That(g.CharacterLevelCount, Is.EqualTo(60), "character level 1 ~ 60");
            Assert.That(g.CharacterStartLevel, Is.EqualTo(10), "new accounts start at the reference level");
            Assert.That(g.GetCharacterLevel(10).multiplier, Is.EqualTo(1f), "reference level = the asset values, exactly");
            for (int l = 1; l <= 60; l++)
            {
                var row = g.GetCharacterLevel(l);
                Assert.That(row.multiplier, Is.EqualTo(0.55f + 0.05f * (l - 1)).Within(Eps), "level " + l + " multiplier");
                if (l < 60) Assert.That(row.expToNext, Is.EqualTo(500 + 100 * (l - 1)), "level " + l + " exp");
            }
            Assert.That(g.AscensionCount, Is.EqualTo(2));
            Assert.That(new[] { g.GetAscension(0).levelCap, g.GetAscension(0).goldCost }, Is.EqualTo(new[] { 20, 5000 }));
            Assert.That(new[] { g.GetAscension(1).levelCap, g.GetAscension(1).goldCost }, Is.EqualTo(new[] { 40, 15000 }));
            Assert.That(g.GetAscension(0).statBonus, Is.EqualTo(0.1f).Within(Eps));
            Assert.That(g.GetAscension(1).statBonus, Is.EqualTo(0.1f).Within(Eps));

            Assert.That(g.WeaponLevelCount, Is.EqualTo(40), "weapon level 1 ~ 40 (no weapon ascension yet)");
            Assert.That(g.WeaponStartLevel, Is.EqualTo(1));
            Assert.That(g.GetWeaponLevel(1).multiplier, Is.EqualTo(1f), "weapon level 1 = the weapon asset value");
            for (int l = 1; l <= 40; l++)
            {
                var row = g.GetWeaponLevel(l);
                Assert.That(row.multiplier, Is.EqualTo(1f + 0.05f * (l - 1)).Within(Eps), "weapon level " + l);
                if (l < 40) Assert.That(row.expToNext, Is.EqualTo(300 + 60 * (l - 1)), "weapon level " + l + " exp");
            }

            Assert.That(g.SkillLevelCount, Is.EqualTo(10), "skill level 1 ~ 10");
            float[] mul = { 1f, 1.075f, 1.15f, 1.25f, 1.325f, 1.4f, 1.5f, 1.6f, 1.7f, 1.8f };
            int[] gold = { 1000, 2000, 3000, 4000, 6000, 8000, 10000, 12000, 15000 };
            Assert.That(g.GetSkillLevel(1).multiplier, Is.EqualTo(1f), "skill level 1 = the skill asset values");
            for (int l = 1; l <= 10; l++) Assert.That(g.GetSkillLevel(l).multiplier, Is.EqualTo(mul[l - 1]).Within(Eps), "skill level " + l);
            for (int l = 1; l <= 9; l++) Assert.That(g.GetSkillLevel(l).goldToNext, Is.EqualTo(gold[l - 1]), "skill gold " + l);
            Assert.That(g.ExpPerItem, Is.EqualTo(1000));

            var r = g.ToRules();
            Assert.That(r.Character.StartPhase, Is.EqualTo(0));
            Assert.That(r.Character.Multiplier(60, 2), Is.EqualTo(3.7f).Within(Eps));
        }

        [Test]
        public void Growth_Totals_AreReachable()
        {
            var r = Load<GrowthTuning>(GrowthPath).ToRules();
            int exp = 0;
            for (int l = 10; l < 20; l++) exp += r.Character.ExpToNext(l);
            Assert.That(exp, Is.EqualTo(18500), "start 10 -> 20 = 18500 exp (about 19 Grunts)");
            int all = 0;
            for (int l = 1; l < 60; l++) all += r.Character.ExpToNext(l);
            Assert.That(all, Is.EqualTo(200600));
            int w = 0;
            for (int l = 1; l < 40; l++) w += r.Weapon.ExpToNext(l);
            Assert.That(w, Is.EqualTo(56160), "weapon 1 -> 40");
            int skill = 0;
            for (int l = 1; l < 10; l++) skill += r.Skill.CostToNext(l);
            Assert.That(skill, Is.EqualTo(61000));
        }

        [Test]
        public void Characters_HaveUniqueSaveIds()
        {
            var ids = PartyIds();
            var seen = new HashSet<string>();
            foreach (var id in ids)
            {
                Assert.That(id, Is.Not.Null.And.Not.Empty);
                Assert.That(Regex.IsMatch(id, "^[a-z0-9_]+$"), Is.True, id + ": lowercase letters, digits, _");
                Assert.That(seen.Add(id), Is.True, id + " twice");
            }
            Assert.That(ids, Is.EqualTo(new[] { "sword_hero", "sword_guardian", "sword_wanderer", "sword_sage" }));
        }

        [Test]
        public void Enemies_DropRewards_MatchBalance17()
        {
            var want = new[] { ("Grunt", 100, 1), ("Archer", 100, 1), ("Brute", 500, 5) };
            foreach (var (name, gold, exp) in want)
            {
                var t = Load<EnemyTuning>("Assets/_Game/Data/Enemies/" + name + ".asset");
                Assert.That(t.GoldOnDefeat, Is.EqualTo(gold), name + " gold");
                Assert.That(t.ExpItemsOnDefeat, Is.EqualTo(exp), name + " exp material");
            }
        }

        PlayerCombat NewPartyPlayer(GameObject go)
        {
            var pc = go.AddComponent<PlayerCombat>();
            var so = new SerializedObject(pc);
            so.FindProperty("weapon").objectReferenceValue = Load<WeaponTuning>("Assets/_Game/Data/Weapons/Sword.asset");
            so.FindProperty("tuning").objectReferenceValue = Load<CombatTuning>(CombatPath);
            so.FindProperty("party").objectReferenceValue = Load<PartyTuning>(PartyPath);
            so.ApplyModifiedPropertiesWithoutUndo();
            pc.Bind(null);
            return pc;
        }

        [Test]
        public void NewAccount_KeepsTheNumbersOf2_1To3_4()
        {
            var growth = new Progression(Load<GrowthTuning>(GrowthPath).ToRules(), PartyIds());
            var go = new GameObject("GrowthAssetTests_Player");
            try
            {
                var pc = NewPartyPlayer(go);
                var party = Load<PartyTuning>(PartyPath);
                int[] hp = { 1000, 1300, 850, 900 };
                for (int i = 0; i < growth.Count; i++)
                {
                    MemberStats s = growth.Stats(i);
                    pc.SetGrowth(i, s);
                    var c = party.GetMember(i);
                    Assert.That(s.ScaleHealth(c.MaxHealth), Is.EqualTo(hp[i]), c.name + " health (BALANCE 13)");
                    Assert.That(s.ScaleDefense(c.Defense), Is.EqualTo(c.Defense), c.name + " defense");
                    Assert.That(pc.MemberAttack(i), Is.EqualTo(c.TotalAttack).Within(Eps), c.name + " attack");
                }
                Assert.That(pc.Attack, Is.EqualTo(100f).Within(Eps));
                var w = pc.Weapon;
                var combo = new int[w.StepCount];
                for (int i = 0; i < combo.Length; i++) combo[i] = Hit(pc.Attack, w.GetStep(i).multiplier);
                Assert.That(combo, Is.EqualTo(new[] { 36, 36, 48, 52, 68 }));
                Assert.That(Hit(pc.Attack, pc.Skills.Tap.slashes[0].multiplier * growth.Stats(0).Skill), Is.EqualTo(136));
                Assert.That(Hit(pc.Attack, pc.Skills.GetBurstSlash(0).multiplier * growth.Stats(0).Burst), Is.EqualTo(320));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void Growth_RaisesDamageAndHealth()
        {
            var growth = new Progression(Load<GrowthTuning>(GrowthPath).ToRules(), PartyIds());
            var go = new GameObject("GrowthAssetTests_Player");
            try
            {
                var pc = NewPartyPlayer(go);
                growth.SetLevel(0, 20);
                growth.Wallet.Add(Currency.Gold, 5000 + 1000);
                Assert.That(growth.Try(0, GrowthAction.Ascend), Is.True);
                Assert.That(growth.Try(0, GrowthAction.SkillUp), Is.True);
                MemberStats s = growth.Stats(0);
                pc.SetGrowth(0, s);
                Assert.That(s.Character, Is.EqualTo(1.6f).Within(Eps));
                Assert.That(pc.CharacterAttack, Is.EqualTo(96f).Within(Eps), "60 x 1.6");
                Assert.That(pc.Attack, Is.EqualTo(136f).Within(Eps), "96 + sword 40");
                Assert.That(Hit(pc.Attack, pc.Weapon.GetStep(0).multiplier), Is.EqualTo(49), "combo 1: 36 -> 49");
                Assert.That(Hit(pc.Attack, pc.Skills.Tap.slashes[0].multiplier * s.Skill), Is.EqualTo(199), "E level 2: 136 -> 199");
                Assert.That(s.ScaleHealth(1000), Is.EqualTo(1600));

                growth.SetWeaponLevel(0, 10);
                pc.SetGrowth(0, growth.Stats(0));
                Assert.That(pc.WeaponAttack, Is.EqualTo(58f).Within(Eps), "40 x 1.45");
                Assert.That(pc.Attack, Is.EqualTo(154f).Within(Eps));
                Assert.That(pc.MemberAttack(1), Is.EqualTo(100f).Within(Eps), "other members unchanged");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        static string TempPath() => Path.Combine(Path.GetTempPath(), "moa_growth_test_" + Guid.NewGuid().ToString("N"), SaveStore.FileName);

        static void CleanUp(string path)
        {
            string dir = Path.GetDirectoryName(path);
            if (dir != null && Directory.Exists(dir) && dir.StartsWith(Path.GetTempPath(), StringComparison.OrdinalIgnoreCase) && dir.Contains("moa_growth_test_"))
                Directory.Delete(dir, true);
        }

        [Test]
        public void SaveStore_RoundTrips_OnATempPath()
        {
            string path = TempPath();
            try
            {
                var store = new SaveStore(path);
                Assert.That(store.Load(out var none), Is.EqualTo(SaveLoadStatus.Missing));
                Assert.That(none, Is.Null);
                var p = new Progression(Load<GrowthTuning>(GrowthPath).ToRules(), PartyIds());
                p.Wallet.Add(Currency.Gold, 4321);
                p.Wallet.Add(Currency.AdventureExp, 7);
                p.SetLevel(2, 37);
                p.SetSkillLevel(3, GrowthAction.BurstUp, 6);
                Assert.That(store.Save(p.ToSnapshot()), Is.True, store.LastError);
                Assert.That(File.Exists(path), Is.True);
                Assert.That(File.Exists(path + ".tmp"), Is.False, "temp file replaced");
                string text = File.ReadAllText(path);
                Assert.That(text, Does.Contain("\"version\": " + SaveFormat.Version));
                Assert.That(text, Does.Contain("sword_wanderer"));
                // 두 번째 저장은 바꿔 끼우기
                p.Wallet.Add(Currency.Gold, 1);
                Assert.That(store.Save(p.ToSnapshot()), Is.True, store.LastError);
                Assert.That(store.SaveCount, Is.EqualTo(2));

                var q = new Progression(Load<GrowthTuning>(GrowthPath).ToRules(), PartyIds());
                Assert.That(new SaveStore(path).Load(out var snap), Is.EqualTo(SaveLoadStatus.Loaded));
                Assert.That(q.Load(snap), Is.EqualTo(0));
                Assert.That(q.Wallet.Get(Currency.Gold), Is.EqualTo(4322));
                Assert.That(q.Wallet.Get(Currency.AdventureExp), Is.EqualTo(7));
                Assert.That(q.Member(2).Level, Is.EqualTo(37));
                Assert.That(q.Member(2).Ascension, Is.EqualTo(1));
                Assert.That(q.Member(3).BurstLevel, Is.EqualTo(6));
            }
            finally { CleanUp(path); }
        }

        [Test]
        public void SaveStore_Broken_BacksUpAndStartsFromZero()
        {
            string path = TempPath();
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                foreach (string bad in new[] { "{not json", "", "{}", "{\"version\": 99, \"wallet\": [], \"characters\": []}" })
                {
                    File.WriteAllText(path, bad);
                    var store = new SaveStore(path);
                    Assert.That(store.Load(out var snap), Is.EqualTo(SaveLoadStatus.Broken), "'" + bad + "'");
                    Assert.That(snap, Is.Null);
                    Assert.That(store.LastBackupPath, Is.Not.Null, "backup made");
                    Assert.That(File.Exists(store.LastBackupPath), Is.True);
                    Assert.That(File.ReadAllText(store.LastBackupPath), Is.EqualTo(bad), "backup keeps the broken content");
                    Assert.That(File.Exists(path), Is.False, "the broken file is moved away");
                    Assert.That(store.Load(out _), Is.EqualTo(SaveLoadStatus.Missing), "next start = new account");
                }
                Assert.That(Directory.GetFiles(Path.GetDirectoryName(path), "*.broken-*.json").Length, Is.EqualTo(4), "one backup per broken file");
            }
            finally { CleanUp(path); }
        }

        [Test]
        public void Screen_BlocksButtons_WhenCurrencyIsShort()
        {
            var growth = new Progression(Load<GrowthTuning>(GrowthPath).ToRules(), PartyIds());
            var canvas = new GameObject("GrowthAssetTests_Canvas", typeof(RectTransform), typeof(Canvas));
            var go = new GameObject("GrowthAssetTests_Player");
            try
            {
                var pc = NewPartyPlayer(go);
                var screen = GrowthScreen.CreateIn(canvas.transform, 100, new Vector2(1680f, 860f), 40f, 20f, new Vector2(44f, 8f), 100f, 40, 24, 22,
                    Color.black, Color.gray, Color.clear, Color.blue, Color.gray, Color.white, Color.black);
                screen.Bind(growth, pc, null, null, null);
                Assert.That(screen.Open, Is.False);
                screen.SetOpen(true);
                Assert.That(screen.Open, Is.True);
                for (int i = 0; i < 4; i++)
                    foreach (GrowthAction a in Enum.GetValues(typeof(GrowthAction)))
                        Assert.That(screen.ButtonEnabled(i, a), Is.False, "slot " + (i + 1) + " " + a + " with no currency");
                Assert.That(screen.Press(0, GrowthAction.LevelUp), Is.False);
                Assert.That(screen.LastBlock, Is.EqualTo(GrowthBlock.NotEnoughExp));

                growth.Wallet.Add(Currency.AdventureExp, 2);
                screen.Refresh();
                Assert.That(screen.ButtonEnabled(0, GrowthAction.LevelUp), Is.True);
                Assert.That(screen.ButtonEnabled(0, GrowthAction.WeaponUp), Is.True);
                Assert.That(screen.ButtonEnabled(0, GrowthAction.SkillUp), Is.False, "skills need gold");
                Assert.That(screen.ButtonEnabled(0, GrowthAction.Ascend), Is.False, "not at the cap");
                Assert.That(screen.Press(0, GrowthAction.LevelUp), Is.True);
                Assert.That(growth.Member(0).Level, Is.EqualTo(11));
                screen.Refresh();
                Assert.That(screen.ButtonEnabled(0, GrowthAction.LevelUp), Is.False, "items spent");
                screen.SetOpen(false);
                Assert.That(screen.Open, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                UnityEngine.Object.DestroyImmediate(canvas);
            }
        }
    }
}
