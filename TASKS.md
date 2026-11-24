# TASKS — MoaRPG

## 진행 요약 (2026-11-24 기준 — 새 세션은 여기부터 읽는다)
**최신 (2026-11-24): Phase 0 0-1 ~ 0-4 구현 — 텍스트 표(ko/en) · 디버그 창 · 폴더/어셈블리(Moa.Core/Unity/Editor/Tests) · 시드 난수 · 고유 ID · GameConfig, `Assets/_Game/Scenes/Main.unity`. 브랜치 `phase0-setup`, 테스트 27개 작성 · 실행 안 함, 플레이 확인 대기: 0-1 · 0-2 · 0-3 · 0-4. Phase 0 완료(플레이 확인 대기). 다음: 테스트 실행(사용자) → Phase 1 후보 묻기.**

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

## Phase 1. 구조 기반 + 핵심 조작 (예정)
- [ ] 3인칭 캐릭터 이동 · 카메라 → 질주 · 회피 · 스태미나 → 등반 · 활공

## 다른 PC에서 할 일
- Claude Hand 패키지(manifest.json)는 PC마다 `unity-install`로 넣는다.
