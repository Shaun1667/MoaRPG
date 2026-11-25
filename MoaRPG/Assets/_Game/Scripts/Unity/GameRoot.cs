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
        [Tooltip("Stamina ring on the HUD canvas (1-2)")]
        [SerializeField] StaminaGauge staminaGauge;
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
            if (staminaGauge != null) staminaGauge.Bind(playerMotor, orbitCamera != null ? orbitCamera.GetComponent<Camera>() : Camera.main);
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
        public void DebugInputSprint(bool sprint) => _input?.DebugSetSprint(sprint);
        public void DebugInputDodge() => _input?.DebugPressDodge();

        // ---- 스태미나 · 질주 · 회피 (1-2) ----

        public bool DebugHasStamina => playerMotor != null && playerMotor.Stamina != null;
        public float DebugStaminaCurrent => DebugHasStamina ? playerMotor.Stamina.Current : 0f;
        public float DebugStaminaMax => DebugHasStamina ? playerMotor.Stamina.Max : 0f;
        public bool DebugStaminaExhausted => DebugHasStamina && playerMotor.Stamina.Exhausted;
        public bool DebugStaminaRegenerating => DebugHasStamina && playerMotor.Stamina.Regenerating;
        /// <summary>회복 시작까지 남은 초.</summary>
        public float DebugStaminaRegenWait => DebugHasStamina ? playerMotor.Stamina.RegenWait : 0f;
        public bool DebugStaminaInfinite => DebugHasStamina && playerMotor.Stamina.Infinite;
        public float DebugStaminaGaugeAlpha => staminaGauge != null ? staminaGauge.Alpha : 0f;
        public bool DebugPlayerSprinting => playerMotor != null && playerMotor.Sprinting;
        public bool DebugPlayerDodging => playerMotor != null && playerMotor.Dodging;
        public bool DebugPlayerInvulnerable => playerMotor != null && playerMotor.Invulnerable;
        public float DebugDodgeCooldown => playerMotor != null && playerMotor.Dodge != null ? playerMotor.Dodge.CooldownLeft : 0f;
        public int DebugDodgeCount => playerMotor != null && playerMotor.Dodge != null ? playerMotor.Dodge.Count : 0;
        /// <summary>마지막 회피 입력이 막힌 까닭 (None · Busy · Cooldown · Airborne · NoStamina).</summary>
        public string DebugDodgeLastBlock => playerMotor != null && playerMotor.Dodge != null ? playerMotor.Dodge.LastBlock.ToString() : "-";

        /// <summary>[상태 변경] 스태미나 가득 채우기 (지침 풀림).</summary>
        public void DebugStaminaFill() { if (DebugHasStamina) playerMotor.Stamina.Fill(); }

        /// <summary>[상태 변경] 스태미나 값 정하기 (0 = 바닥 · 지침).</summary>
        public void DebugStaminaSet(float value) { if (DebugHasStamina) playerMotor.Stamina.Set(value); }

        /// <summary>[상태 변경] 무한 스태미나.</summary>
        public void DebugStaminaSetInfinite(bool infinite) { if (DebugHasStamina) playerMotor.Stamina.Infinite = infinite; }

        // ---- 등반 · 활공 (1-3) ----

        public bool DebugHasClimb => playerMotor != null && playerMotor.Climb != null;
        public bool DebugClimbing => DebugHasClimb && playerMotor.Climb.Active;
        /// <summary>등반 단계 (None · Climbing · Leaping · Mantling).</summary>
        public string DebugClimbPhase => DebugHasClimb ? playerMotor.Climb.Phase.ToString() : "-";
        /// <summary>마지막으로 끝난 까닭 (Dodge · Exhausted · Landed · Mantled · WallEnded · Cancelled).</summary>
        public string DebugClimbLastEnd => DebugHasClimb ? playerMotor.Climb.LastEnd.ToString() : "-";
        /// <summary>마지막으로 붙지 못한 까닭 (WallAngle · InputAngle · Cooldown · NoStamina …).</summary>
        public string DebugClimbLastBlock => DebugHasClimb ? playerMotor.Climb.LastBlock.ToString() : "-";
        public int DebugClimbCount => DebugHasClimb ? playerMotor.Climb.Count : 0;
        public int DebugClimbLeapCount => DebugHasClimb ? playerMotor.Climb.LeapCount : 0;
        public int DebugClimbMantleCount => DebugHasClimb ? playerMotor.Climb.MantleCount : 0;
        /// <summary>다시 붙을 수 있기까지 남은 초.</summary>
        public float DebugClimbReattachWait => DebugHasClimb ? playerMotor.Climb.ReattachLeft : 0f;
        /// <summary>붙어 있는 벽 기울기 (도, 수직 = 90).</summary>
        public float DebugClimbWallAngle => DebugClimbing ? Moa.Core.Motion.Climb.WallAngle(playerMotor.WallNormal.y) : 0f;
        public bool DebugGliding => playerMotor != null && playerMotor.Gliding;
        public string DebugGlideLastEnd => playerMotor != null && playerMotor.Glide != null ? playerMotor.Glide.LastEnd.ToString() : "-";
        public string DebugGlideLastBlock => playerMotor != null && playerMotor.Glide != null ? playerMotor.Glide.LastBlock.ToString() : "-";
        public int DebugGlideCount => playerMotor != null && playerMotor.Glide != null ? playerMotor.Glide.Count : 0;
        /// <summary>발 아래 땅까지 높이 (m).</summary>
        public float DebugPlayerHeightAboveGround => playerMotor != null ? playerMotor.HeightAboveGround : 0f;

        /// <summary>[상태 변경] 등반 중이면 떨어지기.</summary>
        public void DebugClimbDrop() { if (playerMotor != null) playerMotor.DropClimb(); }

        /// <summary>[상태 변경] 활공 펴기 (조건이 안 되면 false, 까닭은 DebugGlideLastBlock).</summary>
        public bool DebugGlideOpen() => playerMotor != null && playerMotor.OpenGlide();

        /// <summary>[상태 변경] 활공 접기.</summary>
        public void DebugGlideClose() { if (playerMotor != null) playerMotor.CloseGlide(); }

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
