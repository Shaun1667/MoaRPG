# BALANCE — MoaRPG

수치의 원본은 데이터 에셋이고, 이 문서는 그 값과 근거를 적는다. 값을 바꾸면 **에셋 · 이 표 · 검산 테스트**를 함께 고친다 (CLAUDE.md: 코드에 수치 기본값 없음).

## 0. 데이터 에셋 위치
| 번호 | 에셋 | 클래스 | 검산 테스트 |
|---|---|---|---|
| 1 | `Assets/_Game/Data/Player/MovementTuning.asset` | `Moa.Unity.Data.MovementTuning` | `Moa.Tests.PlayerTuningAssetTests` |
| 2 | `Assets/_Game/Data/Player/CameraTuning.asset` | `Moa.Unity.Data.CameraTuning` | `Moa.Tests.PlayerTuningAssetTests` |
| — | `Assets/_Game/Data/GameConfig.asset` | `Moa.Unity.Data.GameConfig` | (시드 · 언어 · 글꼴, 0-4) |

## 1. 이동 (TASKS 1-1) — 정한 것 (Claude, 2026-11-24, 확인 필요)
| 항목 | 값 | 단위 | 근거 |
|---|---|---|---|
| walkSpeed | 2.0 | m/s | 사람 걷기 정도 (왼쪽 Ctrl 전환) |
| runSpeed | 5.5 | m/s | 기본 이동. 원신 달리기 체감(약 5~6) |
| groundAcceleration | 30 | m/s² | 0.18초에 최고 속도 — 반응 빠르게 |
| groundDeceleration | 40 | m/s² | 0.14초에 멈춤 |
| airAcceleration | 10 | m/s² | 공중에서는 방향을 조금만 바꿈 |
| airDeceleration | 4 | m/s² | 공중에서 손을 떼도 관성 유지 |
| turnSpeed | 720 | °/s | 뒤돌기(180°) 0.25초 |
| jumpHeight | 1.2 | m | 낮은 벽(0.8m)을 넘는다. 실측 정점 1.28m (고정 스텝 0.02초 오차) |
| gravity | 25 | m/s² | 현실(9.8)보다 무겁게 — 둥둥 뜨지 않게, 체공 약 0.64초 |
| maxFallSpeed | 40 | m/s | 낙하 속도 한계 |
| groundStickSpeed | 8 | m/s | 땅에 붙이는 아래 속도. 뛰며 45° 내리막(5.5 m/s)에서도 뜨지 않게 |
| slopeLimit | 45 | ° | 시험 지형: 35° 오름, 50° 못 오름(미끄러짐) |
| stepOffset | 0.35 | m | 계단 한 단 0.25m를 점프 없이 |
| capsuleHeight | 1.8 | m | 그레이박스 캡슐 = 사람 키 |
| capsuleRadius | 0.35 | m | |
| skinWidth | 0.05 | m | CharacterController 권장(반지름의 10% 정도) |

## 2. 카메라 (TASKS 1-1) — 정한 것 (Claude, 2026-11-24, 확인 필요)
| 항목 | 값 | 단위 | 근거 |
|---|---|---|---|
| pivotHeight | 1.5 | m | 발에서 어깨 높이를 본다 |
| followSharpness | 20 | 1/s | 따라가기 지연 약 0.05초 |
| startPitch | 15 | ° | 살짝 내려다봄 |
| minPitch · maxPitch | −30 · 70 | ° | 올려다보기 · 내려다보기 한계 |
| mouseSensitivity | 0.12 | °/px | 마우스 750px ≈ 90° |
| stickSpeed | 180 | °/s | 패드 오른쪽 스틱 끝까지 |
| invertY | false | | |
| startDistance | 5 | m | |
| minDistance · maxDistance | 2 · 10 | m | 휠 줌 범위 |
| zoomStep | 1 | m | 휠 한 칸 |
| zoomSharpness | 10 | 1/s | 줌 보간 |
| collisionRadius | 0.25 | m | 벽 검사 SphereCast 반지름 |
| collisionPadding | 0.1 | m | 벽 앞 여유 |
| collisionMinDistance | 0.5 | m | 가장 가까이 당겨지는 거리 |
| collisionReleaseSharpness | 5 | 1/s | 벽이 사라지면 천천히 돌아감 (당길 때는 즉시) |
| fieldOfView | 60 | ° | |
| nearClip | 0.1 | m | |

## 검산 케이스
- 점프 처음 속도 √(2 × 25 × 1.2) = 7.75 m/s → `MoveMathTests.JumpVelocity_ReachesHeight`
- 경사 35°(법선 y 0.819) 걸을 수 있음 · 50°(0.643) 못 걸음, 한계 45° → `MoveMathTests.IsWalkable_35Yes_50No_AtLimit45`
- 1 · 2번 표의 값 그대로 → `PlayerTuningAssetTests.Movement_MatchesBalance1` · `Camera_MatchesBalance2`

## 실험 기록
| 날짜 | 무엇 | 결과 |
|---|---|---|
| 2026-11-24 | TestField 코드 주입 플레이 (1-1) | 뛰기 5.5 · 걷기 2.0 m/s, 점프 정점 +1.28m, 35° 오름 · 50° 막힘, 계단 3m 오름, 벽 앞 카메라 6 → 0.92m |
