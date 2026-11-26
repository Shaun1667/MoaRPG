using Moa.Unity;
using UnityEditor;
using UnityEngine;

namespace Moa.Editor.Debugging
{
    /// <summary>
    /// 교체 연계 (3-2): 등장 공격 창 · 등장 공격 진행 · 칸마다 스킬 뒤 교체 창 · 지금 버프(준 · 받은 칸, 남은 시간, 피해 · 경직 · 보호막) · 배율,
    /// [상태 변경] 버프 주기(칸마다) · 버프 끝내기 · 에너지 채우기 · 등장 창 열기 · 쿨 없애기.
    /// </summary>
    public sealed class SwitchComboSection : IDebugSection
    {
        public string Title => "교체 연계";

        public void Draw(GameRoot root)
        {
            if (root != null && !root.DebugHasParty)
            {
                EditorGUILayout.HelpBox("이 씬에는 파티가 연결된 플레이어가 없습니다 (PlayerCombat의 Party 칸, 메뉴 MoaRPG > Setup > Test Field).", MessageType.None);
                return;
            }

            using (new EditorGUI.DisabledScope(root == null))
            {
                var party = root != null ? root.DebugParty : null;
                var entry = root != null ? root.DebugEntry : null;
                var buff = root != null ? root.DebugHandoffBuff : null;
                int count = root != null ? root.DebugPartyCount : 0;

                // 등장 공격
                if (entry != null && party != null)
                {
                    Rect r = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
                    float total = party.Rules.EntryWindow;
                    EditorGUI.ProgressBar(r, total > 0f ? entry.WindowLeft / total : 0f,
                        "등장 공격 창 " + entry.WindowLeft.ToString("0.00") + " / " + total.ToString("0.00") + "초" + (entry.WindowOpen ? " (열림)" : ""));
                    EditorGUILayout.LabelField("등장 공격", (entry.Active ? "베는 중 " + entry.Phase + " " + entry.Elapsed.ToString("0.00") + "초" : "대기")
                        + " · 나감 " + entry.Count + " · 창 " + entry.OpenCount + "번(놓침 " + entry.ExpiredCount + ") · 막힘 " + entry.LastBlock + " · 끝 " + entry.LastEnd);
                    EditorGUILayout.LabelField("등장 적중", "맞힘 " + root.DebugEntryHitCount + " · 그로기 연계 " + root.DebugEntryGroggyHitCount
                        + " (그로기 적이면 ×" + party.Rules.EntryGroggyBonus.ToString("0.##") + ")");
                }

                // 버프
                EditorGUILayout.Space();
                if (buff != null && buff.Active)
                {
                    Rect r = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
                    EditorGUI.ProgressBar(r, buff.Duration > 0f ? buff.TimeLeft / buff.Duration : 0f,
                        "버프 " + (buff.From + 1) + "번 → " + (buff.To + 1) + "번 · " + buff.TimeLeft.ToString("0.00") + " / " + buff.Duration.ToString("0.00") + "초");
                    EditorGUILayout.LabelField("효과", "피해 +" + (buff.Rules.DamageBonus * 100f).ToString("0.#") + "% · 경직 +" + (buff.Rules.PoiseBonus * 100f).ToString("0.#")
                        + "% · 보호막 " + buff.ShieldLeft + " / " + buff.Rules.Shield + " (막은 합 " + buff.AbsorbedTotal + ")");
                }
                else EditorGUILayout.LabelField("버프", buff != null ? "없음 · 마지막 끝 " + buff.LastEnd : "-");
                EditorGUILayout.LabelField("지금 배율", root != null
                    ? "피해 ×" + root.DebugDamageMultiplier.ToString("0.###") + " · 경직 ×" + root.DebugPoiseBuffMultiplier.ToString("0.###")
                      + " · 받은 수 " + (party != null ? party.HandoffCount : 0) + " · 마지막 에너지 +" + (party != null ? party.LastHandoffEnergy.ToString("0.#") : "-")
                    : "-");

                // 칸마다 스킬 뒤 교체 창 · 주는 버프
                float window = party != null ? party.Rules.HandoffWindow : 0f;
                for (int slot = 1; slot <= count; slot++)
                {
                    var h = root.DebugHandoffRules(slot);
                    float left = root.DebugHandoffLeft(slot);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        var cd = root.DebugPartyCharacter(slot);
                        EditorGUILayout.LabelField((slot == root.DebugPartyActiveSlot ? "▶ " : "   ") + slot + "번 " + (cd != null ? cd.name : "-"), GUILayout.Width(170f));
                        Rect r = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
                        EditorGUI.ProgressBar(r, window > 0f ? left / window : 0f, "E 뒤 교체 창 " + left.ToString("0.00") + "초");
                    }
                    EditorGUILayout.LabelField("      주는 버프", "피해 +" + (h.DamageBonus * 100f).ToString("0.#") + "% · 경직 +" + (h.PoiseBonus * 100f).ToString("0.#")
                        + "% · 보호막 " + h.Shield + " · 에너지 +" + h.Energy.ToString("0.#") + " · " + h.Duration.ToString("0.#") + "초");
                }

                EditorGUILayout.Space();
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (int slot = 1; slot <= 4; slot++)
                        if (GUILayout.Button("[상태 변경] " + slot + "번 버프 주기")) root.DebugHandoffGrant(slot);
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("[상태 변경] 버프 끝내기")) root.DebugHandoffClear();
                    if (GUILayout.Button("[상태 변경] 등장 창 열기")) root.DebugEntryOpen();
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("[상태 변경] 현재 에너지 가득")) root.DebugPartyEnergyFill(root.DebugPartyActiveSlot);
                    if (GUILayout.Button("[상태 변경] 모두 에너지 가득")) root.DebugPartyEnergyFill(0);
                    if (GUILayout.Button("[상태 변경] 모든 쿨 없애기")) root.DebugPartyResetAllCooldowns();
                }
                EditorGUILayout.HelpBox("교체하고 " + (party != null ? party.Rules.EntryWindow.ToString("0.0#") : "-") + "초 안에 공격 = 등장 돌진 베기. E를 쓰고 "
                    + (party != null ? window.ToString("0.0#") : "-") + "초 안에 교체하면 들어온 캐릭터가 그 캐릭터의 버프를 받는다(나가면 끝). 쓰러져 바뀐 것은 둘 다 없음.", MessageType.None);
            }
        }
    }
}
