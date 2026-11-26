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
        [Header("Poise / just dodge (2-3, empty in scenes without a player)")]
        [Tooltip("Player side of enemy attacks (just dodge)")]
        [SerializeField] PlayerDefense playerDefense;
        [Tooltip("Poise gauge bars over the dummies on the HUD canvas")]
        [SerializeField] PoiseBars poiseBars;
        [Header("Skill / burst / energy (2-4, empty in scenes without a player)")]
        [Tooltip("E cooldown and Q energy slots on the HUD canvas")]
        [SerializeField] SkillHud skillHud;
        [Header("Party (2-6, empty in scenes without a player)")]
        [Tooltip("Four party slots on the right of the HUD canvas")]
        [SerializeField] PartyHud partyHud;
        [Header("Enemies (2-5, empty in scenes without enemies)")]
        [Tooltip("Melee enemies (EnemyAgent) that chase and attack the player")]
        [SerializeField] EnemyAgent[] enemies;
        [Header("Player health (3-1, empty in scenes without a player)")]
        [Tooltip("Health of each party member (on the player)")]
        [SerializeField] PlayerVitals playerVitals;
        [Tooltip("Field character health bar and the dark screen when everyone is down")]
        [SerializeField] HealthHud healthHud;
        [Tooltip("Where the party comes back after everyone is down (test field start)")]
        [SerializeField] Transform respawnPoint;
        [Tooltip("Healing points: standing there heals the party and revives downed members")]
        [SerializeField] HealingPoint[] healingPoints;
        [Header("Switch combo (3-2, empty in scenes without a player)")]
        [Tooltip("Handoff buff icon and time left above the health bar")]
        [SerializeField] BuffHud buffHud;

        [Tooltip("Debug window teleport targets")]
        [SerializeField] Transform[] debugTeleportPoints;

        IPlayerInput _input;
        DummyAttacker[] _attackers = new DummyAttacker[0];
        bool _attackersPaused;

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
            BindVitals(); // 3-1: PlayerCombat.Bind 뒤 (파티가 있어야 캐릭터마다 체력)
            Camera view = orbitCamera != null ? orbitCamera.GetComponent<Camera>() : Camera.main;
            if (damageNumbers != null)
                damageNumbers.Bind(playerCombat.Tuning, view, UiFont.Get(config));
            BindPoise(view);
            if (skillHud != null) skillHud.Bind(playerCombat, UiFont.Get(config)); // 2-4
            if (partyHud != null) partyHud.Bind(playerCombat, UiFont.Get(config)); // 2-6
            if (buffHud != null) buffHud.Bind(playerCombat, UiFont.Get(config)); // 3-2
        }

        /// <summary>2-3: 저스트 회피(플레이어 쪽) · 공격 허수아비의 대상 · 경직 막대.</summary>
        void BindPoise(Camera view)
        {
            if (playerDefense != null)
            {
                playerDefense.Bind(playerMotor);
                playerDefense.Judged += OnJudged;
            }
            var list = new System.Collections.Generic.List<DummyAttacker>();
            if (trainingDummies != null)
                foreach (var d in trainingDummies)
                {
                    var a = d != null ? d.GetComponent<DummyAttacker>() : null;
                    if (a == null) continue;
                    a.Bind(playerDefense);
                    list.Add(a);
                }
            _attackers = list.ToArray();
            if (enemies != null) foreach (var e in enemies) if (e != null) e.Bind(playerDefense); // 2-5
            if (poiseBars != null) poiseBars.Bind(view, trainingDummies, enemies);
        }

        /// <summary>3-1: 플레이어 체력 · 체력 HUD · 회복 지점 · 전원 쓰러짐 뒤 부활.</summary>
        void BindVitals()
        {
            if (playerVitals == null) return;
            playerVitals.Bind(playerMotor);
            playerVitals.Hurt += OnPlayerHurt;
            playerVitals.WipeStarted += OnWipeStarted;
            playerVitals.Respawned += OnRespawned;
            if (healthHud != null) healthHud.Bind(playerVitals);
            if (healingPoints != null) foreach (var h in healingPoints) if (h != null) h.Bind(playerVitals);
        }

        void OnPlayerHurt(Moa.Core.Combat.PlayerHit hit, Vector3 at)
        {
            if (damageNumbers != null && hit.Requested > 0) damageNumbers.ShowPlayerDamage(at, hit.Requested, hit.Applied == 0);
        }

        void OnWipeStarted()
        {
            if (playerDefense != null) playerDefense.Cancel(); // 슬로모션 중이었으면 끄고 어두워진다
        }

        /// <summary>어두워진 뒤 (3-1): 부활 지점으로 옮기고 적을 모두 제자리로 (체력 가득).</summary>
        void OnRespawned()
        {
            if (respawnPoint != null) DebugTeleportPlayer(respawnPoint.position);
            DebugRespawnEnemies();
            _respawnedCount++;
        }

        int _respawnedCount;

        void OnJudged(Moa.Core.Combat.DodgeJudgement judgement, Vector3 feet)
        {
            if (judgement != Moa.Core.Combat.DodgeJudgement.Just || damageNumbers == null) return;
            float height = playerMotor != null && playerMotor.Tuning != null ? playerMotor.Tuning.CapsuleHeight : 0f;
            damageNumbers.ShowJustDodge(feet + Vector3.up * height);
        }

        /// <summary>전투 엔티티 ID (판을 시작할 때마다 새로 — 핵심 설계 규칙 5).</summary>
        void AssignCombatIds()
        {
            if (_ids == null) return;
            if (playerCombat != null) playerCombat.SetId(_ids.Next());
            if (trainingDummies != null)
                foreach (var d in trainingDummies) if (d != null) d.SetId(_ids.Next());
            if (enemies != null)
                foreach (var e in enemies) if (e != null) e.SetId(_ids.Next()); // 2-5
        }

        void OnHitLanded(HitReport hit)
        {
            if (damageNumbers != null) damageNumbers.Show(hit.NumberPosition, hit.Damage, hit.Applied == 0 && hit.Damage > 0);
            if (damageNumbers != null && hit.Broke) damageNumbers.ShowGroggy(hit.NumberPosition + Vector3.up * GroggyWordRise);
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
            if (playerDefense != null) playerDefense.Cancel();
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
        /// <summary>현재 캐릭터 기본 공격력 · 무기 기본 공격력 (2-6, 합 × 디버그 배수 = DebugAttack).</summary>
        public float DebugCharacterAttack => DebugHasCombat ? playerCombat.CharacterAttack : 0f;
        public float DebugWeaponAttack => DebugHasCombat ? playerCombat.WeaponAttack : 0f;
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
        const float GroggyWordRise = 0.5f; // 그로기 글을 피해 숫자보다 이만큼 위에 (m, 표현 값)

        // ---- 경직 · 그로기 · 저스트 회피 (2-3) ----

        public bool DebugHasDefense => playerDefense != null && playerDefense.Bound;
        /// <summary>마지막 판정 (Hit · Avoided · Just).</summary>
        public string DebugJustLast => DebugHasDefense ? playerDefense.Just.Last.ToString() : "-";
        public int DebugJustCount => DebugHasDefense ? playerDefense.Just.JustCount : 0;
        public int DebugAvoidCount => DebugHasDefense ? playerDefense.Just.AvoidCount : 0;
        /// <summary>플레이어가 맞은 수 (아직 피해 없음).</summary>
        public int DebugPlayerHitCount => DebugHasDefense ? playerDefense.Just.HitCount : 0;
        public float DebugJustSlowLeft => DebugHasDefense ? playerDefense.Just.SlowLeft : 0f;
        public float DebugJustCounterLeft => DebugHasDefense ? playerDefense.Just.CounterLeft : 0f;
        public float DebugPoiseMultiplier => DebugHasDefense ? playerDefense.PoiseMultiplier : 1f;
        public float DebugJustWindow => DebugHasDefense ? playerDefense.Just.Rules.Window : 0f;
        public float DebugTimeScale => Time.timeScale;
        public DummyAttacker[] DebugAttackers => _attackers;
        public bool DebugAttackersPaused => _attackersPaused;
        public int DebugPoiseBarsVisible => poiseBars != null ? poiseBars.VisibleCount : 0;

        /// <summary>[상태 변경] 공격 허수아비 멈춤 / 다시 (멈추면 예고도 취소).</summary>
        public void DebugSetAttackersPaused(bool paused)
        {
            _attackersPaused = paused;
            foreach (var a in _attackers) if (a != null) a.Paused = paused;
        }

        /// <summary>[상태 변경] 공격 허수아비 주기를 처음으로 (다음 예고까지 쉬는 시간부터).</summary>
        public void DebugResetAttackers() { foreach (var a in _attackers) if (a != null) a.ResetCycle(); }

        /// <summary>[상태 변경] 허수아비 경직 게이지를 모두 비운다 (그로기도 풀림).</summary>
        public void DebugResetPoise()
        {
            if (trainingDummies == null) return;
            foreach (var d in trainingDummies) if (d != null) d.ResetPoise();
        }

        /// <summary>[상태 변경] 허수아비를 모두 바로 그로기로 (게이지를 가득 채움).</summary>
        public void DebugBreakDummies()
        {
            if (trainingDummies == null) return;
            foreach (var d in trainingDummies)
                if (d != null && d.Poise != null) d.ReceivePoise(d.Poise.Rules.Max);
        }

        /// <summary>[상태 변경] 슬로모션 · 반격 보너스를 끈다.</summary>
        public void DebugCancelJustDodge() { if (playerDefense != null) playerDefense.Cancel(); }

        // ---- 스킬(E) · 궁극기(Q) · 에너지 (2-4) ----

        /// <summary>[상태 변경] 스킬 짧게 누르기 주입 (다음 고정 스텝에 소비).</summary>
        public void DebugInputSkill() => _input?.DebugPressSkill();
        /// <summary>[상태 변경] 스킬을 누르고 누른 스텝 뒤로 seconds 동안 누른 채로 (길게 누르기 주입).</summary>
        public void DebugInputSkillHold(float seconds) => _input?.DebugHoldSkill(seconds);
        /// <summary>[상태 변경] 궁극기 입력 주입.</summary>
        public void DebugInputBurst() => _input?.DebugPressBurst();
        public bool DebugHasSkills => DebugHasCombat && playerCombat.Skills != null;
        public string DebugSkillsName => playerCombat != null && playerCombat.Skills != null ? playerCombat.Skills.name : "-";
        public Moa.Core.Combat.SkillAttack DebugSkill => DebugHasCombat ? playerCombat.Skill : null;
        public Moa.Core.Combat.BurstAttack DebugBurst => DebugHasCombat ? playerCombat.Burst : null;
        public Moa.Core.Combat.Energy DebugEnergy => DebugHasCombat ? playerCombat.Energy : null;
        public float DebugEnergyFromSkill => playerCombat != null ? playerCombat.EnergyFromSkill : 0f;
        public float DebugEnergyFromDefeat => playerCombat != null ? playerCombat.EnergyFromDefeat : 0f;
        public float DebugLastEnergyGain => playerCombat != null ? playerCombat.LastEnergyGain : 0f;
        public bool DebugBurstInvulnerable => playerMotor != null && playerMotor.BurstInvulnerable;
        public SkillHud DebugSkillHud => skillHud;

        /// <summary>[상태 변경] 에너지 가득 채우기.</summary>
        public void DebugEnergyFill() { if (DebugEnergy != null) DebugEnergy.Fill(); }

        /// <summary>[상태 변경] 에너지 값 정하기.</summary>
        public void DebugEnergySet(float value) { if (DebugEnergy != null) DebugEnergy.Set(value); }

        /// <summary>[상태 변경] 스킬 · 궁극기 쿨타임 없애기.</summary>
        public void DebugResetSkillCooldowns()
        {
            if (!DebugHasCombat) return;
            playerCombat.Skill.ResetCooldown();
            playerCombat.Burst.ResetCooldown();
        }

        // ---- 파티 4인 교체 (2-6) ----

        public bool DebugHasParty => DebugHasCombat && playerCombat.Party != null;
        public Moa.Core.Combat.Party DebugParty => DebugHasCombat ? playerCombat.Party : null;
        public int DebugPartyCount => DebugHasCombat ? playerCombat.CharacterCount : 0;
        /// <summary>현재 칸, 1부터 (0 = 파티 없음).</summary>
        public int DebugPartyActiveSlot => DebugHasParty ? playerCombat.ActiveIndex + 1 : 0;
        /// <summary>칸의 캐릭터 (slot 1 ~ 4, 없으면 null).</summary>
        public CharacterData DebugPartyCharacter(int slot) => playerCombat != null ? playerCombat.GetCharacter(slot - 1) : null;
        /// <summary>칸의 공격 조정 (스킬 · 궁극기 쿨 · 에너지, slot 1 ~ 4, 없으면 null).</summary>
        public Moa.Core.Combat.PlayerAttack DebugPartyMember(int slot) => playerCombat != null ? playerCombat.MemberAttacks(slot - 1) : null;
        public float DebugPartyCooldown => DebugHasParty ? playerCombat.Party.CooldownLeft : 0f;
        public int DebugPartySwitchCount => DebugHasParty ? playerCombat.Party.SwitchCount : 0;
        /// <summary>마지막 교체가 막힌 까닭 (None · NoMember · SameMember · Cooldown · Burst · Plunge · Gliding).</summary>
        public string DebugPartyLastBlock => DebugHasParty ? playerCombat.Party.LastBlock.ToString() : "-";
        /// <summary>마지막 교체로 끊은 공격 (None = 끊은 것 없음).</summary>
        public string DebugPartyLastInterrupted => DebugHasParty ? playerCombat.Party.LastInterrupted.ToString() : "-";
        public float DebugPartyBenchEnergyFromDefeat => playerCombat != null ? playerCombat.BenchEnergyFromDefeat : 0f;
        public float DebugPartyLastBenchEnergyGain => playerCombat != null ? playerCombat.LastBenchEnergyGain : 0f;
        public PartyHud DebugPartyHud => partyHud;
        public string DebugPartyBodyCharacter
        {
            get
            {
                var f = playerMotor != null ? playerMotor.GetComponent<PlayerGrayboxFeedback>() : null;
                return f != null && f.ShownCharacter != null ? f.ShownCharacter.name : "-";
            }
        }

        /// <summary>[상태 변경] 교체 입력 주입 (slot 1 ~ 4, 다음 고정 스텝에 소비 — 쿨 · 막힘 규칙 그대로).</summary>
        public void DebugPartySwitch(int slot) => _input?.DebugPressSwitch(slot);

        /// <summary>[상태 변경] 교체 쿨을 없앤다.</summary>
        public void DebugPartyResetCooldown() { if (DebugHasParty) playerCombat.Party.ResetCooldown(); }

        /// <summary>[상태 변경] 칸의 에너지를 가득 (slot 0 = 모두).</summary>
        public void DebugPartyEnergyFill(int slot)
        {
            if (!DebugHasParty) return;
            for (int i = 0; i < playerCombat.CharacterCount; i++)
                if (slot == 0 || slot == i + 1) playerCombat.MemberAttacks(i).Energy.Fill();
        }

        /// <summary>[상태 변경] 칸의 에너지 값 정하기 (slot 0 = 모두).</summary>
        public void DebugPartyEnergySet(int slot, float value)
        {
            if (!DebugHasParty) return;
            for (int i = 0; i < playerCombat.CharacterCount; i++)
                if (slot == 0 || slot == i + 1) playerCombat.MemberAttacks(i).Energy.Set(value);
        }

        /// <summary>[상태 변경] 모든 캐릭터의 스킬 · 궁극기 쿨과 교체 쿨을 없앤다.</summary>
        public void DebugPartyResetAllCooldowns()
        {
            if (!DebugHasParty) return;
            for (int i = 0; i < playerCombat.CharacterCount; i++)
            {
                playerCombat.MemberAttacks(i).Skill.ResetCooldown();
                playerCombat.MemberAttacks(i).Burst.ResetCooldown();
            }
            playerCombat.Party.ResetCooldown();
        }

        // ---- 적 AI (2-5) ----

        public EnemyAgent[] DebugEnemies => enemies;
        public int DebugEnemyCount => enemies != null ? enemies.Length : 0;
        bool _enemiesPaused, _enemiesInvulnerable;
        public bool DebugEnemiesPaused => _enemiesPaused;
        public bool DebugEnemiesInvulnerable => _enemiesInvulnerable;

        /// <summary>적 하나 (없으면 null).</summary>
        public EnemyAgent DebugEnemy(int index) => enemies != null && index >= 0 && index < enemies.Length ? enemies[index] : null;

        /// <summary>[상태 변경] 적을 모두 집에서 다시 세운다 (체력 · 경직 가득, 대기).</summary>
        public void DebugRespawnEnemies() { if (enemies != null) foreach (var e in enemies) if (e != null) e.Respawn(); }

        /// <summary>[상태 변경] 적 멈춤 / 다시 (멈추면 서 있고 예고도 취소).</summary>
        public void DebugSetEnemiesPaused(bool paused)
        {
            _enemiesPaused = paused;
            if (enemies != null) foreach (var e in enemies) if (e != null) e.Paused = paused;
        }

        /// <summary>[상태 변경] 적 무적 (피해 숫자는 뜨고 체력은 그대로).</summary>
        public void DebugSetEnemiesInvulnerable(bool on)
        {
            _enemiesInvulnerable = on;
            if (enemies != null) foreach (var e in enemies) if (e != null) e.SetInvulnerable(on);
        }

        /// <summary>[상태 변경] 적 하나의 상태를 바꾼다 (Idle · Patrol · Chase · Return · Stagger). 못 바꾸면 false.</summary>
        public bool DebugSetEnemyState(int index, Moa.Core.Combat.EnemyState state)
        {
            var e = DebugEnemy(index);
            return e != null && e.ForceState(state);
        }

        /// <summary>[상태 변경] 적 하나를 쓰러뜨린다 (처치 에너지 없음, RespawnDelay 뒤 다시 남).</summary>
        public void DebugKillEnemy(int index) { var e = DebugEnemy(index); if (e != null) e.Kill(); }

        /// <summary>[상태 변경] 적을 모두 바로 그로기로 (게이지를 가득 채움).</summary>
        public void DebugBreakEnemies()
        {
            if (enemies == null) return;
            foreach (var e in enemies) if (e != null && e.Poise != null) e.ReceivePoise(e.Poise.Rules.Max);
        }

        /// <summary>[상태 변경] 순간이동 지점 이름으로 (없으면 false).</summary>
        public bool DebugTeleportToPoint(string pointName)
        {
            if (debugTeleportPoints == null) return false;
            foreach (var p in debugTeleportPoints)
            {
                if (p == null || p.name != pointName) continue;
                DebugTeleportPlayer(p.position);
                return true;
            }
            return false;
        }

        // ---- 플레이어 체력 · 쓰러짐 (3-1) ----

        public bool DebugHasPlayerHealth => playerVitals != null && playerVitals.Bound;
        /// <summary>체력 규칙 (Core, 없으면 null).</summary>
        public Moa.Core.Combat.PartyVitals DebugPlayerHealth => DebugHasPlayerHealth ? playerVitals.Vitals : null;
        public int DebugPlayerHealthCount => DebugHasPlayerHealth ? playerVitals.Count : 0;
        /// <summary>필드 캐릭터 칸, 1부터 (0 = 없음).</summary>
        public int DebugPlayerHealthActiveSlot => DebugHasPlayerHealth ? playerVitals.Vitals.ActiveIndex + 1 : 0;
        /// <summary>칸(1 ~ 4)의 체력 · 최대 · 방어 · 쓰러짐.</summary>
        public int DebugPlayerHealthCurrent(int slot) => DebugHasPlayerHealth && slot >= 1 && slot <= playerVitals.Count ? playerVitals.Vitals.Member(slot - 1).Current : 0;
        public int DebugPlayerHealthMax(int slot) => DebugHasPlayerHealth && slot >= 1 && slot <= playerVitals.Count ? playerVitals.Vitals.Member(slot - 1).Max : 0;
        public float DebugPlayerHealthDefense(int slot) => DebugHasPlayerHealth && slot >= 1 && slot <= playerVitals.Count ? playerVitals.Vitals.Defense(slot - 1) : 0f;
        public bool DebugPlayerHealthDowned(int slot) => DebugHasPlayerHealth && slot >= 1 && slot <= playerVitals.Count && playerVitals.Vitals.IsDowned(slot - 1);
        public CharacterData DebugPlayerHealthCharacter(int slot) => playerVitals != null ? playerVitals.GetCharacter(slot - 1) : null;
        public bool DebugPlayerHealthStunned => playerVitals != null && playerVitals.Stunned;
        public float DebugPlayerHealthStunLeft => DebugHasPlayerHealth ? playerVitals.Vitals.StunLeft : 0f;
        /// <summary>전멸 단계 (None · FadeOut · Dark · FadeIn).</summary>
        public string DebugPlayerHealthWipe => DebugHasPlayerHealth ? playerVitals.Vitals.Wipe.ToString() : "-";
        public float DebugPlayerHealthDarkness => playerVitals != null ? playerVitals.Darkness : 0f;
        public bool DebugPlayerHealthInvulnerable => playerVitals != null && playerVitals.Invulnerable;
        public Moa.Core.Combat.PlayerHit DebugPlayerHealthLastHit => DebugHasPlayerHealth ? playerVitals.Vitals.LastHit : Moa.Core.Combat.PlayerHit.None(0);
        public int DebugPlayerHealthRespawnCount => _respawnedCount;
        public HealthHud DebugHealthHud => healthHud;
        public HealingPoint[] DebugHealingPoints => healingPoints;
        public Transform DebugRespawnPoint => respawnPoint;

        /// <summary>[상태 변경] 필드 캐릭터에게 amount 피해 (방어 없이, 땅 위면 경직, 무적이면 0).</summary>
        public void DebugPlayerHealthDamage(int amount) { if (playerVitals != null) playerVitals.DebugDamage(amount); }

        /// <summary>[상태 변경] 필드 캐릭터를 쓰러뜨린다 (살아 있는 다음 캐릭터로 자동 교체, 없으면 전멸).</summary>
        public void DebugPlayerHealthDownActive() { if (playerVitals != null) playerVitals.DebugDownActive(); }

        /// <summary>[상태 변경] 전원 쓰러뜨린다 → 어두워졌다 부활 지점에서 되살아난다.</summary>
        public void DebugPlayerHealthDownAll() { if (playerVitals != null) playerVitals.DebugDownAll(); }

        /// <summary>[상태 변경] 전원 가득 · 되살리기.</summary>
        public void DebugPlayerHealthRestoreAll() { if (playerVitals != null) playerVitals.DebugRestoreAll(); }

        /// <summary>[상태 변경] 플레이어 무적 (체력 · 경직 없음, 피해 숫자는 회색).</summary>
        public void DebugPlayerHealthSetInvulnerable(bool on) { if (playerVitals != null) playerVitals.Invulnerable = on; }

        // ---- 교체 연계 (3-2) ----

        /// <summary>필드 캐릭터의 등장 공격 (없으면 null).</summary>
        public Moa.Core.Combat.EntryAttack DebugEntry => DebugHasCombat ? playerCombat.Entry : null;
        /// <summary>지금 걸린 교체 강화 (파티가 없으면 null).</summary>
        public Moa.Core.Combat.HandoffBuff DebugHandoffBuff => DebugHasParty ? playerCombat.Party.Buff : null;
        /// <summary>칸(1 ~ 4)이 스킬을 쓴 뒤 버프를 줄 수 있는 남은 초.</summary>
        public float DebugHandoffLeft(int slot) => DebugHasParty ? playerCombat.Party.HandoffLeft(slot - 1) : 0f;
        /// <summary>칸(1 ~ 4)이 주는 버프 수치.</summary>
        public Moa.Core.Combat.HandoffRules DebugHandoffRules(int slot) => DebugHasParty ? playerCombat.Party.Handoff(slot - 1) : Moa.Core.Combat.HandoffRules.None;
        public float DebugDamageMultiplier => DebugHasParty ? playerCombat.Party.DamageMultiplier : 1f;
        public float DebugPoiseBuffMultiplier => DebugHasParty ? playerCombat.Party.PoiseMultiplier : 1f;
        public int DebugEntryHitCount => playerCombat != null ? playerCombat.EntryHitCount : 0;
        public int DebugEntryGroggyHitCount => playerCombat != null ? playerCombat.EntryGroggyHitCount : 0;
        public BuffHud DebugBuffHud => buffHud;

        /// <summary>[상태 변경] fromSlot(1 ~ 4) 캐릭터의 스킬 뒤 교체 버프를 지금 필드 캐릭터에게 (에너지 포함). 걸렸으면 true.</summary>
        public bool DebugHandoffGrant(int fromSlot) => DebugHasParty && playerCombat.Party.GrantHandoff(fromSlot - 1);

        /// <summary>[상태 변경] 버프를 끝낸다.</summary>
        public void DebugHandoffClear() { if (DebugHasParty) playerCombat.Party.ClearHandoff(); }

        /// <summary>[상태 변경] 필드 캐릭터의 등장 공격 창을 연다 (교체 없이 확인).</summary>
        public void DebugEntryOpen() { if (DebugHasParty) playerCombat.Party.OpenEntry(); }

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
