using Moa.Unity;
using UnityEditor;
using UnityEngine;

namespace Moa.Editor.Debugging
{
    /// <summary>플레이어 (1-1): 위치 · 속도 · 접지 · 걷기/뛰기, [상태 변경] 순간이동, 입력 주입 해제.</summary>
    public sealed class PlayerSection : IDebugSection
    {
        Vector3 _target;

        public string Title => "플레이어 — 위치 · 속도 · 접지 · 순간이동";

        public void Draw(GameRoot root)
        {
            if (root != null && !root.DebugHasPlayer)
            {
                EditorGUILayout.HelpBox("이 씬에는 플레이어가 없습니다 (Assets/_Game/Scenes/TestField.unity).", MessageType.None);
                return;
            }

            using (new EditorGUI.DisabledScope(root == null))
            {
                Vector3 pos = root != null ? root.DebugPlayerPosition : Vector3.zero;
                Vector3 vel = root != null ? root.DebugPlayerVelocity : Vector3.zero;
                float horizontal = new Vector2(vel.x, vel.z).magnitude;
                EditorGUILayout.LabelField("위치", root != null ? Format(pos) : "-");
                EditorGUILayout.LabelField("속도", root != null ? Format(vel) + "  수평 " + horizontal.ToString("0.00") + " m/s" : "-");
                EditorGUILayout.LabelField("접지", root != null ? (root.DebugPlayerGrounded ? "땅 위" : (root.DebugPlayerOnSteepSlope ? "가파른 면 (미끄러짐)" : "공중")) : "-");
                EditorGUILayout.LabelField("걸음", root != null ? (root.DebugPlayerWalking ? "걷기" : "뛰기") + "  (왼쪽 Ctrl로 전환)" : "-");
                EditorGUILayout.LabelField("방향(요)", root != null ? root.DebugPlayerYaw.ToString("0.0") + "°" : "-");

                EditorGUILayout.Space();
                using (new EditorGUILayout.HorizontalScope())
                {
                    _target = EditorGUILayout.Vector3Field("목표 위치", _target);
                    if (GUILayout.Button("지금 위치", GUILayout.Width(70))) _target = pos;
                }
                if (GUILayout.Button("[상태 변경] 순간이동")) root.DebugTeleportPlayer(_target);

                var points = root != null ? root.DebugTeleportPoints : null;
                if (points != null && points.Length > 0)
                {
                    EditorGUILayout.LabelField("[상태 변경] 지점으로 순간이동");
                    int perRow = 3;
                    for (int i = 0; i < points.Length; i += perRow)
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            for (int j = i; j < Mathf.Min(i + perRow, points.Length); j++)
                            {
                                if (points[j] == null) continue;
                                if (GUILayout.Button(points[j].name)) root.DebugTeleportPlayer(points[j].position);
                            }
                        }
                    }
                }

                if (root != null && root.DebugInputOverriding)
                {
                    EditorGUILayout.HelpBox("입력 주입 중 — 키보드 · 마우스 입력이 무시됩니다.", MessageType.Warning);
                    if (GUILayout.Button("[상태 변경] 입력 주입 해제")) root.DebugInputClear();
                }
            }
        }

        static string Format(Vector3 v) => "(" + v.x.ToString("0.00") + ", " + v.y.ToString("0.00") + ", " + v.z.ToString("0.00") + ")";
    }
}
