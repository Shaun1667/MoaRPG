# BALANCE — MoaRPG

수치의 원본은 데이터 에셋이고, 이 문서는 그 값과 근거를 적는다. 값을 바꾸면 **에셋 · 이 표 · 검산 테스트**를 함께 고친다 (CLAUDE.md: 코드에 수치 기본값 없음).

## 0. 데이터 에셋 위치
| 번호 | 에셋 | 클래스 | 검산 테스트 |
|---|---|---|---|
| 1 | `Assets/_Game/Data/Player/MovementTuning.asset` | `Moa.Unity.Data.MovementTuning` | `Moa.Tests.PlayerTuningAssetTests` |
| 2 | `Assets/_Game/Data/Player/CameraTuning.asset` | `Moa.Unity.Data.CameraTuning` | `Moa.Tests.PlayerTuningAssetTests` |
| 3 | `Assets/_Game/Data/Player/StaminaTuning.asset` | `Moa.Unity.Data.StaminaTuning` | `Moa.Tests.StaminaTuningAssetTests` |
| 4 | `Assets/_Game/Data/Player/ClimbTuning.asset` | `Moa.Unity.Data.ClimbTuning` | `Moa.Tests.ClimbGlideTuningAssetTests` |
| 5 | `Assets/_Game/Data/Player/GlideTuning.asset` | `Moa.Unity.Data.GlideTuning` | `Moa.Tests.ClimbGlideTuningAssetTests` |
| 6 | `Assets/_Game/Data/Weapons/Sword.asset` (새 무기 = 이 폴더에 에셋 하나) | `Moa.Unity.Data.WeaponTuning` | `Moa.Tests.CombatTuningAssetTests` |
| 7 | `Assets/_Game/Data/Combat/CombatTuning.asset` | `Moa.Unity.Data.CombatTuning` | `Moa.Tests.CombatTuningAssetTests` |
| 8 | `Assets/_Game/Data/Enemies/TrainingDummy.asset` | `Moa.Unity.Data.DummyTuning` | `Moa.Tests.CombatTuningAssetTests` |
| 9 | `Assets/_Game/Data/Enemies/AttackingDummy.asset` | `Moa.Unity.Data.EnemyAttackTuning` | `Moa.Tests.PoiseTuningAssetTests` |
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

## 3. 스태미나 · 질주 · 회피 (TASKS 1-2) — 시작 값: 기획서 13장(최대 · 회복 · 지연 · 질주 소모 · 회피 비용), 나머지는 정한 것 (Claude, 2026-11-25, 확인 필요)
| 항목 | 값 | 단위 | 근거 |
|---|---|---|---|
| maxStamina | 240 | | 기획서 13장. 가득에서 질주 240 ÷ 18 = 13.3초(약 107m) |
| regenPerSecond | 25 | /s | 기획서 13장. 바닥에서 가득까지 1 + 9.6 = 10.6초 |
| regenDelay | 1.0 | s | 기획서 13장 "소모 멈춘 뒤 약 1초". 쓴 스텝마다 처음부터 |
| exhaustRecover | 60 | | 바닥나면(지침) 60까지 차야 다시 질주 — 바닥에서 1 + 2.4 = 3.4초. 회피(20)는 지침 중에도 양만 있으면 된다 |
| sprintSpeed | 8.0 | m/s | 뛰기(5.5)의 약 1.45배, 원신 질주 체감 |
| sprintDrainPerSecond | 18 | /s | 기획서 13장. 땅 위에서 움직일 때만 (공중 · 제자리 · 회피 중에는 안 줄어듦) |
| dodgeCost | 20 | | 기획서 13장. 모자라면 회피가 안 나가고 스태미나도 그대로 |
| dodgeDistance | 3.5 | m | 짧은 대시. 속력 3.5 ÷ 0.3 = 11.7 m/s |
| dodgeDuration | 0.3 | s | 고정 스텝 15번 |
| dodgeInvulnerableTime | 0.2 | s | 시작부터. 지금은 상태 플래그만 (피해 없음) |
| dodgeCooldown | 0.2 | s | 회피가 끝난 뒤. 연타하면 0.5초마다 한 번 (2초에 4번) |
| dodgeGroundOnly | true | | 원신처럼 땅 위에서만 (공중에서 누르면 무시) |
| gaugeAnchorHeight | 1.2 | m | 게이지가 따라가는 점 (발에서 위로) |
| gaugeScreenOffset | (90, 0) | px (1920×1080 기준) | 캐릭터 오른쪽 |
| gaugeSize | 64 | px (1920×1080 기준) | 원형 게이지 지름 |
| gaugeHideDelay | 0.6 | s | 가득 찬 뒤 보이는 시간 |
| gaugeFadeTime | 0.3 | s | 사라지는 시간 |

## 4. 등반 (TASKS 1-3) — 정한 것 (Claude, 2026-11-25, 확인 필요)
| 항목 | 값 | 단위 | 근거 |
|---|---|---|---|
| minWallAngle · maxWallAngle | 60 · 120 | ° | 위쪽과 벽 법선 사이(수직 = 90). 걸을 수 있는 경사(45°)와 50° 경사는 붙지 않고, 30°까지 앞으로 기운 벽은 붙는다 |
| attachInputAngle | 50 | ° | 입력 방향과 벽 쪽 방향 차이. 비스듬히 스쳐 지나가면 안 붙는다 |
| climbSpeed | 1.5 | m/s | 원신 체감(느리게). 기둥 20m = 13.3초 |
| drainPerSecond | 10 | /s | 움직이는 동안만. 매달려 가만히 있으면 줄지도 차지도 않는다(회복 지연을 처음부터). 기둥 20m = 133 (가득 240에서 가능) |
| jumpCost | 25 | | 위로 도약 1번. 모자라면 안 나가고 그대로 |
| jumpHeight · jumpDuration | 1.8 · 0.35 | m · s | 도약 속력 5.14 m/s, 고정 스텝 18번(마지막 0.01초) |
| reattachDelay | 0.5 | s | 회피 · 바닥남 · 벽 끝으로 떨어진 뒤 다시 붙기까지. 내려와 땅에 닿음 · 올라서기는 지연 없음 |
| probeDistance | 0.3 | m | 벽 · 턱 검사 레이 길이(캡슐 표면에서). 붙기 검사 레이는 캡슐 가운데 높이(0.9m) |
| ledgeHeight | 0.6 | m | 턱 레이 높이 = 캡슐 꼭대기 − 0.6 = 발에서 1.2m. 여기서 벽이 사라지고 몸 레이(0.9m)는 벽이면 올라서기 |
| mantleForward | 0.8 | m | 올라선 뒤 앞으로. 캡슐 지름 0.7보다 길게 — 가장자리를 넘어선다 |
| mantleDuration | 0.35 | s | 앞 절반 위로(턱 높이 + 0.05), 뒤 절반 앞으로 — 벽을 뚫지 않게 |

## 5. 활공 (TASKS 1-3) — 정한 것 (Claude, 2026-11-25, 확인 필요)
| 항목 | 값 | 단위 | 근거 |
|---|---|---|---|
| minHeight | 1.5 | m | 땅에서 이 높이 이상에서만 편다. 평지 점프 정점(1.28m)에서는 안 펴진다 |
| horizontalSpeed | 6 | m/s | 뛰기(5.5)보다 조금 빠르게. 입력이 없으면 몸 앞으로 |
| horizontalAcceleration | 8 | m/s² | 가속 · 감속 같은 값. 0 → 6 m/s 0.75초 |
| fallSpeed | 2 | m/s | 활공비 3:1 (6 ÷ 2) — 기둥 20m에서 약 60m |
| verticalAcceleration | 30 | m/s² | 떨어지던 속도를 −2로 맞추는 빠르기 (−10 → −2 0.27초) |
| drainPerSecond | 5 | /s | 가득에서 48초. 바닥나면 접힌다 |

## 6. 한손검 — 일반 공격 콤보 (TASKS 2-1) · 강공격 · 낙하 공격 (2-2) — 정한 것 (Claude, 2026-11-25, 확인 필요)
무기마다 에셋 하나(`Data/Weapons/`), 콤보 단계 수 = 표의 줄 수. 시간은 고정 스텝(0.02초)의 배수.
| 항목 | 값 | 단위 | 근거 |
|---|---|---|---|
| baseAttack | 100 | | 무기 공격력. 캐릭터 공격력은 파티 · 성장 때 더한다 |
| comboResetTime | 0.5 | s | 단계가 끝난 뒤(예약 없이) 이 안에 누르면 다음 타, 지나면 1타 |
| attackMoveFactor | 0 | × | 공격 중 이동 배율 — 0 = 멈춤(원신 한손검), 몸은 단계 시작 때만 돈다 |
| hitRange | 2.2 | m | 판정 구 반지름(몸 가운데 높이에서 대상 표면까지). 캡슐 지름 0.7의 약 3배 |
| hitAngle | 120 | ° | 앞 부채꼴 전체 각도(좌우 60°) |
| hitHeight | 1.0 | m | 판정 중심 높이(발에서) |

| 타 | multiplier | windup | active | recovery | inputOpen | lunge | 합계 |
|---|---|---|---|---|---|---|---|
| 1 | 0.45 | 0.10 | 0.10 | 0.20 | 0.10 | 0.30 | 0.40 |
| 2 | 0.45 | 0.10 | 0.10 | 0.22 | 0.10 | 0.30 | 0.42 |
| 3 | 0.60 | 0.12 | 0.10 | 0.24 | 0.12 | 0.40 | 0.46 |
| 4 | 0.65 | 0.12 | 0.12 | 0.26 | 0.12 | 0.40 | 0.50 |
| 5 | 0.85 | 0.16 | 0.14 | 0.40 | 0.16 | 0.60 | 0.70 |
- 단위: 배율 ×, 시간 s, 내딛기 m. 선딜(windup) → 판정(active) → 후딜(recovery). inputOpen = 단계 시작부터 다음 입력을 받기 시작하는 때(그 전 입력은 버림) — 선딜이 끝나는 때와 같게. 마지막 타는 예약을 받지 않고 끝나면 바로 1타.
- 근거: 원신 한손검 5타 약 2.5초(합 2.48초), 배율 합 3.0 — 뒤로 갈수록 세고 마지막 타가 가장 세다. 내딛기는 선딜 + 판정 구간 동안 같은 빠르기(합이 정확히 표 값).

### 강공격 (TASKS 2-2) — 정한 것 (Claude, 2026-11-25, 확인 필요)
공격을 누른 스텝 뒤로 7번 chargeHoldTime 동안 누르고 있으면 지금 단계를 끊고 나간다(콤보는 1타로). 베기 수 = 표의 줄 수.
| 항목 | 값 | 단위 | 근거 |
|---|---|---|---|
| chargedStaminaCost | 20 | | 원신 한손검 강공격 20. 모자라면 나가지 않고 콤보는 그대로 |
| chargedHitRange | 2.6 | m | 일반(2.2)보다 넓게 — 크게 휘두름 |
| chargedHitAngle | 180 | ° | 앞 반원(좌우 90°) |

| 베기 | multiplier | windup | active | recovery | lunge | 합계 |
|---|---|---|---|---|---|---|
| 1 | 0.56 | 0.16 | 0.08 | 0.06 | 0.30 | 0.30 |
| 2 | 0.72 | 0.08 | 0.10 | 0.36 | 0.30 | 0.54 |
- 합 0.84초, 배율 합 1.28 — 허수아비(방어 100)에 45 + 58 = 103. inputOpen은 쓰지 않는다(0). 가만히 서서 길게 누르면 1타(0.38초 지점, 후딜 중)를 끊고 이어진다(원신의 N1C).

### 낙하 공격 (TASKS 2-2) — 정한 것 (Claude, 2026-11-25, 확인 필요)
| 항목 | 값 | 단위 | 근거 |
|---|---|---|---|
| plungeLowMultiplier | 1.28 | × | 떨어진 높이 < 7번 plungeHighHeight(5m). 허수아비에 102 |
| plungeHighMultiplier | 1.60 | × | 5m 이상. 허수아비에 128 |
| plungeRange | 2.5 | m | 착지 판정 구 반지름(발 + hitHeight에서, 방향 없음, 한 번만). 허수아비 둘(1.71m)은 안, 셋째(4.16m)는 밖 |

## 7. 전투 공통 (TASKS 2-1 · 2-2) — 정한 것 (Claude, 2026-11-25, 확인 필요)
| 항목 | 값 | 단위 | 근거 |
|---|---|---|---|
| defenseConstant | 400 | | 피해 = 공격력 × 배율 × (1 − 방어 ÷ (방어 + 400)), 0.5 올림 · 최소 1. 방어 400이면 절반 |
| autoAimRange | 5 | m | 이동 입력 없이 공격하면 이 안의 가장 가까운 적 쪽으로 몸을 돌린다(없으면 지금 방향) |
| damageFontSize | 36 | px (1920×1080 기준) | 피해 숫자 크기 |
| damageLifetime | 0.8 | s | 숫자가 떠 있는 시간 |
| damageRise | 0.8 | m | 그동안 떠오르는 높이 |
| damageFadeTime | 0.3 | s | 끝에서 흐려지는 시간 |
| damageSpread | 24 | px | 숫자가 겹치지 않게 차례로 −24 · 0 · +24 옆으로 (난수 없음) |
| chargeHoldTime | 0.36 | s | 2-2. 누른 스텝 뒤로 이만큼(고정 스텝 18번) 계속 누르면 강공격 |
| plungeMinHeight | 1.0 | m | 2-2. 발 아래 이 높이 이상 공중에서만 낙하(활공 중 가능, 등반 · 회피 중 불가). 평지 점프 정점(1.28m)에서도 된다 |
| plungeHighHeight | 5.0 | m | 2-2. 떨어진 높이(시작 발 − 착지 발)가 이 이상이면 높은 낙하 |
| plungeFallSpeed | 20 | m/s | 2-2. 곧장 아래로(수평 0, 이미 더 빠르면 그대로). 10m를 0.5초 |
| plungeLandRecovery | 0.40 | s | 2-2. 착지 뒤 경직(고정 스텝 20번), 이동 없음 · 회피 · 점프로 끊긴다 |
- 치명타 없음(2-1). 넣을 때는 `RandomStream.Combat` 스트림을 쓴다.

## 8. 허수아비 (TASKS 2-1) — 정한 것 (Claude, 2026-11-25, 확인 필요)
| 항목 | 값 | 단위 | 근거 |
|---|---|---|---|
| maxHealth | 600 | | 한손검 콤보 한 번(240)의 2.5배 — 세 번째 콤보 중에 쓰러진다 |
| defense | 100 | | 20% 감소 → 한손검 36 / 36 / 48 / 52 / 68 |
| respawnDelay | 3 | s | 쓰러진 뒤 다시 서기까지(체력 가득) |
| knockbackDistance | 0.15 | m | 맞으면 몸(비주얼만)이 밀리는 거리 — 콜라이더는 제자리 |
| knockbackTime | 0.06 | s | 밀려 나가는 시간, 돌아오는 데는 두 배(0.12) |
| flashTime | 0.1 | s | 맞으면 흰색 번쩍임 |
| numberHeight | 2.0 | m | 피해 숫자가 뜨는 높이(발에서) |

## 9. 경직 · 그로기 · 저스트 회피 · 예고 공격 (TASKS 2-3) — 정한 것 (Claude, 2026-11-25, 확인 필요 — 그로기 5초 · 피해 ×1.3만 GDD)
| 곳 | 항목 | 값 | 근거 |
|---|---|---|---|
| 6번 한손검 `Sword.asset` | 콤보 1~5타 `poise` | 8 · 8 · 10 · 12 · 16 (합 54) | 콤보 한 번으로는 안 되고 두 번째 콤보 5타에서 그로기 |
| 6번 | 강공격 `poise` | 15 · 20 | |
| 6번 | `plungeLowPoise` · `plungeHighPoise` | 20 · 35 | |
| 7번 `CombatTuning.asset` | `justDodgeWindow` | 0.16 s (8스텝) | 닿는 순간 회피 시작 8스텝 이내 + 무적이면 Just, 무적 중 창 밖 Avoided, 무적 아니면 Hit |
| 7번 | `justSlowScale` · `justSlowDuration` | 0.3 · 0.8 s(실제 시간) | |
| 7번 | `justCounterTime` · `justCounterPoiseMultiplier` | 3.0 s(게임 시간) · ×2 | 반격이면 콤보 한 번(108)으로 그로기. 2초면 5타(1.94초 + 슬로모션 0.24초)가 창 밖 |
| 8번 `TrainingDummy.asset` | `maxPoise` | 100 | |
| 8번 | `groggyDuration` · `groggyDamageTaken` | 5 s · ×1.3 | GDD |
| 8번 | `poiseRegenDelay` · `poiseRegenPerSecond` | 2 s · 25/s | 그로기 중 안 쌓임, 끝나면 0 |
| 예고 공격 `AttackingDummy.asset` | `attackInterval` · `telegraphTime` · `recoveryTime` | 2.0 · 0.8 · 0.6 s (주기 3.4초) | 예고 끝나는 스텝에 닿음, 그로기 · 쓰러짐이면 멈추고 처음부터 |
| 예고 공격 | `attackRange` | 3 m (평면 거리) | |

- 표현 값(Setup 표): 그로기 몸 노랑 + 뒤로 14°, 경직 막대 96×10px(발에서 2.3m 위, 주황 → 그로기 때 노랑으로 남은 시간), 예고 원 엷은 노랑 → 빨강 · 닿는 순간 흰색, 글 색 저스트 하늘색 · 그로기 주황

## 검산 케이스
- 점프 처음 속도 √(2 × 25 × 1.2) = 7.75 m/s → `MoveMathTests.JumpVelocity_ReachesHeight`
- 경사 35°(법선 y 0.819) 걸을 수 있음 · 50°(0.643) 못 걸음, 한계 45° → `MoveMathTests.IsWalkable_35Yes_50No_AtLimit45`
- 1 · 2번 표의 값 그대로 → `PlayerTuningAssetTests.Movement_MatchesBalance1` · `Camera_MatchesBalance2`
- 질주 소모 18/s × 2초 = 36 → `StaminaTests.Sprint_Drains18PerSecond`, 가득에서 질주 13.3초 → `FullSprint_LastsAbout13Seconds`
- 회복 지연 1초 동안 그대로 → 그 뒤 25/s → `NoRegen_DuringDelay_ThenRegen25PerSecond`, 바닥에서 가득 10.6초 → `FromEmpty_FullAfter10Point6Seconds`
- 지침: 바닥난 뒤 60 미만이면 질주 불가 → `Exhausted_BlocksSprint_Until60`
- 회피 3.5m ÷ 0.3초, 고정 스텝 15번, 무적 0.2초, 쿨 0.2초, 비용 20, 모자라면 안 나감 · 공중 불가 → `DodgeTests`
- 3번 표의 값 그대로 → `StaminaTuningAssetTests.Stamina_MatchesBalance3`
- 벽 기울기 60 ~ 120°만 · 입력 각도 50° 이내 · 지침이면 안 붙음 → `ClimbTests.Attach_*`, 움직일 때 10/s · 가만히 있으면 그대로 → `Moving_Drains10PerSecond_StillDoesNot`, 회복 막기 → `TickWithoutRegen_RestartsDelay`
- 도약 비용 25 · 1.8m ÷ 0.35초(18스텝) → `ClimbTests.Leap_Costs25_Covers18m_In035Seconds`, 올라서기 0.35초 · 위 먼저 → `Mantle_Takes035Seconds` · `MantlePath_RiseFirst_ThenForward`, 다시 붙기 0.5초 → `ReattachDelay_05Seconds`
- 활공 높이 1.5m · 평지 점프로는 안 펴짐 · 5/s(가득 48초) · 낙하 −2 · 수평 6 (0.76초) → `GlideTests`
- 4 · 5번 표의 값 그대로 → `ClimbGlideTuningAssetTests.Climb_MatchesBalance4` · `Glide_MatchesBalance5`
- 피해 100 × 0.45 = 45 → 방어 100(20%) → 36, 한손검 36 · 36 · 48 · 52 · 68 = 240, 방어 400이면 절반, 0.5 올림 · 최소 1 → `DamageTests`
- 콤보: 판정 0.1초 = 5스텝, 예약하면 후딜 끝(0.40초)에 2타, 입력 열림 전 입력은 버림, 끝나고 0.5초 지나면 1타, 내딛기 합 0.30m, 휘두르기당 대상 한 번, 회피가 끊음, 등반 · 활공 · 공중 불가 → `ComboAttackTests`
- 6 · 7 · 8번 표의 값 그대로 · 시간은 0.02초 배수 · 콤보 합 2.48초 · 허수아비는 2 ~ 3 콤보 → `CombatTuningAssetTests`
- 강공격: 누른 스텝 뒤 18스텝(0.36초)에 나감 · 17스텝은 안 됨, 가만히 길게 = 1타 뒤 강공격(N1C), 비용 20 · 19면 안 나감(콤보 그대로), 2베기 0.84초 · 판정 4 · 5스텝 · 내딛기 0.6m, 3타 중 강공격 → 콤보 1타로, 강공격 중 누름 Busy, 회피가 끊음 → `ChargedPlungeTests`
- 낙하: 높이 1.0m부터(0.99m TooLow), 등반 · 회피 불가 · 활공 가능, 떨어진 높이 5m 기준 1.28 / 1.60, 착지 판정 한 번 · 대상마다 한 번, 경직 0.40초 · 회피 · 점프로 끊김, 경직 0이어도 판정 → `ChargedPlungeTests`
- 6 · 7번 강공격 · 낙하 값 그대로 · 강공격 45 + 58 · 낙하 102 / 128 · 길게 누르기 18스텝 · 낙하 최소 높이 < 점프 높이 < 높은 낙하 → `CombatTuningAssetTests.Sword_ChargedAndPlunge_MatchBalance6` · `Combat_ChargeAndPlunge_MatchBalance7` · `ChargedAndPlunge_AreConsistent`
- 경직 · 그로기 · 저스트: 그로기 피해 47 · 47 · 62 · 68 · 88, 2콤보 5타에 그로기 · 반격이면 1콤보, 8스텝 Just · 9 Avoided · 10 Hit, 주기 3.4초 · 예고 시작 2.0초 · 닿음 2.8초, 9번 표 값 그대로 → `PoiseJustDodgeTests` · `PoiseTuningAssetTests`

## 실험 기록
| 날짜 | 무엇 | 결과 |
|---|---|---|
| 2026-11-24 | TestField 코드 주입 플레이 (1-1) | 뛰기 5.5 · 걷기 2.0 m/s, 점프 정점 +1.28m, 35° 오름 · 50° 막힘, 계단 3m 오름, 벽 앞 카메라 6 → 0.92m |
| 2026-11-25 | TestField 코드 주입 플레이 (1-2) | 질주 8.000 m/s · 소모 18.000/s, 10에서 0.56초에 바닥 → 지침 · 뛰기 5.5 m/s, 회복 지연 1.0초 · 회복 25.000/s, 60.5에서 지침 풀림 → 질주 다시, 바닥에서 가득 10.62초, 게이지 가득 뒤 0.9초에 숨김, 회피 3.500m · 15스텝(0.3초) · 무적 10스텝(0.2초) · 비용 20, 19에서 안 나감(NoStamina), 공중 안 나감(Airborne), 연타 2초에 4번 |
| 2026-11-25 | TestField 코드 주입 플레이 (1-3) | 기둥 붙기 · 오르기 1.500 m/s · 소모 10.0/s, 도약 +1.800m · 비용 25, 가만히 2초 스태미나 그대로, 꼭대기 올라서기 17스텝(0.35초) → 20.05m · 앞 0.8m 접지, 스태미나 5 → 0.5초에 바닥 · 떨어짐(지침 보라) · 60 차면 다시 붙음, 회피 → 떨어짐 · 0.50초 뒤 다시 붙음, 내려가다 땅 → Landed, 옆으로 가다 벽 끝 → WallEnded, 뒤 벽(4m) 올라서기, 35° · 50° 경사 안 붙음, 활공 수평 6.000 · 낙하 2.000 m/s · 5.0/s, 평지 점프(1.31m)에서 TooLow, 점프로 접기/다시 펴기, 착지로 풀림, 스태미나 1 → 0.2초에 접힘 · 지침 중 못 폄, 기둥 앞 공중에서 활공 → 벽에 붙음(Wall) |
| 2026-11-25 | TestField 코드 주입 플레이 (2-1) | 1 ~ 5타 이어짐(2타는 0.40초에 시작), 피해 36 · 36 · 48 · 52 · 68, 1 · 2타는 허수아비 둘을 한 번씩, 3 ~ 5타는 내딛은 뒤 한쪽만(다른 쪽은 부채꼴 밖), 0.5초 대기로 끊김, 점프 · 회피로 끊김, 등반 · 활공 · 공중 공격 막힘, 공격력 ×10(360)으로 두 대에 쓰러짐 → 2.98초 뒤 600으로 다시 섬, 공격 뒤 회피 3.500m |
| 2026-11-25 | TestField 코드 주입 플레이 (2-2) | 길게 누르기 → 0.38초(누른 스텝 + 18)에 강공격 · 240 → 220 · 45 · 58이 허수아비 둘에 · 0.84초, 스태미나 19면 NoStamina · 1타만, 3타 중 강공격 → 다음 1타, DummiesAir(10.02m) 낙하 20 m/s · 0.5초에 착지 · 9.97m 높은 낙하 128 둘(셋째 4.16m 범위 밖) · 경직 0.40초, 점프 정점 1.01m에서 낮은 낙하(1.03m) 102 둘 · 0.20m에서는 TooLow, 활공 중 낙하(활공 접힘), 착지 경직 회피 · 점프로 끊김, 등반 중 막힘, 강공격 중 회피 3.500m |
| 2026-11-25 | TestField 코드 주입 플레이 (2-3) | 2콤보 5타 4.46초에 그로기 · 그로기 중 1타 47 · 4.84초에 풀림, 저스트 8스텝 Just · 9 Avoided · 10 Hit, 슬로모션 실제 0.8초(게임 0.22초), 반격 경직 ×2로 1콤보 그로기, 낙하 경직 35 |
