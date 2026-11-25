using Moa.Unity;
using UnityEditor;
using UnityEngine;

namespace Moa.Editor.Debugging
{
    /// <summary>등반 · 활공 (1-3): 단계 · 끝난 까닭 · 막힌 까닭 · 벽 기울기 · 높이 · 횟수, [상태 변경] 떨어지기 · 활공 펴기/접기.</summary>
    public sealed class ClimbGlideSection : IDebugSection
    {
        public string Title => "등반 · 활공";

        public void Draw(GameRoot root)
        {
            if (root != null && !root.DebugHasClimb)
            {
                EditorGUILayout.HelpBox("이 씬에는 플레이어가 없습니다 (Assets/_Game/Scenes/TestField.unity).", MessageType.None);
                return;
            }

            using (new EditorGUI.DisabledScope(root == null))
            {
                EditorGUILayout.LabelField("등반", root != null ? Climb(root) : "-");
                EditorGUILayout.LabelField("등반 기록", root != null
                    ? "붙음 " + root.DebugClimbCount + " · 도약 " + root.DebugClimbLeapCount + " · 올라서기 " + root.DebugClimbMantleCount
                      + " · 마지막 끝 " + root.DebugClimbLastEnd + " · 마지막 막힘 " + root.DebugClimbLastBlock
                    : "-");
                EditorGUILayout.LabelField("활공", root != null
                    ? (root.DebugGliding ? "펼침" : "접힘") + " · 횟수 " + root.DebugGlideCount
                      + " · 마지막 끝 " + root.DebugGlideLastEnd + " · 마지막 막힘 " + root.DebugGlideLastBlock
                    : "-");
                EditorGUILayout.LabelField("땅까지 높이", root != null ? root.DebugPlayerHeightAboveGround.ToString("0.00") + " m" : "-");

                EditorGUILayout.Space();
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("[상태 변경] 등반 떨어지기")) root.DebugClimbDrop();
                    if (GUILayout.Button("[상태 변경] 활공 펴기")) root.DebugGlideOpen();
                    if (GUILayout.Button("[상태 변경] 활공 접기")) root.DebugGlideClose();
                }
                EditorGUILayout.HelpBox("조작: 벽 쪽으로 움직이면 붙음 · WASD로 벽 위 이동 · Space 위로 도약 · 회피로 떨어짐 / 공중에서 Space로 활공 펴기 · 접기 (GlideTuning 최소 높이 이상). 순간이동 지점 PillarTop · PillarAir는 플레이어 섹션.", MessageType.None);
            }
        }

        static string Climb(GameRoot root)
        {
            if (!root.DebugClimbing)
                return root.DebugClimbReattachWait > 0f ? "안 붙음 · 다시 붙기까지 " + root.DebugClimbReattachWait.ToString("0.00") + "초" : "안 붙음";
            return root.DebugClimbPhase + " · 벽 " + root.DebugClimbWallAngle.ToString("0") + "°";
        }
    }
}
