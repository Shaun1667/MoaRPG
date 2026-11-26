using System;
using System.Linq;
using Moa.Core.Gacha;
using Moa.Core.Growth;
using Moa.Core.Rng;
using Moa.Core.World;
using NUnit.Framework;

namespace Moa.Tests
{
    /// <summary>
    /// 3-7 가챠 규칙 (Core GachaRoll · BannerRules · GachaAccount) — 배너는 BALANCE 19번과 같은 꼴을 여기서 만든다(에셋 검산은 GachaAssetTests).
    /// 확률 곡선 · 시드 고정 10만 회 실측(★5 약 1.6% · ★4 약 13%) · 90회 확정 · ★4 10회 보장(★5와 겹치면 다음으로) · 50/50과 보장 · 배너 종류별 천장 ·
    /// 비용 · 중복(각성 · 재련 · 넘치면 재화) · 저장 왕복과 같은 결과 이어짐 · 고치기 · 형식 2 → 3.
    /// </summary>
    public class GachaTests
    {
        const ulong Seed = 20261126UL;

        static readonly GachaItem Hero = new GachaItem("hero", GachaItemKind.Character, 5);
        static readonly GachaItem Sage = new GachaItem("sage", GachaItemKind.Character, 5);
        static readonly GachaItem Star = new GachaItem("starkeeper", GachaItemKind.Character, 5);
        static readonly GachaItem Guardian = new GachaItem("guardian", GachaItemKind.Character, 4);
        static readonly GachaItem Lancer = new GachaItem("lancer", GachaItemKind.Character, 4);
        static readonly GachaItem Sword = new GachaItem("sword", GachaItemKind.Weapon, 3);
        static readonly GachaItem Bow = new GachaItem("bow", GachaItemKind.Weapon, 3);

        static RateCurve Five() => new RateCurve(0.006, 74, 0.06, 90);
        static RateCurve Four() => new RateCurve(0.051, 9, 0.51, 10);

        static BannerRules Standard() => new BannerRules("standard", BannerKind.Standard, 1, Five(), Four(), 0, new[] { Hero, Sage, Guardian, Lancer, Sword, Bow }, null);
        static BannerRules Limited() => new BannerRules("limited", BannerKind.LimitedCharacter, 1, Five(), Four(), 0.5, new[] { Hero, Sage, Guardian, Lancer, Sword, Bow }, new[] { Star });

        static CollectionRules Collection() => new CollectionRules(6, 5, 10,
            new[] { (5, Currency.Wish, 5), (4, Currency.Wish, 1), (3, Currency.Gold, 100) }, new[] { Hero, Guardian, Sword });

        static GachaAccount New(out Wallet wallet, ulong seed = Seed)
        {
            wallet = new Wallet();
            return new GachaAccount(Collection(), wallet, new RandomStreams(seed).Get(RandomStream.Gacha));
        }

        // ---- 확률 곡선 ----

        [Test]
        public void RateCurve_Five_Base06_RisesFrom74_Certain90()
        {
            RateCurve f = Five();
            Assert.That(f.At(1), Is.EqualTo(0.006).Within(1e-12));
            Assert.That(f.At(73), Is.EqualTo(0.006).Within(1e-12));
            Assert.That(f.At(74), Is.EqualTo(0.066).Within(1e-12), "74th pull: +6%p");
            Assert.That(f.At(89), Is.EqualTo(0.966).Within(1e-12));
            Assert.That(f.At(90), Is.EqualTo(1.0));
            Assert.That(f.At(200), Is.EqualTo(1.0));
        }

        [Test]
        public void RateCurve_Four_Base51_RisesAt9_Certain10()
        {
            RateCurve f = Four();
            Assert.That(f.At(1), Is.EqualTo(0.051).Within(1e-12));
            Assert.That(f.At(8), Is.EqualTo(0.051).Within(1e-12));
            Assert.That(f.At(9), Is.EqualTo(0.561).Within(1e-12));
            Assert.That(f.At(10), Is.EqualTo(1.0));
        }

        [Test]
        public void ConsolidatedRates_About16And13Percent_SumTo1()
        {
            double[] c = Standard().ConsolidatedRates();
            Assert.That(c[2], Is.EqualTo(0.01605).Within(0.0002), "5 star consolidated");
            Assert.That(c[1], Is.EqualTo(0.1306).Within(0.002), "4 star consolidated");
            Assert.That(c[0] + c[1] + c[2], Is.EqualTo(1.0).Within(1e-9));
            BannerRules l = Limited();
            double sum = 0;
            foreach (var id in new[] { "starkeeper", "hero", "sage", "guardian", "lancer", "sword", "bow" }) sum += l.ConsolidatedItemRate(id);
            Assert.That(sum, Is.EqualTo(1.0).Within(1e-9), "item rates add up");
            Assert.That(l.ConsolidatedItemRate("starkeeper") / l.ConsolidatedRates()[2], Is.EqualTo(2.0 / 3.0).Within(1e-9), "50/50 + guarantee = 2/3 featured");
            Assert.That(l.ConsolidatedItemRate("nobody"), Is.EqualTo(0));
        }

        // ---- 시드 고정 10만 회 ----

        [Test]
        public void Simulate100k_Standard_RatesWithinExpected_PityNeverExceeded()
        {
            BannerRules b = Standard();
            var acc = New(out Wallet w);
            const int N = 100000;
            w.Add(Currency.Wish, N);
            int five = 0, four = 0, runNoFive = 0, maxNoFive = 0, runThree = 0, maxThree = 0;
            for (int k = 0; k < N / 10; k++)
                foreach (var r in acc.TryPull(b, 10))
                {
                    if (r.Stars == 5) { five++; runNoFive = 0; } else { runNoFive++; if (runNoFive > maxNoFive) maxNoFive = runNoFive; }
                    if (r.Stars == 4) four++;
                    if (r.Stars == 3) { runThree++; if (runThree > maxThree) maxThree = runThree; } else runThree = 0;
                }
            double r5 = five / (double)N, r4 = four / (double)N;
            double[] c = b.ConsolidatedRates();
            TestContext.WriteLine("standard 100k: 5 star " + five + " (" + (r5 * 100).ToString("0.000") + "%), 4 star " + four + " (" + (r4 * 100).ToString("0.000") + "%), expected " + (c[2] * 100).ToString("0.000") + "% / " + (c[1] * 100).ToString("0.000") + "%");
            Assert.That(r5, Is.InRange(0.0145, 0.0175), "5 star about 1.6%");
            Assert.That(Math.Abs(r5 - c[2]), Is.LessThan(0.0012), "close to the consolidated rate");
            Assert.That(r4, Is.InRange(0.123, 0.137), "4 star about 13%");
            Assert.That(Math.Abs(r4 - c[1]), Is.LessThan(0.004));
            Assert.That(maxNoFive, Is.LessThanOrEqualTo(89), "a 5 star by the 90th pull");
            Assert.That(maxThree, Is.LessThanOrEqualTo(9), "a 4 star or better within 10 pulls");
            Assert.That(acc.Pity(BannerKind.Standard).Pulls, Is.EqualTo(N));
            Assert.That(acc.Pity(BannerKind.Standard).FiveCount, Is.EqualTo(five));
            Assert.That(w.Get(Currency.Wish), Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void Simulate100k_Limited_FiftyFifty_TwoThirdsFeatured_LossAlwaysFollowedByFeatured()
        {
            BannerRules b = Limited();
            var acc = New(out Wallet w);
            const int N = 100000;
            w.Add(Currency.Wish, N);
            int fives = 0, featured = 0, won = 0, lost = 0;
            bool lastLost = false;
            int lostThenFeatured = 0;
            for (int k = 0; k < N / 10; k++)
                foreach (var r in acc.TryPull(b, 10))
                {
                    if (r.Stars != 5) { Assert.That(r.Featured || r.WonFiftyFifty || r.LostFiftyFifty, Is.False); continue; }
                    fives++;
                    if (r.Featured) featured++;
                    if (r.WonFiftyFifty) won++;
                    if (r.LostFiftyFifty)
                    {
                        lost++;
                        Assert.That(r.Item.Id, Is.Not.EqualTo("starkeeper"));
                        Assert.That(lastLost, Is.False, "never two losses in a row");
                    }
                    else
                    {
                        Assert.That(r.Item.Id, Is.EqualTo("starkeeper"));
                        if (lastLost) { lostThenFeatured++; Assert.That(r.WonFiftyFifty, Is.False, "guaranteed, not a coin flip"); }
                    }
                    lastLost = r.LostFiftyFifty;
                }
            double share = featured / (double)fives;
            double flip = won / (double)(won + lost);
            TestContext.WriteLine("limited 100k: 5 star " + fives + ", featured " + featured + " (" + (share * 100).ToString("0.0") + "%), 50/50 won " + won + " lost " + lost);
            Assert.That(fives / (double)N, Is.InRange(0.0145, 0.0175));
            Assert.That(share, Is.InRange(0.62, 0.71), "2/3 featured in the long run");
            Assert.That(flip, Is.InRange(0.43, 0.57), "the coin flip is 50%");
            Assert.That(lostThenFeatured, Is.EqualTo(lost - (lastLost ? 1 : 0)));
            Assert.That(acc.Pity(BannerKind.Standard).Pulls, Is.EqualTo(0), "standard pity untouched");
        }

        [Test]
        public void SameSeed_SameResults_OtherStreamsDoNotMatter()
        {
            var a = New(out Wallet wa);
            var b = New(out Wallet wb);
            wa.Add(Currency.Wish, 50);
            wb.Add(Currency.Wish, 50);
            var streams = new RandomStreams(Seed);
            streams.Get(RandomStream.Drop).NextULong(); // 다른 용도를 굴려도
            var c = new GachaAccount(Collection(), new Wallet(), streams.Get(RandomStream.Gacha));
            c.Wallet.Add(Currency.Wish, 50);
            var ra = a.TryPull(Limited(), 50).Select(r => r.Item.Id).ToArray();
            var rb = b.TryPull(Limited(), 50).Select(r => r.Item.Id).ToArray();
            var rc = c.TryPull(Limited(), 50).Select(r => r.Item.Id).ToArray();
            Assert.That(rb, Is.EqualTo(ra));
            Assert.That(rc, Is.EqualTo(ra));
        }

        // ---- 천장 · 보장 ----

        [Test]
        public void HardPity_90thPullIsFiveStar()
        {
            var acc = New(out Wallet w);
            w.Add(Currency.Wish, 100);
            acc.SetPity(BannerKind.Standard, 89, 0, false, 90, 10);
            var r = acc.TryPull(Standard(), 1)[0];
            Assert.That(r.Stars, Is.EqualTo(5));
            Assert.That(r.PityCount, Is.EqualTo(90));
            Assert.That(acc.Pity(BannerKind.Standard).SinceFive, Is.EqualTo(0));
        }

        [Test]
        public void FourStarGuarantee_10thPull_PushedPastAFiveStar()
        {
            var acc = New(out Wallet w);
            w.Add(Currency.Wish, 100);
            acc.SetPity(BannerKind.Standard, 0, 9, false, 90, 10);
            var r = acc.TryPull(Standard(), 1)[0];
            Assert.That(r.Stars, Is.GreaterThanOrEqualTo(4), "10th pull");
            // ★5와 겹치면: ★5가 먼저, ★4 보장은 다음 회로
            acc.SetPity(BannerKind.Standard, 89, 9, false, 90, 10);
            var two = acc.TryPull(Standard(), 2);
            Assert.That(two[0].Stars, Is.EqualTo(5));
            Assert.That(two[1].Stars, Is.GreaterThanOrEqualTo(4), "pushed to the 11th");
            if (two[1].Stars == 4) Assert.That(acc.Pity(BannerKind.Standard).SinceFour, Is.EqualTo(0), "the 11th took the 4 star");
        }

        [Test]
        public void FiftyFifty_Guaranteed_GivesFeatured_LossSetsGuarantee()
        {
            var acc = New(out Wallet w);
            w.Add(Currency.Wish, 1000);
            BannerRules b = Limited();
            acc.SetPity(BannerKind.LimitedCharacter, 89, 0, true, 90, 10);
            var g = acc.TryPull(b, 1)[0];
            Assert.That(g.Featured, Is.True);
            Assert.That(g.WonFiftyFifty, Is.False, "guaranteed");
            Assert.That(acc.Pity(BannerKind.LimitedCharacter).FeaturedGuaranteed, Is.False);
            // 지기까지 ★5 확정을 되풀이 (난수는 시드 고정)
            GachaPull lost = default;
            for (int i = 0; i < 64 && !lost.LostFiftyFifty; i++)
            {
                acc.SetPity(BannerKind.LimitedCharacter, 89, 0, false, 90, 10);
                lost = acc.TryPull(b, 1)[0];
                if (!lost.LostFiftyFifty) Assert.That(lost.WonFiftyFifty && lost.Featured, Is.True);
            }
            Assert.That(lost.LostFiftyFifty, Is.True);
            Assert.That(new[] { "hero", "sage" }, Does.Contain(lost.Item.Id));
            Assert.That(acc.Pity(BannerKind.LimitedCharacter).FeaturedGuaranteed, Is.True, "next 5 star is featured");
            acc.SetPity(BannerKind.LimitedCharacter, 89, 0, true, 90, 10);
            Assert.That(acc.TryPull(b, 1)[0].Item.Id, Is.EqualTo("starkeeper"));
        }

        [Test]
        public void Pity_IsPerBannerKind_StandardHasNoFiftyFifty()
        {
            var acc = New(out Wallet w);
            w.Add(Currency.Wish, 100);
            acc.TryPull(Limited(), 30);
            int lim = acc.Pity(BannerKind.LimitedCharacter).Pulls;
            acc.TryPull(Standard(), 20);
            Assert.That(acc.Pity(BannerKind.LimitedCharacter).Pulls, Is.EqualTo(lim), "standard pulls do not touch the limited count");
            Assert.That(acc.Pity(BannerKind.Standard).Pulls, Is.EqualTo(20));
            acc.SetPity(BannerKind.Standard, 89, 0, true, 90, 10);
            var r = acc.TryPull(Standard(), 1)[0];
            Assert.That(r.Featured || r.LostFiftyFifty, Is.False);
            Assert.That(new[] { "hero", "sage" }, Does.Contain(r.Item.Id));
        }

        // ---- 비용 ----

        [Test]
        public void Cost_NotEnoughWishes_ChangesNothing_TenPullSpendsTen()
        {
            var acc = New(out Wallet w);
            w.Add(Currency.Wish, 9);
            Assert.That(acc.TryPull(Standard(), 10), Is.Null);
            Assert.That(acc.LastBlock, Is.EqualTo(GachaBlock.NotEnoughWishes));
            Assert.That(w.Get(Currency.Wish), Is.EqualTo(9));
            Assert.That(acc.Pity(BannerKind.Standard).Pulls, Is.EqualTo(0));
            Assert.That(acc.TryPull(Standard(), 0), Is.Null);
            Assert.That(acc.LastBlock, Is.EqualTo(GachaBlock.BadCount));
            Assert.That(acc.TryPull(null, 1), Is.Null);
            Assert.That(acc.LastBlock, Is.EqualTo(GachaBlock.NoBanner));
            w.Add(Currency.Wish, 1);
            int changed = 0;
            acc.Changed += () => changed++;
            Assert.That(acc.TryPull(Standard(), 10).Count, Is.EqualTo(10));
            Assert.That(w.Get(Currency.Wish), Is.LessThan(10), "spent ten (refunds only from overflow)");
            Assert.That(changed, Is.EqualTo(1));
            Assert.That(acc.LastBlock, Is.EqualTo(GachaBlock.None));
        }

        // ---- 중복 ----

        [Test]
        public void Duplicates_CharacterAwakensTo6_ThenConverts_WeaponRefinesTo5_ThenConverts()
        {
            // 등급마다 하나뿐인 배너로 같은 것만 나오게
            var onlyHero = new BannerRules("t", BannerKind.Standard, 0, new RateCurve(1, 0, 0, 1), Four(), 0, new[] { Hero, Lancer, Sword }, null);
            var acc = New(out Wallet w);
            Assert.That(acc.Awakening("hero"), Is.EqualTo(0), "starting item");
            var r = acc.TryPull(onlyHero, 8);
            Assert.That(r.Select(x => x.Outcome).ToArray(), Is.EqualTo(new[]
            {
                GachaOutcome.Awakened, GachaOutcome.Awakened, GachaOutcome.Awakened, GachaOutcome.Awakened, GachaOutcome.Awakened, GachaOutcome.Awakened,
                GachaOutcome.Converted, GachaOutcome.Converted,
            }));
            Assert.That(r[5].Rank, Is.EqualTo(6));
            Assert.That(acc.Awakening("hero"), Is.EqualTo(6));
            Assert.That(w.Get(Currency.Wish), Is.EqualTo(10), "2 overflows x 5");
            Assert.That(r[6].ConvertedTo, Is.EqualTo(Currency.Wish));

            var onlySword = new BannerRules("t3", BannerKind.Standard, 0, new RateCurve(0, 0, 0, 1000), new RateCurve(0, 0, 0, 1000), 0, new[] { Hero, Lancer, Sword }, null);
            var s = acc.TryPull(onlySword, 5);
            Assert.That(s.Select(x => x.Outcome).ToArray(), Is.EqualTo(new[] { GachaOutcome.Refined, GachaOutcome.Refined, GachaOutcome.Refined, GachaOutcome.Refined, GachaOutcome.Converted }));
            Assert.That(acc.Refinement("sword"), Is.EqualTo(5));
            Assert.That(w.Get(Currency.Gold), Is.EqualTo(100));

            var lancer = new BannerRules("t4", BannerKind.Standard, 0, new RateCurve(0, 0, 0, 1000), new RateCurve(1, 0, 0, 1), 0, new[] { Hero, Lancer, Sword }, null);
            var l = acc.TryPull(lancer, 2);
            Assert.That(l[0].Outcome, Is.EqualTo(GachaOutcome.New));
            Assert.That(l[0].Rank, Is.EqualTo(0));
            Assert.That(l[1].Outcome, Is.EqualTo(GachaOutcome.Awakened));
            Assert.That(acc.Owned, Does.Contain("lancer"));
        }

        [Test]
        public void StartingItems_Owned_ResetKeepsThem()
        {
            var acc = New(out Wallet w);
            Assert.That(acc.Owned, Is.EqualTo(new[] { "hero", "guardian", "sword" }));
            Assert.That(acc.Refinement("sword"), Is.EqualTo(1));
            Assert.That(acc.Owns("starkeeper"), Is.False);
            Assert.That(acc.Awakening("starkeeper"), Is.EqualTo(-1));
            w.Add(Currency.Wish, 40);
            acc.TryPull(Limited(), 40);
            acc.ResetAll();
            Assert.That(acc.Owned, Is.EqualTo(new[] { "hero", "guardian", "sword" }));
            Assert.That(acc.Pity(BannerKind.LimitedCharacter).Pulls, Is.EqualTo(0));
        }

        // ---- 저장 ----

        [Test]
        public void Save_RoundTrip_ContinuesTheSameSequence()
        {
            var a = New(out Wallet wa);
            wa.Add(Currency.Wish, 200);
            a.TryPull(Limited(), 37);
            a.TryPull(Standard(), 12);
            GachaSave save = a.ToSave();

            var b = New(out Wallet wb, 999UL); // 다른 시드로 만들어도 저장의 난수 상태를 쓴다
            wb.Add(Currency.Wish, 200);
            Assert.That(b.Load(save, 90, 10), Is.EqualTo(0));
            foreach (BannerKind k in Enum.GetValues(typeof(BannerKind)))
            {
                Assert.That(b.Pity(k).SinceFive, Is.EqualTo(a.Pity(k).SinceFive), k.ToString());
                Assert.That(b.Pity(k).SinceFour, Is.EqualTo(a.Pity(k).SinceFour));
                Assert.That(b.Pity(k).FeaturedGuaranteed, Is.EqualTo(a.Pity(k).FeaturedGuaranteed));
                Assert.That(b.Pity(k).Pulls, Is.EqualTo(a.Pity(k).Pulls));
                Assert.That(b.Pity(k).FiveCount, Is.EqualTo(a.Pity(k).FiveCount));
            }
            Assert.That(b.Owned, Is.EqualTo(a.Owned));
            foreach (var id in a.Owned) Assert.That(b.Copies(id), Is.EqualTo(a.Copies(id)), id);
            Assert.That(b.Random.State, Is.EqualTo(a.Random.State));
            var na = a.TryPull(Limited(), 30).Select(r => r.Item.Id).ToArray();
            var nb = b.TryPull(Limited(), 30).Select(r => r.Item.Id).ToArray();
            Assert.That(nb, Is.EqualTo(na), "same results after loading");
        }

        [Test]
        public void Load_FixesBadValues_NullIsFresh()
        {
            var acc = New(out _);
            ulong state = acc.Random.State;
            var bad = new GachaSave
            {
                rng = "not a number",
                pity = new[]
                {
                    new PitySave { kind = "Standard", sinceFive = 500, sinceFour = -3, pulls = -1, guaranteed = true },
                    new PitySave { kind = "Nope" },
                    null,
                },
                owned = new[]
                {
                    new OwnedSave { id = "hero", kind = "Character", copies = 99 },
                    new OwnedSave { id = "hero", kind = "Character", copies = 1 },
                    new OwnedSave { id = "", kind = "Weapon", copies = 1 },
                    new OwnedSave { id = "x", kind = "Pet", copies = 1 },
                    new OwnedSave { id = "bow", kind = "Weapon", copies = 0 },
                },
            };
            int fixes = acc.Load(bad, 90, 10);
            Assert.That(fixes, Is.EqualTo(11), "rng 1 + pity 3 + unknown kind 1 + null 1 + owned 5");
            Assert.That(acc.Random.State, Is.EqualTo(state), "unreadable rng keeps the current one");
            Assert.That(acc.Pity(BannerKind.Standard).SinceFive, Is.EqualTo(89));
            Assert.That(acc.Pity(BannerKind.Standard).SinceFour, Is.EqualTo(0));
            Assert.That(acc.Pity(BannerKind.Standard).FeaturedGuaranteed, Is.True);
            Assert.That(acc.Awakening("hero"), Is.EqualTo(6), "capped at 1 + 6");
            Assert.That(acc.Refinement("bow"), Is.EqualTo(1));
            Assert.That(acc.Owns("guardian") && acc.Owns("sword"), Is.True, "starting items always owned");
            Assert.That(acc.Load(null, 90, 10), Is.EqualTo(0));
            Assert.That(acc.Pity(BannerKind.Standard).SinceFive, Is.EqualTo(0));
        }

        [Test]
        public void SaveFormat_Version3_Upgrade2To3_AddsEmptyGacha()
        {
            Assert.That(SaveFormat.Version, Is.EqualTo(3));
            var old = new SaveSnapshot { version = 2, gacha = null, wallet = new[] { new CurrencySave { id = "Gold", amount = 5 } } };
            Assert.That(SaveFormat.Validate(old, out _), Is.True, "format 2 does not need gacha");
            Assert.That(SaveFormat.Upgrade(old), Is.EqualTo(2));
            Assert.That(old.version, Is.EqualTo(3));
            Assert.That(old.gacha, Is.Not.Null);
            Assert.That(old.gacha.pity.Length + old.gacha.owned.Length, Is.EqualTo(0));
            Assert.That(old.gacha.rng, Is.Empty);
            Assert.That(old.wallet[0].amount, Is.EqualTo(5));
            var one = new SaveSnapshot { version = 1, world = null, gacha = null };
            Assert.That(SaveFormat.Upgrade(one), Is.EqualTo(1));
            Assert.That(one.version, Is.EqualTo(3));
            Assert.That(one.world != null && one.gacha != null, Is.True);
            Assert.That(SaveFormat.Validate(new SaveSnapshot { version = 3, gacha = null }, out string reason), Is.False);
            Assert.That(reason, Does.Contain("gacha"));
            Assert.That(SaveFormat.Validate(new SaveSnapshot { version = 3, gacha = new GachaSave { owned = null } }, out _), Is.False);
            Assert.That(SaveFormat.Validate(new SaveSnapshot { version = 3 }, out _), Is.True);
        }

        // ---- 재화 · 보상 ----

        [Test]
        public void Wish_IsAppendedCurrency_ChestAndDefeatRewardsAddIt()
        {
            Assert.That(Enum.GetValues(typeof(Currency)).Cast<Currency>().Select(c => (int)c).ToArray(), Is.EqualTo(new[] { 0, 1, 2 }), "saved by name, but append only");
            Assert.That(Enum.GetValues(typeof(BannerKind)).Cast<BannerKind>().Select(c => (int)c).ToArray(), Is.EqualTo(new[] { 0, 1 }));
            var w = new Wallet();
            var world = new WorldProgress(new ExplorationRules(new[] { new ChestReward(300, 2, 2), new ChestReward(1000, 5, 5) }, false), w);
            world.RegisterChest("c", ChestTier.Exquisite, null);
            Assert.That(world.TryOpen("c"), Is.True);
            Assert.That(w.Get(Currency.Wish), Is.EqualTo(5));
            Assert.That(w.Get(Currency.Gold), Is.EqualTo(1000), "3-6 reward unchanged");
            Assert.That(new ChestReward(300, 2).Wishes, Is.EqualTo(0));
        }

        [Test]
        public void BannerRules_Validates()
        {
            Assert.Throws<ArgumentException>(() => new BannerRules("x", BannerKind.Standard, 1, Five(), Four(), 0, new[] { Hero, Sword }, null), "needs a 4 star");
            Assert.Throws<ArgumentException>(() => new BannerRules("x", BannerKind.LimitedCharacter, 1, Five(), Four(), 0.5, new[] { Guardian, Sword }, new[] { Star }), "featured needs another 5 star");
            Assert.Throws<ArgumentException>(() => new BannerRules("x", BannerKind.LimitedCharacter, 1, Five(), Four(), 0.5, new[] { Hero, Guardian, Sword }, new[] { Guardian }), "featured must be 5 stars");
            Assert.Throws<ArgumentOutOfRangeException>(() => new GachaItem("y", GachaItemKind.Weapon, 2));
            var l = Limited();
            Assert.That(l.Pool(5).Select(i => i.Id).ToArray(), Is.EqualTo(new[] { "starkeeper", "hero", "sage" }));
            Assert.That(l.OffFeatured.Select(i => i.Id).ToArray(), Is.EqualTo(new[] { "hero", "sage" }));
        }
    }
}
