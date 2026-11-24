using System.Text;
using Moa.Core.Ids;
using Moa.Core.Localization;
using Moa.Core.Rng;
using Moa.Unity.Cameras;
using Moa.Unity.Data;
using Moa.Unity.Player;
using Moa.Unity.UI;
using UnityEngine;

namespace Moa.Unity
{
    /// <summary>
    /// 컴포지션 루트 — 씬 연결은 여기 한 곳 (MY_DEV_GUIDE 9번). 싱글턴을 두지 않는다.
    /// 새 판 시작과 디버그 재시작은 같은 <see cref="StartRun"/>을 탄다.
    /// 개발자 진입점은 DebugXxx (에디터 디버그 창이 부른다).
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class GameRoot : MonoBehaviour
    {
        [SerializeField] GameConfig config;
        [Tooltip("Phase 0 temporary label (text table check)")]
        [SerializeField] LocalizedLabel bootLabel;

        [Header("Player (1-1, empty in scenes without a player)")]
        [Tooltip("Component implementing IPlayerInput (PlayerInputReader)")]
        [SerializeField] MonoBehaviour playerInput;
        [SerializeField] PlayerMotor playerMotor;
        [SerializeField] OrbitCamera orbitCamera;
        [Tooltip("Debug window teleport targets")]
        [SerializeField] Transform[] debugTeleportPoints;

        IPlayerInput _input;

        int _seed;
        RandomStreams _random;
        IdGenerator _ids;
        int _runCount;

        void Awake()
        {
            if (config == null) { Debug.LogError("GameRoot: GameConfig is not assigned"); enabled = false; return; }
            if (config.TextTable == null) { Debug.LogError("GameRoot: text table is not assigned in GameConfig"); enabled = false; return; }
            Text.Load(TextTable.ParseCsv(config.TextTable.text), config.DefaultLanguage);
            UiFont.ApplyTo(transform, config);
            StartRun(config.DefaultSeed);
            BindPlayer();
        }

        void BindPlayer()
        {
            if (playerMotor == null) return;
            _input = playerInput as IPlayerInput;
            if (_input == null) { Debug.LogError("GameRoot: playerInput does not implement IPlayerInput"); return; }
            playerMotor.Bind(_input, orbitCamera);
            if (orbitCamera != null) orbitCamera.Bind(_input, playerMotor);
        }

        void StartRun(int seed)
        {
            _seed = seed;
            _random = new RandomStreams(RandomStreams.SeedFromInt(seed));
            _ids = new IdGenerator();
            _runCount++;
            if (bootLabel != null) bootLabel.SetArgs(seed);
        }

        // ---- 디버그 진입점 (에디터 디버그 창 전용) ----

        public int DebugSeed => _seed;

        /// <summary>[상태 변경] 같은 시드로 새 판.</summary>
        public void DebugRestart() => StartRun(_seed);

        /// <summary>[상태 변경] 정한 시드로 새 판.</summary>
        public void DebugRestart(int seed) => StartRun(seed);

        /// <summary>[보기] 화면 언어 바꾸기.</summary>
        public void DebugSetLanguage(string language) => Text.SetLanguage(language);

        // ---- 플레이어 · 카메라 (1-1) ----

        public bool DebugHasPlayer => playerMotor != null && _input != null;
        public Vector3 DebugPlayerPosition => playerMotor != null ? playerMotor.transform.position : Vector3.zero;
        public Vector3 DebugPlayerVelocity => playerMotor != null ? playerMotor.Velocity : Vector3.zero;
        public bool DebugPlayerGrounded => playerMotor != null && playerMotor.Grounded;
        public bool DebugPlayerOnSteepSlope => playerMotor != null && playerMotor.OnSteepSlope;
        public bool DebugPlayerWalking => playerMotor != null && playerMotor.Walking;
        public float DebugPlayerYaw => playerMotor != null ? playerMotor.Yaw : 0f;
        public Transform[] DebugTeleportPoints => debugTeleportPoints;

        /// <summary>[상태 변경] 플레이어 순간이동 (속도 0, 카메라도 바로 따라감).</summary>
        public void DebugTeleportPlayer(Vector3 position)
        {
            if (playerMotor == null) return;
            playerMotor.Teleport(position);
            if (orbitCamera != null) orbitCamera.SnapToTarget();
        }

        public bool DebugHasCamera => orbitCamera != null;
        public float DebugCameraDistance => orbitCamera != null ? orbitCamera.Distance : 0f;
        public float DebugCameraDesiredDistance => orbitCamera != null ? orbitCamera.DesiredDistance : 0f;
        public float DebugCameraYaw => orbitCamera != null ? orbitCamera.Yaw : 0f;
        public float DebugCameraPitch => orbitCamera != null ? orbitCamera.Pitch : 0f;
        public bool DebugCameraBlocked => orbitCamera != null && orbitCamera.Blocked;
        public Camera DebugCamera => orbitCamera != null ? orbitCamera.GetComponent<Camera>() : null;

        /// <summary>[보기] 카메라 각도.</summary>
        public void DebugSetCameraAngles(float yaw, float pitch) { if (orbitCamera != null) orbitCamera.SetAngles(yaw, pitch); }

        // 입력 주입 (에디터 포커스 없이 확인할 때) — DebugInputClear까지 실제 장치 입력 대신 쓴다
        public void DebugInputMove(Vector2 move) => _input?.DebugSetMove(move);
        public void DebugInputLook(Vector2 mousePixels) => _input?.DebugAddLook(mousePixels);
        public void DebugInputJump() => _input?.DebugPressJump();
        public void DebugInputZoom(float scroll) => _input?.DebugZoom(scroll);
        public void DebugInputWalk(bool walk) => _input?.DebugSetWalk(walk);
        public void DebugInputClear() => _input?.DebugClear();
        public bool DebugInputOverriding => _input != null && _input.DebugOverriding;

        /// <summary>지금 상태 요약 (개발자용, 텍스트 표 대상 아님).</summary>
        public string DebugState()
        {
            var sb = new StringBuilder();
            sb.Append("run: ").Append(_runCount).AppendLine();
            sb.Append("seed: ").Append(_seed).Append(" (master ").Append(_random != null ? _random.MasterSeed.ToString() : "-").Append(')').AppendLine();
            if (_random != null)
            {
                foreach (RandomStream s in System.Enum.GetValues(typeof(RandomStream)))
                    sb.Append("  rng ").Append(s).Append(" state: ").Append(_random.Get(s).State).AppendLine();
            }
            sb.Append("ids issued: ").Append(_ids != null ? _ids.IssuedCount : 0).Append(", next: ").Append(_ids != null ? _ids.NextValue : 0).AppendLine();
            sb.Append("language: ").Append(Text.Language ?? "-");
            if (Text.Table != null) sb.Append(" / ").Append(string.Join(",", Text.Table.Languages)).Append(" (keys ").Append(Text.Table.Keys.Count).Append(')');
            return sb.ToString();
        }
    }
}
