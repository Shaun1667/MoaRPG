# CLAUDE.md — MoaRPG

## 프로젝트 개요
원신 라이크 3D 오픈월드 액션 RPG. 오픈월드를 달리고 오르고 날며 탐험하고, 4인 파티를 실시간으로 교체하며 스킬 · 궁극기로 싸우고, 캐릭터 · 무기는 가챠로 얻는다. **속성 · 속성 반응은 없다** — 전투의 깊이는 경직(그로기) · 교체 연계 · 저스트 회피로 만든다. 참고: 《원신》(구조만 참고, 그대로 베끼지 않음).
기획은 GDD.md(상세판: Notion "MoaRPG 기획서 — 오픈월드 액션 RPG" https://app.notion.com/p/42274e10a76481419d1bd8dcf9344380), 작업 순서와 일지는 TASKS.md, 수치는 BALANCE.md(첫 수치가 생기면). 작업 방식 원본은 MY_DEV_GUIDE.md(= Docs/Source/MY_DEV_GUIDE.md, 고치지 않음).
- **새 세션을 시작하면** 이 문서 → TASKS.md 맨 위 "진행 요약" 순서로 읽고 다음 항목을 확인한 뒤 시작한다. 다른 PC면 Docs/진행상황.md도. "다음 작업: 미정"이면 후보를 먼저 묻는다.
- 수치는 BALANCE.md와 데이터 에셋 기준이며 코드에 임의로 넣지 않는다.
- Unity 프로젝트 폴더는 `MoaRPG/` (저장소 루트의 한 단계 아래).

## 참조 문서
| 문서 | 역할 | 상태 |
|---|---|---|
| GDD.md | 기획 요약 (상세는 Notion) | 초안 (2026-11-24) |
| TASKS.md | 진행 요약 · 작업 순서 · 일지 | 사용 중 |
| BALANCE.md | 수치 (0번 = 데이터 에셋 위치 표) | 사용 중 (1-1부터) |
| MY_DEV_GUIDE.md | 협업 · 개발 방식 원본 | 고치지 않음 |
| MoaRPG/ClaudeHand/NOTES.md | Claude Hand 하위 세션 메모 | 사용 중 |

## 협업 방식
- 한국어. 1인 개발. 설계 결정은 선택지 + 추천안으로 묻고 `결정 (날짜, 사용자)`로 TASKS에 남긴다. 사소한 건 정하고 "정한 것 (Claude, 확인 필요)"로 보고.
- 한 항목 = 작업 브랜치(`phaseN-내용`) 커밋 + 보고. 지시 없이 main에 커밋 · 푸시하지 않는다. "main에 병합하고 푸시" = fast-forward 병합 · 푸시 · 작업 브랜치 삭제.
- 커밋 메시지 `MM.DD/Phase-항목/PascalCase요약`, 수정은 `…Fix`. 한 항목 = 한 커밋.
- 보고: 한 일 · 확인한 것 · 확인 못 한 것 · 확인 방법 · 커밋 상태 (+ 정한 것, 다음 추천).
- 테스트 실행은 사용자가 한다 (결정 2026-11-24, MY_DEV_GUIDE 기본값) — Claude는 작성만, 보고에 "테스트 작성, 실행 안 함".
- 완료 = 테스트 통과 + 확인 방법 + 사용자 플레이 확인. 구현만 = `— 플레이 확인 대기`.

## 작업 원칙
- 한 번에 기능 하나 (TASKS 현재 항목만). 요청받지 않은 시스템을 미리 만들지 않는다 — 단 핵심 설계 규칙의 구조는 처음부터.
- 구현 전 문서를 읽고 구현 후 문서를 고친다. 다르면 문서를 실제 기준으로 고치고 알린다.
- 에디터 수작업은 목록으로 안내, 반복 생성은 멱등 Setup 메뉴로.
- 실패 · 생략 · 미확인은 그대로 보고.

## 기술 스택
| 항목 | 값 | 상태 |
|---|---|---|
| 엔진 | Unity 6000.3.25f1 | 확정 2026-11-24, 변경 금지 |
| 렌더 | URP 17.3 + 애니메 툰 셰이딩(Pop의 "Pop/Toon Character"를 출발점으로) | 결정 2026-11-24, 사용자 |
| 입력 | Input System 1.20 (키보드 · 마우스 · 패드) | 확정 2026-11-24, 변경 금지 |
| UI | uGUI | 정한 것 (Claude, 확인 필요) |
| 플랫폼 | PC | GDD 기준, 모바일은 나중에 검토 |
| 언어 | 한국어(기본) + 영어 | 결정 2026-11-24, 사용자 |
| 3D 에셋 | VARCO 3D 생성 + Mixamo 리깅 (Pop · 3DRoguelike와 같은 파이프라인) | 정한 것 (확인 필요) |
| 저장소 | GitHub Shaun1667/MoaRPG | — |

## 폴더 구조 (Phase 0에서 만든다)
```
MoaRPG/Assets/_Game/
  Scripts/Core/      규칙 코드 — 엔진 참조 없음 (Moa.Core.asmdef, noEngineReferences)
                     전투 수치 · 스킬 쿨타임 · 에너지 · 경직 · 가챠 · 성장 · 인벤토리 · 저장 상태
  Scripts/Unity/     엔진 어댑터 — 이동 · 물리 · 카메라 · 애니메이션 · 히트 판정 · UI (Moa.Unity.asmdef)
  Scripts/Unity/Input/  입력 한 곳 (Moa.Unity.Input.asmdef — Input System 참조는 여기만)
  Scripts/Editor/    에디터 도구 · Setup 메뉴 · 디버그 창 (Moa.Editor.asmdef)
  Scripts/Editor/Setup/  Setup 메뉴 (Moa.Editor.Setup.asmdef — Moa.Unity.Input이 필요한 에디터 코드)
  Data/              ScriptableObject 데이터 (캐릭터 · 무기 · 스킬 · 적 · 배너 · 수치)
  Text/              텍스트 표 (키 → 한국어 · 영어)
  Input/             입력 액션 에셋 (MoaControls.inputactions)
  Art/               모델 · 애니메이션 · 셰이더 · 텍스처
  Scenes/
  Tests/EditMode/    단위 테스트 (Moa.Tests.asmdef)
```

## 코딩 규칙
- 네임스페이스 `Moa.Core.*` / `Moa.Unity.*` / `Moa.Editor.*`.
- 화면에 보이는 모든 글은 텍스트 표 한곳 — 코드 · 프리팹 · 데이터에 문장 금지, 코드는 `Text.Get(키)`, `{0}` 자리표시자 (MY_DEV_GUIDE 1번). 캐릭터 · 무기 · 스킬 이름과 설명도 키로.
- 개발자 디버그 기능은 에디터 디버그 창(메뉴 `MoaRPG/Debug/Debug Window`)에 섹션으로, 게임 쪽 진입점은 `GameRoot.DebugXxx`, 상태를 바꾸면 [상태 변경] (MY_DEV_GUIDE 2번).
- 입력은 한 곳에서만 읽는다.
- 텍스트 표는 `Moa.Core.Localization.Text`(Get · SetLanguage) 한 곳, 표 `Assets/_Game/Text/strings.csv`(머리줄 `key,ko,en` — 첫 언어 열 = 기본 언어), 새 문장 = 줄 하나 · 새 언어 = 열 하나. 씬 · 프리팹 라벨은 `LocalizedLabel`(키만 저장). 텍스트 키를 담는 직렬화 필드는 소문자로 시작하고 `Key`로 끝나는 이름(textKey · nameKey — 테스트가 이 이름으로 찾는다). UI 글꼴은 `UiFont`가 OS 글꼴(맑은 고딕 등)을 동적 글꼴로, 후보 이름은 GameConfig (0-1).
- 디버그 창은 `Moa.Editor.Debugging.DebugWindow` 한 곳, 새 섹션 = `IDebugSection` 클래스 하나 + `DebugWindow.Sections`에 한 줄, 게임 쪽 진입점은 `GameRoot.DebugXxx` (0-2).
- 씬 연결은 `GameRoot`(컴포지션 루트) 한 곳, 새 판과 디버그 재시작은 같은 `StartRun(시드)` (0-3).
- 난수는 `RandomStreams` 한 곳 — 용도 `RandomStream`(Gacha · Drop · Combat · Presentation)별 `SeededRandom`, `System.Random` · `UnityEngine.Random` 직접 사용 금지, 새 용도 = enum 끝에 한 줄 (0-3).
- 엔티티 ID는 `IdGenerator.Next()` → `EntityId` 한 곳 (0 = 없음), 저장할 값은 `NextValue` (0-3).
- 데이터 에셋은 `Assets/_Game/Data/<분류>/`(Characters · Weapons · Skills · Enemies · Banners …)에 ScriptableObject, 클래스는 `Scripts/Unity/Data/`. 전역 설정은 `Data/GameConfig.asset` 하나(시드 기본값 · 기본 언어 · 텍스트 표 · UI 글꼴 후보). 코드에는 수치 기본값을 두지 않는다 (0-4).
- 이동 · 카메라는 `Moa.Unity.Player.PlayerMotor`(CharacterController, FixedUpdate, 비주얼은 자식 `Visual`) · `Moa.Unity.Cameras.OrbitCamera`(직접 작성, Cinemachine 등 패키지 없음) 한 곳, 순수 계산은 Core `Moa.Core.Motion.MoveMath` · `OrbitMath`, 수치 `Data/Player/MovementTuning.asset` · `CameraTuning.asset`(BALANCE 1 · 2번). 입력은 `Moa.Unity.Input.PlayerInputReader` 한 곳(에셋 `Assets/_Game/Input/MoaControls.inputactions`), 나머지 코드는 `IPlayerInput`만 본다 — 새 액션 = `PlayerControls` 상수 + 에셋에 액션 한 줄. 확인용 입력 주입은 `GameRoot.DebugInputXxx`. 시험 지형은 메뉴 `MoaRPG/Setup/Test Field`(멱등, 배치 표 한 줄) → `Scenes/TestField.unity` (1-1).
- 스태미나 · 질주 · 회피 규칙은 Core `Moa.Core.Motion.Stamina`(Drain · TrySpend · Tick · Fill, 지침) · `Dodge`(TryStart · Advance · 무적 · 쿨) 한 곳 — 이동과 회피가 스태미나 하나를 같이 쓴다(1-3 등반 · 활공도 이것을 쓴다). 실행은 `PlayerMotor`가 고정 스텝에서(쓸 것을 쓰고 마지막에 `Stamina.Tick`), 수치 `Data/Player/StaminaTuning.asset`(BALANCE 3번). 화면 게이지는 `Moa.Unity.UI.StaminaGauge`(HUD 캔버스, 가득이면 숨김), 그레이박스 상태 색은 `PlayerGrayboxFeedback`(색은 Setup 표). 입력 `Sprint` · `Dodge`(왼쪽 Shift · RT / 오른쪽 클릭 · B), 주입 `GameRoot.DebugInputSprint/Dodge`, 디버그 창 `StaminaSection` (1-2).
- 등반 · 활공 규칙은 Core `Moa.Core.Motion.Climb`(TryAttach 붙는 조건 · Hold 움직일 때만 소모 · TryLeap/AdvanceLeap 위로 도약 · StartMantle/AdvanceMantle/MantlePath 올라서기 · End 끝난 까닭 + 다시 붙기 지연) · `Glide`(TryOpen 펴는 조건 · Hold 소모 · Vertical/Horizontal 속도 맞추기 · Close 까닭) 한 곳 — 스태미나는 같은 `Stamina`, 등반 중에는 `Stamina.Tick(dt, false)`(회복 안 함 · 지연 처음부터). 벽 · 턱 · 높이 레이 검사와 실제 이동은 `PlayerMotor`(자기 콜라이더 제외), 수치 `Data/Player/ClimbTuning.asset` · `GlideTuning.asset`(BALANCE 4 · 5번). 입력은 새 액션 없이 기존 Jump(공중에서 활공 펴기/접기 · 벽에서 도약) · Dodge(벽에서 떨어지기), 벽 쪽으로 움직이면 붙는다. 상태 색 · 날개 상자 `PlayerGrayboxFeedback`, 진입점 `GameRoot.DebugClimbXxx` · `DebugGlideXxx`, 디버그 창 `ClimbGlideSection`, 시험 지점 `PillarTop` · `PillarAir` (1-3).
- 일반 공격 · 피해 · 체력 규칙은 Core `Moa.Core.Combat.ComboAttack`(단계 표: 선딜 · 판정 · 후딜 · 입력 열림 · 내딛기, Press 시작/예약 · Advance → ComboTick · TryHit 휘두르기당 대상 한 번(EntityId) · Interrupt 회피/점프/공중 → 1타) · `DamageMath`(공격력 × 배율 → 방어 ÷ (방어 + 방어 상수) → 정수, 0.5 올림 · 최소 1) · `Health`(Damaged · Died) 한 곳 — 등반 · 활공 · 회피 중 · 공중에서는 공격 입력을 버린다. 무기 = 에셋 하나 `Data/Weapons/<무기>.asset`(`WeaponTuning`, 단계 수 = 표 줄 수, 판정 방식(근접/투사체) 말고는 무기 종류별 분기 코드 금지), 공통 `Data/Combat/CombatTuning.asset`, 허수아비 `Data/Enemies/TrainingDummy.asset`(BALANCE 6 · 7 · 8번). 실행은 `Moa.Unity.Combat.PlayerCombat`(PlayerMotor가 고정 스텝마다 Step, 이동 뒤 판정 구간이면 CheckHits = OverlapSphere + 앞 부채꼴, 자기 콜라이더 제외, 레이어 추가 없음) · 맞는 쪽은 `IHitTarget`(새 적 = 이것을 구현) · 허수아비 `TrainingDummy` · 피해 숫자 `Moa.Unity.UI.DamageNumbers`(키 `ui.combat.damage` `{0}`) · 칼 그레이박스 `SwordSwingVisual`. 전투 엔티티 ID는 `GameRoot`가 판을 시작할 때 발급. 입력 `Attack`(커서가 잠겼을 때 왼쪽 클릭 · 패드 X, 풀렸을 때의 클릭은 잠금만), 주입 `GameRoot.DebugInputAttack`, 디버그 창 `CombatSection`, 시험 지점 `Dummies`. 치명타는 아직 없음 — 넣을 때 `RandomStream.Combat` (2-1).
- 강공격 · 낙하 공격은 Core `Moa.Core.Combat.PlayerAttack`(콤보 · 강공격 · 낙하를 한 곳에서 고름 — 공중에서 누르면 낙하 시도, 땅에서 누르면 콤보 + 길게 누르기 세기, 누른 스텝 뒤로 holdTime(18스텝) 누르고 있으면 강공격: 지금 단계를 끊고(AttackEnd.Charged) 콤보 1타로, 스태미나가 모자라면 아무것도 안 함(ChargeBlock.NoStamina), 강공격 · 낙하 중 누름은 Busy) · `ChargedAttack`(ChargedRules 베기 표 + 스태미나 · TryStart · Advance → ComboTick · 베기당 대상 한 번) · `PlungeAttack`(PlungeRules 최소 높이 · 높은 낙하 기준 · 배율 · 낙하 속력 · 착지 경직 · TryStart 등반 · 회피 불가, 활공 가능 · Land 떨어진 높이 → 배율 · 착지 판정 한 번(대상마다 한 번) · 경직은 회피 · 점프로 끊김) 한 곳. 강공격 표 · 범위와 낙하 배율 · 범위는 무기 에셋 `WeaponTuning`, 길게 누르기 · 낙하 높이 · 속력 · 경직은 `CombatTuning`(BALANCE 6 · 7번). 실행은 `PlayerCombat.Step(ctx, stamina, dt)` → `AttackTick` · 강공격 판정은 같은 `CheckHits`(강공격 범위 · 각도) · 착지는 `PlayerCombat.Land`(발 + hitHeight에서 반지름 plungeRange 구, `PlungeLanded` 이벤트) — `PlayerMotor`가 낙하 중 수평 0 · 아래로 fallSpeed, 닿으면(Below) Land, 활공 중 낙하는 활공을 접고(GlideEnd.Plunge), 공격 · 낙하 중 벽에 안 붙는다. 입력은 새 액션 없이 기존 Attack의 `IPlayerInput.AttackHeld`, 주입 `GameRoot.DebugInputAttackHold(초)` · `DebugTeleportAboveDummies(m)`, 피해 기록 `HitReport.Kind`(AttackMode), 상태 색 우선순위 무적 > 회피 > 낙하 > 강공격 > 공격 > 등반 > 활공 > 지침 > 질주, 칼 `SwordSwingVisual`(강공격 큰 휘두르기 + 궤적 색 · 낙하 칼 아래로 · 착지 원), 디버그 창 `CombatSection`, 시험 지점 `DummiesAir` (2-2).
- 경직 · 그로기 · 저스트 회피는 Core `Poise`(Apply · Advance · 그로기 중 안 쌓임 · 끝나면 0) · `JustDodge`(Classify · Judge · 슬로모션 실제 시간 / 반격 게임 시간) · `EnemyAttackCycle`(쉬기 → 예고 → 닿음 → 쉬는 자세, 행동 불가면 처음부터) 한 곳. 경직 수치는 무기 표 `WeaponTuning.Step.poise` · plunge*Poise, 적 게이지는 `DummyTuning`, 저스트는 `CombatTuning`, 예고 공격은 `EnemyAttackTuning`(`Data/Enemies/AttackingDummy.asset`, BALANCE 9번). 맞는 쪽 경직 = `IPoiseTarget`, 플레이어 쪽 = `PlayerDefense`(적이 `ReceiveEnemyAttack`, Time.timeScale은 여기만 바꿈), 공격 허수아비 `DummyAttacker`, 막대 `PoiseBars`, 글 `ui.combat.just_dodge` · `ui.combat.groggy`, 디버그 창 `PoiseSection`, 시험 지점 `AttackDummy` (2-3).
- 스킬(E) · 궁극기(Q) · 에너지는 Core `PlayerAttack`(궁극기 → 스킬 → 일반 공격 순으로 고름, AttackInput) · `SkillAttack`(Press · HoldStep: 떼면 Tap, HoldTime이면 Hold · 쿨은 쓰는 순간 · TakeEnergy 한 번) · `BurstAttack`(가득 · 쿨 · 무적, 회피/점프로 안 끊김) · `Energy` · `SlashSequence` 한 곳. 수치는 캐릭터마다 에셋 하나 `Data/Skills/<캐릭터>.asset`(SkillTuning, 베기 한 줄 = WeaponTuning.Step), 처치 에너지는 적 표 `energyOnDefeat`(IEnergyDrop), BALANCE 10번. 입력 Skill · Burst(E · RB / Q · Y, 없으면 조용히 건너뜀), 주입 `GameRoot.DebugInputSkill/SkillHold/Burst`, HUD `SkillHud`(키 ui.skill.* · ui.burst.*), 상태 색 궁극기 금색 · 스킬 청록, 디버그 창 `SkillSection` (2-4).
- 적 AI는 Core `Moa.Core.Combat.EnemyBrain`(상태 기계, 공격 주기 EnemyAttackCycle · 시야 CanSee · 공격 자리 InHitArea) 한 곳, Unity `EnemyAgent`(CharacterController · 시야선 레이 · PlayerDefense.ReceiveEnemyAttack), 적 한 종류 = `Data/Enemies/<적>.asset`(EnemyTuning, BALANCE 11번), 배치는 TestFieldSetup의 Enemies 표 한 줄, 진입점 `GameRoot.DebugEnemy*`, 디버그 창 `EnemySection` (2-5).
- 파티 교체는 Core `Moa.Core.Combat.Party`(캐릭터마다 PlayerAttack — 스킬 · 궁극기 쿨 · 에너지 따로, TrySwitch 쿨 · 막힘, Advance 대기 쿨, GainDefeatEnergy 현재 그대로 · 대기 ×비율) 한 곳, 캐릭터 = `Data/Characters/<캐릭터>.asset`(CharacterData: nameKey · 색 · 무기 · 스킬 · 캐릭터 기본 공격력, 공격력 = 캐릭터 + 무기 기본 공격력, 파티가 없으면 PlayerCombat의 character 칸이 캐릭터 공격력), 파티 = `Data/Characters/Party.asset`(PartyTuning, BALANCE 12번). 실행은 `PlayerCombat`(Step 처음에 교체 입력, 끝에 Party.Advance), 입력 Party1~4(1~4 · 십자, 없으면 건너뜀), HUD `PartyHud`(키 ui.party.slot), 몸 색 `PlayerGrayboxFeedback`, 진입점 `GameRoot.DebugParty*`, 디버그 창 `PartySection`(캐릭터 · 무기 · 합 공격력) (2-6).
- 플레이어 체력 · 쓰러짐은 Core `Moa.Core.Combat.PartyVitals`(캐릭터마다 Health · 방어, 필드 캐릭터만 맞음 · DamageMath, 땅 위 경직 + 밀려남, 0이면 다음 살아 있는 칸으로 Party.ForceSwitch(쿨 무시) · 쓰러진 칸은 SwitchBlock.Downed, 전원 쓰러짐 → WipePhase 어두워짐 · 부활 · 밝아짐, 회복 지점 Heal) 한 곳, 수치 `Data/Player/PlayerHealthTuning.asset` · 캐릭터 에셋 maxHealth · defense · 적 에셋 attack · attackMultiplier(BALANCE 13번). 실행은 `Moa.Unity.Combat.PlayerVitals`(PlayerMotor가 고정 스텝 처음에 Step, 적 공격은 PlayerDefense.ReceiveEnemyAttack(from, attack, multiplier)가 Hit일 때만, 쓰러져 교체되면 PlayerCombat.SyncActive) · 회복 지점 `HealingPoint`(배치는 TestFieldSetup HealPoints 표 한 줄) · HUD `HealthHud`(아래 가운데 막대 + 검은 판) · PartyHud 칸 막대 · 플레이어 피해 숫자 `DamageNumbers.ShowPlayerDamage`, 부활 지점 · 적 되돌리기는 GameRoot(respawnPoint), 진입점 `GameRoot.DebugPlayerHealth*`, 디버그 창 `PlayerHealthSection` (3-1).
- 교체 연계는 Core `Moa.Core.Combat.EntryAttack`(교체 직후 창 안 공격 = 등장 베기 한 번, E · Q가 창을 닫음) · `HandoffRules` · `HandoffBuff`(피해 · 경직 배율 · 보호막, 받은 캐릭터가 나가면 끝) 한 곳 — 창을 열고 버프를 거는 것은 `Party`(교체 때, 쓰러져 바뀐 교체는 없음), 보호막은 `PartyVitals`가 먼저 쓴다. 수치는 무기 `WeaponTuning.entrySlash` · 파티 `PartyTuning`(entryWindow · handoffWindow · entryGroggyBonus) · 캐릭터 스킬 `SkillTuning.handoff*`(BALANCE 14번). 피해 = 배율 × 그로기 × 버프 × 등장 그로기 보너스(PlayerCombat.Report), 버프가 없으면 정확히 1. HUD `BuffHud`(체력 막대 위, 키 ui.buff.*) · `PartyHud` 등장 창 빛남, 진입점 `GameRoot.DebugEntry*/DebugHandoff*`, 디버그 창 `SwitchComboSection` (3-2).
- 무기 종류는 `WeaponTuning`(kind = `WeaponKind` 모델 · 표시만, 동작마다 판정 방식 `HitDelivery` Melee/Projectile · 투사체 수치 · autoAimRange · 강공격 chargedAimTime(조준형) / chargedSpinDrainPerSecond(회전형)) 한 곳 — 분기는 판정 방식 하나. 투사체 규칙은 Core `Moa.Core.Combat.ProjectileSet`(빠르기 · 수명 · 한 발에 한 대상 · 풀링), 실행은 `Moa.Unity.Combat.ProjectileShooter`(SphereCast · 화살 풀, 맞으면 PlayerCombat이 근접과 같은 Report), 조준 · 회전은 `ChargedAttack`. 모델 고르기 `WeaponGrayboxVisual`(표 한 줄 = 종류 하나), 진입점 `GameRoot.DebugWeapon*/DebugProjectile*/DebugSetWeapon`, 디버그 창 `WeaponSection`, BALANCE 15번. 새 무기 종류 = enum 끝에 한 줄 + 에셋 하나 (3-3).
- 적 공격 · 길찾기는 Core `EnemyBrain`(공격 표 `EnemyAttackSpec` 한 줄 = 공격 하나, 차례로 · 근접은 예고 끝 스텝 · 돌진(Active)은 이어지며 한 번 · 투사체는 FireProjectile · 원거리 RetreatRange · HitStunTime 0 = 안 멈춤) 한 곳, 길은 `IEnemyPath.Steer`(다음 지점만 — Unity `EnemyAgent`가 NavMeshAgent로 계산, 이동은 CharacterController, NavMesh가 없으면 곧장). 적 = `Data/Enemies/<적>.asset`(EnemyTuning: EnemyRank · attacks 표, BALANCE 11 · 16번), 적 투사체 `EnemyProjectiles`(3-3 ProjectileSet), 넘어짐은 Core `PartyVitals`(knockdownTime, 회피로 못 끊음) · `PlayerDefense.ReceiveEnemyAttack(from, attack, multiplier, knockdownTime)`. NavMesh는 TestFieldSetup이 Environment에서 `Scenes/TestField/NavMesh-Environment.asset`으로 굽고(멱등), 적 종류 = Setup `EnemyKinds` 한 줄 + 에셋 하나, 진입점 `GameRoot.DebugKnockdownPlayer`, 디버그 창 `EnemySection` (3-4). asmdef: Moa.Unity · Moa.Editor.Setup이 Unity.AI.Navigation을 참조.
- (시스템이 생길 때마다 한 줄씩: "○○는 `클래스` 한 곳, 데이터 `에셋`, 새 항목 = 표 한 줄")

## 핵심 설계 규칙 (변경 금지)
바꾸고 싶어지면 영향 범위를 표로 정리하고 사용자 결정을 받은 뒤에만.
1. 규칙과 표현 분리 — 피해 계산 · 쿨타임 · 에너지 · 경직 · 가챠 · 성장은 Core. 이동 · 물리 · 히트 판정은 Unity가 하고 Core에 "맞았다" 이벤트만 넘긴다.
2. 전투 규칙은 고정 틱으로 진행 (이동 · 물리는 Unity FixedUpdate).
3. 저장할 상태는 스냅샷 하나 (계정 · 파티 · 캐릭터 · 무기 · 재화 · 가챠 기록 · 월드 진행).
4. 시드 난수 주입 · 용도별 분리 — 가챠 ≠ 드롭 ≠ 전투(치명타) ≠ 연출. 가챠 확률 · 천장은 데이터 + 검산 테스트로 고정.
5. 모든 엔티티 고유 ID.
6. **속성 없음** — 속성 · 속성 반응 · 속성 저항 필드를 만들지 않는다 (결정 2026-11-24, 사용자).
7. 데이터 주도 — 새 캐릭터 · 무기 · 스킬 · 적 · 배너 = 표 한 줄.

## 아트 · 그래픽 규칙
- 애니메 툰 셰이딩 3D (결정 2026-11-24, 사용자). 캐릭터 셰이더는 Pop의 툰 셰이더(2단 램프 · 크림 림 · 일정 두께 외곽선 · SRP Batcher 호환)를 옮겨 와 시작.
- 모델은 VARCO 생성 → Mixamo 리깅(OBJ ×100) → Humanoid. 데이터는 비주얼을 ID로만 참조, 없으면 그레이박스.

## Git · 환경
- 커밋 전 `git status` → 내 파일만 `git add` → `git diff --cached --stat`. 다른 세션 변경 제외.
- **`MoaRPG/Packages/manifest.json`의 Claude Hand 패키지 줄과 `manifest.json.claude-hand.bak`은 커밋하지 않는다** (이 PC 경로가 들어 있음).
- Claude Hand 하위 세션 `unity-moarpg`로 에디터를 다룬다.
