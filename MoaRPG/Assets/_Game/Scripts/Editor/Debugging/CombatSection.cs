using Moa.Unity;
using Moa.Unity.Combat;
using UnityEditor;
using UnityEngine;

namespace Moa.Editor.Debugging
{
    /// <summary>전투 (2-1 · 2-2, 강공격 · 낙하 상태 · [상태 변경] 길게 누르기 · 허수아비 위로): 콤보 단계 · 구간 · 예약 · 끊김, 허수아비 체력, 마지막 피해, [상태 변경] 공격 주입 · 허수아비 다시 세우기 · 무적 · 공격력 배수.</summary>
    public sealed class CombatSection : IDebugSection
    {
        static readonly float[] AttackScales = { 1f, 2f, 5f, 10f };

        public string Title => "전투 — 일반 공격 · 강공격 · 낙하 · 허수아비";

        public void Draw(GameRoot root)
        {
            if (root != null && !root.DebugHasCombat)
            {
                EditorGUILayout.HelpBox("이 씬에는 전투가 연결된 플레이어가 없습니다 (Assets/_Game/Scenes/TestField.unity, 메뉴 MoaRPG > Setup > Test Field).", MessageType.None);
                return;
            }

            using (new EditorGUI.DisabledScope(root == null))
            {
                EditorGUILayout.LabelField("무기", root != null ? root.DebugWeaponName + " · 공격력 " + root.DebugAttack.ToString("0.#") + " (×" + root.DebugAttackScale.ToString("0.#") + ")" : "-");
                EditorGUILayout.LabelField("콤보", root != null ? Combo(root) : "-");
                EditorGUILayout.LabelField("콤보 기록", root != null
                    ? "휘두름 " + root.DebugComboSwingCount + " · 끊김 " + root.DebugComboBreakCount
                      + " · 마지막 끝 " + root.DebugComboLastEnd + " · 마지막 막힘 " + root.DebugComboLastBlock
                    : "-");
                EditorGUILayout.LabelField("마지막 피해", root != null ? LastHit(root) : "-");
                EditorGUILayout.LabelField("피해 숫자", root != null ? "떠 있음 " + root.DebugDamageNumbersActive + " · 마지막 \"" + (root.DebugDamageNumbersLastText ?? "-") + "\"" : "-");
                EditorGUILayout.LabelField("공격", root != null ? Mode(root) : "-");
                EditorGUILayout.LabelField("강공격", root != null ? Charged(root) : "-");
                EditorGUILayout.LabelField("낙하", root != null ? Plunge(root) : "-");

                TrainingDummy[] dummies = root != null ? root.DebugDummies : null;
                if (dummies != null)
                {
                    foreach (var d in dummies)
                    {
                        if (d == null || d.Health == null) continue;
                        Rect r = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
                        string label = d.name + " " + d.Id + "  " + d.Health.Current + " / " + d.Health.Max
                            + (d.Alive ? "" : " · 쓰러짐 (다시 서기까지 " + d.RespawnLeft.ToString("0.0") + "초)")
                            + " · 맞음 " + d.HitCount + " · 쓰러짐 " + d.DeathCount;
                        EditorGUI.ProgressBar(r, d.Health.Ratio, label);
                    }
                }

                EditorGUILayout.Space();
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("[상태 변경] 공격 입력")) root.DebugInputAttack();
                    if (GUILayout.Button("[상태 변경] 허수아비 다시 세우기")) root.DebugRespawnDummies();
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("[상태 변경] 강공격 입력 (길게 누르기)")) root.DebugInputAttackHold(root.DebugChargeHoldTime + 0.1f);
                    if (GUILayout.Button("[상태 변경] 허수아비 위 10m로")) root.DebugTeleportAboveDummies(10f);
                    if (GUILayout.Button("[상태 변경] 허수아비 위 3m로")) root.DebugTeleportAboveDummies(3f);
                }
                bool inv = root != null && root.DebugDummiesInvulnerable;
                bool next = EditorGUILayout.ToggleLeft("[상태 변경] 허수아비 무적 (숫자만 뜨고 체력은 그대로)", inv);
                if (root != null && next != inv) root.DebugSetDummiesInvulnerable(next);
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PrefixLabel("[상태 변경] 공격력 배수");
                    foreach (float s in AttackScales)
                        if (GUILayout.Button("×" + s.ToString("0"))) root.DebugSetAttackScale(s);
                }
                EditorGUILayout.HelpBox("조작: 커서가 잠겨 있을 때 왼쪽 클릭 · 패드 X = 일반 공격 (풀려 있을 때의 클릭은 커서 잠금). 길게 누르면 강공격(스태미나), 공중에서 누르면 낙하 공격. 공격 · 착지 경직 중 회피 · 점프로 끊김. 순간이동 지점 Dummies · DummiesAir는 플레이어 섹션.", MessageType.None);
            }
        }

        static string Combo(GameRoot root)
        {
            if (!root.DebugAttacking)
            {
                string wait = root.DebugComboResetLeft > 0f ? " · 끊기기까지 " + root.DebugComboResetLeft.ToString("0.00") + "초" : "";
                return "대기 · 다음 " + root.DebugComboNextStep + "타 / " + root.DebugComboStepCount + wait;
            }
            return root.DebugComboStep + "타 / " + root.DebugComboStepCount + " · " + root.DebugComboPhase + " " + root.DebugComboElapsed.ToString("0.00") + "초"
                + (root.DebugComboQueued ? " · 다음 예약" : "");
        }

        static string Mode(GameRoot root)
        {
            string hold = root.DebugAttackHolding
                ? " · 길게 누르는 중 " + root.DebugAttackHoldElapsed.ToString("0.00") + " / " + root.DebugChargeHoldTime.ToString("0.00") + "초"
                : "";
            return root.DebugAttackMode + hold + " · 마지막 막힘 " + root.DebugAttackLastBlock + " · Busy " + root.DebugAttackBusyCount + "번";
        }

        static string Charged(GameRoot root)
        {
            string now = root.DebugCharging
                ? root.DebugChargedSlash + "베기 / " + root.DebugChargedSlashCount + " · " + root.DebugChargedPhase + " " + root.DebugChargedElapsed.ToString("0.00") + "초"
                : "대기";
            return now + " · 나간 수 " + root.DebugChargedCount + " · 비용 " + root.DebugChargedStaminaCost.ToString("0")
                + " · 마지막 끝 " + root.DebugChargedLastEnd + " · 못 나간 까닭 " + root.DebugChargeLastBlock;
        }

        static string Plunge(GameRoot root)
        {
            string now = root.DebugPlungePhase == "Falling" ? "떨어지는 중 (시작 발 " + root.DebugPlungeStartY.ToString("0.00") + "m)"
                : root.DebugPlungePhase == "Landing" ? "착지 경직 " + root.DebugPlungeRecoveryLeft.ToString("0.00") + "초"
                : "대기 (높이 " + root.DebugPlayerHeightAboveGround.ToString("0.00") + " / " + root.DebugPlungeMinHeight.ToString("0.0") + "m)";
            string last = root.DebugPlungeLandCount > 0
                ? " · 마지막 " + (root.DebugPlungeHigh ? "높은" : "낮은") + " 낙하 " + root.DebugPlungeFallHeight.ToString("0.00") + "m ×" + root.DebugPlungeMultiplier.ToString("0.00")
                  + " · 맞힘 " + root.DebugPlungeLastLandHits
                : "";
            return now + " · 낙하 " + root.DebugPlungeCount + " · 착지 " + root.DebugPlungeLandCount + last
                + " · 마지막 막힘 " + root.DebugPlungeLastBlock + " · 마지막 끝 " + root.DebugPlungeLastEnd;
        }

        static string LastHit(GameRoot root)
        {
            if (root.DebugHitCount == 0) return "없음";
            HitReport h = root.DebugLastHit;
            return h.Damage + " → " + h.TargetName + " " + h.Target + " (" + h.Step + "타, 휘두름 " + h.Swing + ", 줄어든 체력 " + h.Applied + ") · 총 " + root.DebugHitCount + "번";
        }
    }
}
