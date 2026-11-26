using System;
using System.Linq;
using Moa.Core.Combat;
using Moa.Core.Growth;
using NUnit.Framework;

namespace Moa.Tests
{
    /// <summary>
    /// 3-5 성장 규칙 (Core Progression · LevelCurve · SkillCurve · Wallet · SaveSnapshot) — 곡선은 BALANCE 17번과 같은 꼴을 여기서 만든다(에셋 검산은 GrowthAssetTests).
    /// 레벨업은 모자란 만큼 재료(올림) · 남은 경험치 이어짐 · 20 · 40 돌파 · 스킬은 골드 · 무기는 재료 · 모자라면 그대로 · 저장 왕복 · 깨진 값 고치기.
    /// </summary>
    public class GrowthTests
    {
        const float Eps = 1e-4f;

        static GrowthRules Rules()
        {
            var exp = new int[60];
            var mul = new float[60];
            for (int l = 1; l <= 60; l++)
            {
                exp[l - 1] = l < 60 ? 500 + 100 * (l - 1) : 0;
                mul[l - 1] = (float)Math.Round(0.55 + 0.05 * (l - 1), 4);
            }
            var wexp = new int[40];
            var wmul = new float[40];
            for (int l = 1; l <= 40; l++)
            {
                wexp[l - 1] = l < 40 ? 300 + 60 * (l - 1) : 0;
                wmul[l - 1] = (float)Math.Round(1.0 + 0.05 * (l - 1), 4);
            }
            var asc = new[] { new AscensionStep(20, 5000, 0.1f), new AscensionStep(40, 15000, 0.1f) };
            var skill = new SkillCurve(new[] { 1f, 1.075f, 1.15f, 1.25f, 1.325f, 1.4f, 1.5f, 1.6f, 1.7f, 1.8f },
                new[] { 1000, 2000, 3000, 4000, 6000, 8000, 10000, 12000, 15000, 0 });
            return new GrowthRules(new LevelCurve(exp, mul, asc, 10), new LevelCurve(wexp, wmul, null, 1), skill, 1000);
        }

        static Progression New() => new Progression(Rules(), new[] { "hero", "guardian", "wanderer", "sage" });

        [Test]
        public void NewAccount_StartsAtReferenceLevel_StatsAreOne()
        {
            var p = New();
            for (int i = 0; i < p.Count; i++)
            {
                var m = p.Member(i);
                Assert.That(m.Level, Is.EqualTo(10));
                Assert.That(m.Ascension, Is.EqualTo(0));
                Assert.That(m.Exp, Is.EqualTo(0));
                Assert.That(m.SkillLevel, Is.EqualTo(1));
                Assert.That(m.BurstLevel, Is.EqualTo(1));
                Assert.That(m.WeaponLevel, Is.EqualTo(1));
                Assert.That(m.Refinement, Is.EqualTo(1));
                var s = p.Stats(i);
                Assert.That(s.Character, Is.EqualTo(1f), "level 10 = the character asset values");
                Assert.That(s.Weapon, Is.EqualTo(1f));
                Assert.That(s.Skill, Is.EqualTo(1f));
                Assert.That(s.Burst, Is.EqualTo(1f));
                Assert.That(s.ScaleHealth(1000), Is.EqualTo(1000));
            }
            Assert.That(p.Wallet.Get(Currency.Gold), Is.EqualTo(0));
            Assert.That(p.Wallet.Get(Currency.AdventureExp), Is.EqualTo(0));
            Assert.That(p.LevelCap(0), Is.EqualTo(20));
        }

        [Test]
        public void LevelUp_SpendsJustEnoughItems_CarriesExp()
        {
            var p = New();
            p.Wallet.Add(Currency.AdventureExp, 10);
            Assert.That(p.Check(0, GrowthAction.LevelUp, out Currency c, out int cost), Is.EqualTo(GrowthBlock.None));
            Assert.That(c, Is.EqualTo(Currency.AdventureExp));
            Assert.That(cost, Is.EqualTo(2), "1400 exp to 11 = 2 items of 1000");
            Assert.That(p.Try(0, GrowthAction.LevelUp), Is.True);
            Assert.That(p.Member(0).Level, Is.EqualTo(11));
            Assert.That(p.Member(0).Exp, Is.EqualTo(600));
            Assert.That(p.Check(0, GrowthAction.LevelUp, out _, out cost), Is.EqualTo(GrowthBlock.None));
            Assert.That(cost, Is.EqualTo(1), "1500 - 600 = 900 -> 1 item");
            Assert.That(p.Try(0, GrowthAction.LevelUp), Is.True);
            Assert.That(p.Member(0).Level, Is.EqualTo(12));
            Assert.That(p.Member(0).Exp, Is.EqualTo(100));
            Assert.That(p.Wallet.Get(Currency.AdventureExp), Is.EqualTo(7));
            Assert.That(p.Stats(0).Character, Is.EqualTo(1.10f).Within(Eps));
            Assert.That(p.Member(1).Level, Is.EqualTo(10), "other members unchanged");
        }

        [Test]
        public void NotEnough_Blocks_AndChangesNothing()
        {
            var p = New();
            p.Wallet.Add(Currency.AdventureExp, 1);
            int changed = 0;
            p.Changed += _ => changed++;
            Assert.That(p.Try(0, GrowthAction.LevelUp), Is.False);
            Assert.That(p.LastBlock, Is.EqualTo(GrowthBlock.NotEnoughExp));
            Assert.That(p.Member(0).Level, Is.EqualTo(10));
            Assert.That(p.Wallet.Get(Currency.AdventureExp), Is.EqualTo(1));
            Assert.That(p.Try(0, GrowthAction.SkillUp), Is.False);
            Assert.That(p.LastBlock, Is.EqualTo(GrowthBlock.NotEnoughGold));
            Assert.That(p.Try(0, GrowthAction.WeaponUp), Is.True, "1 item is enough for weapon level 2");
            Assert.That(changed, Is.EqualTo(2), "wallet + member once each");
        }

        [Test]
        public void Level20_NeedsAscension_ThenGoldRaisesCapAndStats()
        {
            var p = New();
            p.SetLevel(0, 19);
            p.Wallet.Add(Currency.AdventureExp, 100);
            Assert.That(p.Try(0, GrowthAction.LevelUp), Is.True);
            Assert.That(p.Member(0).Level, Is.EqualTo(20));
            Assert.That(p.Member(0).Exp, Is.EqualTo(0), "at the cap the rest is dropped");
            Assert.That(p.Check(0, GrowthAction.LevelUp), Is.EqualTo(GrowthBlock.NeedAscension));
            Assert.That(p.Check(0, GrowthAction.Ascend, out Currency c, out int cost), Is.EqualTo(GrowthBlock.NotEnoughGold));
            Assert.That(c, Is.EqualTo(Currency.Gold));
            Assert.That(cost, Is.EqualTo(5000));
            Assert.That(p.Stats(0).Character, Is.EqualTo(1.5f).Within(Eps));
            p.Wallet.Add(Currency.Gold, 5000);
            Assert.That(p.Try(0, GrowthAction.Ascend), Is.True);
            Assert.That(p.Member(0).Ascension, Is.EqualTo(1));
            Assert.That(p.LevelCap(0), Is.EqualTo(40));
            Assert.That(p.Stats(0).Character, Is.EqualTo(1.6f).Within(Eps), "ascension +0.10");
            Assert.That(p.Wallet.Get(Currency.Gold), Is.EqualTo(0));
            Assert.That(p.Try(0, GrowthAction.LevelUp), Is.True, "levels again after ascending");
            Assert.That(p.Member(0).Level, Is.EqualTo(21));
        }

        [Test]
        public void Ascend_BelowCap_IsBlocked()
        {
            var p = New();
            p.Wallet.Add(Currency.Gold, 100000);
            Assert.That(p.Try(0, GrowthAction.Ascend), Is.False);
            Assert.That(p.LastBlock, Is.EqualTo(GrowthBlock.NotAtCap));
            Assert.That(p.Wallet.Get(Currency.Gold), Is.EqualTo(100000));
        }

        [Test]
        public void Level40_SecondAscension_Level60_IsMax()
        {
            var p = New();
            p.SetLevel(0, 40);
            Assert.That(p.Member(0).Ascension, Is.EqualTo(1), "level 40 without the second ascension");
            Assert.That(p.Check(0, GrowthAction.LevelUp), Is.EqualTo(GrowthBlock.NeedAscension));
            Assert.That(p.Check(0, GrowthAction.Ascend, out _, out int cost), Is.EqualTo(GrowthBlock.NotEnoughGold));
            Assert.That(cost, Is.EqualTo(15000));
            p.Wallet.Add(Currency.Gold, 15000);
            Assert.That(p.Try(0, GrowthAction.Ascend), Is.True);
            Assert.That(p.LevelCap(0), Is.EqualTo(60));
            p.SetLevel(0, 60);
            Assert.That(p.Member(0).Ascension, Is.EqualTo(2));
            Assert.That(p.Check(0, GrowthAction.LevelUp), Is.EqualTo(GrowthBlock.MaxLevel));
            Assert.That(p.Check(0, GrowthAction.Ascend), Is.EqualTo(GrowthBlock.MaxLevel));
            Assert.That(p.Stats(0).Character, Is.EqualTo(3.7f).Within(Eps));
            p.SetLevel(0, 1);
            Assert.That(p.Stats(0).Character, Is.EqualTo(0.55f).Within(Eps), "below the reference level the stats are lower");
        }

        [Test]
        public void SkillAndBurst_CostGold_UpTo10()
        {
            var p = New();
            p.Wallet.Add(Currency.Gold, 61000);
            for (int i = 0; i < 9; i++) Assert.That(p.Try(0, GrowthAction.SkillUp), Is.True, "skill up " + (i + 2));
            Assert.That(p.Member(0).SkillLevel, Is.EqualTo(10));
            Assert.That(p.Wallet.Get(Currency.Gold), Is.EqualTo(0), "1000 + 2000 + ... + 15000 = 61000");
            Assert.That(p.Check(0, GrowthAction.SkillUp), Is.EqualTo(GrowthBlock.MaxLevel));
            Assert.That(p.Stats(0).Skill, Is.EqualTo(1.8f).Within(Eps));
            Assert.That(p.Stats(0).Burst, Is.EqualTo(1f), "burst level is separate");
            p.Wallet.Add(Currency.Gold, 1000);
            Assert.That(p.Try(0, GrowthAction.BurstUp), Is.True);
            Assert.That(p.Stats(0).Burst, Is.EqualTo(1.075f).Within(Eps));
            Assert.That(p.Stats(0).Character, Is.EqualTo(1f), "skills do not change base stats");
        }

        [Test]
        public void WeaponUp_OneItemCanRaiseSeveralLevels_UpTo40()
        {
            var p = New();
            p.Wallet.Add(Currency.AdventureExp, 1);
            Assert.That(p.Try(0, GrowthAction.WeaponUp), Is.True);
            Assert.That(p.Member(0).WeaponLevel, Is.EqualTo(3), "1000 exp: 300 to 2, 360 to 3, 340 left < 420");
            Assert.That(p.Member(0).WeaponExp, Is.EqualTo(340));
            Assert.That(p.Stats(0).Weapon, Is.EqualTo(1.10f).Within(Eps));
            p.Wallet.Add(Currency.AdventureExp, 1000);
            int guard = 0;
            while (p.Try(0, GrowthAction.WeaponUp) && guard++ < 100) { }
            Assert.That(p.Member(0).WeaponLevel, Is.EqualTo(40));
            Assert.That(p.Member(0).WeaponExp, Is.EqualTo(0));
            Assert.That(p.LastBlock, Is.EqualTo(GrowthBlock.MaxLevel));
            Assert.That(p.Stats(0).Weapon, Is.EqualTo(2.95f).Within(Eps));
            Assert.That(p.Member(0).Level, Is.EqualTo(10), "weapon exp does not level the character");
        }

        [Test]
        public void Rewards_GoToTheWallet()
        {
            var p = New();
            int walletChanges = 0;
            p.Changed += i => { if (i < 0) walletChanges++; };
            p.AddReward(100, 1);
            p.AddReward(500, 5);
            Assert.That(p.Wallet.Get(Currency.Gold), Is.EqualTo(600));
            Assert.That(p.Wallet.Get(Currency.AdventureExp), Is.EqualTo(6));
            Assert.That(p.LastRewardGold, Is.EqualTo(500));
            Assert.That(p.LastRewardExp, Is.EqualTo(5));
            Assert.That(walletChanges, Is.EqualTo(4));
            p.AddReward(0, 0);
            Assert.That(walletChanges, Is.EqualTo(4), "nothing gained - no change");
        }

        [Test]
        public void Snapshot_RoundTrips()
        {
            var p = New();
            p.Wallet.Add(Currency.Gold, 12345);
            p.Wallet.Add(Currency.AdventureExp, 3);
            p.SetLevel(1, 20);
            p.Wallet.Add(Currency.Gold, 5000);
            p.Try(1, GrowthAction.Ascend);
            p.SetSkillLevel(2, GrowthAction.SkillUp, 4);
            p.SetSkillLevel(2, GrowthAction.BurstUp, 7);
            p.SetWeaponLevel(3, 25);
            p.Wallet.Add(Currency.AdventureExp, 2);
            p.Try(0, GrowthAction.LevelUp);
            SaveSnapshot s = p.ToSnapshot();
            Assert.That(s.version, Is.EqualTo(SaveFormat.Version));
            Assert.That(SaveFormat.Validate(s, out _), Is.True);

            var q = New();
            Assert.That(q.Load(s), Is.EqualTo(0), "nothing to fix");
            Assert.That(q.Wallet.Get(Currency.Gold), Is.EqualTo(p.Wallet.Get(Currency.Gold)));
            Assert.That(q.Wallet.Get(Currency.AdventureExp), Is.EqualTo(p.Wallet.Get(Currency.AdventureExp)));
            for (int i = 0; i < p.Count; i++)
            {
                var a = p.Member(i);
                var b = q.Member(i);
                Assert.That(new[] { b.Level, b.Exp, b.Ascension, b.SkillLevel, b.BurstLevel, b.WeaponLevel, b.WeaponExp, b.Refinement },
                    Is.EqualTo(new[] { a.Level, a.Exp, a.Ascension, a.SkillLevel, a.BurstLevel, a.WeaponLevel, a.WeaponExp, a.Refinement }), a.Id);
                Assert.That(q.Stats(i).Character, Is.EqualTo(p.Stats(i).Character));
            }
        }

        [Test]
        public void Load_FixesOutOfRange_KeepsOtherCharacters()
        {
            var s = new SaveSnapshot
            {
                version = SaveFormat.Version,
                wallet = new[] { new CurrencySave { id = "Gold", amount = -50 }, new CurrencySave { id = "Gems", amount = 9 }, new CurrencySave { id = "AdventureExp", amount = 4 } },
                characters = new[]
                {
                    new MemberSave { id = "hero", level = 99, exp = -5, ascension = 7, skillLevel = 0, burstLevel = 12, weaponLevel = 77, weaponExp = 5, refinement = 0 },
                    new MemberSave { id = "guardian", level = 25, exp = 99999, ascension = 0, skillLevel = 3, burstLevel = 3, weaponLevel = 5, weaponExp = 10, refinement = 1 },
                    new MemberSave { id = "elsewhere", level = 33, ascension = 1, skillLevel = 2, burstLevel = 2, weaponLevel = 3, refinement = 1 },
                },
            };
            var p = New();
            Assert.That(p.Load(s), Is.GreaterThan(0));
            Assert.That(p.Wallet.Get(Currency.Gold), Is.EqualTo(0));
            Assert.That(p.Wallet.Get(Currency.AdventureExp), Is.EqualTo(4));
            var hero = p.Member(0);
            Assert.That(new[] { hero.Level, hero.Ascension, hero.Exp, hero.SkillLevel, hero.BurstLevel, hero.WeaponLevel, hero.WeaponExp, hero.Refinement },
                Is.EqualTo(new[] { 60, 2, 0, 1, 10, 40, 0, 1 }));
            var guardian = p.Member(1);
            Assert.That(guardian.Ascension, Is.EqualTo(1), "level 25 needs the first ascension");
            Assert.That(guardian.Exp, Is.EqualTo(p.Rules.Character.ExpToNext(25) - 1));
            Assert.That(p.Member(2).Level, Is.EqualTo(10), "not in the save = new account");
            Assert.That(p.ToSnapshot().characters.Select(m => m.id), Does.Contain("elsewhere"), "characters outside the party are kept");
        }

        [Test]
        public void Validate_RejectsBrokenSnapshots()
        {
            Assert.That(SaveFormat.Validate(null, out _), Is.False);
            Assert.That(SaveFormat.Validate(new SaveSnapshot(), out _), Is.False, "version 0 = no version");
            Assert.That(SaveFormat.Validate(new SaveSnapshot { version = SaveFormat.Version + 1 }, out _), Is.False, "newer format");
            Assert.That(SaveFormat.Validate(new SaveSnapshot { version = 1, wallet = null }, out _), Is.False);
            Assert.That(SaveFormat.Validate(new SaveSnapshot { version = 1, characters = new[] { new MemberSave() } }, out _), Is.False, "character without id");
            Assert.That(SaveFormat.Validate(new SaveSnapshot { version = 1 }, out _), Is.True);
            Assert.Throws<ArgumentException>(() => New().Load(new SaveSnapshot()));
        }

        [Test]
        public void ResetAll_IsANewAccount()
        {
            var p = New();
            p.Wallet.Add(Currency.Gold, 999);
            p.SetLevel(0, 33);
            p.SetWeaponLevel(0, 9);
            p.ResetAll();
            Assert.That(p.Member(0).Level, Is.EqualTo(10));
            Assert.That(p.Member(0).WeaponLevel, Is.EqualTo(1));
            Assert.That(p.Wallet.Get(Currency.Gold), Is.EqualTo(0));
            Assert.That(p.Stats(0).Character, Is.EqualTo(1f));
        }

        [Test]
        public void Ids_MustBeUniqueAndPresent()
        {
            Assert.Throws<ArgumentException>(() => new Progression(Rules(), new[] { "a", "a" }));
            Assert.Throws<ArgumentException>(() => new Progression(Rules(), new[] { "a", "" }));
            Assert.Throws<ArgumentException>(() => new LevelCurve(new[] { 1, 1 }, new[] { 1f, 1f, 1f }, new[] { new AscensionStep(3, 0, 0f) }, 1), "cap at the max level");
        }

        [Test]
        public void Health_SetMax_RaisesCurrentByTheIncrease()
        {
            var h = new Health(1000);
            h.TakeDamage(100);
            h.SetMax(1100);
            Assert.That(h.Max, Is.EqualTo(1100));
            Assert.That(h.Current, Is.EqualTo(1000), "900 + 200");
            h.SetMax(800);
            Assert.That(h.Current, Is.EqualTo(800), "lowered max clamps");
            h.TakeDamage(9999);
            h.SetMax(1500);
            Assert.That(h.IsDead, Is.True, "a downed member stays down");
        }

        [Test]
        public void PartyVitals_SetStats_ChangesHealthAndDefense()
        {
            var rules = new VitalsRules(0.3f, 0.5f, 1f, 0.6f, 0.8f, 0.5f, 0.5f);
            var v = new PartyVitals(rules, new[] { 1000 }, new[] { 100f }, null);
            Assert.That(v.TakeHit(100f, 1.25f, 400f, false).Requested, Is.EqualTo(100), "Grunt on the hero (3-1)");
            v.SetStats(0, 1600, 160f);
            Assert.That(v.Member(0).Max, Is.EqualTo(1600));
            Assert.That(v.Member(0).Current, Is.EqualTo(1500), "900 + 600");
            Assert.That(v.Defense(0), Is.EqualTo(160f));
            Assert.That(v.TakeHit(100f, 1.25f, 400f, false).Requested, Is.EqualTo(DamageMath.Compute(100f, 1.25f, 160f, 400f)));
            Assert.That(DamageMath.Compute(100f, 1.25f, 160f, 400f), Is.EqualTo(89));
        }

        [Test]
        public void MemberStats_ScaleHealth_RoundsHalfUp()
        {
            Assert.That(new MemberStats(1.05f, 1f, 1f, 1f).ScaleHealth(1000), Is.EqualTo(1050));
            Assert.That(new MemberStats(0.55f, 1f, 1f, 1f).ScaleHealth(850), Is.EqualTo(468), "467.5 -> 468");
            Assert.That(MemberStats.Identity.ScaleHealth(1300), Is.EqualTo(1300));
            Assert.That(MemberStats.Identity.ScaleDefense(200f), Is.EqualTo(200f));
        }
    }
}
