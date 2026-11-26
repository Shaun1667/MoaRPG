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
| 10 | `Assets/_Game/Data/Skills/SwordHero.asset` (새 캐릭터 = 이 폴더에 에셋 하나) | `Moa.Unity.Data.SkillTuning` | `Moa.Tests.SkillTuningAssetTests` |
| 11 | `Assets/_Game/Data/Enemies/Grunt.asset` (새 적 = 이 폴더에 에셋 하나) | `Moa.Unity.Data.EnemyTuning` | `Moa.Tests.EnemyTuningAssetTests` |
| 12 | `Assets/_Game/Data/Characters/Party.asset` + 캐릭터 에셋 (새 캐릭터 = 이 폴더에 에셋 하나) | `Moa.Unity.Data.PartyTuning` · `CharacterData` | `Moa.Tests.PartyAssetTests` |
| 13 | `Assets/_Game/Data/Player/PlayerHealthTuning.asset` + 캐릭터 에셋 `maxHealth` · `defense` + 적 에셋 `attack` · `attackMultiplier` | `Moa.Unity.Data.PlayerHealthTuning` · `CharacterData` · `EnemyTuning` | `Moa.Tests.PlayerHealthAssetTests` |
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
| baseAttack | 40 | | 무기 기본 공격력. 캐릭터 기본 공격력(12번, 영웅 60)을 더해 100 — 피해는 2-1과 같음 (2026-11-26 100 → 40) |
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
| energyOnDefeat | 5 | | 2-4. 처치하면 궁극기 에너지 (궁극기 중 처치는 0) |

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

## 10. 스킬(E) · 궁극기(Q) · 에너지 (TASKS 2-4) — 정한 것 (Claude, 2026-11-25, 확인 필요 — GDD에는 "스킬(E) · 궁극기(Q, 공통 에너지)"만)
`Data/Skills/SwordHero.asset`(SkillTuning, 베기 한 줄 = WeaponTuning.Step)

| 항목 | 값 | 근거 · 메모 |
|---|---|---|
| E 길게 누르기 기준 | 0.50 s (25스텝) | 그 전에 떼면 짧게, 0.5초가 되는 순간 길게(떼기를 기다리지 않음). 누르는 동안 제자리 |
| E 짧게 (회전 베기) | 배율 1.70 · 선딜 0.12 · 판정 0.10 · 후딜 0.30 (합 0.52 s) · 내딛기 0.4 m · 경직 30 · 범위 3.0 m 360° · 쿨 5 s · 에너지 10 | 허수아비 136 |
| E 길게 (모아 두 번 베기) | 1.20 / 0.10 · 0.08 · 0.04 / 0.5 m / 경직 20 → 2.00 / 0.10 · 0.12 · 0.40 / 1.0 m / 40 (합 0.84 s) · 범위 3.5 m 180° · 쿨 8 s · 에너지 15 | 허수아비 96 + 160 |
| Q 궁극기 | 비용 40 (= 에너지 최대, 가득이어야) · 쿨 12 s · 쓰는 1.2 s 내내 무적 · 배율 4.00 · 선딜 0.60 · 판정 0.10 · 후딜 0.50 · 경직 60 · 범위 5.0 m 360° | 허수아비 320 |
| 에너지 | 스킬이 처음 맞힌 순간 한 번 · 허수아비 처치 +5 (`energyOnDefeat`) · 일반 · 강공격 · 낙하 0 · 궁극기 중 0 · 판 시작 0 | 원소 입자 오브젝트 없음(속성 없음 규칙) |

- 규칙: 쿨은 쓰는 순간부터, E를 누르는 중 끊기면 쿨 안 씀. E는 콤보 · 강공격을 끊고, Q는 콤보 · 강공격 · 스킬을 끊음. 낙하 중엔 둘 다 Busy. 회피 · 점프 · 공중이 되면 E가 끊김, Q 중 회피 · 점프 입력은 버림. 둘 다 땅 위에서만. Q 무적 중 적 공격은 Avoided(저스트 보상은 회피 무적일 때만)
- 입력 E · 패드 RB / Q · 패드 Y. HUD 오른쪽 아래 두 칸(E 쿨 덮개 · 남은 초, Q 원형 게이지 · 현재/40, 가득이면 금색). 상태 색 궁극기 금색 · 스킬 청록

## 11. 근접 졸병 Grunt (TASKS 2-5) — 정한 것 (Claude, 2026-11-26, 확인 필요 — 그로기 5초 · ×1.3만 GDD)
| 항목 | 값 | 근거 |
|---|---|---|
| maxHealth · defense · energyOnDefeat | 500 · 100 · 5 | 한손검 36 · 36 · 48 · 52 · 68(허수아비와 같은 방어), 두 콤보(480) + 1타에 쓰러짐 |
| maxPoise · groggy · regen | 60 · 5 s ×1.3 · 2 s 뒤 25/s | 콤보 한 번(54)으로는 안 되고 다음 1타(62)에 그로기 |
| detectRange · viewAngle · noticeRange | 10 m · 120° · 3 m | 앞 시야 또는 아주 가까이, 시야선 필요 |
| loseRange · loseSightTime · leashRange | 15 m · 2 s · 20 m | 넘으면 복귀, 집에 오면 체력 · 경직 가득 |
| patrol · chase · return speed · turn | 1.5 · 3.5 · 4.0 m/s · 360°/s | 추적 < 플레이어 뛰기 5.5 |
| patrolWait · arriveDistance | 2 s · 0.3 m | |
| attackRange · hitRange · hitAngle | 2.0 m · 2.6 m · 120° | 멈춘 자리에서 닿음, 회피 3.5m면 빠져나감 |
| attackInterval · telegraph · recovery | 0.6 · 0.8 · 0.8 s | 사거리에 들어와 1.4초에 첫 공격, 다음 공격 2.2초마다 |
| hitStunTime · respawnDelay | 0.3 s · 5 s | 예고 중에는 경직 없음 |
| capsule · eye · gravity · flash · knockback · number | 1.4 / 0.4 m · 1.1 m · 25 · 0.1 s · 0.15 m / 0.06 s · 1.7 m | |
- 표현 값(Setup 표): 몸 초록, 예고 중 빨강, 그로기 노랑 + 뒤로 14°, 앞 부채꼴 선 엷은 노랑 → 빨강 · 닿는 순간 흰색
- 이동은 NavMesh 없이 곧장(asmdef에 Unity.AI.Navigation 참조가 없고 구운 NavMesh도 없음) — 큰 벽은 돌아가지 못하고 미끄러짐

## 12. 파티 · 캐릭터 (TASKS 2-6) — 정한 것 (Claude, 2026-11-26, 확인 필요 — 교체 쿨 1초 · 대기 ×0.6은 원신 값)
| 항목 | 값 | 근거 |
|---|---|---|
| switchCooldown | 1.0 s (50스텝) | 원신, 교체한 스텝부터 |
| benchEnergyRatio | 0.6 (처치 5 → 대기 3) | 원신 4인 파티 대기 비율 |

| 칸 | 캐릭터 | 공격력 (캐릭터 + 무기) | E 배율 · 쿨 · 에너지 | Q 비용 · 쿨 · 배율 | 허수아비 E / Q 피해 | 몸 색 |
|---|---|---|---|---|---|---|
| 1 | 영웅 `SwordHero` | 60 + 40 = 100 | 1.7 · 5 s · 10 | 40 · 12 s · 4.0 | 136 / 320 | 주황 (1, .62, .22) |
| 2 | 수호자 `SwordGuardian` | 60 + 40 = 100 | 1.4 · 4 s · 10, 경직 40 | 60 · 15 s · 3.4, 경직 80 | 112 / 272 | 파랑 (.25, .5, .95) |
| 3 | 떠돌이 `SwordWanderer` | 70 + 40 = 110 | 2.0 · 6 s · 8 | 50 · 12 s · 4.6 | 176 / 405 | 짙은 회색 (.28, .28, .34) |
| 4 | 현자 `SwordSage` | 60 + 40 = 100 | 1.5 · 5 s · 14, 길게 에너지 20 | 40 · 15 s · 3.0 | 120 / 240 | 자홍 (.85, .25, .6) |
- 모두 한손검 `Sword.asset`. 영웅 콤보 36 · 36 · 48 · 52 · 68 그대로, 떠돌이 콤보 40 · 40 · 53 · 57 · 75. 에너지 최대 = 그 캐릭터의 궁극기 비용
- 공격력 = 캐릭터 기본 + 무기 기본(원신 방식, 따로 둔다). 비율 60 : 40 — Sword는 시작 무기라 캐릭터보다 낮게(원신 1~2성 무기 ≈ 캐릭터의 0.7배), 나중 4 · 5성 무기가 더 높아질 여지. 파티가 없으면 PlayerCombat의 character 칸(영웅)의 기본 공격력을 더한다 (결정 2026-11-26, 사용자 "모아 캐릭터 공격력도 따로 두자" · 비율은 정한 것, Claude, 확인 필요)
- 검산: `PartyTests` · `PartyAssetTests`

## 13. 플레이어 체력 · 쓰러짐 (TASKS 3-1) — 정한 것 (Claude, 2026-11-26, 확인 필요)
| 칸 | 캐릭터 | 최대 체력 | 방어 (감소율) | Grunt 한 대 | 한 대에 깎이는 % | 쓰러지기까지 |
|---|---|---|---|---|---|---|
| 1 | 영웅 `SwordHero` | 1000 | 100 (20%) | 100 | 10.0% | 10대 |
| 2 | 수호자 `SwordGuardian` | 1300 | 200 (33.3%) | 83 | 6.4% | 16대 |
| 3 | 떠돌이 `SwordWanderer` | 850 | 80 (16.7%) | 104 | 12.2% | 9대 |
| 4 | 현자 `SwordSage` | 900 | 100 (20%) | 100 | 11.1% | 9대 |
- Grunt `Grunt.asset`: attack 100 · attackMultiplier 1.25 → 125 × (1 − 방어 ÷ (방어 + 400)). 영웅 기준 한 대 10%, Grunt 하나(2.2초마다)면 가만히 22초. 수호자가 체력 · 방어 모두 가장 높고, 떠돌이는 공격력이 가장 높은 대신 가장 약하다. 파티 전원은 Grunt 한 마리에 약 44대(약 97초)
- 공격 허수아비 `AttackingDummy.asset`은 피해 없음(저스트 연습용)

| 항목 (`PlayerHealthTuning.asset`) | 값 | 근거 |
|---|---|---|
| hitStunTime · knockbackDistance | 0.30 s (15스텝) · 0.5 m | Grunt 경직과 같은 길이, 땅 위에서만, 회피로 끊김 |
| wipeFadeOutTime · wipeDarkTime · wipeFadeInTime | 1.0 · 0.6 · 0.8 s | 어두워짐이 끝나는 스텝에 시작 지점(Spawn)으로 옮기고 부활 · 적 제자리 |
| reviveHealthRatio | 0.5 | 전원 쓰러진 뒤 최대 체력의 절반 |
| healPointRadius · healPerSecond | 2.5 m · 0.5 (최대 체력 비율/초) | 바닥에서 가득까지 2초, 쓰러진 캐릭터도 부활 |
- 맞는 것은 필드 캐릭터뿐. 0이 되면 다음 칸부터 돌아 살아 있는 캐릭터로 자동 교체(쿨 무시, 새 쿨 없음), 쓰러진 칸으로는 교체 불가
- 표현 값(Setup 표): 플레이어 피해 숫자 빨강 (1, .32, .28), 피격 몸 검붉은 (.55, .08, .08), 전멸 몸 회색, 아래 가운데 막대 420×14px(30% 이하면 빨강), 파티 칸 막대 6px · 쓰러지면 회색, 회복 지점 청록 원판(회복 중 밝게)

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
- 스킬 · 궁극기: 짧게 26스텝 · 판정 5스텝 · 내딛기 0.4m, 길게 25스텝 뒤 · 42스텝 · 1.5m, 쿨 5초(250스텝) · 8 · 12, 에너지는 한 번에 10/15 · 짧게 4번이면 가득, Q는 가득이어야 · 60스텝 무적 · 회피로 안 끊김 · 궁극기 중 에너지 0, 누르는 중 회피면 쿨 안 씀 → `SkillBurstTests`
- 적 AI: 순찰 2초 뒤 1.5 m/s, 시야 120° · 10m · 뒤 3m, 사거리 2m에서 70스텝째 닿음 · 다음 110스텝, 예고 중 안 돌고 옆으로 피하면 빗나감(스침 판정), 시야 2초 · 15m · 집 20m 복귀, 복귀 4 m/s · 도착하면 회복, 경직 15스텝 · 예고 중 경직 없음, 그로기 중 정지, 쓰러지고 250스텝 뒤 다시 남 → `EnemyBrainTests` · `EnemyTuningAssetTests`
- 파티: 교체 쿨 50스텝 · 쿨 중 막힘(Cooldown) · 궁극기 · 낙하로 떨어지는 중 · 활공 중 막힘, 교체하면 콤보 · 강공격 · E 누르는 중 · 착지 경직이 끊김, 대기 캐릭터 스킬 · 궁극기 쿨이 돎, 처치 에너지 현재 5 · 대기 3 · 캐릭터 60 · 60 · 70 · 60 + 무기 40, 파티 없이도 100 → `PartyTests` · `PartyAssetTests`(CharacterAndWeaponAttack_AreSeparate_TotalsAsBefore · PlayerCombat_WithoutParty_AddsCharacterAttack_LikeWithParty)
- 플레이어 체력: Grunt 100/83/104/100 · 10/6.4/12.2/11.1% · 10/16/9/9대, 경직 15스텝 · 공중이면 없음, 대기 캐릭터 체력 그대로, 쓰러지면 다음 살아 있는 칸(한 바퀴) · 쿨 무시 · 쓰러진 칸 교체 Downed, 전멸 50스텝에 50% 부활 · 120스텝에 끝 · 연출 중 무피해, 회복 지점 한 스텝 10/13 · 2초 가득 · 부활 → `PlayerHealthTests` · `PlayerHealthAssetTests`
- 10번 값: 136 · 96+160 · 320, 시간은 0.02 배수, 60+30+16 ≥ 100 → `SkillTuningAssetTests`

## 실험 기록
| 날짜 | 무엇 | 결과 |
|---|---|---|
| 2026-11-24 | TestField 코드 주입 플레이 (1-1) | 뛰기 5.5 · 걷기 2.0 m/s, 점프 정점 +1.28m, 35° 오름 · 50° 막힘, 계단 3m 오름, 벽 앞 카메라 6 → 0.92m |
| 2026-11-25 | TestField 코드 주입 플레이 (1-2) | 질주 8.000 m/s · 소모 18.000/s, 10에서 0.56초에 바닥 → 지침 · 뛰기 5.5 m/s, 회복 지연 1.0초 · 회복 25.000/s, 60.5에서 지침 풀림 → 질주 다시, 바닥에서 가득 10.62초, 게이지 가득 뒤 0.9초에 숨김, 회피 3.500m · 15스텝(0.3초) · 무적 10스텝(0.2초) · 비용 20, 19에서 안 나감(NoStamina), 공중 안 나감(Airborne), 연타 2초에 4번 |
| 2026-11-25 | TestField 코드 주입 플레이 (1-3) | 기둥 붙기 · 오르기 1.500 m/s · 소모 10.0/s, 도약 +1.800m · 비용 25, 가만히 2초 스태미나 그대로, 꼭대기 올라서기 17스텝(0.35초) → 20.05m · 앞 0.8m 접지, 스태미나 5 → 0.5초에 바닥 · 떨어짐(지침 보라) · 60 차면 다시 붙음, 회피 → 떨어짐 · 0.50초 뒤 다시 붙음, 내려가다 땅 → Landed, 옆으로 가다 벽 끝 → WallEnded, 뒤 벽(4m) 올라서기, 35° · 50° 경사 안 붙음, 활공 수평 6.000 · 낙하 2.000 m/s · 5.0/s, 평지 점프(1.31m)에서 TooLow, 점프로 접기/다시 펴기, 착지로 풀림, 스태미나 1 → 0.2초에 접힘 · 지침 중 못 폄, 기둥 앞 공중에서 활공 → 벽에 붙음(Wall) |
| 2026-11-25 | TestField 코드 주입 플레이 (2-1) | 1 ~ 5타 이어짐(2타는 0.40초에 시작), 피해 36 · 36 · 48 · 52 · 68, 1 · 2타는 허수아비 둘을 한 번씩, 3 ~ 5타는 내딛은 뒤 한쪽만(다른 쪽은 부채꼴 밖), 0.5초 대기로 끊김, 점프 · 회피로 끊김, 등반 · 활공 · 공중 공격 막힘, 공격력 ×10(360)으로 두 대에 쓰러짐 → 2.98초 뒤 600으로 다시 섬, 공격 뒤 회피 3.500m |
| 2026-11-25 | TestField 코드 주입 플레이 (2-2) | 길게 누르기 → 0.38초(누른 스텝 + 18)에 강공격 · 240 → 220 · 45 · 58이 허수아비 둘에 · 0.84초, 스태미나 19면 NoStamina · 1타만, 3타 중 강공격 → 다음 1타, DummiesAir(10.02m) 낙하 20 m/s · 0.5초에 착지 · 9.97m 높은 낙하 128 둘(셋째 4.16m 범위 밖) · 경직 0.40초, 점프 정점 1.01m에서 낮은 낙하(1.03m) 102 둘 · 0.20m에서는 TooLow, 활공 중 낙하(활공 접힘), 착지 경직 회피 · 점프로 끊김, 등반 중 막힘, 강공격 중 회피 3.500m |
| 2026-11-25 | TestField 코드 주입 플레이 (2-3) | 2콤보 5타 4.46초에 그로기 · 그로기 중 1타 47 · 4.84초에 풀림, 저스트 8스텝 Just · 9 Avoided · 10 Hit, 슬로모션 실제 0.8초(게임 0.22초), 반격 경직 ×2로 1콤보 그로기, 낙하 경직 35 |
| 2026-11-25 | TestField 코드 주입 플레이 (2-4) | E 짧게 136 둘 · 에너지 +10 한 번 · 쿨 250스텝, E 길게 26스텝째 Hold · 96/160 · +15, 처치 +5, Q 320 셋 · 1.2초 무적 · 회피/점프 무시 · 공격 허수아비 Avoided, 쿨 중 Q 막힘, 궁극기 처치 에너지 0, 공중 E Airborne, 누르는 중 회피 쿨 안 씀 |
| 2026-11-26 | TestField 코드 주입 플레이 (2-5) | 순찰 1.500 m/s, 9.96m 감지 · 추적 3.500 m/s, 1.95m에서 공격 · 0.6초 뒤 예고 · 0.8초 뒤 닿음 Hit, 0.12초 전 옆 회피 Just, 36 · 36 · 48 · 52 · 68 · 다음 1타 그로기 · 그로기 중 47, 처치 에너지 +5 · 5초 뒤 다시 남, 엄폐 뒤 2.00초 LostSight 복귀 4.000 m/s · 400 → 500, 15m TooFar 복귀 |
| 2026-11-26 | TestField 코드 주입 플레이 (2-6) | 영웅 E 136 → 2번 교체(콤보 끊김 · 쿨 0.98) · 쿨 중 3번 막힘, 수호자 E 112 · 영웅 에너지 그대로 · 대기 E 쿨 4.98 → 4.02(48스텝), 떠돌이 콤보 40 · 40 · 53 · 57 · 75, 처치 에너지 현재 5 · 대기 3, 뛰는 중 교체 5.5 m/s 그대로, 현자 Q 240, 궁극기 · 활공 · 낙하로 떨어지는 중 막힘, 등반 · 회피 중 교체 |
| 2026-11-26 | TestField 코드 주입 플레이 (3-1) | Grunt 한 대 영웅 1000 → 900 · 경직 0.30초 · 0.5m 밀려남, 저스트 회피 무피해, 영웅 쓰러짐 → 수호자 자동 교체 · 수호자 83, 전원 쓰러짐 → 1.0초 뒤 Spawn에서 500/650/425/450 · 적 제자리 · 2.4초에 끝, 회복 지점 2초에 가득 · 쓰러진 캐릭터 부활, 무적 0 |
