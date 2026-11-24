using System.IO;
using Moa.Unity;
using Moa.Unity.Cameras;
using Moa.Unity.Data;
using Moa.Unity.Input;
using Moa.Unity.Player;
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
        const string GameConfigPath = "Assets/_Game/Data/GameConfig.asset";
        const string GrayboxFolder = "Assets/_Game/Art/Graybox";
        const string CheckerPath = GrayboxFolder + "/Checker.png";
        const string LitShader = "Universal Render Pipeline/Lit";

        static readonly string[] GeneratedRoots = { "GameRoot", "Main Camera", "Directional Light", "Player", "Environment", "DebugPoints" };

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
            EnsureFolder(GrayboxFolder);
            EnsureFolder("Assets/_Game/Scenes");

            // 씬을 먼저 연다 — 씬을 열면 쓰이지 않는 에셋이 내려가 미리 읽어 둔 참조가 빈다
            Scene scene = File.Exists(ScenePath)
                ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var inputAsset = EnsureInputAsset();
            var move = LoadOrCreate<MovementTuning>(MovementTuningPath);
            var cam = LoadOrCreate<CameraTuning>(CameraTuningPath);
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
            var player = BuildPlayer(move, inputAsset, mats);
            var camera = BuildCamera(cam, player.transform);
            BuildGameRoot(config, player, camera, points);

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
            public readonly Material Ground, Ramp, SteepRamp, Stairs, Wall, Pillar, Player, Nose;

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

        static PlayerMotor BuildPlayer(MovementTuning tuning, Object inputAsset, Materials mats)
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
            _ = body;

            var reader = go.AddComponent<PlayerInputReader>();
            Set(reader, "actions", inputAsset);
            var motor = go.AddComponent<PlayerMotor>();
            Set(motor, "tuning", tuning);
            Set(motor, "visual", visual);
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

        static void BuildGameRoot(GameConfig config, PlayerMotor motor, OrbitCamera camera, Transform[] points)
        {
            var go = new GameObject("GameRoot");
            var root = go.AddComponent<GameRoot>();
            var so = new SerializedObject(root);
            so.FindProperty("config").objectReferenceValue = config;
            so.FindProperty("playerInput").objectReferenceValue = motor.GetComponent<PlayerInputReader>();
            so.FindProperty("playerMotor").objectReferenceValue = motor;
            so.FindProperty("orbitCamera").objectReferenceValue = camera;
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
