# TASKS — MoaRPG

## 진행 요약 (2026-11-24 기준 — 새 세션은 여기부터 읽는다)
**최신 (2026-11-24): MY_DEV_GUIDE 적용 시작 — 문서 3종(CLAUDE · GDD · TASKS) 작성, Notion 기획서 작성, Claude Hand 연결 완료. 브랜치 `phase0-setup`(커밋 전), 테스트 0개. 다음: 0-1 텍스트 표 → 0-2 디버그 창 → 0-3 폴더 · 어셈블리 · 테스트.**

## Phase 0. 세팅
- [x] **0-0. 문서 3종 + 기획서** (2026-11-24 사용자 "원신 라이크 기획서 노션에", "MY_DEV_GUIDE 대입") — CLAUDE.md · GDD.md · TASKS.md, Notion 기획서
  - 결정 (2026-11-24, 사용자): 오픈월드 · 스킬 · 전투 · 가챠만, 속성 없음 / 애니메 툰 셰이딩 3D / 한국어 + 영어
- [ ] **0-1. 텍스트 표** — 키 → 한국어 · 영어 표, `Text.Get(키)`, 폰트(한글 + 영문) 확인, 테스트 3종
- [ ] **0-2. 디버그 창** — 메뉴 `MoaRPG/Debug/Debug Window`, 첫 섹션: 재시작 · 시드 표시 · 지금 상태
- [ ] **0-3. 폴더 · 어셈블리 · 테스트** — CLAUDE.md 폴더 구조대로, Core는 noEngineReferences, EditMode 테스트 어셈블리
- [ ] **0-4. 데이터 에셋 자리** — Data/ 아래 첫 표 — "코드에 숫자 금지"
- [ ] **0-5. Git** — 브랜치 `phase0-setup`, `.gitignore`의 `**/[Bb]uild/` · `**/[Bb]uilds/` 넓은 패턴을 루트 기준 경로로, `.gitattributes`
- [ ] **0-6. MCP 연결 확인** — 2026-11-24 Claude Hand `unity-moarpg` 연결됨(Interaction Mode No Throttling)

## Phase 1. 구조 기반 + 핵심 조작 (예정)
- [ ] 3인칭 캐릭터 이동 · 카메라 → 질주 · 회피 · 스태미나 → 등반 · 활공

## 다른 PC에서 할 일
- Claude Hand 패키지(manifest.json)는 PC마다 `unity-install`로 넣는다.
