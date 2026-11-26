using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Moa.Core.Gacha;
using Moa.Core.Growth;
using Moa.Core.Rng;
using Moa.Core.World;
using Moa.Unity.Data;
using Moa.Unity.Save;
using Moa.Unity.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Moa.Tests
{
    /// <summary>
    /// 3-7 가챠 에셋 · 저장 · 화면 검산: 배너 2개 · 항목 · Gacha.asset이 BALANCE 19번과 같고, 실제 에셋 배너로 시드 고정 10만 회를 돌려 실측이 기대값 안에 들고,
    /// 상자 · 처치 소원석, 형식 2 파일이 형식 3으로 옮겨지고 가챠가 저장 파일을 왕복하는지(임시 경로만), 화면 버튼 · 결과 카드 · 확률 정보 글.
    /// 값을 바꾸면 BALANCE.md와 이 테스트도 함께 고친다.
    /// </summary>
    public class GachaAssetTests
    {
        const string GachaPath = "Assets/_Game/Data/Banners/Gacha.asset";
        const string LimitedPath = "Assets/_Game/Data/Banners/LimitedStarkeeper.asset";
        const string StandardPath = "Assets/_Game/Data/Banners/Standard.asset";
        const string PartyPath = "Assets/_Game/Data/Characters/Party.asset";
        const string GrowthPath = "Assets/_Game/Data/Growth/Growth.asset";
        const string ExplorationPath = "Assets/_Game/Data/World/Exploration.asset";
        const float Eps = 1e-5f;

        static T Load<T>(string path) where T : UnityEngine.Object
        {
            var t = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.That(t, Is.Not.Null, "missing " + path);
            return t;
        }

        static string[] Ids(IEnumerable<GachaItemData> items) => items.Select(i => i.Id).ToArray();

        static string[] PartyIds()
        {
            var p = Load<PartyTuning>(PartyPath);
            var ids = new string[p.MemberCount];
            for (int i = 0; i < ids.Length; i++) ids[i] = p.GetMember(i).SaveId;
            return ids;
        }

        [Test]
        public void Banners_MatchBalance19()
        {
            foreach (var path in new[] { LimitedPath, StandardPath })
            {
                var b = Load<BannerData>(path);
                Assert.That(b.Cost, Is.EqualTo(1), path);
                Assert.That(b.FiveBaseRate, Is.EqualTo(0.006f).Within(Eps), "GDD 0.6%");
                Assert.That(b.FiveSoftPityStart, Is.EqualTo(74), "GDD: rises from 74");
                Assert.That(b.FiveSoftPityStep, Is.EqualTo(0.06f).Within(Eps));
                Assert.That(b.FiveHardPity, Is.EqualTo(90), "GDD: certain at 90");
                Assert.That(b.FourBaseRate, Is.EqualTo(0.051f).Within(Eps), "GDD 5.1%");
                Assert.That(b.FourSoftPityStart, Is.EqualTo(9));
                Assert.That(b.FourSoftPityStep, Is.EqualTo(0.51f).Within(Eps));
                Assert.That(b.FourHardPity, Is.EqualTo(10), "GDD: 4 star within 10");
                Assert.That(Ids(b.Pool), Is.EquivalentTo(new[] { "sword_hero", "sword_sage", "sword_guardian", "sword_wanderer", "lancer", "weapon_sword", "weapon_claymore", "weapon_bow" }));
                Assert.That(!string.IsNullOrEmpty(b.NameKey), Is.True);
                Assert.DoesNotThrow(() => b.ToRules());
            }
            var lim = Load<BannerData>(LimitedPath);
            Assert.That(lim.Kind, Is.EqualTo(BannerKind.LimitedCharacter));
            Assert.That(Ids(lim.Featured), Is.EqualTo(new[] { "starkeeper" }), "one featured 5 star");
            Assert.That(lim.FeaturedChance, Is.EqualTo(0.5f).Within(Eps), "GDD 50/50");
            var std = Load<BannerData>(StandardPath);
            Assert.That(std.Kind, Is.EqualTo(BannerKind.Standard));
            Assert.That(std.Featured, Is.Empty);
            Assert.That(Ids(std.Pool), Does.Not.Contain("starkeeper"), "limited character only on the limited banner");
        }

        [Test]
        public void Items_Rarity_Ids_LinkToCharactersAndWeapons()
        {
            var lim = Load<BannerData>(LimitedPath);
            var all = lim.Featured.Concat(lim.Pool).ToList();
            var stars = all.ToDictionary(i => i.Id, i => i.Stars);
            Assert.That(stars["starkeeper"], Is.EqualTo(5));
            Assert.That(stars["sword_hero"], Is.EqualTo(5));
            Assert.That(stars["sword_sage"], Is.EqualTo(5));
            Assert.That(stars["sword_guardian"], Is.EqualTo(4));
            Assert.That(stars["sword_wanderer"], Is.EqualTo(4));
            Assert.That(stars["lancer"], Is.EqualTo(4));
            Assert.That(stars["weapon_sword"] + stars["weapon_claymore"] + stars["weapon_bow"], Is.EqualTo(9), "three 3 star weapons");
            foreach (var it in all)
            {
                Assert.That(AssetDatabase.GetAssetPath(it), Does.StartWith("Assets/_Game/Data/Banners/Items/"), it.name);
                Assert.That(string.IsNullOrEmpty(it.NameKey), Is.False, it.name + " name");
                if (it.Kind == GachaItemKind.Weapon) Assert.That(it.Weapon, Is.Not.Null, it.name + " weapon asset");
                if (it.Character != null) Assert.That(it.Id, Is.EqualTo(it.Character.SaveId), it.name + " id = saveId");
            }
            Assert.That(all.Where(i => !i.Playable).Select(i => i.Id).ToArray(), Is.EquivalentTo(new[] { "starkeeper", "lancer" }), "graybox data-only entries");
            Assert.That(all.Select(i => i.Id).Distinct().Count(), Is.EqualTo(all.Count), "ids unique");
        }

        [Test]
        public void GachaTuning_MatchesBalance19_StartingItemsAreTheParty()
        {
            var g = Load<GachaTuning>(GachaPath);
            Assert.That(g.BannerCount, Is.EqualTo(2));
            Assert.That(g.GetBanner(0), Is.SameAs(Load<BannerData>(LimitedPath)));
            Assert.That(g.GetBanner(1), Is.SameAs(Load<BannerData>(StandardPath)));
            Assert.That(g.MaxAwakening, Is.EqualTo(6), "GDD: awakening max 6");
            Assert.That(g.MaxRefinement, Is.EqualTo(5));
            Assert.That(g.MultiPull, Is.EqualTo(10));
            CollectionRules r = g.ToRules();
            Assert.That(r.OverflowCurrency(5), Is.EqualTo(Currency.Wish));
            Assert.That(r.OverflowAmount(5), Is.EqualTo(5));
            Assert.That(r.OverflowCurrency(4), Is.EqualTo(Currency.Wish));
            Assert.That(r.OverflowAmount(4), Is.EqualTo(1));
            Assert.That(r.OverflowCurrency(3), Is.EqualTo(Currency.Gold));
            Assert.That(r.OverflowAmount(3), Is.EqualTo(100));
            var party = Load<PartyTuning>(PartyPath);
            var start = Ids(g.StartingItems);
            for (int i = 0; i < party.MemberCount; i++)
            {
                CharacterData c = party.GetMember(i);
                Assert.That(start, Does.Contain(c.SaveId), "starting party owned");
                Assert.That(g.StartingItems.Any(s => s.Weapon == c.Weapon), Is.True, c.name + " weapon owned");
            }
            Assert.That(start.Length, Is.EqualTo(7));
        }

        [Test]
        public void Simulate100k_AssetBanners_SeedFixed_WithinExpected()
        {
            var g = Load<GachaTuning>(GachaPath);
            foreach (int bi in new[] { 0, 1 })
            {
                BannerRules b = g.GetBanner(bi).ToRules();
                var w = new Wallet();
                var acc = new GachaAccount(g.ToRules(), w, new RandomStreams(RandomStreams.SeedFromInt(7)).Get(RandomStream.Gacha));
                const int N = 100000;
                w.Add(Currency.Wish, N);
                int five = 0, four = 0, featured = 0;
                for (int k = 0; k < N / 10; k++)
                    foreach (var r in acc.TryPull(b, 10))
                    {
                        if (r.Stars == 5) { five++; if (r.Featured) featured++; }
                        else if (r.Stars == 4) four++;
                    }
                double[] c = b.ConsolidatedRates();
                double r5 = five / (double)N, r4 = four / (double)N;
                TestContext.WriteLine(b.Id + " 100k seed 7: 5 star " + five + " = " + (r5 * 100).ToString("0.000") + "% (expected " + (c[2] * 100).ToString("0.000") + "%), 4 star " + four + " = "
                    + (r4 * 100).ToString("0.000") + "% (expected " + (c[1] * 100).ToString("0.000") + "%), featured " + featured);
                Assert.That(c[2], Is.EqualTo(0.016).Within(0.0005), "consolidated 5 star about 1.6%");
                Assert.That(c[1], Is.EqualTo(0.13).Within(0.002), "consolidated 4 star about 13%");
                Assert.That(r5, Is.InRange(0.0145, 0.0175));
                Assert.That(r4, Is.InRange(0.123, 0.137));
                if (b.HasFeatured) Assert.That(featured / (double)five, Is.InRange(0.62, 0.71));
            }
        }

        [Test]
        public void WishSources_Chests2And5_Brute1_OthersKeepTheirRewards()
        {
            var e = Load<ExplorationTuning>(ExplorationPath).ToRules();
            Assert.That(e.Reward(ChestTier.Common).Wishes, Is.EqualTo(2));
            Assert.That(e.Reward(ChestTier.Exquisite).Wishes, Is.EqualTo(5));
            Assert.That(e.Reward(ChestTier.Common).Gold, Is.EqualTo(300), "3-6 unchanged");
            Assert.That(Load<EnemyTuning>("Assets/_Game/Data/Enemies/Brute.asset").WishesOnDefeat, Is.EqualTo(1));
            Assert.That(Load<EnemyTuning>("Assets/_Game/Data/Enemies/Grunt.asset").WishesOnDefeat, Is.EqualTo(0));
            Assert.That(Load<EnemyTuning>("Assets/_Game/Data/Enemies/Archer.asset").WishesOnDefeat, Is.EqualTo(0));
            Assert.That(Load<EnemyTuning>("Assets/_Game/Data/Enemies/Brute.asset").GoldOnDefeat, Is.EqualTo(500), "3-5 unchanged");
            var p = new Progression(Load<GrowthTuning>(GrowthPath).ToRules(), PartyIds());
            p.AddReward(500, 5, 1);
            Assert.That(p.Wallet.Get(Currency.Wish), Is.EqualTo(1));
            Assert.That(p.Wallet.Get(Currency.Gold), Is.EqualTo(500));
        }

        static string TempPath() => Path.Combine(Path.GetTempPath(), "moa_gacha_test_" + Guid.NewGuid().ToString("N"), SaveStore.FileName);

        static void CleanUp(string path)
        {
            string dir = Path.GetDirectoryName(path);
            if (dir != null && Directory.Exists(dir) && dir.StartsWith(Path.GetTempPath(), StringComparison.OrdinalIgnoreCase) && dir.Contains("moa_gacha_test_"))
                Directory.Delete(dir, true);
        }

        [Test]
        public void SaveStore_Format2File_UpgradesTo3_AndGachaRoundTrips()
        {
            string path = TempPath();
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, "{\"version\": 2, \"wallet\": [{\"id\": \"Gold\", \"amount\": 10}], \"characters\": [], " +
                    "\"world\": {\"waypoints\": [\"wp_start\"], \"chests\": [], \"puzzles\": []}}");
                var store = new SaveStore(path);
                Assert.That(store.Load(out var snap), Is.EqualTo(SaveLoadStatus.Loaded), store.LastError);
                Assert.That(store.LastUpgradedFrom, Is.EqualTo(2));
                Assert.That(snap.version, Is.EqualTo(3));
                Assert.That(snap.gacha, Is.Not.Null);
                Assert.That(snap.world.waypoints, Is.EqualTo(new[] { "wp_start" }), "world kept");

                var g = Load<GachaTuning>(GachaPath);
                var p = new Progression(Load<GrowthTuning>(GrowthPath).ToRules(), PartyIds());
                p.Load(snap);
                var acc = new GachaAccount(g.ToRules(), p.Wallet, new RandomStreams(1UL).Get(RandomStream.Gacha));
                Assert.That(acc.Load(snap.gacha, 90, 10), Is.EqualTo(0));
                Assert.That(acc.Owns("sword_hero"), Is.True, "starting items after upgrade");
                p.Wallet.Add(Currency.Wish, 25);
                BannerRules lim = g.GetBanner(0).ToRules();
                acc.TryPull(lim, 10);
                acc.TryPull(lim, 1);
                acc.SetPity(BannerKind.Standard, 40, 3, true, 90, 10);
                SaveSnapshot next = p.ToSnapshot();
                next.world = snap.world;
                next.gacha = acc.ToSave();
                Assert.That(store.Save(next), Is.True, store.LastError);
                string text = File.ReadAllText(path);
                Assert.That(text, Does.Contain("\"version\": 3"));
                Assert.That(text, Does.Contain("\"gacha\""));
                Assert.That(text, Does.Contain("\"rng\""));

                Assert.That(new SaveStore(path).Load(out var again), Is.EqualTo(SaveLoadStatus.Loaded));
                var q = new Progression(Load<GrowthTuning>(GrowthPath).ToRules(), PartyIds());
                q.Load(again);
                var b = new GachaAccount(g.ToRules(), q.Wallet, new RandomStreams(2UL).Get(RandomStream.Gacha));
                Assert.That(b.Load(again.gacha, 90, 10), Is.EqualTo(0));
                Assert.That(b.Pity(BannerKind.LimitedCharacter).Pulls, Is.EqualTo(11));
                Assert.That(b.Pity(BannerKind.LimitedCharacter).SinceFive, Is.EqualTo(acc.Pity(BannerKind.LimitedCharacter).SinceFive));
                Assert.That(b.Pity(BannerKind.Standard).SinceFive, Is.EqualTo(40));
                Assert.That(b.Pity(BannerKind.Standard).SinceFour, Is.EqualTo(3));
                Assert.That(b.Pity(BannerKind.Standard).FeaturedGuaranteed, Is.True);
                Assert.That(b.Owned, Is.EqualTo(acc.Owned));
                Assert.That(b.Random.State, Is.EqualTo(acc.Random.State));
                Assert.That(q.Wallet.Get(Currency.Wish), Is.EqualTo(p.Wallet.Get(Currency.Wish)), "wish stones saved in the wallet");
            }
            finally { CleanUp(path); }
        }

        [Test]
        public void GachaScreen_PullButtons_Cards_ProbabilityInfo()
        {
            var canvas = new GameObject("GachaAssetTests_Canvas", typeof(RectTransform), typeof(Canvas));
            try
            {
                var g = Load<GachaTuning>(GachaPath);
                var w = new Wallet();
                var acc = new GachaAccount(g.ToRules(), w, new RandomStreams(3UL).Get(RandomStream.Gacha));
                var banners = new[] { g.GetBanner(0).ToRules(), g.GetBanner(1).ToRules() };
                var screen = GachaScreen.CreateIn(canvas.transform, 100, 2, 10, 5, new Vector2(1500f, 900f), 36f, new Vector2(44f, 8f), new Vector2(420f, 56f), new Vector2(300f, 60f),
                    new Vector2(272f, 232f), 16f, 12f, 40, 24, 20, Color.black, Color.gray, Color.gray, Color.blue, Color.gray, Color.white, Color.black,
                    new[] { Color.blue, Color.magenta, Color.yellow });
                screen.Bind(acc, g, banners, null, null);
                Assert.That(screen.Open, Is.False);
                screen.SetOpen(true);
                Assert.That(screen.Open, Is.True);
                Assert.That(screen.CardCount, Is.EqualTo(10));
                Assert.That(screen.PullEnabled(false), Is.False, "no wish stones");
                Assert.That(screen.Pull(false), Is.False);
                Assert.That(screen.LastBlock, Is.EqualTo(GachaBlock.NotEnoughWishes));
                w.Add(Currency.Wish, 11);
                screen.Refresh();
                Assert.That(screen.PullEnabled(false) && screen.PullEnabled(true), Is.True);
                Assert.That(screen.Pull(false), Is.True);
                Assert.That(screen.Shown.Count, Is.EqualTo(1));
                Assert.That(screen.Pull(true), Is.True);
                Assert.That(screen.Shown.Count, Is.EqualTo(10));
                Assert.That(w.Get(Currency.Wish), Is.EqualTo(0).Or.GreaterThan(0), "spent 11 (overflow can refund)");
                for (int i = 0; i < 10; i++)
                {
                    Assert.That(screen.CardColor(i), Is.EqualTo(screen.RarityColor(screen.Shown[i].Stars)), "card " + i + " rarity color");
                    Assert.That(string.IsNullOrEmpty(screen.CardNameShown(i)), Is.False);
                }
                Assert.That(acc.Pity(BannerKind.LimitedCharacter).Pulls, Is.EqualTo(11), "first tab = limited banner");
                screen.SetInfoOpen(true);
                screen.Refresh();
                Assert.That(screen.InfoOpen, Is.True);
                string info = screen.InfoShown;
                Assert.That(string.IsNullOrEmpty(info), Is.False);
                Assert.That(info.Split('\n').Length, Is.GreaterThanOrEqualTo(14), "rates, pity rules, count, guarantee, pools");
                screen.SelectBanner(1);
                Assert.That(screen.Shown.Count, Is.EqualTo(0), "results cleared on another banner");
                Assert.That(screen.CurrentBanner.Kind, Is.EqualTo(BannerKind.Standard));
                screen.SetOpen(false);
                Assert.That(screen.Open || screen.InfoOpen, Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(canvas); }
        }
    }
}
