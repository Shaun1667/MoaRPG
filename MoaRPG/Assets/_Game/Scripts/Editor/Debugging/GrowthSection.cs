using Moa.Core.Growth;
using Moa.Unity;
using UnityEditor;
using UnityEngine;

namespace Moa.Editor.Debugging
{
    /// <summary>성장 (3-5): 재화 · 칸마다 레벨 · 경험치 · 돌파 · E/Q · 무기 레벨 · 배율 · 저장 파일, [상태 변경] 재화 +N · 레벨 지정 · 동작 · 초기화 · 화면 열기.</summary>
    public sealed class GrowthSection : IDebugSection
    {
        public string Title => "성장 · 저장";

        int _amount = 10000;
        int _level = 20;
        int _skill = 1, _burst = 1, _weapon = 1;
        int _slot = 1;

        public void Draw(GameRoot root)
        {
            if (root != null && !root.DebugHasGrowth)
            {
                EditorGUILayout.HelpBox("이 씬에는 성장이 연결되지 않았습니다 (GameRoot의 Growth 칸, 메뉴 MoaRPG > Setup > Test Field).", MessageType.None);
                return;
            }

            using (new EditorGUI.DisabledScope(root == null))
            {
                Progression g = root != null ? root.DebugGrowth : null;
                EditorGUILayout.LabelField("재화", g != null
                    ? "골드 " + g.Wallet.Get(Currency.Gold) + " · 모험 경험 " + g.Wallet.Get(Currency.AdventureExp) + " · 마지막 처치 +" + g.LastRewardGold + " / +" + g.LastRewardExp
                    : "-");
                EditorGUILayout.LabelField("저장", root != null
                    ? root.DebugGrowthLoadStatus + " · 저장 " + root.DebugGrowthSaveCount + "번" + (root.DebugGrowthLastError != null ? " · 오류 " + root.DebugGrowthLastError : "")
                    : "-");
                EditorGUILayout.SelectableLabel(root != null ? root.DebugGrowthSavePath : "-", GUILayout.Height(EditorGUIUtility.singleLineHeight));
                if (root != null && root.DebugGrowthBackupPath != null) EditorGUILayout.LabelField("깨진 파일 백업", root.DebugGrowthBackupPath);
                EditorGUILayout.LabelField("마지막 막힘", g != null ? g.LastBlock.ToString() : "-");

                if (g != null)
                    for (int i = 0; i < g.Count; i++)
                    {
                        MemberGrowth m = g.Member(i);
                        MemberStats s = g.Stats(i);
                        EditorGUILayout.LabelField((i + 1) + "번 " + m.Id, "Lv " + m.Level + "/" + g.LevelCap(i) + " · 경험 " + m.Exp + "/" + g.Rules.Character.ExpToNext(m.Level)
                            + " · 돌파 " + m.Ascension + " · E " + m.SkillLevel + " · Q " + m.BurstLevel + " · 무기 " + m.WeaponLevel + "/" + g.WeaponCap + " (경험 " + m.WeaponExp + ")");
                        EditorGUILayout.LabelField("      배율", "캐릭터 ×" + s.Character.ToString("0.###") + " · 무기 ×" + s.Weapon.ToString("0.###") + " · E ×" + s.Skill.ToString("0.###")
                            + " · Q ×" + s.Burst.ToString("0.###") + " · 체력 " + root.DebugPlayerHealthMax(i + 1) + " · 방어 " + root.DebugPlayerHealthDefense(i + 1).ToString("0.#"));
                    }

                EditorGUILayout.Space();
                using (new EditorGUILayout.HorizontalScope())
                {
                    _amount = EditorGUILayout.IntField("N", _amount);
                    if (GUILayout.Button("[상태 변경] 골드 +N")) root.DebugGrowthAddCurrency(Currency.Gold, _amount);
                    if (GUILayout.Button("[상태 변경] 모험 경험 +N")) root.DebugGrowthAddCurrency(Currency.AdventureExp, _amount);
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    _slot = EditorGUILayout.IntSlider("칸 (0 = 모두)", _slot, 0, 4);
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    _level = EditorGUILayout.IntField("레벨", _level);
                    if (GUILayout.Button("[상태 변경] 레벨 지정")) root.DebugGrowthSetLevel(_slot, _level);
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    _skill = EditorGUILayout.IntField("E", _skill);
                    _burst = EditorGUILayout.IntField("Q", _burst);
                    _weapon = EditorGUILayout.IntField("무기", _weapon);
                    if (GUILayout.Button("[상태 변경] E · Q · 무기 지정")) root.DebugGrowthSetLevels(_slot, _skill, _burst, _weapon);
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    int slot = Mathf.Max(1, _slot);
                    if (GUILayout.Button("[상태 변경] 레벨업")) root.DebugGrowthTry(slot, GrowthAction.LevelUp);
                    if (GUILayout.Button("[상태 변경] 돌파")) root.DebugGrowthTry(slot, GrowthAction.Ascend);
                    if (GUILayout.Button("[상태 변경] E 올리기")) root.DebugGrowthTry(slot, GrowthAction.SkillUp);
                    if (GUILayout.Button("[상태 변경] Q 올리기")) root.DebugGrowthTry(slot, GrowthAction.BurstUp);
                    if (GUILayout.Button("[상태 변경] 무기 올리기")) root.DebugGrowthTry(slot, GrowthAction.WeaponUp);
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(root != null && root.DebugGrowthScreenOpen ? "[보기] 캐릭터 화면 닫기" : "[보기] 캐릭터 화면 열기")) root.DebugSetGrowthScreenOpen(!root.DebugGrowthScreenOpen);
                    if (GUILayout.Button("[상태 변경] 지금 저장")) root.DebugGrowthSaveNow();
                    if (GUILayout.Button("[상태 변경] 초기화 (새 계정)")) root.DebugGrowthReset();
                }
                EditorGUILayout.HelpBox("C(패드 View)로 캐릭터 화면. 적을 처치하면 골드 · 모험 경험(적 에셋), 레벨업 · 무기는 모험 경험, 돌파 · E · Q는 골드. 20 · 40에서 돌파. 바뀔 때마다 저장 파일(위 경로)에 쓴다 — 초기화는 파일도 새 계정으로.", MessageType.None);
            }
        }
    }
}
