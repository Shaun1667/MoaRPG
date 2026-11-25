using Moa.Unity;
using Moa.Unity.Combat;
using UnityEditor;
using UnityEngine;

namespace Moa.Editor.Debugging
{
    /// <summary>전투 (2-1): 콤보 단계 · 구간 · 예약 · 끊김, 허수아비 체력, 마지막 피해, [상태 변경] 공격 주입 · 허수아비 다시 세우기 · 무적 · 공격력 배수.</summary>
    public sealed class CombatSection : IDebugSection
    {
        static readonly float[] AttackScales = { 1f, 2f, 5f, 10f };

        public string Title => "전투 — 일반 공격 · 허수아비";

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
                bool inv = root != null && root.DebugDummiesInvulnerable;
                bool next = EditorGUILayout.ToggleLeft("[상태 변경] 허수아비 무적 (숫자만 뜨고 체력은 그대로)", inv);
                if (root != null && next != inv) root.DebugSetDummiesInvulnerable(next);
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PrefixLabel("[상태 변경] 공격력 배수");
                    foreach (float s in AttackScales)
                        if (GUILayout.Button("×" + s.ToString("0"))) root.DebugSetAttackScale(s);
                }
                EditorGUILayout.HelpBox("조작: 커서가 잠겨 있을 때 왼쪽 클릭 · 패드 X = 일반 공격 (풀려 있을 때의 클릭은 커서 잠금). 공격 중 회피로 끊김. 순간이동 지점 Dummies는 플레이어 섹션.", MessageType.None);
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

        static string LastHit(GameRoot root)
        {
            if (root.DebugHitCount == 0) return "없음";
            HitReport h = root.DebugLastHit;
            return h.Damage + " → " + h.TargetName + " " + h.Target + " (" + h.Step + "타, 휘두름 " + h.Swing + ", 줄어든 체력 " + h.Applied + ") · 총 " + root.DebugHitCount + "번";
        }
    }
}
