using Moa.Unity;
using UnityEditor;
using UnityEngine;

namespace Moa.Editor.Debugging
{
    /// <summary>스킬(E) · 궁극기(Q) · 에너지 (2-4): 누르기 · 형태 · 쿨타임 · 에너지 · 막힌 까닭, [상태 변경] 짧게 · 길게 · 궁극기 입력 · 에너지 채우기 · 쿨타임 없애기.</summary>
    public sealed class SkillSection : IDebugSection
    {
        public string Title => "스킬(E) · 궁극기(Q) · 에너지";

        public void Draw(GameRoot root)
        {
            if (root != null && !root.DebugHasSkills)
            {
                EditorGUILayout.HelpBox("이 씬에는 스킬이 연결된 플레이어가 없습니다 (PlayerCombat의 Skills 칸, 메뉴 MoaRPG > Setup > Test Field).", MessageType.None);
                return;
            }

            using (new EditorGUI.DisabledScope(root == null))
            {
                var s = root != null ? root.DebugSkill : null;
                var b = root != null ? root.DebugBurst : null;
                var e = root != null ? root.DebugEnergy : null;
                EditorGUILayout.LabelField("스킬 표", root != null ? root.DebugSkillsName : "-");
                EditorGUILayout.LabelField("스킬(E)", s != null
                    ? (s.Pressing ? "누르는 중 " + s.PressElapsed.ToString("0.00") + " / " + s.Rules.HoldTime.ToString("0.00") + "초"
                        : s.Casting ? s.Form + " " + (s.SlashIndex + 1) + "베기 " + s.Phase + " " + s.Elapsed.ToString("0.00") + "초"
                        : "대기")
                      + " · 쿨 " + s.CooldownLeft.ToString("0.00") + "초"
                    : "-");
                EditorGUILayout.LabelField("스킬 기록", s != null
                    ? "전체 " + s.Count + " (짧게 " + s.TapCount + " · 길게 " + s.HoldCount + ") · 마지막 " + s.Form + " · 끝 " + s.LastEnd + " · 막힘 " + s.LastBlock
                    : "-");
                EditorGUILayout.LabelField("궁극기(Q)", b != null
                    ? (b.Active ? (b.SlashIndex + 1) + "베기 " + b.Phase + " " + b.Elapsed.ToString("0.00") + "초" + (b.Invulnerable ? " · 무적" : "") : "대기")
                      + " · 쿨 " + b.CooldownLeft.ToString("0.00") + "초 · 쓴 수 " + b.Count + " · 끝 " + b.LastEnd + " · 막힘 " + b.LastBlock
                    : "-");
                if (e != null)
                {
                    Rect r = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
                    EditorGUI.ProgressBar(r, e.Ratio, "에너지 " + e.Current.ToString("0.#") + " / " + e.Max.ToString("0") + (e.Full ? " (가득)" : ""));
                }
                EditorGUILayout.LabelField("에너지 기록", root != null
                    ? "스킬 " + root.DebugEnergyFromSkill.ToString("0.#") + " · 처치 " + root.DebugEnergyFromDefeat.ToString("0.#") + " · 마지막 +" + root.DebugLastEnergyGain.ToString("0.#")
                    : "-");

                EditorGUILayout.Space();
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("[상태 변경] E 짧게")) root.DebugInputSkill();
                    if (GUILayout.Button("[상태 변경] E 길게")) root.DebugInputSkillHold(s != null ? s.Rules.HoldTime + 0.1f : 1f);
                    if (GUILayout.Button("[상태 변경] Q")) root.DebugInputBurst();
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("[상태 변경] 에너지 가득")) root.DebugEnergyFill();
                    if (GUILayout.Button("[상태 변경] 에너지 0")) root.DebugEnergySet(0f);
                    if (GUILayout.Button("[상태 변경] 쿨타임 없애기")) root.DebugResetSkillCooldowns();
                }
                EditorGUILayout.HelpBox("E: 짧게 누르면 회전 베기, 길게 누르면 모아 두 번 베기 — 처음 맞힌 순간 에너지가 찬다. 허수아비를 쓰러뜨려도 찬다. Q: 에너지가 가득이면 큰 범위 베기(쓰는 동안 무적).", MessageType.None);
            }
        }
    }
}
