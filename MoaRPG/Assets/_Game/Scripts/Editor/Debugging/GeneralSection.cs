using Moa.Core.Localization;
using Moa.Unity;
using UnityEditor;
using UnityEngine;

namespace Moa.Editor.Debugging
{
    /// <summary>첫 섹션: 재시작 · 시드 표시 · 언어(보기) · 지금 상태.</summary>
    public sealed class GeneralSection : IDebugSection
    {
        int _newSeed;
        bool _seedInitialized;

        public string Title => "일반 — 재시작 · 시드 · 지금 상태";

        public void Draw(GameRoot root)
        {
            using (new EditorGUI.DisabledScope(root == null))
            {
                // 시드
                EditorGUILayout.LabelField("시드", root != null ? root.DebugSeed.ToString() : "-");
                if (root != null && !_seedInitialized) { _newSeed = root.DebugSeed; _seedInitialized = true; }
                if (root == null) _seedInitialized = false;

                // 재시작
                if (GUILayout.Button("[상태 변경] 재시작 (같은 시드)")) root.DebugRestart();
                using (new EditorGUILayout.HorizontalScope())
                {
                    _newSeed = EditorGUILayout.IntField("새 시드", _newSeed);
                    if (GUILayout.Button("[상태 변경] 이 시드로 재시작")) root.DebugRestart(_newSeed);
                }

                // 언어 (보기만 바뀜)
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PrefixLabel("[보기] 언어");
                    if (Text.Table != null)
                    {
                        foreach (var lang in Text.Table.Languages)
                        {
                            using (new EditorGUI.DisabledScope(lang == Text.Language))
                                if (GUILayout.Button(lang)) root.DebugSetLanguage(lang);
                        }
                    }
                }

                // 지금 상태
                EditorGUILayout.LabelField("지금 상태");
                EditorGUILayout.HelpBox(root != null ? root.DebugState() : "-", MessageType.None);
            }
        }
    }
}
