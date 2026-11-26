using Moa.Unity;
using UnityEditor;
using UnityEngine;

namespace Moa.Editor.Debugging
{
    /// <summary>파티 4인 교체 (2-6): 파티 · 현재 · 교체 쿨 · 막힌 까닭 · 캐릭터별 에너지 · 스킬/궁극기 쿨, [상태 변경] 교체 · 에너지 채우기 · 쿨 없애기.</summary>
    public sealed class PartySection : IDebugSection
    {
        public string Title => "파티 4인 교체";

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
                int count = root != null ? root.DebugPartyCount : 0;
                EditorGUILayout.LabelField("현재", party != null
                    ? root.DebugPartyActiveSlot + "번 " + Name(root, root.DebugPartyActiveSlot) + " · 몸 색 " + root.DebugPartyBodyCharacter
                    : "-");
                EditorGUILayout.LabelField("공격력", root != null
                    ? "캐릭터 " + root.DebugCharacterAttack.ToString("0.#") + " + 무기 " + root.DebugWeaponAttack.ToString("0.#") + " = "
                      + (root.DebugCharacterAttack + root.DebugWeaponAttack).ToString("0.#") + " (×" + root.DebugAttackScale.ToString("0.##") + " → " + root.DebugAttack.ToString("0.#") + ")"
                    : "-");
                EditorGUILayout.LabelField("교체 쿨", party != null
                    ? party.CooldownLeft.ToString("0.00") + " / " + party.Rules.SwitchCooldown.ToString("0.00") + "초 · 교체 " + party.SwitchCount
                      + "번 · 막힘 " + party.LastBlock + " · 끊은 공격 " + party.LastInterrupted
                    : "-");
                EditorGUILayout.LabelField("처치 에너지(대기)", root != null
                    ? "비율 ×" + (party != null ? party.Rules.BenchEnergyRatio.ToString("0.##") : "-") + " · 합 " + root.DebugPartyBenchEnergyFromDefeat.ToString("0.#")
                      + " · 마지막 +" + root.DebugPartyLastBenchEnergyGain.ToString("0.#")
                    : "-");

                for (int slot = 1; slot <= count; slot++)
                {
                    var m = root.DebugPartyMember(slot);
                    if (m == null) continue;
                    bool active = slot == root.DebugPartyActiveSlot;
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField((active ? "▶ " : "   ") + slot + "번 " + Name(root, slot), GUILayout.Width(170f));
                        Rect r = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
                        EditorGUI.ProgressBar(r, m.Energy.Ratio, "에너지 " + m.Energy.Current.ToString("0.#") + " / " + m.Energy.Max.ToString("0") + (m.Energy.Full ? " (가득)" : ""));
                    }
                    var cd = root.DebugPartyCharacter(slot);
                    if (cd != null)
                        EditorGUILayout.LabelField("      공격력", "캐릭터 " + cd.BaseAttack.ToString("0.#") + " + 무기 " + (cd.Weapon != null ? cd.Weapon.BaseAttack.ToString("0.#") + " (" + cd.Weapon.name + ")" : "-")
                            + " = " + cd.TotalAttack.ToString("0.#"));
                    EditorGUILayout.LabelField("      쿨", "E " + m.Skill.CooldownLeft.ToString("0.00") + "초 · Q " + m.Burst.CooldownLeft.ToString("0.00")
                        + "초 · 지금 " + m.Mode + " · 콤보 끝 " + m.Combo.LastEnd);
                }

                EditorGUILayout.Space();
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (int slot = 1; slot <= 4; slot++)
                        if (GUILayout.Button("[상태 변경] " + slot + "번으로")) root.DebugPartySwitch(slot);
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("[상태 변경] 현재 에너지 가득")) root.DebugPartyEnergyFill(root.DebugPartyActiveSlot);
                    if (GUILayout.Button("[상태 변경] 모두 에너지 가득")) root.DebugPartyEnergyFill(0);
                    if (GUILayout.Button("[상태 변경] 모두 에너지 0")) root.DebugPartyEnergySet(0, 0f);
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("[상태 변경] 교체 쿨 없애기")) root.DebugPartyResetCooldown();
                    if (GUILayout.Button("[상태 변경] 모든 쿨 없애기")) root.DebugPartyResetAllCooldowns();
                }
                EditorGUILayout.HelpBox("1 ~ 4 키(패드 십자)로 교체, 교체 쿨 1초. 공격 · 스킬 중이면 끊고 바꾼다. 궁극기 · 낙하(떨어지는 중) · 활공 중에는 못 바꾼다. 대기 중에도 스킬 · 궁극기 쿨이 돈다.", MessageType.None);
            }
        }

        static string Name(GameRoot root, int slot)
        {
            var c = root.DebugPartyCharacter(slot);
            if (c == null) return "-";
            string key = c.NameKey;
            return (string.IsNullOrEmpty(key) ? c.name : Moa.Core.Localization.Text.Get(key)) + " (" + c.name + ")";
        }
    }
}
