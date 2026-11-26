using Moa.Unity;
using UnityEditor;
using UnityEngine;

namespace Moa.Editor.Debugging
{
    /// <summary>
    /// MoaRPG 디버그 창 (메뉴 MoaRPG/Debug/Debug Window). 에디터 전용이라 빌드에 들어가지 않는다.
    /// 게임 쪽 진입점은 GameRoot.DebugXxx만 부른다. 새 섹션 = <see cref="Sections"/>에 한 줄.
    /// </summary>
    public sealed class DebugWindow : EditorWindow
    {
        public const string MenuPath = "MoaRPG/Debug/Debug Window";

        static readonly IDebugSection[] Sections =
        {
            new GeneralSection(),
            new PlayerSection(),
            new CameraSection(),
            new StaminaSection(),
            new ClimbGlideSection(),
            new CombatSection(),
            new PoiseSection(),
            new SkillSection(),
            new EnemySection(),
            new PartySection(),
            new PlayerHealthSection(),
            new SwitchComboSection(),
            new WeaponSection(),
            new GrowthSection(),
            new ExplorationSection(), // 3-6
        };

        bool[] _open;
        Vector2 _scroll;

        [MenuItem(MenuPath)]
        public static void Open()
        {
            GetWindow<DebugWindow>(false, "MoaRPG Debug", true).Show();
        }

        void OnInspectorUpdate()
        {
            if (Application.isPlaying) Repaint();
        }

        void OnGUI()
        {
            if (_open == null || _open.Length != Sections.Length)
            {
                _open = new bool[Sections.Length];
                for (int i = 0; i < _open.Length; i++) _open[i] = true;
            }

            GameRoot root = Application.isPlaying ? Object.FindFirstObjectByType<GameRoot>() : null;
            if (!Application.isPlaying)
                EditorGUILayout.HelpBox("플레이 모드에서 동작합니다 (Assets/_Game/Scenes/Main.unity · TestField.unity).", MessageType.Info);
            else if (root == null)
                EditorGUILayout.HelpBox("씬에 GameRoot가 없습니다.", MessageType.Warning);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            for (int i = 0; i < Sections.Length; i++)
            {
                _open[i] = EditorGUILayout.BeginFoldoutHeaderGroup(_open[i], Sections[i].Title);
                if (_open[i])
                {
                    EditorGUI.indentLevel++;
                    Sections[i].Draw(root);
                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.EndFoldoutHeaderGroup();
                EditorGUILayout.Space();
            }
            EditorGUILayout.EndScrollView();
        }
    }
}
