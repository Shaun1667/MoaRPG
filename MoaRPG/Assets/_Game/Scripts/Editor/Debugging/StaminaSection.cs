using Moa.Unity;
using UnityEditor;
using UnityEngine;

namespace Moa.Editor.Debugging
{
    /// <summary>스태미나 (1-2): 현재/최대 · 상태(지침 · 회복 지연 · 회복 중) · 질주 · 회피 · 무적, [상태 변경] 가득 채우기 · 바닥내기 · 무한 스태미나.</summary>
    public sealed class StaminaSection : IDebugSection
    {
        public string Title => "스태미나 — 질주 · 회피";

        public void Draw(GameRoot root)
        {
            if (root != null && !root.DebugHasStamina)
            {
                EditorGUILayout.HelpBox("이 씬에는 플레이어가 없습니다 (Assets/_Game/Scenes/TestField.unity).", MessageType.None);
                return;
            }

            using (new EditorGUI.DisabledScope(root == null))
            {
                float cur = root != null ? root.DebugStaminaCurrent : 0f;
                float max = root != null ? root.DebugStaminaMax : 0f;
                Rect r = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
                EditorGUI.ProgressBar(r, max > 0f ? cur / max : 0f, root != null ? cur.ToString("0.0") + " / " + max.ToString("0") : "-");

                EditorGUILayout.LabelField("상태", root != null ? State(root) : "-");
                EditorGUILayout.LabelField("움직임", root != null ? Motion(root) : "-");
                EditorGUILayout.LabelField("회피", root != null
                    ? "횟수 " + root.DebugDodgeCount + " · 쿨 " + root.DebugDodgeCooldown.ToString("0.00") + "초 · 마지막 막힘 " + root.DebugDodgeLastBlock
                    : "-");
                EditorGUILayout.LabelField("게이지", root != null ? "보임 " + root.DebugStaminaGaugeAlpha.ToString("0.00") : "-");

                EditorGUILayout.Space();
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("[상태 변경] 가득 채우기")) root.DebugStaminaFill();
                    if (GUILayout.Button("[상태 변경] 바닥내기")) root.DebugStaminaSet(0f);
                }
                bool inf = root != null && root.DebugStaminaInfinite;
                bool next = EditorGUILayout.ToggleLeft("[상태 변경] 무한 스태미나", inf);
                if (root != null && next != inf) root.DebugStaminaSetInfinite(next);
            }
        }

        static string State(GameRoot root)
        {
            if (root.DebugStaminaInfinite) return "무한";
            if (root.DebugStaminaExhausted) return "지침 (질주 불가)" + Regen(root);
            if (root.DebugStaminaCurrent >= root.DebugStaminaMax) return "가득";
            return "보통" + Regen(root);
        }

        static string Regen(GameRoot root) => root.DebugStaminaRegenerating
            ? " · 회복 중"
            : " · 회복까지 " + root.DebugStaminaRegenWait.ToString("0.00") + "초";

        static string Motion(GameRoot root)
        {
            if (root.DebugPlayerDodging) return root.DebugPlayerInvulnerable ? "회피 중 · 무적" : "회피 중";
            if (root.DebugPlayerSprinting) return "질주 중 (Shift · RT)";
            return "-";
        }
    }
}
