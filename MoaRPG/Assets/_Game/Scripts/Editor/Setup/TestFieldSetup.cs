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
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine.AI;

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
        public const string AttackingDummyPath = "Assets/_Game/Data/Enemies/AttackingDummy.asset"; // 2-3: 예고 공격 (EnemyAttackTuning)
        public const string SkillTuningPath = "Assets/_Game/Data/Skills/SwordHero.asset";       // 2-4: 지금 캐릭터(한손검)의 스킬 · 궁극기 (SkillTuning)
        public const string GruntTuningPath = "Assets/_Game/Data/Enemies/Grunt.asset";          // 2-5: 근접 졸병 (EnemyTuning)
        public const string PartyPath = "Assets/_Game/Data/Characters/Party.asset";             // 2-6: 교체 수치 + 시작 파티 (PartyTuning, 캐릭터는 같은 폴더)
        public const string HeroCharacterPath = "Assets/_Game/Data/Characters/SwordHero.asset"; // 2-6: 파티가 없을 때 캐릭터 기본 공격력 (CharacterData)
        public const string PlayerHealthTuningPath = "Assets/_Game/Data/Player/PlayerHealthTuning.asset"; // 3-1: 피격 · 전멸 · 회복 지점 (PlayerHealthTuning)
        public const string ArcherTuningPath = "Assets/_Game/Data/Enemies/Archer.asset";        // 3-4: 원거리 궁수 (EnemyTuning)
        public const string BruteTuningPath = "Assets/_Game/Data/Enemies/Brute.asset";          // 3-4: 정예 (EnemyTuning)
        public const string NavMeshDataPath = "Assets/_Game/Scenes/TestField/NavMesh-Environment.asset"; // 3-4: Environment에서 구운 NavMesh (다시 굽으면 이 에셋을 고침)

        const string GameConfigPath = "Assets/_Game/Data/GameConfig.asset";
        const string GrayboxFolder = "Assets/_Game/Art/Graybox";
        const string CheckerPath = GrayboxFolder + "/Checker.png";
        const string RingPath = GrayboxFolder + "/Ring.png";
        const string LitShader = "Universal Render Pipeline/Lit";
        const string TrailShader = "Sprites/Default";                       // 궤적 (정점 색 · 투명)
        const string TrailShaderFallback = "Universal Render Pipeline/Unlit";

        static readonly string[] GeneratedRoots = { "GameRoot", "Main Camera", "Directional Light", "Player", "Environment", "DebugPoints", "HUD", "Dummies", "Enemies" };

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

        // ---- 표현 표 (2-3 경직 · 그로기 · 저스트 회피, 게임 수치 아님) ----
        static readonly Color DummyGroggyColor = new Color(1.00f, 0.85f, 0.15f);  // 그로기 허수아비 몸: 노랑 (번쩍임이 앞선다)
        const float DummyGroggyTilt = 14f;                                         // 그로기 때 뒤로 기울기 (도)
        static readonly Color PoiseBackColor = new Color(0f, 0f, 0f, 0.55f);       // 경직 막대 바탕
        static readonly Color PoiseFillColor = new Color(1.00f, 0.55f, 0.15f);     // 쌓인 경직: 주황
        static readonly Color PoiseGroggyColor = new Color(1.00f, 0.90f, 0.20f);   // 그로기 남은 시간: 노랑
        static readonly Vector2 PoiseBarSize = new Vector2(96f, 10f);              // px (1920×1080 기준)
        const float PoiseBarHeight = 2.3f;                                         // 발에서 막대까지 (m)
        static readonly Color JustDodgeTextColor = new Color(0.45f, 0.95f, 1.00f); // "저스트 회피" 글
        static readonly Color GroggyTextColor = new Color(1.00f, 0.80f, 0.15f);    // "그로기" 글
        static readonly Color AttackDummyColor = new Color(0.72f, 0.38f, 0.30f);   // 공격 허수아비 (붉은 짚)
        static readonly Color WarnStartColor = new Color(1.00f, 0.80f, 0.20f, 0.45f); // 예고 원: 시작 (엷은 노랑)
        static readonly Color WarnEndColor = new Color(1.00f, 0.15f, 0.10f, 0.95f);   // 예고 원: 닿기 직전 (진한 빨강)
        static readonly Color WarnHitColor = new Color(1.00f, 1.00f, 1.00f, 1.00f);   // 닿는 순간
        const float WarnRingWidth = 0.10f, WarnHitFlashTime = 0.16f, WarnRingHeight = 0.05f; // 선 두께(m) · 닿은 뒤 보이는 시간(초) · 바닥 위 높이(m)

        // ---- 표현 표 (2-4 스킬 · 궁극기 · 에너지, 게임 수치 아님) ----
        static readonly Color SkillColor = new Color(0.20f, 0.75f, 0.70f);        // 스킬 중(누르는 중 포함): 청록 (회피 무적 > 궁극기 > 회피 > 스킬 > 낙하 …)
        static readonly Color BurstColor = new Color(1.00f, 0.78f, 0.10f);        // 궁극기 중(무적): 금색
        static readonly Color SkillTrailColor = new Color(0.30f, 1.00f, 0.90f, 0.85f);
        static readonly Color BurstTrailColor = new Color(1.00f, 0.85f, 0.25f, 0.95f);
        static readonly Vector3 SwordBurstStart = new Vector3(-80f, 0f, 0f);    // 궁극기: 머리 위 → 아래로 크게
        static readonly Vector3 SwordBurstEnd = new Vector3(80f, 0f, 0f);
        static readonly Color BurstRingColor = new Color(1.00f, 0.80f, 0.20f, 0.95f); // 궁극기 범위 원
        const float BurstRingTime = 1.2f;                                         // 원을 보이는 시간(초) — 궁극기 길이와 같게
        const float SkillSlotSize = 96f, SkillSlotGap = 16f;                      // HUD 칸 크기 · 사이 (px, 1920×1080 기준)
        static readonly Vector2 SkillHudMargin = new Vector2(60f, 90f);           // 화면 오른쪽 · 아래에서
        const int SkillKeyFontSize = 40, SkillNumberFontSize = 24;
        static readonly Color SkillSlotColor = new Color(0f, 0f, 0f, 0.55f);
        static readonly Color SkillPressingColor = new Color(0.20f, 0.55f, 0.55f, 0.85f);
        static readonly Color SkillCastingColor = new Color(0.25f, 0.70f, 0.65f, 0.85f);
        static readonly Color SkillCooldownCover = new Color(0f, 0f, 0f, 0.6f);
        static readonly Color EnergyColor = new Color(0.55f, 0.80f, 1.00f);
        static readonly Color EnergyFullColor = new Color(1.00f, 0.85f, 0.25f);
        static readonly Color SkillTextColor = new Color(1f, 1f, 1f);

        // ---- 표현 표 (2-5 적 AI, 게임 수치 아님) ----
        static readonly Color GruntColor = new Color(0.36f, 0.62f, 0.30f);        // 근접 졸병 몸: 초록
        static readonly Color GruntTelegraphColor = new Color(0.95f, 0.25f, 0.20f); // 예고 중 몸: 빨강 (번쩍임 > 그로기 > 예고)
        static readonly Color GruntClubColor = new Color(0.45f, 0.32f, 0.20f);    // 몽둥이 상자
        static readonly Vector3 GruntClubSize = new Vector3(0.12f, 0.12f, 0.8f);  // m
        static readonly Vector3 GruntClubOffset = new Vector3(0.45f, 0.75f, 0.25f); // 발 기준 (오른쪽 앞)
        const float GruntEyeSize = 0.18f;                                          // 앞을 보여 주는 눈 상자 (m)
        const float GruntGroggyTilt = 14f;
        const float FanWidth = 0.08f, FanHitFlashTime = 0.16f, FanHeight = 0.05f;  // 예고 부채꼴 선 두께(m) · 닿은 뒤 보이는 시간(초) · 바닥 위 높이(m)

        // ---- 표현 표 (3-4 적 종류, 게임 수치 아님) — 새 적 종류 = 줄 하나 (몸 크기는 EnemyTuning) ----
        // (종류 이름, 수치 에셋, 몸 머티리얼 이름 · 색, 무기 머티리얼 이름 · 색, 무기 상자 크기 · 발 기준 자리, 화살을 쏘는지)
        static readonly (string kind, string tuningPath, string bodyMat, Color body, string weaponMat, Color weapon, Vector3 weaponSize, Vector3 weaponOffset, bool shoots)[] EnemyKinds =
        {
            ("Grunt", GruntTuningPath, "Grunt", GruntColor, "GruntClub", GruntClubColor, GruntClubSize, GruntClubOffset, false),
            ("Archer", ArcherTuningPath, "Archer", new Color(0.30f, 0.52f, 0.78f), "Bow", new Color(0.55f, 0.36f, 0.18f), new Vector3(0.05f, 1.0f, 0.08f), new Vector3(-0.38f, 0.75f, 0.28f), true), // 파랑 캡슐 + 활 상자 (활 색 = 3-3 BowColor, 아래에 선언돼 값으로)
            ("Brute", BruteTuningPath, "Brute", new Color(0.58f, 0.30f, 0.24f), "BruteAxe", new Color(0.34f, 0.34f, 0.40f), new Vector3(0.28f, 0.28f, 1.7f), new Vector3(0.95f, 1.35f, 0.45f), false), // 큰 갈색 캡슐 + 큰 무기 상자
        };
        const float KnockdownTilt = 75f;                                           // 3-4: 넘어진 플레이어 몸 기울기 (도)
        static readonly Color PlayerKnockdownColor = new Color(0.40f, 0.20f, 0.12f); // 3-4: 넘어진 몸 색 (갈색, 피격 검붉은색과 구별)


        // ---- 표현 표 (2-6 파티 HUD, 게임 수치 아님) ----
        static readonly Vector2 PartyRowSize = new Vector2(240f, 56f);            // 칸 하나 (px, 1920×1080 기준)
        const float PartyRowGap = 8f, PartySwatchWidth = 8f, PartyDotSize = 22f;   // 칸 사이 · 색 띠 너비 · Q 준비 고리
        static readonly Vector2 PartyHudMargin = new Vector2(24f, 60f);           // 화면 오른쪽에서 · 가운데에서 위로
        const int PartyNumberFontSize = 28, PartyNameFontSize = 24;
        static readonly Color PartySlotColor = new Color(0f, 0f, 0f, 0.45f);
        static readonly Color PartyActiveColor = new Color(0.95f, 0.85f, 0.55f, 0.55f); // 현재 캐릭터 칸 바탕
        static readonly Color PartyCooldownCover = new Color(0f, 0f, 0f, 0.5f);

        // ---- 표현 표 (3-1 플레이어 체력, 게임 수치 아님) ----
        static readonly Color PlayerDamageColor = new Color(1.00f, 0.32f, 0.28f);   // 플레이어가 받은 피해 숫자: 빨강 (적에게 준 피해는 노랑)
        static readonly Color PlayerHitColor = new Color(0.55f, 0.08f, 0.08f);      // 피격 경직 몸: 검붉은색
        static readonly Color PlayerDownedColor = new Color(0.35f, 0.35f, 0.38f);   // 전멸 중 몸: 회색
        static readonly Vector2 HealthBarSize = new Vector2(420f, 14f);            // 아래 가운데 체력 막대 (px, 1920×1080 기준)
        const float HealthBarBottom = 36f, HealthBarBorder = 3f;                   // 화면 아래에서 · 바탕 테두리
        static readonly Color HealthBackColor = new Color(0f, 0f, 0f, 0.55f);
        static readonly Color HealthFillColor = new Color(0.45f, 0.88f, 0.40f);    // 초록
        static readonly Color HealthLowColor = new Color(0.95f, 0.30f, 0.25f);     // 낮으면 빨강
        const float HealthLowRatio = 0.3f;                                         // 이 비율 이하면 빨강 (표현 값)
        static readonly Color FadeColor = new Color(0f, 0f, 0f, 1f);               // 전원 쓰러짐 때 화면
        const float PartyHealthHeight = 6f, PartyHealthInset = 4f;                 // 파티 칸 작은 체력 막대 높이 · 가장자리에서
        static readonly Color PartyDownedColor = new Color(0.25f, 0.25f, 0.25f, 0.70f); // 쓰러진 칸 바탕
        static readonly Color PartyDownedTextColor = new Color(0.55f, 0.55f, 0.55f);    // 쓰러진 칸 글 · 색 띠
        static readonly Color HealPointColor = new Color(0.35f, 0.85f, 0.75f);     // 회복 지점 바닥 원판
        static readonly Color HealPointActiveColor = new Color(0.60f, 1.00f, 0.90f); // 회복 중
        const float HealDiscThickness = 0.04f;                                     // 원판 두께 (m)
        static readonly Vector3 HealStatueSize = new Vector3(0.6f, 2.4f, 0.6f);    // 가운데 기둥 (m, 충돌 없음)

        // ---- 표현 표 (3-2 교체 연계, 게임 수치 아님) ----
        static readonly Color EntryTrailColor = new Color(1.00f, 0.95f, 0.60f, 0.95f);  // 등장 돌진 베기 궤적: 밝은 크림
        static readonly Color PartyEntryColor = new Color(1.00f, 0.95f, 0.45f, 0.95f);  // 등장 공격 창 동안 현재 칸 바탕 (남은 시간만큼 현재 칸 색으로)
        const float BuffIconSize = 36f, BuffBarHeight = 4f, BuffGap = 4f;              // 버프 아이콘 · 아래 남은 시간 띠 · 사이 (px)
        const float BuffHudBottom = 66f;                                               // 화면 아래에서 (체력 막대 36 + 20 위로 10)
        const int BuffFontSize = 22;
        static readonly Color BuffBackColor = new Color(0f, 0f, 0f, 0.55f);
        static readonly Color BuffBarColor = new Color(1.00f, 0.85f, 0.30f);

        // ---- 표현 표 (3-3 무기 종류, 게임 수치 아님) ----
        static readonly Color ClaymoreColor = new Color(0.50f, 0.54f, 0.62f);      // 대검 칼날: 짙은 강철
        static readonly Vector3 ClaymoreBladeSize = new Vector3(0.16f, 0.05f, 1.50f); // 큰 상자 (m)
        const float ClaymoreBladeOffset = 0.90f;                                     // 회전 중심에서 칼날 가운데까지 (로컬 +Z)
        static readonly Vector3 ClaymoreGuardSize = new Vector3(0.42f, 0.08f, 0.08f);
        static readonly Color BowColor = new Color(0.55f, 0.36f, 0.18f);           // 활 상자: 나무
        static readonly Color ArrowColor = new Color(0.95f, 0.92f, 0.78f);         // 화살 막대
        static readonly Vector3 BowPosition = new Vector3(-0.30f, 1.15f, 0.30f);   // 활 (발 기준, m) — 왼손 앞
        static readonly Vector3 BowSize = new Vector3(0.05f, 1.20f, 0.08f);
        static readonly Vector3 NockedArrowSize = new Vector3(0.03f, 0.03f, 0.70f); // 시위에 건 화살 (활에서 몸 쪽으로)
        static readonly Vector3 ArrowShaftSize = new Vector3(0.04f, 0.04f, 0.80f);  // 날아가는 화살 막대 (앞끝 = 투사체 자리)
        static readonly Vector3 ArrowHeadSize = new Vector3(0.09f, 0.09f, 0.12f);

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
            ("EnemyCover", new Vector3(6f, 1.5f, 26f), new Vector3(4f, 3f, 0.5f)), // 2-5: 뒤에 숨어 적 시야를 끊는 벽
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
            ("AttackDummy", new Vector3(-22.0f, 0.05f, 4.2f)), // 2-3: 공격 허수아비 앞 1.8m (공격 범위 안, 칼이 닿는 거리)
            ("Enemies", new Vector3(-1.0f, 0.05f, 18.0f)),     // 2-5: 적 셋 앞 (가장 가까운 Grunt1까지 12.4m — 감지 거리 밖)
            ("EnemyCover", new Vector3(6.0f, 0.05f, 24.5f)),   // 2-5: 엄폐벽 뒤 (Grunt2 시야선이 막힘)
            ("HealPoint", new Vector3(5.8f, 0.05f, 2.0f)),     // 3-1: 회복 지점 원 안 (기둥 옆)
            ("Archers", new Vector3(-15.5f, 0.05f, 20.0f)),    // 3-4: 궁수 둘 앞 (가까운 Archer1까지 14.2m — 감지 거리 밖)
            ("Brute", new Vector3(18.0f, 0.05f, 21.0f)),       // 3-4: 정예 앞 (13m — 감지 거리 밖)
        };

        // 2-1: 허수아비 배치 (발 위치, 바라보는 방향 도, 2-3: 예고 공격을 하는지) — 새 허수아비 = 줄 하나
        static readonly (string name, Vector3 position, float yaw, bool attacks)[] Dummies =
        {
            ("Dummy1", new Vector3(-16.0f, 0f, -2.0f), 180f, false),
            ("Dummy2", new Vector3(-14.8f, 0f, -2.0f), 180f, false),
            ("Dummy3", new Vector3(-12.0f, 0f, -6.0f), 180f, false),
            ("AttackDummy", new Vector3(-22.0f, 0f, 6.0f), 180f, true), // 2-3: 저스트 회피 시험용 (다른 허수아비에서 멀리)
        };

        // 2-5: 적 배치 (발 위치 = 집, 바라보는 방향 도, 순찰 지점 = 집에서 이만큼 — 0이면 제자리, 3-4: 종류 = EnemyKinds의 이름) — 새 적 = 줄 하나
        static readonly (string name, Vector3 position, float yaw, Vector3 patrol, string kind)[] Enemies =
        {
            ("Grunt1", new Vector3(-4.0f, 0f, 30.0f), 180f, new Vector3(4.0f, 0f, 0f), "Grunt"),
            ("Grunt2", new Vector3(2.0f, 0f, 32.0f), 180f, Vector3.zero, "Grunt"),
            ("Grunt3", new Vector3(-1.0f, 0f, 36.0f), 180f, new Vector3(0f, 0f, 3.0f), "Grunt"),
            ("Archer1", new Vector3(-18.0f, 0f, 34.0f), 180f, Vector3.zero, "Archer"), // 3-4
            ("Archer2", new Vector3(-13.0f, 0f, 37.0f), 180f, Vector3.zero, "Archer"),
            ("Brute", new Vector3(18.0f, 0f, 34.0f), 180f, Vector3.zero, "Brute"),
        };

        // 3-1: 회복 지점 배치 (바닥 가운데) — 새 회복 지점 = 줄 하나. 반지름은 PlayerHealthTuning
        static readonly (string name, Vector3 position)[] HealPoints =
        {
            ("HealPoint", new Vector3(5.0f, 0f, 2.0f)),
        };
        const string RespawnPointName = "Spawn"; // 3-1: 전원 쓰러진 뒤 되살아나는 자리 = 디버그 지점 Spawn (시작 지점)

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
            EnsureFolder("Assets/_Game/Data/Skills");
            EnsureFolder("Assets/_Game/Data/Characters");

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
            var attackTuning = LoadOrCreate<EnemyAttackTuning>(AttackingDummyPath);
            var skills = LoadOrCreate<SkillTuning>(SkillTuningPath);
            var grunt = LoadOrCreate<EnemyTuning>(GruntTuningPath); // 2-5
            var enemyTunings = new EnemyTuning[EnemyKinds.Length]; // 3-4: 종류마다 에셋 하나
            for (int i = 0; i < EnemyKinds.Length; i++) enemyTunings[i] = LoadOrCreate<EnemyTuning>(EnemyKinds[i].tuningPath);
            var party = LoadOrCreate<PartyTuning>(PartyPath); // 2-6
            var hero = LoadOrCreate<CharacterData>(HeroCharacterPath); // 2-6: 파티 없이 쓸 때의 캐릭터 공격력
            var health = LoadOrCreate<PlayerHealthTuning>(PlayerHealthTuningPath); // 3-1

            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(GameConfigPath);
            if (config == null) Debug.LogError("TestFieldSetup: GameConfig missing at " + GameConfigPath);
            if (inputAsset == null) Debug.LogError("TestFieldSetup: input actions missing at " + InputPath);

            var mats = new Materials();

            foreach (var go in scene.GetRootGameObjects())
                if (System.Array.IndexOf(GeneratedRoots, go.name) >= 0) Object.DestroyImmediate(go);

            if (RenderSettings.skybox == null) RenderSettings.skybox = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat");
            RenderSettings.ambientMode = AmbientMode.Skybox;

            BuildLight();
            var env = BuildEnvironment(mats);
            string nav = BakeNavMesh(env); // 3-4: Environment의 충돌체로 NavMesh (같은 에셋을 다시 굽는다)
            var points = BuildDebugPoints();
            var player = BuildPlayer(move, stamina, climb, glide, weapon, combat, skills, party, hero, inputAsset, mats);
            AddPlayerHealth(player, health); // 3-1
            var camera = BuildCamera(cam, player.transform);
            var gauge = BuildHud();
            var poiseBars = PoiseBars.CreateIn(gauge.transform.parent, PoiseBackColor, PoiseFillColor, PoiseGroggyColor, PoiseBarSize, PoiseBarHeight); // 숫자보다 먼저 = 아래에 그린다
            var numbers = DamageNumbers.CreateIn(gauge.transform.parent, DamageColor, DamageBlockedColor, DamageOutlineColor);
            SetColor(numbers, "justDodgeColor", JustDodgeTextColor);
            SetColor(numbers, "groggyColor", GroggyTextColor);
            var skillHud = SkillHud.CreateIn(gauge.transform.parent, EnsureRing(), SkillSlotSize, SkillSlotGap, SkillHudMargin, SkillKeyFontSize, SkillNumberFontSize,
                SkillSlotColor, SkillPressingColor, SkillCastingColor, SkillCooldownCover, EnergyColor, EnergyFullColor, SkillTextColor, DamageOutlineColor); // 2-4
            var partyHud = PartyHud.CreateIn(gauge.transform.parent, EnsureRing(), PartyRowSize, PartyRowGap, PartyHudMargin, PartySwatchWidth, PartyDotSize,
                PartyNumberFontSize, PartyNameFontSize, PartySlotColor, PartyActiveColor, PartyCooldownCover, EnergyFullColor, SkillTextColor, DamageOutlineColor); // 2-6
            PartyHud.AddHealthBars(partyHud, PartySwatchWidth, PartyHealthHeight, PartyHealthInset, HealthBackColor, HealthFillColor, PartyDownedColor, PartyDownedTextColor); // 3-1
            SetColor(numbers, "playerDamageColor", PlayerDamageColor); // 3-1
            SetColor(partyHud, "entryColor", PartyEntryColor); // 3-2
            var buffHud = BuffHud.CreateIn(gauge.transform.parent, BuffHudBottom, BuffIconSize, BuffBarHeight, BuffGap, BuffFontSize,
                BuffBackColor, BuffBarColor, SkillTextColor, DamageOutlineColor); // 3-2: 체력 HUD보다 먼저 (검은 판이 위)
            var healthHud = HealthHud.CreateIn(gauge.transform.parent, HealthBarSize, HealthBarBottom, HealthBarBorder, HealthBackColor, HealthFillColor, HealthLowColor,
                HealthLowRatio, FadeColor); // 3-1: HUD 마지막 자식 — 검은 판이 맨 위
            var dummies = BuildDummies(dummyTuning, attackTuning, mats);
            var enemies = BuildEnemies(enemyTunings, mats); // 2-5 · 3-4
            var root = BuildGameRoot(config, player, camera, points, gauge, numbers, dummies, poiseBars, skillHud, enemies, partyHud);
            var heals = BuildHealingPoints(env, health, mats); // 3-1
            ConnectHealth(root, player, healthHud, points, heals);
            Set(root, "buffHud", buffHud); // 3-2

            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene, ScenePath);
            return "scene " + ScenePath + (saved ? " saved" : " NOT saved") + ", roots " + string.Join(",", GeneratedRoots) + ", " + nav;
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
            public readonly Material Ground, Ramp, SteepRamp, Stairs, Wall, Pillar, Player, Nose, Wings, Dummy, Sword, Hilt, SwordTrail, AttackDummy, Grunt, GruntClub, HealPoint;
            public readonly Material Claymore, Bow, Arrow; // 3-3

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
                AttackDummy = Get("AttackDummy", AttackDummyColor, null, Vector2.one);
                Grunt = Get("Grunt", GruntColor, null, Vector2.one);             // 2-5
                GruntClub = Get("GruntClub", GruntClubColor, null, Vector2.one);
                HealPoint = Get("HealPoint", HealPointColor, null, Vector2.one); // 3-1
                Claymore = Get("Claymore", ClaymoreColor, null, Vector2.one);    // 3-3
                Bow = Get("Bow", BowColor, null, Vector2.one);
                Arrow = Get("Arrow", ArrowColor, null, Vector2.one);
            }

            internal static Material Get(string name, Color color, Texture tex, Vector2 tiling)
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

        static Transform BuildEnvironment(Materials mats)
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
            return env;
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
            WeaponTuning weapon, CombatTuning combatTuning, SkillTuning skills, PartyTuning party, CharacterData character, Object inputAsset, Materials mats)
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
            SetColor(feedback, "skillColor", SkillColor);
            SetColor(feedback, "burstColor", BurstColor);


            // 2-1: 일반 공격 + 그레이박스 칼 (회전 중심 → 칼날 상자 · 손잡이 · 끝에 궤적)
            var combat = go.AddComponent<PlayerCombat>();
            Set(combat, "weapon", weapon);
            Set(combat, "tuning", combatTuning);
            Set(combat, "skills", skills); // 2-4
            Set(combat, "party", party); // 2-6: 캐릭터마다 무기 · 스킬 · 색 (weapon · skills 칸은 파티가 없을 때)
            Set(combat, "character", character); // 2-6: 파티가 없을 때 무기에 더하는 캐릭터 기본 공격력

            var pivot = new GameObject("SwordPivot").transform;
            pivot.SetParent(visual, false);
            pivot.localPosition = SwordPivot;
            pivot.localRotation = Quaternion.Euler(SwordRest);
            // 3-3: 무기 종류마다 모델 묶음 하나 (WeaponGrayboxVisual이 현재 무기 종류 것만 보인다)
            var swordModel = new GameObject("Sword").transform;
            swordModel.SetParent(pivot, false);
            Primitive(PrimitiveType.Cube, swordModel, "Blade", new Vector3(0f, 0f, BladeOffset), Quaternion.identity, BladeSize, mats.Sword);
            Primitive(PrimitiveType.Cube, swordModel, "Hilt", new Vector3(0f, 0f, BladeOffset - BladeSize.z * 0.5f), Quaternion.identity, HiltSize, mats.Hilt);
            var claymoreModel = new GameObject("Claymore").transform; // 대검: 큰 상자 (같은 회전 중심으로 휘두른다)
            claymoreModel.SetParent(pivot, false);
            Primitive(PrimitiveType.Cube, claymoreModel, "Blade", new Vector3(0f, 0f, ClaymoreBladeOffset), Quaternion.identity, ClaymoreBladeSize, mats.Claymore);
            Primitive(PrimitiveType.Cube, claymoreModel, "Guard", new Vector3(0f, 0f, ClaymoreBladeOffset - ClaymoreBladeSize.z * 0.5f), Quaternion.identity, ClaymoreGuardSize, mats.Hilt);
            var bowModel = new GameObject("Bow").transform; // 활: 활 상자 + 시위에 건 화살 막대 (휘두르지 않는다 — Visual 아래)
            bowModel.SetParent(visual, false);
            bowModel.localPosition = BowPosition;
            Primitive(PrimitiveType.Cube, bowModel, "Limb", Vector3.zero, Quaternion.identity, BowSize, mats.Bow);
            var nocked = Primitive(PrimitiveType.Cube, bowModel, "NockedArrow", new Vector3(0.08f, 0f, -NockedArrowSize.z * 0.5f + 0.05f), Quaternion.identity, NockedArrowSize, mats.Arrow);
            claymoreModel.gameObject.SetActive(false);
            bowModel.gameObject.SetActive(false);
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
            // 2-4: 스킬 · 궁극기 칼 자세 · 궤적 색 · 궁극기 범위 원 (착지 원과 같은 LineRenderer)
            SetColor(swing, "skillTrailColor", SkillTrailColor);
            SetVector(swing, "burstStartEuler", SwordBurstStart);
            SetVector(swing, "burstEndEuler", SwordBurstEnd);
            SetColor(swing, "burstTrailColor", BurstTrailColor);
            SetColor(swing, "burstRingColor", BurstRingColor);
            SetFloat(swing, "burstRingTime", BurstRingTime);
            SetColor(swing, "entryTrailColor", EntryTrailColor); // 3-2

            // 3-3: 무기 모델 고르기 (표 한 줄 = 종류 하나) · 투사체 (화살 템플릿을 풀에서 다시 쓴다)
            var models = go.AddComponent<WeaponGrayboxVisual>();
            Set(models, "combat", combat);
            var mso = new SerializedObject(models);
            var marr = mso.FindProperty("models");
            var table = new (WeaponKind kind, GameObject root, GameObject nocked)[]
            {
                (WeaponKind.Sword, swordModel.gameObject, null),
                (WeaponKind.Claymore, claymoreModel.gameObject, null),
                (WeaponKind.Bow, bowModel.gameObject, nocked),
            };
            marr.arraySize = table.Length;
            for (int i = 0; i < table.Length; i++)
            {
                var e = marr.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("kind").enumValueIndex = (int)table[i].kind;
                e.FindPropertyRelative("root").objectReferenceValue = table[i].root;
                e.FindPropertyRelative("nocked").objectReferenceValue = table[i].nocked;
            }
            mso.ApplyModifiedPropertiesWithoutUndo();

            var arrow = new GameObject("ArrowTemplate");
            arrow.transform.SetParent(go.transform, false);
            Primitive(PrimitiveType.Cube, arrow.transform, "Shaft", new Vector3(0f, 0f, -ArrowShaftSize.z * 0.5f), Quaternion.identity, ArrowShaftSize, mats.Arrow);
            Primitive(PrimitiveType.Cube, arrow.transform, "Head", new Vector3(0f, 0f, -ArrowHeadSize.z * 0.5f), Quaternion.identity, ArrowHeadSize, mats.Hilt);
            arrow.SetActive(false);
            var shooter = go.AddComponent<ProjectileShooter>();
            Set(shooter, "arrowTemplate", arrow);
            Set(shooter, "owner", go.transform);

            // 2-3: 적 공격을 받는 쪽 (저스트 회피)
            var defense = go.AddComponent<PlayerDefense>();
            Set(defense, "tuning", combatTuning);
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
        static TrainingDummy[] BuildDummies(DummyTuning tuning, EnemyAttackTuning attackTuning, Materials mats)
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
                Material bodyMat = Dummies[i].attacks ? mats.AttackDummy : mats.Dummy;
                var body = Primitive(PrimitiveType.Capsule, visual, "Body", new Vector3(0f, DummyHeight * 0.5f, 0f), Quaternion.identity,
                    new Vector3(DummyRadius * 2f, DummyHeight * 0.5f, DummyRadius * 2f), bodyMat);
                var arms = Primitive(PrimitiveType.Cube, visual, "Arms", new Vector3(0f, DummyHeight * 0.72f, 0f), Quaternion.identity, DummyArmsSize, bodyMat);
                var dummy = go.AddComponent<TrainingDummy>();
                Set(dummy, "tuning", tuning);
                Set(dummy, "visual", visual);
                SetColor(dummy, "flashColor", DummyFlashColor);
                SetColor(dummy, "groggyColor", DummyGroggyColor);
                SetFloat(dummy, "groggyTilt", DummyGroggyTilt);
                if (Dummies[i].attacks) AddAttacker(go, attackTuning, mats);
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

        /// <summary>공격 허수아비 (2-3): DummyAttacker + 바닥 예고 원(월드 좌표 LineRenderer, 예고 중에만 보인다).</summary>
        static void AddAttacker(GameObject go, EnemyAttackTuning tuning, Materials mats)
        {
            var ringGo = new GameObject("WarningRing");
            ringGo.transform.SetParent(go.transform, false);
            var ring = ringGo.AddComponent<LineRenderer>();
            ring.sharedMaterial = mats.SwordTrail;
            ring.useWorldSpace = true;
            ring.loop = true;
            ring.widthMultiplier = WarnRingWidth;
            ring.startColor = WarnStartColor;
            ring.endColor = WarnStartColor;
            ring.shadowCastingMode = ShadowCastingMode.Off;
            ring.positionCount = 0;
            ring.enabled = false;
            var attacker = go.AddComponent<DummyAttacker>();
            Set(attacker, "tuning", tuning);
            Set(attacker, "warningRing", ring);
            SetColor(attacker, "warnStartColor", WarnStartColor);
            SetColor(attacker, "warnEndColor", WarnEndColor);
            SetColor(attacker, "hitColor", WarnHitColor);
            SetFloat(attacker, "hitFlashTime", WarnHitFlashTime);
            SetFloat(attacker, "ringHeight", WarnRingHeight);
        }

        /// <summary>
        /// 적 (2-5 · 3-4): 루트에 CharacterController + NavMeshAgent(꺼 둠 — EnemyAgent가 NavMesh가 있을 때만 켬다) + EnemyAgent, 자식 Visual에 몸 캡슐 · 눈 · 무기 상자
        /// (번쓰임 대상은 몸), 예고 부채꼴 LineRenderer, 화살을 쏘면 EnemyProjectiles + 화살 템플릿. 배치는 Enemies 표, 종류는 EnemyKinds 표.
        /// 몸 크기는 EnemyTuning(데이터), 색 · 무기 · 눈은 표현 표.
        /// </summary>
        static EnemyAgent[] BuildEnemies(EnemyTuning[] tunings, Materials mats)
        {
            var root = new GameObject("Enemies").transform;
            var result = new EnemyAgent[Enemies.Length];
            for (int i = 0; i < Enemies.Length; i++)
            {
                int k = System.Array.FindIndex(EnemyKinds, x => x.kind == Enemies[i].kind);
                if (k < 0) { Debug.LogError("TestFieldSetup: no enemy kind '" + Enemies[i].kind + "' for " + Enemies[i].name); continue; }
                var kind = EnemyKinds[k];
                EnemyTuning tuning = tunings[k];
                float height = tuning.CapsuleHeight, radius = tuning.CapsuleRadius;
                if (height <= 0f || radius <= 0f) Debug.LogWarning("TestFieldSetup: " + kind.tuningPath + " capsule size is empty - fill it from BALANCE.md and run again");

                var go = new GameObject(Enemies[i].name);
                go.transform.SetParent(root, false);
                go.transform.SetPositionAndRotation(Enemies[i].position, Quaternion.Euler(0f, Enemies[i].yaw, 0f));
                var cc = go.AddComponent<CharacterController>();
                cc.height = height;
                cc.radius = radius;
                cc.center = new Vector3(0f, height * 0.5f, 0f);
                var navAgent = go.AddComponent<NavMeshAgent>(); // 3-4: 길 계산만 (이동은 CharacterController)
                navAgent.radius = radius;
                navAgent.height = height;
                navAgent.updatePosition = false;
                navAgent.updateRotation = false;
                navAgent.enabled = false;
                var visual = new GameObject("Visual").transform;
                visual.SetParent(go.transform, false);
                var body = Primitive(PrimitiveType.Capsule, visual, "Body", new Vector3(0f, height * 0.5f, 0f), Quaternion.identity,
                    new Vector3(radius * 2f, height * 0.5f, radius * 2f), Materials.Get(kind.bodyMat, kind.body, null, Vector2.one));
                Primitive(PrimitiveType.Cube, visual, "Eye", new Vector3(0f, height * 0.78f, radius), Quaternion.identity,
                    new Vector3(GruntEyeSize * 1.6f, GruntEyeSize * 0.6f, GruntEyeSize), mats.Nose);
                Primitive(PrimitiveType.Cube, visual, "Weapon", kind.weaponOffset, Quaternion.identity, kind.weaponSize,
                    Materials.Get(kind.weaponMat, kind.weapon, null, Vector2.one));

                var fanGo = new GameObject("WarningFan");
                fanGo.transform.SetParent(go.transform, false);
                var fan = fanGo.AddComponent<LineRenderer>();
                fan.sharedMaterial = mats.SwordTrail;
                fan.useWorldSpace = true;
                fan.loop = true;
                fan.widthMultiplier = FanWidth;
                fan.startColor = WarnStartColor;
                fan.endColor = WarnStartColor;
                fan.shadowCastingMode = ShadowCastingMode.Off;
                fan.positionCount = 0;
                fan.enabled = false;

                var agent = go.AddComponent<EnemyAgent>();
                Set(agent, "tuning", tuning);
                Set(agent, "visual", visual);
                SetColor(agent, "flashColor", DummyFlashColor);
                SetColor(agent, "groggyColor", DummyGroggyColor);
                SetColor(agent, "telegraphColor", GruntTelegraphColor);
                SetFloat(agent, "groggyTilt", GruntGroggyTilt);
                SetVector(agent, "patrolOffset", Enemies[i].patrol);
                Set(agent, "warningFan", fan);
                SetColor(agent, "warnStartColor", WarnStartColor);
                SetColor(agent, "warnEndColor", WarnEndColor);
                SetColor(agent, "hitColor", WarnHitColor);
                SetFloat(agent, "hitFlashTime", FanHitFlashTime);
                SetFloat(agent, "fanHeight", FanHeight);
                if (kind.shoots)
                {
                    // 3-4: 화살 템플릿 (플레이어 화살과 같은 모양) + 투사체 실행
                    var arrow = new GameObject("ArrowTemplate");
                    arrow.transform.SetParent(go.transform, false);
                    Primitive(PrimitiveType.Cube, arrow.transform, "Shaft", new Vector3(0f, 0f, -ArrowShaftSize.z * 0.5f), Quaternion.identity, ArrowShaftSize, mats.Arrow);
                    Primitive(PrimitiveType.Cube, arrow.transform, "Head", new Vector3(0f, 0f, -ArrowHeadSize.z * 0.5f), Quaternion.identity, ArrowHeadSize, mats.Hilt);
                    arrow.SetActive(false);
                    var shooter = go.AddComponent<EnemyProjectiles>();
                    Set(shooter, "arrowTemplate", arrow);
                    Set(shooter, "owner", go.transform);
                    Set(agent, "projectiles", shooter);
                }
                var so = new SerializedObject(agent);
                var arr = so.FindProperty("flashRenderers");
                arr.arraySize = 1;
                arr.GetArrayElementAtIndex(0).objectReferenceValue = body.GetComponent<MeshRenderer>();
                so.ApplyModifiedPropertiesWithoutUndo();
                result[i] = agent;
            }
            return result;
        }

        /// <summary>
        /// 3-4: Environment 루트에 NavMeshSurface(자식 · 물리 충돌체만)를 두고 굽는다 — 데이터는 에셋 하나(NavMeshDataPath)를 제자리에서 고친다(멱등).
        /// 플레이어 · 적 · 허수아비는 Environment 밖이라 들어가지 않는다. 요약 글을 돌려준다.
        /// </summary>
        static string BakeNavMesh(Transform env)
        {
            var surface = env.gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            NavMeshBuildSettings settings = surface.GetBuildSettings();
            Physics.SyncTransforms(); // 방금 만든 충돌체의 자리를 물리에 올린다 (에디터에서는 저절로 안 됨)
            var sources = new List<NavMeshBuildSource>();
            UnityEngine.AI.NavMeshBuilder.CollectSources(env, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), sources);
            // 범위 = Environment 모델들의 렌더러 범위 (렌더러 범위는 바로 맞는다)
            var bounds = new Bounds(env.position, Vector3.zero);
            bool first = true;
            foreach (var c in env.GetComponentsInChildren<Renderer>())
            {
                if (first) { bounds = c.bounds; first = false; }
                else bounds.Encapsulate(c.bounds);
            }
            EnsureFolder(Path.GetDirectoryName(NavMeshDataPath).Replace('\\', '/'));
            var data = AssetDatabase.LoadAssetAtPath<NavMeshData>(NavMeshDataPath);
            bool created = false;
            if (data == null)
            {
                data = new NavMeshData(settings.agentTypeID) { name = Path.GetFileNameWithoutExtension(NavMeshDataPath) };
                AssetDatabase.CreateAsset(data, NavMeshDataPath);
                created = true;
            }
            bool ok = UnityEngine.AI.NavMeshBuilder.UpdateNavMeshData(data, settings, sources, bounds);
            if (!ok) Debug.LogWarning("TestFieldSetup: NavMesh bake failed");
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssetIfDirty(data);
            surface.navMeshData = data;
            surface.RemoveData();
            surface.AddData();
            return "navmesh " + NavMeshDataPath + (created ? " created" : " rebaked") + (ok ? "" : " FAILED") + " (" + sources.Count + " sources)";
        }

        static GameRoot BuildGameRoot(GameConfig config, PlayerMotor motor, OrbitCamera camera, Transform[] points, StaminaGauge gauge,
            DamageNumbers numbers, TrainingDummy[] dummies, PoiseBars poiseBars, SkillHud skillHud, EnemyAgent[] enemies, PartyHud partyHud)
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
            so.FindProperty("playerDefense").objectReferenceValue = motor.GetComponent<PlayerDefense>();
            so.FindProperty("poiseBars").objectReferenceValue = poiseBars;
            so.FindProperty("skillHud").objectReferenceValue = skillHud;
            so.FindProperty("partyHud").objectReferenceValue = partyHud; // 2-6

            var dummyArr = so.FindProperty("trainingDummies");
            dummyArr.arraySize = dummies.Length;
            for (int i = 0; i < dummies.Length; i++) dummyArr.GetArrayElementAtIndex(i).objectReferenceValue = dummies[i];
            var enemyArr = so.FindProperty("enemies"); // 2-5
            enemyArr.arraySize = enemies.Length;
            for (int i = 0; i < enemies.Length; i++) enemyArr.GetArrayElementAtIndex(i).objectReferenceValue = enemies[i];
            var arr = so.FindProperty("debugTeleportPoints");
            arr.arraySize = points.Length;
            for (int i = 0; i < points.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = points[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        /// <summary>3-1: 플레이어 체력(PlayerVitals) + 피격 · 전멸 몸 색.</summary>
        static void AddPlayerHealth(PlayerMotor motor, PlayerHealthTuning tuning)
        {
            var vitals = motor.gameObject.AddComponent<PlayerVitals>();
            Set(vitals, "tuning", tuning);
            var feedback = motor.GetComponent<PlayerGrayboxFeedback>();
            SetColor(feedback, "hitColor", PlayerHitColor);
            SetColor(feedback, "downedColor", PlayerDownedColor);
            SetColor(feedback, "knockdownColor", PlayerKnockdownColor); // 3-4
            SetFloat(feedback, "knockdownTilt", KnockdownTilt);
        }

        /// <summary>3-1: 회복 지점 (Environment 아래) — 바닥 원판(반지름 = PlayerHealthTuning) + 가운데 기둥, 둘 다 충돌 없음. 배치는 HealPoints 표.</summary>
        static HealingPoint[] BuildHealingPoints(Transform env, PlayerHealthTuning tuning, Materials mats)
        {
            float r = tuning.HealPointRadius;
            if (r <= 0f) Debug.LogWarning("TestFieldSetup: PlayerHealthTuning heal point radius is empty - fill it from BALANCE.md and run again");
            var result = new HealingPoint[HealPoints.Length];
            for (int i = 0; i < HealPoints.Length; i++)
            {
                var go = new GameObject(HealPoints[i].name);
                go.transform.SetParent(env, false);
                go.transform.position = HealPoints[i].position;
                var disc = Primitive(PrimitiveType.Cylinder, go.transform, "Disc", new Vector3(0f, HealDiscThickness * 0.5f, 0f), Quaternion.identity,
                    new Vector3(r * 2f, HealDiscThickness * 0.5f, r * 2f), mats.HealPoint);
                Primitive(PrimitiveType.Cube, go.transform, "Statue", new Vector3(0f, HealStatueSize.y * 0.5f, 0f), Quaternion.identity, HealStatueSize, mats.HealPoint);
                var hp = go.AddComponent<HealingPoint>();
                Set(hp, "disc", disc.GetComponent<MeshRenderer>());
                SetColor(hp, "idleColor", HealPointColor);
                SetColor(hp, "healingColor", HealPointActiveColor);
                result[i] = hp;
            }
            return result;
        }

        /// <summary>3-1: GameRoot에 체력 · 체력 HUD · 부활 지점(디버그 지점 Spawn) · 회복 지점을 잇는다.</summary>
        static void ConnectHealth(GameRoot root, PlayerMotor motor, HealthHud healthHud, Transform[] points, HealingPoint[] heals)
        {
            var so = new SerializedObject(root);
            so.FindProperty("playerVitals").objectReferenceValue = motor.GetComponent<PlayerVitals>();
            so.FindProperty("healthHud").objectReferenceValue = healthHud;
            Transform respawn = null;
            foreach (var p in points) if (p != null && p.name == RespawnPointName) respawn = p;
            if (respawn == null) Debug.LogError("TestFieldSetup: no debug point named " + RespawnPointName + " for the respawn point");
            so.FindProperty("respawnPoint").objectReferenceValue = respawn;
            var arr = so.FindProperty("healingPoints");
            arr.arraySize = heals.Length;
            for (int i = 0; i < heals.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = heals[i];
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
