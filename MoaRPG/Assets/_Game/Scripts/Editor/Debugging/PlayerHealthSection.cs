using Moa.Unity;
using UnityEditor;
using UnityEngine;

namespace Moa.Editor.Debugging
{
    /// <summary>플레이어 체력 · 쓰러짐 (3-1): 캐릭터별 체력 · 방어 · 쓰러짐 · 경직 · 전멸 단계, [상태 변경] 피해 주기 · 쓰러뜨리기 · 전원 쓰러짐 · 회복 · 플레이어 무적.</summary>
    public sealed class PlayerHealthSection : IDebugSection
    {
        public string Title => "플레이어 체력 · 쓰러짐";

        public void Draw(GameRoot root)
        {
            if (root != null && !root.DebugHasPlayerHealth)
            {
                EditorGUILayout.HelpBox("이 씬에는 체력이 연결된 플레이어가 없습니다 (Player의 PlayerVitals · 캐릭터 최대 체력, 메뉴 MoaRPG > Setup > Test Field).", MessageType.None);
                return;
            }

            using (new EditorGUI.DisabledScope(root == null))
            {
                var v = root != null ? root.DebugPlayerHealth : null;
                int count = root != null ? root.DebugPlayerHealthCount : 0;
                for (int slot = 1; slot <= count; slot++)
                {
                    bool active = slot == root.DebugPlayerHealthActiveSlot;
                    bool downed = root.DebugPlayerHealthDowned(slot);
                    int cur = root.DebugPlayerHealthCurrent(slot), max = root.DebugPlayerHealthMax(slot);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField((active ? "▶ " : "   ") + slot + "번 " + Name(root, slot), GUILayout.Width(170f));
                        Rect r = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
                        EditorGUI.ProgressBar(r, max > 0 ? (float)cur / max : 0f,
                            cur + " / " + max + " · 방어 " + root.DebugPlayerHealthDefense(slot).ToString("0") + (downed ? " (쓰러짐)" : ""));
                    }
                }
                EditorGUILayout.LabelField("경직", v != null ? (root.DebugPlayerHealthStunned ? "경직 " + root.DebugPlayerHealthStunLeft.ToString("0.00") + "초" : "없음") : "-");
                EditorGUILayout.LabelField("전멸", v != null
                    ? root.DebugPlayerHealthWipe + " · 어둡기 " + root.DebugPlayerHealthDarkness.ToString("0.00") + " · 전멸 " + v.WipeCount + "번 · 부활 " + root.DebugPlayerHealthRespawnCount + "번"
                    : "-");
                var last = root != null ? root.DebugPlayerHealthLastHit : default;
                EditorGUILayout.LabelField("마지막 피해", v != null && !last.Ignored
                    ? last.Member + 1 + "번 " + last.Requested + " (줄어든 " + last.Applied + ")" + (last.Stunned ? " · 경직" : "") + (last.Downed ? " · 쓰러짐" : "")
                      + (last.SwitchedTo >= 0 ? " → " + (last.SwitchedTo + 1) + "번" : "") + (last.AllDown ? " · 전원 쓰러짐" : "")
                    : "-");
                EditorGUILayout.LabelField("기록", v != null
                    ? "맞음 " + v.HitCount + " · 쓰러짐 " + v.DownCount + " · 회복 합 " + v.HealedTotal + " · 회복 지점 부활 " + v.ReviveByHealCount
                    : "-");

                EditorGUILayout.Space();
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("[상태 변경] 피해 100")) root.DebugPlayerHealthDamage(100);
                    if (GUILayout.Button("[상태 변경] 피해 500")) root.DebugPlayerHealthDamage(500);
                    if (GUILayout.Button("[상태 변경] 쓰러뜨리기")) root.DebugPlayerHealthDownActive();
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("[상태 변경] 전원 쓰러짐")) root.DebugPlayerHealthDownAll();
                    if (GUILayout.Button("[상태 변경] 전원 회복")) root.DebugPlayerHealthRestoreAll();
                }
                bool inv = root != null && root.DebugPlayerHealthInvulnerable;
                bool next = EditorGUILayout.Toggle("[상태 변경] 플레이어 무적", inv);
                if (root != null && next != inv) root.DebugPlayerHealthSetInvulnerable(next);
                EditorGUILayout.HelpBox("맞는 것은 필드 캐릭터뿐. 0이 되면 살아 있는 다음 캐릭터로 자동 교체(쿨 무시), 쓰러진 캐릭터로는 못 바꾼다. 전원 쓰러지면 어두워졌다 시작 지점에서 절반 체력으로 되살아나고 적은 제자리로. 회복 지점(지점 HealPoint)에 서 있으면 전원 회복 · 부활.", MessageType.None);
            }
        }

        static string Name(GameRoot root, int slot)
        {
            var c = root.DebugPlayerHealthCharacter(slot);
            if (c == null) return "-";
            string key = c.NameKey;
            return (string.IsNullOrEmpty(key) ? c.name : Moa.Core.Localization.Text.Get(key)) + " (" + c.name + ")";
        }
    }
}
