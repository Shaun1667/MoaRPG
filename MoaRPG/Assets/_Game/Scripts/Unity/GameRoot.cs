using System.Text;
using Moa.Core.Ids;
using Moa.Core.Localization;
using Moa.Core.Rng;
using Moa.Unity.Cameras;
using Moa.Unity.Combat;
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
        [Header("Combat (2-1, empty in scenes without a player)")]
        [SerializeField] PlayerCombat playerCombat;
        [SerializeField] TrainingDummy[] trainingDummies;
        [Tooltip("Floating damage numbers on the HUD canvas")]
        [SerializeField] DamageNumbers damageNumbers;
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
            BindCombat();
        }

        void BindCombat()
        {
            if (playerCombat == null) return;
            playerCombat.Bind(_input);
            playerCombat.HitLanded += OnHitLanded;
            if (damageNumbers != null)
                damageNumbers.Bind(playerCombat.Tuning, orbitCamera != null ? orbitCamera.GetComponent<Camera>() : Camera.main, UiFont.Get(config));
        }

        /// <summary>전투 엔티티 ID (판을 시작할 때마다 새로 — 핵심 설계 규칙 5).</summary>
        void AssignCombatIds()
        {
            if (_ids == null) return;
            if (playerCombat != null) playerCombat.SetId(_ids.Next());
            if (trainingDummies == null) return;
            foreach (var d in trainingDummies) if (d != null) d.SetId(_ids.Next());
        }

        void OnHitLanded(HitReport hit)
        {
            if (damageNumbers != null) damageNumbers.Show(hit.NumberPosition, hit.Damage, hit.Applied == 0 && hit.Damage > 0);
        }

        void StartRun(int seed)
        {
            _seed = seed;
            _random = new RandomStreams(RandomStreams.SeedFromInt(seed));
            _ids = new IdGenerator();
            _runCount++;
            AssignCombatIds();
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

        // ---- 전투 (2-1) ----

        public bool DebugHasCombat => playerCombat != null && playerCombat.Bound;
        /// <summary>[상태 변경] 공격 입력 주입 (다음 고정 스텝에 소비).</summary>
        public void DebugInputAttack() => _input?.DebugPressAttack();
        public bool DebugAttacking => DebugHasCombat && playerCombat.Attacking;
        /// <summary>지금(또는 마지막) 콤보 단계, 1부터 (0 = 아직 없음).</summary>
        public int DebugComboStep => DebugHasCombat ? playerCombat.Combo.StepIndex + 1 : 0;
        public int DebugComboStepCount => DebugHasCombat ? playerCombat.Combo.Rules.StepCount : 0;
        /// <summary>대기 중에 누르면 나갈 단계, 1부터.</summary>
        public int DebugComboNextStep => DebugHasCombat ? playerCombat.Combo.NextStep + 1 : 0;
        /// <summary>단계 안 구간 (Idle · Windup · Active · Recovery).</summary>
        public string DebugComboPhase => DebugHasCombat ? playerCombat.Combo.Phase.ToString() : "-";
        public float DebugComboElapsed => DebugHasCombat ? playerCombat.Combo.Elapsed : 0f;
        public bool DebugComboQueued => DebugHasCombat && playerCombat.Combo.Queued;
        /// <summary>콤보가 1타로 돌아가기까지 남은 초.</summary>
        public float DebugComboResetLeft => DebugHasCombat ? playerCombat.Combo.ResetLeft : 0f;
        public int DebugComboSwingCount => DebugHasCombat ? playerCombat.Combo.SwingCount : 0;
        public int DebugComboBreakCount => DebugHasCombat ? playerCombat.Combo.BreakCount : 0;
        public string DebugComboLastBlock => DebugHasCombat ? playerCombat.Combo.LastBlock.ToString() : "-";
        public string DebugComboLastEnd => DebugHasCombat ? playerCombat.Combo.LastEnd.ToString() : "-";
        public int DebugHitCount => DebugHasCombat ? playerCombat.HitCount : 0;
        public HitReport DebugLastHit => DebugHasCombat ? playerCombat.LastHit : default;
        public float DebugAttack => DebugHasCombat ? playerCombat.Attack : 0f;
        public float DebugAttackScale => DebugHasCombat ? playerCombat.AttackScale : 0f;
        public string DebugWeaponName => playerCombat != null && playerCombat.Weapon != null ? playerCombat.Weapon.name : "-";
        public long DebugPlayerEntityId => playerCombat != null ? playerCombat.Id.Value : 0;
        public TrainingDummy[] DebugDummies => trainingDummies;
        public int DebugDamageNumbersActive => damageNumbers != null ? damageNumbers.ActiveCount : 0;
        public string DebugDamageNumbersLastText => damageNumbers != null ? damageNumbers.LastText : null;
        bool _dummiesInvulnerable;
        public bool DebugDummiesInvulnerable => _dummiesInvulnerable;

        /// <summary>[상태 변경] 공격력 배수 (1 = 그대로).</summary>
        public void DebugSetAttackScale(float scale) { if (playerCombat != null) playerCombat.AttackScale = scale; }

        /// <summary>[상태 변경] 허수아비를 모두 다시 세운다 (체력 가득).</summary>
        public void DebugRespawnDummies()
        {
            if (trainingDummies == null) return;
            foreach (var d in trainingDummies) if (d != null) d.Respawn();
        }

        /// <summary>[상태 변경] 허수아비 무적 (피해 숫자는 뜨고 체력은 안 줌).</summary>
        public void DebugSetDummiesInvulnerable(bool on)
        {
            _dummiesInvulnerable = on;
            if (trainingDummies == null) return;
            foreach (var d in trainingDummies) if (d != null) d.SetInvulnerable(on);
        }

        // ---- 강공격 · 낙하 공격 (2-2) ----

        /// <summary>[상태 변경] 공격을 누르고 누른 스텝 뒤로 seconds 동안 누른 채로 (길게 누르기 주입, 고정 스텝 시간 기준).</summary>
        public void DebugInputAttackHold(float seconds) => _input?.DebugHoldAttack(seconds);
        /// <summary>지금 공격 (None · Combo · Charged · Plunge).</summary>
        public string DebugAttackMode => DebugHasCombat ? playerCombat.Mode.ToString() : "-";
        public bool DebugAttackHolding => DebugHasCombat && playerCombat.Attacks.Holding;
        public float DebugAttackHoldElapsed => DebugHasCombat ? playerCombat.Attacks.HoldElapsed : 0f;
        public float DebugChargeHoldTime => DebugHasCombat ? playerCombat.Attacks.HoldTime : 0f;
        /// <summary>마지막 누름이 막힌 까닭 (콤보 · 낙하 · Busy).</summary>
        public string DebugAttackLastBlock => DebugHasCombat ? playerCombat.Attacks.LastBlock.ToString() : "-";
        /// <summary>마지막 길게 누르기가 강공격이 되지 못한 까닭 (None = 나감 · NoStamina …).</summary>
        public string DebugChargeLastBlock => DebugHasCombat ? playerCombat.Attacks.LastChargeBlock.ToString() : "-";
        public int DebugAttackBusyCount => DebugHasCombat ? playerCombat.Attacks.BusyCount : 0;
        public bool DebugCharging => DebugHasCombat && playerCombat.Charged.Active;
        /// <summary>지금(또는 마지막) 강공격 베기, 1부터 (0 = 아직 없음).</summary>
        public int DebugChargedSlash => DebugHasCombat ? playerCombat.Charged.SlashIndex + 1 : 0;
        public int DebugChargedSlashCount => DebugHasCombat ? playerCombat.Charged.Rules.SlashCount : 0;
        public string DebugChargedPhase => DebugHasCombat ? playerCombat.Charged.Phase.ToString() : "-";
        public float DebugChargedElapsed => DebugHasCombat ? playerCombat.Charged.Elapsed : 0f;
        public int DebugChargedCount => DebugHasCombat ? playerCombat.Charged.Count : 0;
        public string DebugChargedLastEnd => DebugHasCombat ? playerCombat.Charged.LastEnd.ToString() : "-";
        public float DebugChargedStaminaCost => DebugHasCombat ? playerCombat.Charged.Rules.StaminaCost : 0f;
        public bool DebugPlunging => DebugHasCombat && playerCombat.Plunging;
        /// <summary>낙하 단계 (None · Falling · Landing).</summary>
        public string DebugPlungePhase => DebugHasCombat ? playerCombat.Plunge.Phase.ToString() : "-";
        public float DebugPlungeStartY => DebugHasCombat ? playerCombat.Plunge.StartY : 0f;
        public float DebugPlungeFallHeight => DebugHasCombat ? playerCombat.Plunge.FallHeight : 0f;
        public bool DebugPlungeHigh => DebugHasCombat && playerCombat.Plunge.High;
        public float DebugPlungeMultiplier => DebugHasCombat ? playerCombat.Plunge.Multiplier : 0f;
        public float DebugPlungeRecoveryLeft => DebugHasCombat ? playerCombat.Plunge.RecoveryLeft : 0f;
        public int DebugPlungeCount => DebugHasCombat ? playerCombat.Plunge.Count : 0;
        public int DebugPlungeLandCount => DebugHasCombat ? playerCombat.Plunge.LandCount : 0;
        public string DebugPlungeLastBlock => DebugHasCombat ? playerCombat.Plunge.LastBlock.ToString() : "-";
        public string DebugPlungeLastEnd => DebugHasCombat ? playerCombat.Plunge.LastEnd.ToString() : "-";
        public float DebugPlungeMinHeight => DebugHasCombat ? playerCombat.Plunge.Rules.MinHeight : 0f;
        public Vector3 DebugPlungeLastLand => playerCombat != null ? playerCombat.LastLandPosition : Vector3.zero;
        public int DebugPlungeLastLandHits => playerCombat != null ? playerCombat.LastLandHits : 0;

        /// <summary>[상태 변경] 순간이동 지점 Dummies의 meters 위 공중으로 (낙하 공격 확인용). 지점이 없으면 false.</summary>
        public bool DebugTeleportAboveDummies(float meters)
        {
            if (debugTeleportPoints == null) return false;
            foreach (var p in debugTeleportPoints)
            {
                if (p == null || p.name != DummiesPointName) continue;
                DebugTeleportPlayer(p.position + Vector3.up * meters);
                return true;
            }
            return false;
        }

        const string DummiesPointName = "Dummies"; // 시험 지형 순간이동 지점 이름 (Setup 배치 표)

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
