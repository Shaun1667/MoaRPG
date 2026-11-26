using System;
using System.IO;
using Moa.Core.Growth;
using Moa.Core.Motion;
using Moa.Core.World;
using Moa.Unity.Data;
using Moa.Unity.Save;
using Moa.Unity.UI;
using Moa.Unity.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Moa.Tests
{
    /// <summary>
    /// 3-6 탐험 에셋 · 저장 검산: SwimTuning · Exploration 에셋이 BALANCE 18번과 같고, 수영 예산(가득 40초 · 질주 12초)이 맞고,
    /// 형식 1 저장 파일이 형식 2로 옮겨져 성장이 그대로 읽히고, 월드 진행이 저장 파일을 왕복하는지 — 임시 경로만 쓴다.
    /// 물 영역 깊이 · 물가 후보, 지도 화면 버튼(켜진 지점만 · 누르면 이동 · 닫힘). 값을 바꾸면 BALANCE.md와 이 테스트도 함께 고친다.
    /// </summary>
    public class ExplorationAssetTests
    {
        const string SwimPath = "Assets/_Game/Data/Player/SwimTuning.asset";
        const string StaminaPath = "Assets/_Game/Data/Player/StaminaTuning.asset";
        const string ExplorationPath = "Assets/_Game/Data/World/Exploration.asset";
        const string GrowthPath = "Assets/_Game/Data/Growth/Growth.asset";
        const string PartyPath = "Assets/_Game/Data/Characters/Party.asset";
        const string ClimbPath = "Assets/_Game/Data/Player/ClimbTuning.asset";
        const float Eps = 1e-4f;

        static T Load<T>(string path) where T : UnityEngine.Object
        {
            var t = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.That(t, Is.Not.Null, "missing " + path);
            return t;
        }

        static string[] PartyIds()
        {
            var p = Load<PartyTuning>(PartyPath);
            var ids = new string[p.MemberCount];
            for (int i = 0; i < ids.Length; i++) ids[i] = p.GetMember(i).SaveId;
            return ids;
        }

        static string TempPath() => Path.Combine(Path.GetTempPath(), "moa_explore_test_" + Guid.NewGuid().ToString("N"), SaveStore.FileName);

        static void CleanUp(string path)
        {
            string dir = Path.GetDirectoryName(path);
            if (dir != null && Directory.Exists(dir) && dir.StartsWith(Path.GetTempPath(), StringComparison.OrdinalIgnoreCase) && dir.Contains("moa_explore_test_"))
                Directory.Delete(dir, true);
        }

        [Test]
        public void Swim_MatchesBalance18()
        {
            var s = Load<SwimTuning>(SwimPath);
            Assert.That(s.EnterDepth, Is.EqualTo(1.2f).Within(Eps));
            Assert.That(s.ExitDepth, Is.EqualTo(1.0f).Within(Eps));
            Assert.That(s.FloatDepth, Is.EqualTo(1.3f).Within(Eps));
            Assert.That(s.FloatSharpness, Is.EqualTo(6f).Within(Eps));
            Assert.That(s.FloatMaxSpeed, Is.EqualTo(3f).Within(Eps));
            Assert.That(s.Speed, Is.EqualTo(3f).Within(Eps));
            Assert.That(s.SprintSpeed, Is.EqualTo(5.5f).Within(Eps));
            Assert.That(s.Acceleration, Is.EqualTo(10f).Within(Eps));
            Assert.That(s.DrainPerSecond, Is.EqualTo(6f).Within(Eps));
            Assert.That(s.SprintDrainPerSecond, Is.EqualTo(20f).Within(Eps));
            Assert.That(s.DrownHealthRatio, Is.EqualTo(0.25f).Within(Eps));
            Assert.That(s.ShoreMargin, Is.EqualTo(1.0f).Within(Eps));
        }

        [Test]
        public void Swim_IsConsistent_WithMovementAndStamina()
        {
            var s = Load<SwimTuning>(SwimPath);
            var st = Load<StaminaTuning>(StaminaPath);
            var mv = Load<MovementTuning>("Assets/_Game/Data/Player/MovementTuning.asset");
            Assert.That(s.ExitDepth, Is.LessThan(s.EnterDepth), "gap so swimming does not flicker");
            Assert.That(s.FloatDepth, Is.GreaterThanOrEqualTo(s.EnterDepth), "floating keeps swimming");
            Assert.That(s.FloatDepth, Is.LessThan(mv.CapsuleHeight), "head above the water");
            Assert.That(st.MaxStamina / s.DrainPerSecond, Is.EqualTo(40f).Within(Eps), "full stamina = 40 s in the water");
            Assert.That(st.MaxStamina / s.SprintDrainPerSecond, Is.EqualTo(12f).Within(Eps), "sprint swim = 12 s");
            Assert.That(s.Speed, Is.LessThan(mv.RunSpeed), "swimming is slower than running");
            Assert.That(s.SprintSpeed, Is.LessThanOrEqualTo(st.SprintSpeed));
            var climb = Load<ClimbTuning>(ClimbPath);
            Assert.That(s.ShoreMargin, Is.GreaterThan(mv.CapsuleRadius * 2f), "shore spot clear of the water edge");
            Assert.That(climb.DrainPerSecond, Is.GreaterThan(0f));
        }

        [Test]
        public void Exploration_MatchesBalance18()
        {
            var e = Load<ExplorationTuning>(ExplorationPath);
            Assert.That(e.WaypointRadius, Is.EqualTo(3f).Within(Eps));
            Assert.That(e.ChestRadius, Is.EqualTo(1.5f).Within(Eps));
            Assert.That(e.PlateRadius, Is.EqualTo(0.9f).Within(Eps));
            Assert.That(e.ReachHeight, Is.EqualTo(1.5f).Within(Eps));
            Assert.That(e.TeleportHealsParty, Is.False, "healing stays at healing points");
            ExplorationRules r = e.ToRules();
            Assert.That(r.Reward(ChestTier.Common).Gold, Is.EqualTo(300));
            Assert.That(r.Reward(ChestTier.Common).ExpItems, Is.EqualTo(2));
            Assert.That(r.Reward(ChestTier.Exquisite).Gold, Is.EqualTo(1000));
            Assert.That(r.Reward(ChestTier.Exquisite).ExpItems, Is.EqualTo(5));
            Assert.That(e.ChestRowCount, Is.EqualTo(Enum.GetValues(typeof(ChestTier)).Length), "one row per tier");
        }

        [Test]
        public void SaveStore_Format1File_UpgradesAndKeepsGrowth()
        {
            string path = TempPath();
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                // 3-5 형식 1 파일 (world 칸 없음)
                File.WriteAllText(path, "{\"version\": 1, \"wallet\": [{\"id\": \"Gold\", \"amount\": 4321}, {\"id\": \"AdventureExp\", \"amount\": 3}], " +
                    "\"characters\": [{\"id\": \"sword_hero\", \"level\": 25, \"exp\": 10, \"ascension\": 1, \"skillLevel\": 2, \"burstLevel\": 3, \"weaponLevel\": 4, \"weaponExp\": 0, \"refinement\": 1}]}");
                var store = new SaveStore(path);
                Assert.That(store.Load(out var snap), Is.EqualTo(SaveLoadStatus.Loaded), store.LastError);
                Assert.That(store.LastUpgradedFrom, Is.EqualTo(1));
                Assert.That(snap.version, Is.EqualTo(SaveFormat.Version));
                Assert.That(snap.world, Is.Not.Null);
                Assert.That(snap.world.waypoints, Is.Empty);

                var p = new Progression(Load<GrowthTuning>(GrowthPath).ToRules(), PartyIds());
                Assert.That(p.Load(snap), Is.EqualTo(0));
                Assert.That(p.Wallet.Get(Currency.Gold), Is.EqualTo(4321));
                int hero = p.IndexOf("sword_hero");
                Assert.That(hero, Is.GreaterThanOrEqualTo(0));
                Assert.That(p.Member(hero).Level, Is.EqualTo(25));
                Assert.That(p.Member(hero).BurstLevel, Is.EqualTo(3));
                var w = new WorldProgress(Load<ExplorationTuning>(ExplorationPath).ToRules(), p.Wallet);
                Assert.That(w.Load(snap.world), Is.EqualTo(0));

                // 다음 저장은 새 형식
                SaveSnapshot next = p.ToSnapshot();
                next.world = w.ToSave();
                Assert.That(store.Save(next), Is.True, store.LastError);
                string text = File.ReadAllText(path);
                Assert.That(text, Does.Contain("\"version\": 2"));
                Assert.That(text, Does.Contain("\"world\""));
                Assert.That(new SaveStore(path).Load(out _), Is.EqualTo(SaveLoadStatus.Loaded));
            }
            finally { CleanUp(path); }
        }

        [Test]
        public void SaveStore_WorldRoundTrips_OnATempPath()
        {
            string path = TempPath();
            try
            {
                var rules = Load<ExplorationTuning>(ExplorationPath).ToRules();
                var p = new Progression(Load<GrowthTuning>(GrowthPath).ToRules(), PartyIds());
                var w = new WorldProgress(rules, p.Wallet);
                w.RegisterWaypoint("wp_start");
                w.RegisterWaypoint("wp_pond");
                w.RegisterChest("chest_high", ChestTier.Exquisite, null);
                w.RegisterChest("chest_puzzle", ChestTier.Exquisite, "puzzle_plates");
                w.RegisterPuzzle("puzzle_plates", 3);
                w.TryActivate("wp_pond");
                Assert.That(w.TryOpen("chest_high"), Is.True);
                for (int i = 0; i < 3; i++) w.PressPlate("puzzle_plates", i);
                SaveSnapshot s = p.ToSnapshot();
                s.world = w.ToSave();
                var store = new SaveStore(path);
                Assert.That(store.Save(s), Is.True, store.LastError);

                Assert.That(new SaveStore(path).Load(out var snap), Is.EqualTo(SaveLoadStatus.Loaded));
                var q = new Progression(Load<GrowthTuning>(GrowthPath).ToRules(), PartyIds());
                q.Load(snap);
                var v = new WorldProgress(rules, q.Wallet);
                v.RegisterWaypoint("wp_start");
                v.RegisterWaypoint("wp_pond");
                v.RegisterChest("chest_high", ChestTier.Exquisite, null);
                v.RegisterChest("chest_puzzle", ChestTier.Exquisite, "puzzle_plates");
                v.RegisterPuzzle("puzzle_plates", 3);
                Assert.That(v.Load(snap.world), Is.EqualTo(0));
                Assert.That(v.IsActivated("wp_pond") && !v.IsActivated("wp_start"), Is.True);
                Assert.That(v.IsOpened("chest_high"), Is.True);
                Assert.That(v.CheckOpen("chest_high"), Is.EqualTo(ChestBlock.AlreadyOpen), "opened chests stay open");
                Assert.That(v.CheckOpen("chest_puzzle"), Is.EqualTo(ChestBlock.None), "solved puzzle stays solved");
                Assert.That(q.Wallet.Get(Currency.Gold), Is.EqualTo(1000), "reward saved with the wallet");
            }
            finally { CleanUp(path); }
        }

        [Test]
        public void WaterVolume_Depth_AndShoreCandidates()
        {
            var go = new GameObject("ExplorationAssetTests_Water");
            try
            {
                go.transform.position = new Vector3(32f, 1.3f, -28f);
                var v = go.AddComponent<WaterVolume>();
                var so = new SerializedObject(v);
                so.FindProperty("size").vector3Value = new Vector3(12f, 2.6f, 12f);
                so.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(v.SurfaceY, Is.EqualTo(2.6f).Within(Eps));
                Assert.That(v.DepthAt(new Vector3(32f, 0f, -28f)), Is.EqualTo(2.6f).Within(Eps));
                Assert.That(v.DepthAt(new Vector3(32f, 1.3f, -28f)), Is.EqualTo(1.3f).Within(Eps));
                Assert.That(v.DepthAt(new Vector3(32f, 3f, -28f)), Is.EqualTo(0f), "above the surface");
                Assert.That(v.DepthAt(new Vector3(39f, 1f, -28f)), Is.EqualTo(0f), "outside the area");
                Assert.That(WaterVolume.Deepest(new[] { v, null }, new Vector3(30f, 1f, -30f), out var water), Is.EqualTo(1.6f).Within(Eps));
                Assert.That(water, Is.SameAs(v));
                Vector3[] c = v.ShoreCandidates(new Vector3(36f, 1.3f, -28f), 1f);
                Assert.That(c[0].x, Is.EqualTo(39f).Within(Eps), "nearest edge first (east, 1 m out)");
                Assert.That(c[0].z, Is.EqualTo(-28f).Within(Eps));
                Assert.That(c.Length, Is.EqualTo(4));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void MapScreen_OnlyLitWaypoints_PressTeleportsAndCloses()
        {
            var canvas = new GameObject("ExplorationAssetTests_Canvas", typeof(RectTransform), typeof(Canvas));
            var a = new GameObject("ExplorationAssetTests_WpA");
            var b = new GameObject("ExplorationAssetTests_WpB");
            try
            {
                var wa = a.AddComponent<Waypoint>();
                var wb = b.AddComponent<Waypoint>();
                SetString(wa, "id", "wp_a");
                SetString(wb, "id", "wp_b");
                a.transform.position = new Vector3(30f, 0f, 0f);
                b.transform.position = new Vector3(-60f, 0f, 60f);
                var world = new WorldProgress(Load<ExplorationTuning>(ExplorationPath).ToRules(), null);
                world.RegisterWaypoint("wp_a");
                world.RegisterWaypoint("wp_b");
                world.TryActivate("wp_a");
                var screen = MapScreen.CreateIn(canvas.transform, 100, 2, 720f, 40f, new Vector2(240f, 32f), 60f, 36, 22, Vector2.zero, 120f,
                    Color.black, Color.gray, Color.green, Color.cyan, Color.gray, Color.yellow, Color.white, Color.black);
                string moved = null;
                screen.Bind(world, new[] { wa, wb }, null, null, id => { moved = id; return world.TryTeleport(id, false); }, null);
                Assert.That(screen.Open, Is.False);
                screen.SetOpen(true);
                Assert.That(screen.Open, Is.True);
                Assert.That(screen.ButtonEnabled(0), Is.True);
                Assert.That(screen.ButtonEnabled(1), Is.False, "not activated");
                Assert.That(screen.Press(1), Is.False);
                Assert.That(world.LastTeleportBlock, Is.EqualTo(WaypointBlock.NotActivated));
                Assert.That(screen.Open, Is.True);
                Assert.That(screen.Press(0), Is.True);
                Assert.That(moved, Is.EqualTo("wp_a"));
                Assert.That(screen.Open, Is.False, "closes after teleport");
                Vector2 pos = screen.ToMap(new Vector3(30f, 0f, 0f));
                Assert.That(pos.x, Is.EqualTo(180f).Within(0.01f), "30 m of 120 m = a quarter of 720 px");
                Assert.That(screen.ToMap(new Vector3(-60f, 0f, 60f)).y, Is.EqualTo(360f).Within(0.01f), "+Z is up");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(a);
                UnityEngine.Object.DestroyImmediate(b);
                UnityEngine.Object.DestroyImmediate(canvas);
            }
        }

        static void SetString(UnityEngine.Object target, string property, string value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(property).stringValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
