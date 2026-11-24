using Moa.Unity;
using UnityEditor;
using UnityEngine;

namespace Moa.Editor.Debugging
{
    /// <summary>카메라 (1-1): 거리 · 각도 · 벽 당김 표시, [보기] 각도 맞추기.</summary>
    public sealed class CameraSection : IDebugSection
    {
        float _yaw;
        float _pitch;

        public string Title => "카메라 — 거리 · 각도";

        public void Draw(GameRoot root)
        {
            if (root != null && !root.DebugHasCamera)
            {
                EditorGUILayout.HelpBox("이 씬에는 궤도 카메라가 없습니다.", MessageType.None);
                return;
            }

            using (new EditorGUI.DisabledScope(root == null))
            {
                EditorGUILayout.LabelField("거리", root != null
                    ? root.DebugCameraDistance.ToString("0.00") + " m (휠로 정한 거리 " + root.DebugCameraDesiredDistance.ToString("0.00") + ")" + (root.DebugCameraBlocked ? "  — 벽에 당겨짐" : "")
                    : "-");
                EditorGUILayout.LabelField("요 · 피치", root != null ? root.DebugCameraYaw.ToString("0.0") + "° · " + root.DebugCameraPitch.ToString("0.0") + "°" : "-");

                using (new EditorGUILayout.HorizontalScope())
                {
                    _yaw = EditorGUILayout.FloatField("요", _yaw);
                    _pitch = EditorGUILayout.FloatField("피치", _pitch);
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("지금 각도")) { _yaw = root.DebugCameraYaw; _pitch = root.DebugCameraPitch; }
                    if (GUILayout.Button("[보기] 이 각도로")) root.DebugSetCameraAngles(_yaw, _pitch);
                }
            }
        }
    }
}
