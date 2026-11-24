# TASKS — MoaRPG

## 진행 요약 (2026-11-24 기준 — 새 세션은 여기부터 읽는다)
**최신 (2026-11-24): Phase 1 1-1 3인칭 이동 · 카메라 구현 — 그레이박스 플레이어(CharacterController) · 직접 작성한 궤도 카메라 · 시험 지형 `Assets/_Game/Scenes/TestField.unity`(메뉴 `MoaRPG/Setup/Test Field`) · 디버그 창 플레이어/카메라 섹션 · BALANCE.md 시작(1 이동 · 2 카메라). 브랜치 `phase1-move`(커밋 전), 테스트 51개 작성(Phase 0 27 + 1-1 24) · 한 번도 실행 안 함 — **테스트 돌릴 때 됐다**. 플레이 확인 대기: 0-1 · 0-2 · 0-3 · 0-4 · 1-1. 다음: 테스트 실행(사용자) → 1-2 질주 · 회피 · 스태미나.**

## Phase 0. 세팅
- [x] **0-0. 문서 3종 + 기획서** (2026-11-24 사용자 "원신 라이크 기획서 노션에", "MY_DEV_GUIDE 대입") — CLAUDE.md · GDD.md · TASKS.md, Notion 기획서
  - 결정 (2026-11-24, 사용자): 오픈월드 · 스킬 · 전투 · 가챠만, 속성 없음 / 애니메 툰 셰이딩 3D / 한국어 + 영어
- [x] **0-1. 텍스트 표** (2026-11-24 사용자 "두 프로젝트의 페이즈 0 진행") — 플레이 확인 대기
  - 구현: `Moa.Core.Localization.Text`(Get · Get(키, 값...) · SetLanguage · LanguageChanged) · `TextTable`(CSV 읽기, 따옴표 · 쉼표 · 줄바꿈 · `#` 주석 줄) · 표 `Assets/_Game/Text/strings.csv`(key,ko,en) · `Moa.Unity.UI.LocalizedLabel`(씬에는 키만) · `UiFont`(OS 글꼴 동적 생성, Pop KoreanUIFont 방식)
  - 정한 것 (Claude, 확인 필요): 작은 표는 CSV(ScriptableObject 아님 — 엑셀 · 시트로 고치기 쉬움) / 지금 언어 칸이 비면 기본 언어(첫 열), 키가 없으면 `#키#` 표시 / 자리표시자는 `string.Format`(InvariantCulture) / 글꼴은 OS 글꼴(Malgun Gothic → Apple SD Gothic Neo → Noto Sans CJK KR → Noto Sans KR → NanumGothic → Arial), 파일 다운로드 없음 / 텍스트 키 필드 이름 규칙 `…Key` / UI 글은 uGUI 기본 Text(TextMeshPro 아님 — 한글 TMP 폰트 에셋이 없어서)
  - 확인: 컴파일 오류 0 · Main 씬 플레이 → 라벨 "MoaRPG 준비 완료 · 시드 20261124"(글꼴 Malgun Gothic) → `GameRoot.DebugSetLanguage("en")` → "MoaRPG ready · seed 20261124" · 캡처 `MoaRPG/Screenshots/phase0_label_ko.png` · `phase0_label_en.png` · 콘솔 오류 0(프로젝트 밖 AI 패키지 NoSubscription 1건 제외). 확인 못 함: 다른 OS(맥 · 안드로이드) 글꼴, 빌드에서의 글꼴
  - 테스트 작성, 실행 안 함: `Moa.Tests.TextTableRulesTests`(① 쓰는 키가 표에 있음 ② 기본 언어 문장 ③ 게임 코드에 한글 문자열 없음) · `Moa.Tests.TextTests`(9개)
  - 확인 방법: `Assets/_Game/Scenes/Main.unity` 플레이 → 화면 위에 한글 라벨 → 디버그 창 [보기] 언어 en → 영어 라벨
- [x] **0-2. 디버그 창** (2026-11-24 사용자 "두 프로젝트의 페이즈 0 진행") — 플레이 확인 대기
  - 구현: `Moa.Editor.Debugging.DebugWindow`(메뉴 `MoaRPG/Debug/Debug Window`, 섹션 접기) · `IDebugSection` · 첫 섹션 `GeneralSection`(시드 표시 · [상태 변경] 재시작(같은 시드) · [상태 변경] 이 시드로 재시작 · [보기] 언어 ko/en · 지금 상태) · 게임 쪽 진입점 `GameRoot.DebugSeed` · `DebugRestart()` · `DebugRestart(시드)` · `DebugSetLanguage` · `DebugState()`
  - 정한 것 (Claude, 확인 필요): 재시작 = 씬을 다시 읽지 않고 같은 `StartRun`으로 새 판(난수 · ID 새로) / 지금 상태 글은 개발자용 영어(텍스트 표 대상 아님) / 플레이 중이 아니면 안내문만
  - 확인: 컴파일 오류 0 · 플레이 중 메뉴로 창 열림 · `DebugRestart()` 후 run 2 표시 · 캡처 `MoaRPG/Screenshots/phase0_debug_window.png`. 확인 못 함: 창의 버튼을 실제 마우스로 누르기(진입점은 코드로 불러 확인)
  - 확인 방법: Main 씬 플레이 → 메뉴 MoaRPG > Debug > Debug Window → 시드 20261124 · 지금 상태 보임 → [상태 변경] 재시작 누르면 run 숫자 증가 → en 누르면 화면 라벨이 영어
- [x] **0-3. 폴더 · 어셈블리 · 테스트** (2026-11-24 사용자 "두 프로젝트의 페이즈 0 진행") — 플레이 확인 대기
  - 구현: `Assets/_Game/`(Scripts/Core · Unity · Editor, Data, Text, Art, Scenes, Tests/EditMode) · `Moa.Core.asmdef`(noEngineReferences) · `Moa.Unity.asmdef`(→ Core, UnityEngine.UI) · `Moa.Editor.asmdef`(Editor 전용) · `Moa.Tests.asmdef`(EditMode) · `SeededRandom`(SplitMix64, 상태 저장 · 복원) · `RandomStream`(Gacha · Drop · Combat · Presentation) · `RandomStreams`(판 시드 → 용도별 시드) · `EntityId` · `IdGenerator` · `GameRoot`(컴포지션 루트) · 씬 `Assets/_Game/Scenes/Main.unity`(SampleScene 복사 + GameRoot + 임시 라벨)
  - 정한 것 (Claude, 확인 필요): 난수 알고리즘 SplitMix64(상태 64비트 하나라 스냅샷 저장이 쉬움) / 용도별 시드 = Mix(판 시드 ^ Mix(용도 번호 + 1)) / 난수 이름공간은 `Moa.Core.Rng`(`Random`이면 UnityEngine.Random과 겹침) / ID는 1부터, 0 = 없음 / Main 씬은 빌드 씬 목록에 아직 안 넣음
  - 확인: 컴파일 오류 0 · 어셈블리 4개 로드 · Moa.Core 참조 = netstandard만 · 플레이에서 용도별 난수 상태 4개 · ID 발급기 표시(디버그 창 캡처)
  - 테스트 작성, 실행 안 함: `Moa.Tests.RandomStreamsTests`(9개 — 같은 시드 = 같은 결과, 스트림끼리 독립, 상태 왕복) · `Moa.Tests.IdGeneratorTests`(5개) · `Moa.Tests.CoreAssemblyTests`(Core가 Unity를 참조하지 않음)
  - 확인 방법: Window > General > Test Runner > EditMode > Moa.Tests 실행 → 27개 통과
- [x] **0-4. 데이터 에셋 자리** (2026-11-24 사용자 "두 프로젝트의 페이즈 0 진행") — 플레이 확인 대기
  - 구현: `Moa.Unity.Data.GameConfig`(ScriptableObject, 메뉴 Create > MoaRPG > Data > Game Config) · 에셋 `Assets/_Game/Data/GameConfig.asset`(시드 기본값 20261124 · 기본 언어 ko · 텍스트 표 · UI 글꼴 후보 · 글꼴 크기 32) · 표 위치 규칙 CLAUDE.md 코딩 규칙에 한 줄
  - 정한 것 (Claude, 확인 필요): 시드 기본값 20261124(오늘 날짜) / 코드에 기본값을 두지 않고 에셋에만 / 앞으로의 표는 `Data/<분류>/`, 클래스는 `Scripts/Unity/Data/`
  - 확인: 플레이에서 GameRoot가 GameConfig의 시드 · 언어 · 표 · 글꼴로 시작(라벨 · 디버그 창 캡처)
  - 확인 방법: GameConfig.asset의 Default Seed를 바꾸고 Main 씬 플레이 → 라벨 시드 숫자가 바뀜
- [x] **0-5. Git** (2026-11-24) — 브랜치 `phase0-setup` · `.gitignore` 빌드 패턴을 루트 기준(`/MoaRPG/[Bb]uild/` · `/MoaRPG/[Bb]uilds/` · `/[Bb]uilds/`) · `/MoaRPG/Screenshots/` 무시 · `.gitattributes`(코드 CRLF, Unity YAML LF + unityyamlmerge, 이미지 · 모델 바이너리). manifest.json · ProjectSettings 변경은 커밋 제외
- [x] **0-6. MCP 연결 확인** — 2026-11-24 Claude Hand `unity-moarpg` 연결됨(Interaction Mode No Throttling)

## Phase 1. 구조 기반 + 핵심 조작
- [x] **1-1. 3인칭 캐릭터 이동 · 카메라** (2026-11-24 사용자 "다음 진행사항 진행해줘") — 플레이 확인 대기
  - 구현: Core `Moa.Core.Motion.MoveMath`(카메라 기준 방향 · 가속/감속 · 회전 · 점프 속도 · 중력 · 경사 판정) · `OrbitMath`(피치 제한 · 휠 줌 · 벽 당김 거리 · 지수 감쇠 보간 · 카메라 오프셋) / `Moa.Unity.Player.PlayerMotor`(CharacterController, FixedUpdate 이동, 비주얼 자식 `Visual`을 스텝 사이 보간) · `Moa.Unity.Cameras.OrbitCamera`(LateUpdate, SphereCast 벽 당김, 패키지 없음) · `IPlayerInput` / 입력 한 곳 `Moa.Unity.Input.PlayerInputReader` · `PlayerControls`(액션 이름 · 기본 키) · 에셋 `Assets/_Game/Input/MoaControls.inputactions` / 수치 `MovementTuning` · `CameraTuning` → `Assets/_Game/Data/Player/` (BALANCE 1 · 2번) / `GameRoot`가 플레이어 · 카메라 · 입력을 Bind, 진입점 `DebugPlayerXxx` · `DebugTeleportPlayer` · `DebugCameraXxx` · `DebugSetCameraAngles` · 입력 주입 `DebugInputMove/Look/Jump/Zoom/Walk/Clear` / 디버그 창 `PlayerSection`(위치 · 속도 · 접지 · 걷기/뛰기 · [상태 변경] 순간이동 · 지점 버튼 7개 · 입력 주입 해제) · `CameraSection`(거리 · 각도 · 벽 당김 · [보기] 각도) / 메뉴 `MoaRPG/Setup/Test Field`(`Moa.Editor.Setup.TestFieldSetup`, 멱등) → 씬 `Assets/_Game/Scenes/TestField.unity`(평지 120m 체커 · 경사 15°/35°/50° + 꼭대기 단(높이 3m) · 계단 12단(한 단 0.25m) · 벽 4(낮은 벽 0.8m 포함) · 높은 기둥 20m(등반 시험용) · 순간이동 지점 7) · 그레이박스 머티리얼 · 체커 `Assets/_Game/Art/Graybox/`
  - 정한 것 (Claude, 확인 필요): 기본 = 뛰기, **왼쪽 Ctrl(패드: 왼쪽 스틱 누르기)로 걷기 ↔ 뛰기 전환**(원신 방식, Shift는 1-2 질주용으로 비워 둠) / 점프 Space · 패드 A / 커서: 시작 때 잠금 · ESC 풀기 · 왼쪽 클릭 다시 잠금 / 기존 `InputSystem_Actions`는 그대로 두고 새 액션 에셋 `MoaControls`(없을 때만 Setup이 코드로 만든다 — 만든 뒤에는 에셋을 고친다) / 이동은 FixedUpdate(핵심 규칙 2), 떨림을 막으려 비주얼 자식만 보간 / 45°보다 가파른 면은 접지 아님 → 중력으로 미끄러짐 / 벽 당김은 즉시, 풀릴 때는 천천히 / 휠은 부호만(한 칸 = 1m) / 카메라 SphereCast는 레이어 추가 없이 플레이어 자신의 콜라이더만 제외 / 시험 지형 배치 수치는 Setup 스크립트의 표(게임 수치 아님) / 화면 글 없음(텍스트 표 키 추가 없음) / Phase 1 첫 줄을 1-1 · 1-2 · 1-3으로 나눔
  - 구조 메모: 원래 `Moa.Unity.asmdef`에 Input System 참조를 넣으려 했으나 이 세션에서 기존 asmdef를 고칠 수 없어, 입력만 새 어셈블리 `Moa.Unity.Input`(→ Moa.Core · Moa.Unity · Unity.InputSystem)으로 분리하고 Moa.Unity는 `IPlayerInput`만 안다. Setup 메뉴도 그 타입이 필요해 새 에디터 어셈블리 `Moa.Editor.Setup`(→ Moa.Core · Moa.Unity · Moa.Unity.Input · Moa.Editor · Unity.InputSystem). 기존 asmdef 4개는 그대로.
  - 수치: BALANCE 1번(이동) · 2번(카메라)
  - 확인: 컴파일 오류 0 · Setup 메뉴 3번 실행 → 같은 루트 6개(멱등) · TestField 플레이(에디터 포커스 없음 → 일시정지 + `EditorApplication.Step`, 입력은 `GameRoot.DebugInputXxx`로 주입): 전진 수평 5.5 m/s(10프레임 안에 최고 속도) / 대각선(W+D, 카메라 요 30°) 5.5 m/s(빨라지지 않음) · 진행 · 몸 방향 75° / 손 떼면 약 0.14초에 정지 / 점프 정점 +1.28m(목표 1.2, 고정 스텝 오차) · 32프레임 뒤 착지 / 35° 경사 꼭대기(3m)까지 오름 / 50° 경사 아래에서 막힘(높이 변화 0) / 계단 12단을 접지 유지하며 3m까지 / 걷기 2.0 · 뛰기 5.5 m/s / 마우스 −1000px → 요 −120°, 피치 −30° ~ 70°에서 멈춤, 줌 2 ~ 10m에서 멈춤 / 뒤 벽 앞에서 카메라 거리 6 → 0.92m(벽면 0.35m 앞), 벽에서 멀어지면 천천히 복귀 · 콘솔 오류 0(프로젝트 밖 AI 패키지 NoSubscription 1건 제외)
    캡처: `MoaRPG/Screenshots/p1_forward.png` · `p1_diagonal.png` · `p1_jump.png` · `p1_slope35.png` · `p1_slope50.png` · `p1_stairs.png` · `p1_camera_rotate.png` · `p1_camera_wall.png`
    확인 못 함: 실제 키보드 · 마우스 · 패드 입력(주입으로만 확인), 커서 잠금 · ESC, 실제 프레임레이트에서의 보간 떨림 체감, 디버그 창 버튼을 마우스로 누르기, 15° 경사(35°가 되므로 생략). 참고: 벽에 바짝 붙으면 카메라가 0.9m까지 와서 캡슐이 화면을 크게 가린다(`p1_camera_wall.png`) — 가까울 때 비주얼 숨기기는 나중 후보
  - 테스트 작성, 실행 안 함: `Moa.Tests.MoveMathTests`(13) · `Moa.Tests.OrbitMathTests`(7) · `Moa.Tests.PlayerTuningAssetTests`(4 — BALANCE 1 · 2번 값 검산)
  - 확인 방법: (TestField가 없으면 메뉴 MoaRPG > Setup > Test Field) → `Assets/_Game/Scenes/TestField.unity` 플레이 → Game 뷰 클릭(커서 잠김) → WASD 이동 · 마우스 시점 · 휠 줌 · Space 점프 · 왼쪽 Ctrl 걷기/뛰기 · ESC 커서 풀기 → 파란 35° 경사는 올라가고 빨간 50° 경사는 못 오름, 계단 오름, 뒤쪽 벽에 붙어 카메라를 벽 쪽으로 돌리면 카메라가 앞으로 당겨짐. 디버그 창(MoaRPG > Debug > Debug Window) 플레이어 섹션에 위치 · 속도 · 접지가 보이고 지점 버튼으로 순간이동
  - 피드백: Setup 첫 실행에서 GameRoot · 입력 참조가 비어 있었음 → 원인: 에셋을 읽은 뒤 씬을 열어 쓰이지 않는 에셋이 내려가 참조가 빔 → 수정: 씬을 먼저 열고 에셋을 읽는다. 검사: 플레이 시작 때 "is not assigned" 오류
- [ ] 1-2. 질주 · 회피 · 스태미나
- [ ] 1-3. 등반 · 활공 (TestField의 `TallPillar` 준비됨)

## 다른 PC에서 할 일
- Claude Hand 패키지(manifest.json)는 PC마다 `unity-install`로 넣는다.
