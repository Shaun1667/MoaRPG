using System.IO;
using Moa.Unity;
using Moa.Unity.Cameras;
using Moa.Unity.Combat;
using Moa.Unity.Data;
using Moa.Unity.Input;
using Moa.Unity.Player;
using Moa.Unity.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Moa.Editor.Setup
{
    /// <summary>
    /// 1-1 시험 지형 씬을 만든다 (메뉴 MoaRPG/Setup/Test Field). 몇 번 실행해도 같은 결과:
    /// 이 메뉴가 만드는 루트 오브젝트(<see cref="GeneratedRoots"/>)를 지우고 다시 만든다. 그 밖의 오브젝트는 건드리지 않는다.
    /// 없으면 만드는 것: 입력 액션 에셋 · 이동/카메라 수치 에셋(빈 값 — BALANCE.md 1 · 2번대로 채운다) · 그레이박스 머티리얼.
    /// 이미 있으면 그대로 둔다 (수치 · 키 배치를 덮어쓰지 않는다).
    /// 지형 배치 표는 아래 상수 — 새 경사 = Ramps에 한 줄.
    /// </summary>
    public static class TestFieldSetup
    {
        public const string MenuPath = "MoaRPG/Setup/Test Field";
        public const string ScenePath = "Assets/_Game/Scenes/TestField.unity";
        public const string InputPath = "Assets/_Game/Input/MoaControls.inputactions";
        public const string MovementTuningPath = "Assets/_Game/Data/Player/MovementTuning.asset";
        public const string CameraTuningPath = "Assets/_Game/Data/Player/CameraTuning.asset";
        public const string StaminaTuningPath = "Assets/_Game/Data/Player/StaminaTuning.asset";
        public const string ClimbTuningPath = "Assets/_Game/Data/Player/ClimbTuning.asset";
        public const string GlideTuningPath = "Assets/_Game/Data/Player/GlideTuning.asset";
        public const string WeaponPath = "Assets/_Game/Data/Weapons/Sword.asset";              // 2-1: 첫 무기 한손검
        public const string CombatTuningPath = "Assets/_Game/Data/Combat/CombatTuning.asset";
        public const string DummyTuningPath = "Assets/_Game/Data/Enemies/TrainingDummy.asset";
        const string GameConfigPath = "Assets/_Game/Data/GameConfig.asset";
        const string GrayboxFolder = "Assets/_Game/Art/Graybox";
        const string CheckerPath = GrayboxFolder + "/Checker.png";
        const string RingPath = GrayboxFolder + "/Ring.png";
        const string LitShader = "Universal Render Pipeline/Lit";
        const string TrailShader = "Sprites/Default";                       // 궤적 (정점 색 · 투명)
        const string TrailShaderFallback = "Universal Render Pipeline/Unlit";

        static readonly string[] GeneratedRoots = { "GameRoot", "Main Camera", "Directional Light", "Player", "Environment", "DebugPoints", "HUD", "Dummies" };

        // ---- 표현 표 (1-2, 게임 수치 아님) ----
        static readonly Color SprintColor = new Color(1.00f, 0.86f, 0.20f);      // 질주 중: 노랑
        static readonly Color DodgeColor = new Color(0.30f, 0.80f, 1.00f);       // 회피 중(무적 끝난 뒤): 하늘
        static readonly Color InvulnerableColor = new Color(1.00f, 1.00f, 1.00f); // 무적 중: 흰색
        static readonly Color ExhaustedColor = new Color(0.50f, 0.36f, 0.62f);   // 지침: 보라
        static readonly Color ClimbColor = new Color(0.35f, 0.85f, 0.45f);       // 등반 중(올라서기 포함): 초록 (1-3)
        static readonly Color GlideColor = new Color(0.95f, 0.55f, 0.85f);       // 활공 중: 분홍 (1-3)
        static readonly Color WingsColor = new Color(0.95f, 0.95f, 0.85f);       // 날개 상자 (1-3)
        static readonly Vector3 WingsSize = new Vector3(2.4f, 0.06f, 0.7f);      // 날개 상자 크기 (m, 표현 값)
        const float WingsHeightRatio = 0.85f;                                    // 날개 높이 = 키 × 이 비율
        static readonly Color GaugeColor = new Color(0.60f, 0.95f, 0.35f);       // 게이지 채움
        static readonly Color GaugeExhaustedColor = new Color(1.00f, 0.35f, 0.30f);
        static readonly Color GaugeBackColor = new Color(0f, 0f, 0f, 0.45f);
        static readonly Vector2 HudReference = new Vector2(1920f, 1080f);

        // ---- 표현 표 (2-1 전투, 게임 수치 아님) ----
        static readonly Color AttackColor = new Color(0.95f, 0.35f, 0.30f);      // 공격 중: 빨강 (무적 > 회피 > 공격 > 등반 …)
        static readonly Color SwordColor = new Color(0.82f, 0.86f, 0.92f);       // 칼 상자
        static readonly Color HiltColor = new Color(0.35f, 0.25f, 0.18f);
        static readonly Color TrailColor = new Color(1.00f, 1.00f, 1.00f, 0.75f); // 휘두르기 궤적 (끝으로 투명)
        const float TrailTime = 0.15f, TrailWidth = 0.25f;                       // 궤적 길이(초) · 너비(m)
        static readonly Vector3 SwordPivot = new Vector3(0.32f, 1.15f, 0.10f);   // 칼 회전 중심 (발 기준, m) — 오른손
        static readonly Vector3 BladeSize = new Vector3(0.07f, 0.03f, 0.90f);
        const float BladeOffset = 0.55f;                                          // 회전 중심에서 칼날 가운데까지 (로컬 +Z)
        static readonly Vector3 HiltSize = new Vector3(0.18f, 0.05f, 0.05f);
        static readonly Vector3 SwordRest = new Vector3(65f, 25f, 0f);           // 쉬는 자세 (회전 중심 로컬 오일러)
        static readonly Vector3 SwordSweepStart = new Vector3(10f, 80f, 0f);     // 옆 베기: 오른쪽 → 왼쪽 (짝수 타는 반대)
        static readonly Vector3 SwordSweepEnd = new Vector3(10f, -80f, 0f);
        static readonly Vector3 SwordFinisherStart = new Vector3(-60f, 0f, 0f);  // 마지막 타: 위 → 아래
        static readonly Vector3 SwordFinisherEnd = new Vector3(70f, 0f, 0f);
        static readonly Color DummyColor = new Color(0.80f, 0.66f, 0.42f);       // 허수아비 (짚 색)
        static readonly Color DummyFlashColor = new Color(1.00f, 1.00f, 1.00f);  // 맞으면 번쩍임
        static readonly Color DamageColor = new Color(1.00f, 0.92f, 0.55f);      // 피해 숫자
        static readonly Color DamageBlockedColor = new Color(0.70f, 0.70f, 0.70f); // 무적 대상 (체력 안 줌)
        static readonly Color DamageOutlineColor = new Color(0f, 0f, 0f, 0.8f);
        const float DummyHeight = 1.8f, DummyRadius = 0.35f;                     // 허수아비 몸 (m, 표현 · 충돌 크기)
        static readonly Vector3 DummyArmsSize = new Vector3(1.4f, 0.12f, 0.12f);

        // ---- 표현 표 (2-2 강공격 · 낙하, 게임 수치 아님) ----
        static readonly Color ChargedColor = new Color(0.62f, 0.06f, 0.20f);     // 강공격 중: 진홍 (무적 > 회피 > 낙하 > 강공격 > 공격 …)
        static readonly Color PlungeColor = new Color(0.10f, 0.20f, 0.62f);      // 낙하 중(착지 경직 포함): 진파랑
        static readonly Color ChargedTrailColor = new Color(1.00f, 0.25f, 0.40f, 0.85f); // 강공격 궤적
        static readonly Vector3 SwordChargedStart = new Vector3(5f, 115f, 0f);   // 강공격: 더 크게 오른쪽 → 왼쪽 (2베기는 반대)
        static readonly Vector3 SwordChargedEnd = new Vector3(5f, -115f, 0f);
        static readonly Vector3 SwordPlunge = new Vector3(90f, 0f, 0f);          // 낙하: 칼날을 아래로
        static readonly Color LandRingColor = new Color(0.35f, 0.60f, 1.00f, 0.90f); // 착지 범위 원
        const float LandRingWidth = 0.12f, LandRingTime = 0.4f;                  // 원 선 두께(m) · 보이는 시간(초)

        // ---- 배치 표 (시험 지형, 단위 m · 도) ----
        static readonly Vector3 Spawn = new Vector3(0f, 0.05f, 0f);
        const float GroundSize = 120f;
        const float CheckerMeters = 2f;           // 체커 한 장(2×2칸)이 덮는 길이
        const float RampStartZ = 6f, RampRise = 3f, RampWidth = 4f, RampThickness = 0.5f, PlatformDepth = 4f;
        static readonly (string name, float angle, float x)[] Ramps =
        {
            ("Slope15", 15f, -10f),
            ("Slope35", 35f, -4f),
            ("Slope50", 50f, 2f),
        };
        const float StairsX = 10f, StairsStartZ = 6f, StairsWidth = 3f, StepRise = 0.25f, StepDepth = 0.4f, StairsTopDepth = 3f;
        const int StepCount = 12;
        static readonly (string name, Vector3 center, Vector3 size)[] Walls =
        {
            ("WallLeft", new Vector3(-8f, 2f, -6f), new Vector3(0.5f, 4f, 8f)),
            ("WallRight", new Vector3(8f, 2f, -6f), new Vector3(0.5f, 4f, 8f)),
            ("WallBack", new Vector3(0f, 2f, -12f), new Vector3(10f, 4f, 0.5f)),
            ("LowWall", new Vector3(4f, 0.4f, -4f), new Vector3(3f, 0.8f, 0.5f)),
        };
        static readonly (string name, Vector3 center, Vector3 size) Pillar = ("TallPillar", new Vector3(16f, 10f, 0f), new Vector3(3f, 20f, 3f));
        static readonly (string name, Vector3 position)[] DebugPoints =
        {
            ("Spawn", new Vector3(0f, 0.05f, 0f)),
            ("Slope15", new Vector3(-10f, 0.05f, 3f)),
            ("Slope35", new Vector3(-4f, 0.05f, 3f)),
            ("Slope50", new Vector3(2f, 0.05f, 3f)),
            ("Stairs", new Vector3(10f, 0.05f, 3f)),
            ("WallBack", new Vector3(0f, 0.05f, -10.5f)),
            ("Pillar", new Vector3(16f, 0.05f, -3f)),
            ("PillarTop", new Vector3(16f, 20.05f, 0f)),   // 1-3: 기둥 꼭대기 (활공 시작)
            ("PillarAir", new Vector3(16f, 12f, -9f)),     // 1-3: 기둥 앞 공중 (+Z로 활공해 벽에 붙기)
            ("Dummies", new Vector3(-15.4f, 0.05f, -3.6f)), // 2-1: 허수아비 둘 앞 (+Z를 보면 둘이 부채꼴 안)
            ("DummiesAir", new Vector3(-15.4f, 10.05f, -3.6f)), // 2-2: 허수아비 위 10m (높은 낙하)
        };

        // 2-1: 허수아비 배치 (발 위치, 바라보는 방향 도) — 새 허수아비 = 줄 하나
        static readonly (string name, Vector3 position, float yaw)[] Dummies =
        {
            ("Dummy1", new Vector3(-16.0f, 0f, -2.0f), 180f),
            ("Dummy2", new Vector3(-14.8f, 0f, -2.0f), 180f),
            ("Dummy3", new Vector3(-12.0f, 0f, -6.0f), 180f),
        };

        [MenuItem(MenuPath)]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("TestFieldSetup: stop play mode first"); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Debug.Log("TestFieldSetup: " + Build());
        }

        /// <summary>씬을 열거나 만들고 생성 루트를 다시 만든 뒤 저장한다. 요약 글을 돌려준다.</summary>
        public static string Build()
        {
            EnsureFolder("Assets/_Game/Input");
            EnsureFolder("Assets/_Game/Data/Player");
            EnsureFolder("Assets/_Game/Data/Weapons");
            EnsureFolder("Assets/_Game/Data/Combat");
            EnsureFolder("Assets/_Game/Data/Enemies");
            EnsureFolder(GrayboxFolder);
            EnsureFolder("Assets/_Game/Scenes");

            // 씬을 먼저 연다 — 씬을 열면 쓰이지 않는 에셋이 내려가 미리 읽어 둔 참조가 빈다
            Scene scene = File.Exists(ScenePath)
                ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var inputAsset = EnsureInputAsset();
            var move = LoadOrCreate<MovementTuning>(MovementTuningPath);
            var cam = LoadOrCreate<CameraTuning>(CameraTuningPath);
            var stamina = LoadOrCreate<StaminaTuning>(StaminaTuningPath);
            var climb = LoadOrCreate<ClimbTuning>(ClimbTuningPath);
            var glide = LoadOrCreate<GlideTuning>(GlideTuningPath);
            var weapon = LoadOrCreate<WeaponTuning>(WeaponPath);
            var combat = LoadOrCreate<CombatTuning>(CombatTuningPath);
            var dummyTuning = LoadOrCreate<DummyTuning>(DummyTuningPath);
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(GameConfigPath);
            if (config == null) Debug.LogError("TestFieldSetup: GameConfig missing at " + GameConfigPath);
            if (inputAsset == null) Debug.LogError("TestFieldSetup: input actions missing at " + InputPath);

            var mats = new Materials();

            foreach (var go in scene.GetRootGameObjects())
                if (System.Array.IndexOf(GeneratedRoots, go.name) >= 0) Object.DestroyImmediate(go);

            if (RenderSettings.skybox == null) RenderSettings.skybox = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat");
            RenderSettings.ambientMode = AmbientMode.Skybox;

            BuildLight();
            BuildEnvironment(mats);
            var points = BuildDebugPoints();
            var player = BuildPlayer(move, stamina, climb, glide, weapon, combat, inputAsset, mats);
            var camera = BuildCamera(cam, player.transform);
            var gauge = BuildHud();
            var numbers = DamageNumbers.CreateIn(gauge.transform.parent, DamageColor, DamageBlockedColor, DamageOutlineColor);
            var dummies = BuildDummies(dummyTuning, mats);
            BuildGameRoot(config, player, camera, points, gauge, numbers, dummies);

            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene, ScenePath);
            return "scene " + ScenePath + (saved ? " saved" : " NOT saved") + ", roots " + string.Join(",", GeneratedRoots);
        }

        // ---- 에셋 ----

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        static Object EnsureInputAsset()
        {
            if (!File.Exists(InputPath))
            {
                File.WriteAllText(InputPath, PlayerControls.BuildDefaultJson());
                AssetDatabase.ImportAsset(InputPath);
            }
            return AssetDatabase.LoadMainAssetAtPath(InputPath);
        }

        static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a != null) return a;
            a = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(a, path);
            Debug.LogWarning("TestFieldSetup: created empty " + path + " - fill the values from BALANCE.md");
            return a;
        }

        sealed class Materials
        {
            public readonly Material Ground, Ramp, SteepRamp, Stairs, Wall, Pillar, Player, Nose, Wings, Dummy, Sword, Hilt, SwordTrail;

            public Materials()
            {
                var checker = EnsureChecker();
                float tiles = GroundSize / CheckerMeters;
                Ground = Get("Ground", new Color(0.62f, 0.66f, 0.62f), checker, new Vector2(tiles, tiles));
                Ramp = Get("Ramp", new Color(0.50f, 0.66f, 0.86f), null, Vector2.one);
                SteepRamp = Get("SteepRamp", new Color(0.86f, 0.50f, 0.46f), null, Vector2.one);
                Stairs = Get("Stairs", new Color(0.78f, 0.72f, 0.56f), null, Vector2.one);
                Wall = Get("Wall", new Color(0.70f, 0.70f, 0.76f), null, Vector2.one);
                Pillar = Get("Pillar", new Color(0.55f, 0.55f, 0.62f), null, Vector2.one);
                Player = Get("Player", new Color(1.00f, 0.62f, 0.22f), null, Vector2.one);
                Nose = Get("PlayerNose", new Color(0.15f, 0.15f, 0.20f), null, Vector2.one);
                Wings = Get("PlayerWings", WingsColor, null, Vector2.one);
                Dummy = Get("Dummy", DummyColor, null, Vector2.one);
                Sword = Get("Sword", SwordColor, null, Vector2.one);
                Hilt = Get("SwordHilt", HiltColor, null, Vector2.one);
                SwordTrail = GetTrail("SwordTrail");
            }

            static Material Get(string name, Color color, Texture tex, Vector2 tiling)
            {
                string path = GrayboxFolder + "/" + name + ".mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m != null) return m;
                m = new Material(Shader.Find(LitShader)) { name = name };
                m.SetColor("_BaseColor", color);
                if (tex != null)
                {
                    m.SetTexture("_BaseMap", tex);
                    m.SetTextureScale("_BaseMap", tiling);
                }
                m.SetFloat("_Smoothness", 0.1f);
                AssetDatabase.CreateAsset(m, path);
                return m;
            }

            /// <summary>궤적용 반투명 머티리얼 (색은 TrailRenderer가 입힌다).</summary>
            static Material GetTrail(string name)
            {
                string path = GrayboxFolder + "/" + name + ".mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m != null) return m;
                Shader shader = Shader.Find(TrailShader) ?? Shader.Find(TrailShaderFallback);
                m = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(m, path);
                return m;
            }

            static Texture2D EnsureChecker()
            {
                if (!File.Exists(CheckerPath))
                {
                    const int size = 64, half = size / 2;
                    var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
                    var light = new Color(0.95f, 0.95f, 0.95f);
                    var dark = new Color(0.78f, 0.78f, 0.78f);
                    for (int y = 0; y < size; y++)
                        for (int x = 0; x < size; x++)
                            t.SetPixel(x, y, ((x < half) ^ (y < half)) ? dark : light);
                    t.Apply();
                    File.WriteAllBytes(CheckerPath, t.EncodeToPNG());
                    Object.DestroyImmediate(t);
                    AssetDatabase.ImportAsset(CheckerPath);
                    var imp = (TextureImporter)AssetImporter.GetAtPath(CheckerPath);
                    imp.wrapMode = TextureWrapMode.Repeat;
                    imp.filterMode = FilterMode.Point;
                    imp.SaveAndReimport();
                }
                return AssetDatabase.LoadAssetAtPath<Texture2D>(CheckerPath);
            }
        }

        // ---- 씬 ----

        static void BuildLight()
        {
            var go = new GameObject("Directional Light");
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.color = new Color(1f, 0.96f, 0.88f);
            light.shadows = LightShadows.Soft;
            go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        static void BuildEnvironment(Materials mats)
        {
            var env = new GameObject("Environment").transform;
            Box(env, "Ground", new Vector3(0f, -0.5f, 0f), Quaternion.identity, new Vector3(GroundSize, 1f, GroundSize), mats.Ground);

            foreach (var r in Ramps)
            {
                float a = r.angle * Mathf.Deg2Rad;
                float length = RampRise / Mathf.Sin(a);
                // 윗면의 아래 가장자리가 (x, 0, RampStartZ)에 오도록
                var center = new Vector3(r.x,
                    -Mathf.Cos(a) * RampThickness * 0.5f + Mathf.Sin(a) * length * 0.5f,
                    RampStartZ + Mathf.Sin(a) * RampThickness * 0.5f + Mathf.Cos(a) * length * 0.5f);
                var mat = r.angle > 45f ? mats.SteepRamp : mats.Ramp;
                Box(env, r.name, center, Quaternion.Euler(-r.angle, 0f, 0f), new Vector3(RampWidth, RampThickness, length), mat);
                float topZ = RampStartZ + Mathf.Cos(a) * length;
                Box(env, r.name + "_Top", new Vector3(r.x, RampRise * 0.5f, topZ + PlatformDepth * 0.5f), Quaternion.identity,
                    new Vector3(RampWidth, RampRise, PlatformDepth), mats.Pillar);
            }

            var stairs = new GameObject("Stairs").transform;
            stairs.SetParent(env, false);
            for (int i = 0; i < StepCount; i++)
            {
                float h = StepRise * (i + 1);
                Box(stairs, "Step" + (i + 1).ToString("00"), new Vector3(StairsX, h * 0.5f, StairsStartZ + StepDepth * (i + 0.5f)), Quaternion.identity,
                    new Vector3(StairsWidth, h, StepDepth), mats.Stairs);
            }
            float topH = StepRise * StepCount;
            Box(stairs, "StairsTop", new Vector3(StairsX, topH * 0.5f, StairsStartZ + StepDepth * StepCount + StairsTopDepth * 0.5f), Quaternion.identity,
                new Vector3(StairsWidth, topH, StairsTopDepth), mats.Stairs);

            foreach (var w in Walls) Box(env, w.name, w.center, Quaternion.identity, w.size, mats.Wall);
            Box(env, Pillar.name, Pillar.center, Quaternion.identity, Pillar.size, mats.Pillar);
        }

        static Transform[] BuildDebugPoints()
        {
            var root = new GameObject("DebugPoints").transform;
            var result = new Transform[DebugPoints.Length];
            for (int i = 0; i < DebugPoints.Length; i++)
            {
                var t = new GameObject(DebugPoints[i].name).transform;
                t.SetParent(root, false);
                t.position = DebugPoints[i].position;
                result[i] = t;
            }
            return result;
        }

        static PlayerMotor BuildPlayer(MovementTuning tuning, StaminaTuning stamina, ClimbTuning climb, GlideTuning glide,
            WeaponTuning weapon, CombatTuning combatTuning, Object inputAsset, Materials mats)
        {
            float height = tuning.CapsuleHeight, radius = tuning.CapsuleRadius;
            if (height <= 0f || radius <= 0f) Debug.LogWarning("TestFieldSetup: MovementTuning capsule size is empty - fill it and run again");

            var go = new GameObject("Player");
            go.transform.SetPositionAndRotation(Spawn, Quaternion.identity);
            var cc = go.AddComponent<CharacterController>();
            cc.height = height;
            cc.radius = radius;
            cc.center = new Vector3(0f, height * 0.5f, 0f);
            cc.slopeLimit = tuning.SlopeLimit;
            cc.stepOffset = tuning.StepOffset;
            cc.skinWidth = tuning.SkinWidth;

            // 비주얼은 자식으로 분리 — 나중에 캐릭터 모델로 바꾼다
            var visual = new GameObject("Visual").transform;
            visual.SetParent(go.transform, false);
            var body = Primitive(PrimitiveType.Capsule, visual, "Body", new Vector3(0f, height * 0.5f, 0f), Quaternion.identity,
                new Vector3(radius * 2f, height * 0.5f, radius * 2f), mats.Player);
            float nose = radius * 0.5f;
            Primitive(PrimitiveType.Cube, visual, "Nose", new Vector3(0f, height * 0.78f, radius + nose * 0.4f), Quaternion.identity,
                new Vector3(nose, nose, nose * 1.6f), mats.Nose);
            var wings = Primitive(PrimitiveType.Cube, visual, "Wings", new Vector3(0f, height * WingsHeightRatio, 0f), Quaternion.identity,
                WingsSize, mats.Wings);
            wings.SetActive(false); // 활공 중에만 보인다 (PlayerGrayboxFeedback)

            var reader = go.AddComponent<PlayerInputReader>();
            Set(reader, "actions", inputAsset);
            var motor = go.AddComponent<PlayerMotor>();
            Set(motor, "tuning", tuning);
            Set(motor, "visual", visual);
            Set(motor, "staminaTuning", stamina);
            Set(motor, "climbTuning", climb);
            Set(motor, "glideTuning", glide);

            var feedback = go.AddComponent<PlayerGrayboxFeedback>();
            Set(feedback, "body", body.GetComponent<MeshRenderer>());
            SetColor(feedback, "sprintColor", SprintColor);
            SetColor(feedback, "dodgeColor", DodgeColor);
            SetColor(feedback, "invulnerableColor", InvulnerableColor);
            SetColor(feedback, "exhaustedColor", ExhaustedColor);
            SetColor(feedback, "climbColor", ClimbColor);
            SetColor(feedback, "glideColor", GlideColor);
            Set(feedback, "wings", wings);
            SetColor(feedback, "attackColor", AttackColor);
            SetColor(feedback, "chargedColor", ChargedColor);
            SetColor(feedback, "plungeColor", PlungeColor);

            // 2-1: 일반 공격 + 그레이박스 칼 (회전 중심 → 칼날 상자 · 손잡이 · 끝에 궤적)
            var combat = go.AddComponent<PlayerCombat>();
            Set(combat, "weapon", weapon);
            Set(combat, "tuning", combatTuning);
            var pivot = new GameObject("SwordPivot").transform;
            pivot.SetParent(visual, false);
            pivot.localPosition = SwordPivot;
            pivot.localRotation = Quaternion.Euler(SwordRest);
            Primitive(PrimitiveType.Cube, pivot, "Blade", new Vector3(0f, 0f, BladeOffset), Quaternion.identity, BladeSize, mats.Sword);
            Primitive(PrimitiveType.Cube, pivot, "Hilt", new Vector3(0f, 0f, BladeOffset - BladeSize.z * 0.5f), Quaternion.identity, HiltSize, mats.Hilt);
            var tip = new GameObject("Tip").transform;
            tip.SetParent(pivot, false);
            tip.localPosition = new Vector3(0f, 0f, BladeOffset + BladeSize.z * 0.5f);
            var trail = tip.gameObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial = mats.SwordTrail;
            trail.time = TrailTime;
            trail.widthMultiplier = TrailWidth;
            trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(TrailColor, 0f), new GradientColorKey(TrailColor, 1f) },
                new[] { new GradientAlphaKey(TrailColor.a, 0f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = g;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.emitting = false;
            var swing = go.AddComponent<SwordSwingVisual>();
            Set(swing, "combat", combat);
            Set(swing, "pivot", pivot);
            Set(swing, "trail", trail);
            SetVector(swing, "restEuler", SwordRest);
            SetVector(swing, "sweepStartEuler", SwordSweepStart);
            SetVector(swing, "sweepEndEuler", SwordSweepEnd);
            SetVector(swing, "finisherStartEuler", SwordFinisherStart);
            SetVector(swing, "finisherEndEuler", SwordFinisherEnd);
            SetVector(swing, "chargedStartEuler", SwordChargedStart);
            SetVector(swing, "chargedEndEuler", SwordChargedEnd);
            SetColor(swing, "chargedTrailColor", ChargedTrailColor);
            SetVector(swing, "plungeEuler", SwordPlunge);
            // 2-2: 낙하 착지 원 (월드 좌표 고리, 착지할 때만 보인다)
            var ringGo = new GameObject("LandRing");
            ringGo.transform.SetParent(go.transform, false);
            var ring = ringGo.AddComponent<LineRenderer>();
            ring.sharedMaterial = mats.SwordTrail;
            ring.useWorldSpace = true;
            ring.loop = true;
            ring.widthMultiplier = LandRingWidth;
            ring.startColor = LandRingColor;
            ring.endColor = LandRingColor;
            ring.shadowCastingMode = ShadowCastingMode.Off;
            ring.positionCount = 0;
            ring.enabled = false;
            Set(swing, "landRing", ring);
            SetFloat(swing, "landRingTime", LandRingTime);
            return motor;
        }

        static OrbitCamera BuildCamera(CameraTuning tuning, Transform player)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            go.AddComponent<Camera>();
            go.AddComponent<AudioListener>();
            var orbit = go.AddComponent<OrbitCamera>();
            Set(orbit, "tuning", tuning);
            go.transform.position = player.position + new Vector3(0f, tuning.PivotHeight + 1f, -tuning.StartDistance);
            go.transform.LookAt(player.position + Vector3.up * tuning.PivotHeight);
            return orbit;
        }

        /// <summary>화면 HUD (1-2): Overlay 캔버스 + 스태미나 원형 게이지. 만드는 코드는 StaminaGauge.CreateHud (uGUI 참조가 Moa.Unity에 있어서). 글 없음.</summary>
        static StaminaGauge BuildHud()
        {
            return StaminaGauge.CreateHud("HUD", EnsureRing(), HudReference, GaugeBackColor, GaugeColor, GaugeExhaustedColor);
        }



        /// <summary>원형 게이지용 링 스프라이트 (없을 때만 만든다, 흰색 — 색은 Image가 입힌다).</summary>
        static Sprite EnsureRing()
        {
            if (!File.Exists(RingPath))
            {
                const int size = 128;
                const float outer = 63f, inner = 47f;
                var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
                float c = (size - 1) * 0.5f;
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                        float a = Mathf.Clamp01(outer - d + 0.5f) * Mathf.Clamp01(d - inner + 0.5f);
                        t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                    }
                t.Apply();
                File.WriteAllBytes(RingPath, t.EncodeToPNG());
                Object.DestroyImmediate(t);
                AssetDatabase.ImportAsset(RingPath);
                var imp = (TextureImporter)AssetImporter.GetAtPath(RingPath);
                imp.textureType = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.alphaIsTransparency = true;
                imp.mipmapEnabled = false;
                imp.wrapMode = TextureWrapMode.Clamp;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(RingPath);
        }

        /// <summary>허수아비 (2-1): 루트에 캡슐 콜라이더 + TrainingDummy, 자식 Visual에 몸 캡슐 · 팔 상자 (번쩍임 대상). 배치는 Dummies 표.</summary>
        static TrainingDummy[] BuildDummies(DummyTuning tuning, Materials mats)
        {
            var root = new GameObject("Dummies").transform;
            var result = new TrainingDummy[Dummies.Length];
            for (int i = 0; i < Dummies.Length; i++)
            {
                var go = new GameObject(Dummies[i].name);
                go.transform.SetParent(root, false);
                go.transform.SetPositionAndRotation(Dummies[i].position, Quaternion.Euler(0f, Dummies[i].yaw, 0f));
                var col = go.AddComponent<CapsuleCollider>();
                col.height = DummyHeight;
                col.radius = DummyRadius;
                col.center = new Vector3(0f, DummyHeight * 0.5f, 0f);
                var visual = new GameObject("Visual").transform;
                visual.SetParent(go.transform, false);
                var body = Primitive(PrimitiveType.Capsule, visual, "Body", new Vector3(0f, DummyHeight * 0.5f, 0f), Quaternion.identity,
                    new Vector3(DummyRadius * 2f, DummyHeight * 0.5f, DummyRadius * 2f), mats.Dummy);
                var arms = Primitive(PrimitiveType.Cube, visual, "Arms", new Vector3(0f, DummyHeight * 0.72f, 0f), Quaternion.identity, DummyArmsSize, mats.Dummy);
                var dummy = go.AddComponent<TrainingDummy>();
                Set(dummy, "tuning", tuning);
                Set(dummy, "visual", visual);
                SetColor(dummy, "flashColor", DummyFlashColor);
                var so = new SerializedObject(dummy);
                var arr = so.FindProperty("flashRenderers");
                arr.arraySize = 2;
                arr.GetArrayElementAtIndex(0).objectReferenceValue = body.GetComponent<MeshRenderer>();
                arr.GetArrayElementAtIndex(1).objectReferenceValue = arms.GetComponent<MeshRenderer>();
                so.ApplyModifiedPropertiesWithoutUndo();
                result[i] = dummy;
            }
            return result;
        }

        static void BuildGameRoot(GameConfig config, PlayerMotor motor, OrbitCamera camera, Transform[] points, StaminaGauge gauge,
            DamageNumbers numbers, TrainingDummy[] dummies)
        {
            var go = new GameObject("GameRoot");
            var root = go.AddComponent<GameRoot>();
            var so = new SerializedObject(root);
            so.FindProperty("config").objectReferenceValue = config;
            so.FindProperty("playerInput").objectReferenceValue = motor.GetComponent<PlayerInputReader>();
            so.FindProperty("playerMotor").objectReferenceValue = motor;
            so.FindProperty("orbitCamera").objectReferenceValue = camera;
            so.FindProperty("staminaGauge").objectReferenceValue = gauge;
            so.FindProperty("playerCombat").objectReferenceValue = motor.GetComponent<PlayerCombat>();
            so.FindProperty("damageNumbers").objectReferenceValue = numbers;
            var dummyArr = so.FindProperty("trainingDummies");
            dummyArr.arraySize = dummies.Length;
            for (int i = 0; i < dummies.Length; i++) dummyArr.GetArrayElementAtIndex(i).objectReferenceValue = dummies[i];
            var arr = so.FindProperty("debugTeleportPoints");
            arr.arraySize = points.Length;
            for (int i = 0; i < points.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = points[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---- 도우미 ----

        static GameObject Box(Transform parent, string name, Vector3 center, Quaternion rotation, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(center, rotation);
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            go.isStatic = true;
            return go;
        }

        static GameObject Primitive(PrimitiveType type, Transform parent, string name, Vector3 localPos, Quaternion localRot, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>()); // 충돌은 CharacterController 하나만
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        static void SetVector(Object target, string property, Vector3 value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(property);
            if (p == null) { Debug.LogError("TestFieldSetup: no field '" + property + "' on " + target.GetType().Name); return; }
            p.vector3Value = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetFloat(Object target, string property, float value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(property);
            if (p == null) { Debug.LogError("TestFieldSetup: no field '" + property + "' on " + target.GetType().Name); return; }
            p.floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetColor(Object target, string property, Color value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(property);
            if (p == null) { Debug.LogError("TestFieldSetup: no field '" + property + "' on " + target.GetType().Name); return; }
            p.colorValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void Set(Object target, string property, Object value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(property);
            if (p == null) { Debug.LogError("TestFieldSetup: no field '" + property + "' on " + target.GetType().Name); return; }
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
