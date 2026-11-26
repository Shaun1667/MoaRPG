using System;
using System.Linq;
using Moa.Core.Growth;
using Moa.Core.Motion;
using Moa.Core.World;
using NUnit.Framework;

namespace Moa.Tests
{
    /// <summary>
    /// 3-6 탐험 규칙 (Core): 수영(들어가고 나오는 깊이 · 물에서 스태미나 · 질주 수영 · 익사 · 떠 있는 깊이 · 익사 피해),
    /// 월드 진행(순간이동 지점 활성화 · 이동 조건, 상자 등급 보상 · 한 번만, 퍼즐 상자 잠김 · 압력판 켜짐 유지 · 풀림, 저장 왕복 · 모르는 id 유지),
    /// 저장 형식 1 → 2 옮기기. 수치는 BALANCE 18번 값으로 만든다 (에셋 검산은 ExplorationAssetTests).
    /// </summary>
    public class ExplorationTests
    {
        const float Dt = 0.02f;
        const float Eps = 1e-3f;

        static SwimRules SwimR() => new SwimRules(1.2f, 1.0f, 1.3f, 6f, 3f, 3f, 5.5f, 10f, 6f, 20f, 0.25f);
        static Stamina NewStamina() => new Stamina(new StaminaRules(240f, 25f, 1f, 60f));

        static ExplorationRules WorldR(bool heal = false) => new ExplorationRules(new[] { new ChestReward(300, 2), new ChestReward(1000, 5) }, heal);

        static WorldProgress NewWorld(Wallet wallet = null)
        {
            var w = new WorldProgress(WorldR(), wallet);
            w.RegisterWaypoint("wp_a");
            w.RegisterWaypoint("wp_b");
            w.RegisterChest("c_common", ChestTier.Common, null);
            w.RegisterChest("c_fancy", ChestTier.Exquisite, "");
            w.RegisterChest("c_puzzle", ChestTier.Exquisite, "pz");
            w.RegisterPuzzle("pz", 3);
            return w;
        }

        // ---- 수영 ----

        [Test]
        public void Swim_EnterAt12_ExitBelow10_NoFlicker()
        {
            var s = new Swim(SwimR());
            Assert.That(s.ShouldEnter(1.19f), Is.False);
            Assert.That(s.ShouldEnter(1.2f), Is.True);
            s.Enter();
            Assert.That(s.Active && s.Count == 1, Is.True);
            Assert.That(s.ShouldEnter(2f), Is.False, "already swimming");
            Assert.That(s.ShouldExit(1.1f), Is.False, "between exit and enter = still swimming");
            Assert.That(s.ShouldExit(0.99f), Is.True);
            s.End(SwimEnd.Exited);
            Assert.That(s.Active, Is.False);
            Assert.That(s.LastEnd, Is.EqualTo(SwimEnd.Exited));
            Assert.That(s.ShouldEnter(1.1f), Is.False, "out of the water until 1.2 again");
        }

        [Test]
        public void Swim_Drains6PerSecond_EvenStill_Sprint20WhenMoving()
        {
            var s = new Swim(SwimR());
            var st = NewStamina();
            s.Enter();
            for (int i = 0; i < 50; i++) { Assert.That(s.Hold(st, false, false, Dt), Is.True); st.Tick(Dt); }
            Assert.That(st.Current, Is.EqualTo(234f).Within(Eps), "1 s still in the water");
            for (int i = 0; i < 50; i++) { s.Hold(st, true, false, Dt); st.Tick(Dt); }
            Assert.That(s.Sprinting, Is.False, "sprint needs movement");
            Assert.That(st.Current, Is.EqualTo(228f).Within(Eps));
            for (int i = 0; i < 50; i++) { s.Hold(st, true, true, Dt); st.Tick(Dt); }
            Assert.That(s.Sprinting, Is.True);
            Assert.That(st.Current, Is.EqualTo(208f).Within(Eps), "sprint swim 20/s");
            Assert.That(s.TargetSpeed(1f), Is.EqualTo(5.5f).Within(Eps));
            s.Hold(st, false, true, Dt);
            Assert.That(s.TargetSpeed(1f), Is.EqualTo(3f).Within(Eps));
            Assert.That(s.TargetSpeed(0.5f), Is.EqualTo(1.5f).Within(Eps));
            Assert.That(s.TargetSpeed(0f), Is.EqualTo(0f));
        }

        [Test]
        public void Swim_FullStamina_Lasts40Seconds_Sprint12()
        {
            var r = SwimR();
            Assert.That(240f / r.DrainPerSecond, Is.EqualTo(40f).Within(Eps));
            Assert.That(240f / r.SprintDrainPerSecond, Is.EqualTo(12f).Within(Eps));
            var s = new Swim(r);
            var st = NewStamina();
            s.Enter();
            int steps = 0;
            while (s.Hold(st, false, false, Dt)) { st.Tick(Dt); steps++; if (steps > 5000) break; }
            Assert.That(steps, Is.EqualTo(1999).Within(1), "about 2000 steps = 40 s");
            Assert.That(s.LastEnd, Is.EqualTo(SwimEnd.Drowned));
            Assert.That(s.DrownCount, Is.EqualTo(1));
            Assert.That(s.Active, Is.False);
        }

        [Test]
        public void Swim_Exhausted_DrownsAtOnce_InfiniteNever()
        {
            var s = new Swim(SwimR());
            var st = NewStamina();
            st.Set(0f);
            st.Set(30f); // 지침 중 (60 전)
            Assert.That(st.Exhausted, Is.True);
            s.Enter();
            Assert.That(s.Hold(st, false, true, Dt), Is.False, "exhausted = cannot keep spending");
            Assert.That(s.LastEnd, Is.EqualTo(SwimEnd.Drowned));

            var inf = NewStamina();
            inf.Infinite = true;
            s.Enter();
            for (int i = 0; i < 5000; i++) Assert.That(s.Hold(inf, true, true, Dt), Is.True);
            Assert.That(inf.Current, Is.EqualTo(240f));
        }

        [Test]
        public void Swim_Vertical_FloatsAt13_Clamped3()
        {
            var s = new Swim(SwimR());
            Assert.That(s.Vertical(1.3f), Is.EqualTo(0f).Within(Eps));
            Assert.That(s.Vertical(1.5f), Is.EqualTo(1.2f).Within(Eps), "0.2 m too deep x 6/s = rise 1.2 m/s");
            Assert.That(s.Vertical(1.2f), Is.EqualTo(-0.6f).Within(Eps));
            Assert.That(s.Vertical(5f), Is.EqualTo(3f).Within(Eps), "clamped");
            Assert.That(s.Vertical(-5f), Is.EqualTo(-3f).Within(Eps));
        }

        [Test]
        public void DrownDamage_QuarterOfMax_KeepsOne()
        {
            Assert.That(Swim.DrownDamage(1000, 1000, 0.25f), Is.EqualTo(250));
            Assert.That(Swim.DrownDamage(850, 850, 0.25f), Is.EqualTo(213), "212.5 rounds up");
            Assert.That(Swim.DrownDamage(1000, 100, 0.25f), Is.EqualTo(99), "never knocks out");
            Assert.That(Swim.DrownDamage(1000, 1, 0.25f), Is.EqualTo(0));
            Assert.That(Swim.DrownDamage(1000, 0, 0.25f), Is.EqualTo(0));
            Assert.That(Swim.DrownDamage(1000, 1000, 0f), Is.EqualTo(0));
        }

        [Test]
        public void Swim_CancelKeepsDrownReason()
        {
            var s = new Swim(SwimR());
            var st = NewStamina();
            st.Set(0.05f);
            s.Enter();
            Assert.That(s.Hold(st, false, false, Dt), Is.False);
            s.Cancel(); // 익사 뒤 순간이동이 부른다
            Assert.That(s.LastEnd, Is.EqualTo(SwimEnd.Drowned));
            s.Enter();
            s.Cancel();
            Assert.That(s.LastEnd, Is.EqualTo(SwimEnd.Cancelled));
        }

        // ---- 순간이동 지점 ----

        [Test]
        public void Waypoint_ActivatesOnce_TeleportNeedsActivation()
        {
            var w = NewWorld();
            int changed = 0, activated = 0;
            w.Changed += () => changed++;
            w.WaypointActivated += _ => activated++;
            Assert.That(w.CheckTeleport("wp_a", false), Is.EqualTo(WaypointBlock.NotActivated));
            Assert.That(w.CheckTeleport("nope", false), Is.EqualTo(WaypointBlock.Unknown));
            Assert.That(w.TryActivate("nope"), Is.False);
            Assert.That(w.TryActivate("wp_a"), Is.True);
            Assert.That(w.TryActivate("wp_a"), Is.False, "only once");
            Assert.That(activated, Is.EqualTo(1));
            Assert.That(changed, Is.EqualTo(1));
            Assert.That(w.CheckTeleport("wp_a", true), Is.EqualTo(WaypointBlock.Busy));
            Assert.That(w.TryTeleport("wp_b", false), Is.False);
            Assert.That(w.LastTeleportBlock, Is.EqualTo(WaypointBlock.NotActivated));
            Assert.That(w.TryTeleport("wp_a", false), Is.True);
            Assert.That(w.TeleportCount, Is.EqualTo(1));
            Assert.That(w.Rules.TeleportHealsParty, Is.False);
        }

        // ---- 상자 ----

        [Test]
        public void Chest_OpensOnce_GivesTierReward()
        {
            var wallet = new Wallet();
            var w = NewWorld(wallet);
            string opened = null;
            w.ChestOpened += (id, r) => opened = id + ":" + r.Gold + "/" + r.ExpItems;
            Assert.That(w.TryOpen("c_common"), Is.True);
            Assert.That(opened, Is.EqualTo("c_common:300/2"));
            Assert.That(wallet.Get(Currency.Gold), Is.EqualTo(300));
            Assert.That(wallet.Get(Currency.AdventureExp), Is.EqualTo(2));
            Assert.That(w.TryOpen("c_common"), Is.False);
            Assert.That(w.LastChestBlock, Is.EqualTo(ChestBlock.AlreadyOpen));
            Assert.That(wallet.Get(Currency.Gold), Is.EqualTo(300), "no second reward");
            Assert.That(w.TryOpen("c_fancy"), Is.True);
            Assert.That(wallet.Get(Currency.Gold), Is.EqualTo(1300));
            Assert.That(wallet.Get(Currency.AdventureExp), Is.EqualTo(7));
            Assert.That(w.TryOpen("nope"), Is.False);
            Assert.That(w.LastChestBlock, Is.EqualTo(ChestBlock.Unknown));
            Assert.That(w.PuzzleOf("c_fancy"), Is.Null, "empty puzzle id = none");
        }

        [Test]
        public void Chest_WithoutWallet_StillOpens()
        {
            var w = NewWorld(null);
            Assert.That(w.TryOpen("c_common"), Is.True);
            Assert.That(w.LastReward.Gold, Is.EqualTo(300));
        }

        // ---- 퍼즐 ----

        [Test]
        public void PuzzleChest_LockedUntilEveryPlate_PlatesStayLit()
        {
            var wallet = new Wallet();
            var w = NewWorld(wallet);
            Assert.That(w.CheckOpen("c_puzzle"), Is.EqualTo(ChestBlock.Locked));
            Assert.That(w.TryOpen("c_puzzle"), Is.False);
            Assert.That(wallet.Get(Currency.Gold), Is.EqualTo(0));
            Assert.That(w.PressPlate("pz", 0), Is.EqualTo(PlateResult.Lit));
            Assert.That(w.PressPlate("pz", 0), Is.EqualTo(PlateResult.AlreadyLit), "stays lit");
            Assert.That(w.PressPlate("pz", 3), Is.EqualTo(PlateResult.Unknown));
            Assert.That(w.PressPlate("other", 0), Is.EqualTo(PlateResult.Unknown));
            Assert.That(w.PressPlate("pz", 2), Is.EqualTo(PlateResult.Lit));
            Assert.That(w.LitCount("pz"), Is.EqualTo(2));
            Assert.That(w.CheckOpen("c_puzzle"), Is.EqualTo(ChestBlock.Locked));
            Assert.That(w.PressPlate("pz", 1), Is.EqualTo(PlateResult.Solved));
            Assert.That(w.IsSolved("pz"), Is.True);
            Assert.That(w.PressPlate("pz", 1), Is.EqualTo(PlateResult.AlreadyLit));
            Assert.That(w.CheckOpen("c_puzzle"), Is.EqualTo(ChestBlock.None));
            Assert.That(w.TryOpen("c_puzzle"), Is.True);
            Assert.That(wallet.Get(Currency.Gold), Is.EqualTo(1000));
        }

        // ---- 디버그 ----

        [Test]
        public void OpenAll_ActivateAll_Reset()
        {
            var wallet = new Wallet();
            var w = NewWorld(wallet);
            w.ActivateAll();
            Assert.That(w.IsActivated("wp_a") && w.IsActivated("wp_b"), Is.True);
            Assert.That(w.OpenAll(), Is.EqualTo(3), "puzzle solved first, then every chest");
            Assert.That(wallet.Get(Currency.Gold), Is.EqualTo(2300));
            Assert.That(w.OpenAll(), Is.EqualTo(0));
            w.ResetAll();
            Assert.That(w.IsActivated("wp_a") || w.IsOpened("c_common") || w.IsSolved("pz"), Is.False);
            Assert.That(w.LitCount("pz"), Is.EqualTo(0));
            Assert.That(wallet.Get(Currency.Gold), Is.EqualTo(2300), "reset does not take rewards back");
        }

        [Test]
        public void Register_RejectsBlankAndDuplicateIds()
        {
            var w = new WorldProgress(WorldR(), null);
            w.RegisterWaypoint("a");
            Assert.Throws<ArgumentException>(() => w.RegisterWaypoint("a"));
            Assert.Throws<ArgumentException>(() => w.RegisterWaypoint(""));
            w.RegisterChest("c", ChestTier.Common, null);
            Assert.Throws<ArgumentException>(() => w.RegisterChest("c", ChestTier.Exquisite, null));
            Assert.Throws<ArgumentException>(() => w.RegisterPuzzle("p", 0));
            Assert.Throws<ArgumentNullException>(() => new WorldProgress(null, null));
        }

        // ---- 저장 ----

        [Test]
        public void World_SaveRoundTrip_KeepsOtherRegions_PlatesNotSaved()
        {
            var w = NewWorld();
            w.TryActivate("wp_b");
            w.TryOpen("c_common");
            w.PressPlate("pz", 0);
            WorldSave save = w.ToSave();
            Assert.That(save.waypoints, Is.EqualTo(new[] { "wp_b" }));
            Assert.That(save.chests, Is.EqualTo(new[] { "c_common" }));
            Assert.That(save.puzzles, Is.Empty, "unsolved plates are not saved");

            save.waypoints = new[] { "wp_b", "wp_far_region" };
            save.puzzles = new[] { "pz" };
            var q = NewWorld();
            Assert.That(q.Load(save), Is.EqualTo(0));
            Assert.That(q.IsActivated("wp_b") && !q.IsActivated("wp_a"), Is.True);
            Assert.That(q.IsOpened("c_common"), Is.True);
            Assert.That(q.IsSolved("pz") && q.IsPlateLit("pz", 2), Is.True, "solved puzzle shows every plate lit");
            Assert.That(q.CheckOpen("c_puzzle"), Is.EqualTo(ChestBlock.None));
            Assert.That(q.ToSave().waypoints, Is.EqualTo(new[] { "wp_b", "wp_far_region" }), "ids from elsewhere kept, sorted");
        }

        [Test]
        public void World_Load_DropsBlankAndDuplicates_NullIsFresh()
        {
            var w = NewWorld();
            w.TryActivate("wp_a");
            Assert.That(w.Load(new WorldSave { waypoints = new[] { "wp_b", "", null, "wp_b" }, chests = null, puzzles = new string[0] }), Is.EqualTo(3));
            Assert.That(w.IsActivated("wp_a"), Is.False, "load replaces");
            Assert.That(w.ToSave().waypoints, Is.EqualTo(new[] { "wp_b" }));
            Assert.That(w.Load(null), Is.EqualTo(0));
            Assert.That(w.ToSave().waypoints, Is.Empty);
        }

        [Test]
        public void SaveFormat_Upgrade_1To2_AddsEmptyWorld()
        {
            Assert.That(SaveFormat.Version, Is.EqualTo(2));
            var old = new SaveSnapshot { version = 1, world = null, wallet = new[] { new CurrencySave { id = "Gold", amount = 77 } } };
            Assert.That(SaveFormat.Validate(old, out _), Is.True, "format 1 does not need world");
            Assert.That(SaveFormat.Upgrade(old), Is.EqualTo(1));
            Assert.That(old.version, Is.EqualTo(2));
            Assert.That(old.world, Is.Not.Null);
            Assert.That(old.world.waypoints.Length + old.world.chests.Length + old.world.puzzles.Length, Is.EqualTo(0));
            Assert.That(old.wallet[0].amount, Is.EqualTo(77), "growth untouched");
            Assert.That(SaveFormat.Validate(old, out _), Is.True);
            Assert.That(SaveFormat.Upgrade(old), Is.EqualTo(0), "already current");
            Assert.That(SaveFormat.Upgrade(new SaveSnapshot { version = 0 }), Is.EqualTo(0));
            Assert.That(SaveFormat.Upgrade(new SaveSnapshot { version = 99 }), Is.EqualTo(0));
            Assert.That(SaveFormat.Upgrade(null), Is.EqualTo(0));
        }

        [Test]
        public void SaveFormat_Version2_NeedsWorld()
        {
            Assert.That(SaveFormat.Validate(new SaveSnapshot { version = 2 }, out _), Is.True, "new snapshot has an empty world");
            Assert.That(SaveFormat.Validate(new SaveSnapshot { version = 2, world = null }, out string reason), Is.False);
            Assert.That(reason, Does.Contain("world"));
            Assert.That(SaveFormat.Validate(new SaveSnapshot { version = 2, world = new WorldSave { chests = null } }, out _), Is.False);
        }

        [Test]
        public void ExplorationRules_RewardPerTier_MissingTierIsZero()
        {
            var r = new ExplorationRules(new[] { new ChestReward(300, 2) }, true);
            Assert.That(r.Reward(ChestTier.Common).Gold, Is.EqualTo(300));
            Assert.That(r.Reward(ChestTier.Exquisite).Gold, Is.EqualTo(0));
            Assert.That(r.TeleportHealsParty, Is.True);
            Assert.That(Enum.GetValues(typeof(ChestTier)).Cast<ChestTier>().Select(t => (int)t).ToArray(), Is.EqualTo(new[] { 0, 1 }), "tiers are saved in assets as numbers - append only");
        }
    }
}
