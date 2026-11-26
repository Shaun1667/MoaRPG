using Moa.Core.World;
using Moa.Unity;
using Moa.Unity.World;
using UnityEditor;
using UnityEngine;

namespace Moa.Editor.Debugging
{
    /// <summary>탐험 (3-6): 수영 · 물 깊이 · 익사, 순간이동 지점 · 상자 · 퍼즐 상태, [상태 변경] 모두 열기 · 초기화 · 지점 모두 활성화 · 지점으로 순간이동 · 지도 열기.</summary>
    public sealed class ExplorationSection : IDebugSection
    {
        public string Title => "탐험 (수영 · 지점 · 상자)";

        public void Draw(GameRoot root)
        {
            if (root != null && root.DebugWorld == null)
            {
                EditorGUILayout.HelpBox("이 씬에는 탐험이 연결되지 않았습니다 (GameRoot의 Exploration 칸 · 성장 필요, 메뉴 MoaRPG > Setup > Test Field).", MessageType.None);
                return;
            }

            using (new EditorGUI.DisabledScope(root == null))
            {
                var swim = root != null ? root.DebugSwim : null;
                EditorGUILayout.LabelField("수영", swim != null
                    ? (swim.Active ? (swim.Sprinting ? "질주 수영" : "수영 중") : "아님") + " · 깊이 " + root.DebugWaterDepth.ToString("0.00") + "m · 끝난 까닭 " + swim.LastEnd
                      + " · 들어간 " + swim.Count + " · 익사 " + swim.DrownCount
                    : "-");
                EditorGUILayout.LabelField("저장 형식", root != null ? "지금 " + Moa.Core.Growth.SaveFormat.Version + (root.DebugSaveUpgradedFrom > 0 ? " · 시작할 때 " + root.DebugSaveUpgradedFrom + "에서 옮김" : "") : "-");

                WorldProgress w = root != null ? root.DebugWorld : null;
                Waypoint[] points = root != null ? root.DebugWaypoints : null;
                EditorGUILayout.LabelField("순간이동 지점", w != null ? "켜짐 " + Count(w, points) + " / " + (points != null ? points.Length : 0) + " · 이동 " + w.TeleportCount + " · 마지막 막힘 " + w.LastTeleportBlock : "-");
                if (points != null)
                    foreach (var p in points)
                    {
                        if (p == null) continue;
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.LabelField("   " + p.Id, p.Activated ? "켜짐" : "꺼짐");
                            if (GUILayout.Button("[상태 변경] 이 지점으로", GUILayout.Width(160))) root.TeleportToWaypoint(p.Id);
                        }
                    }

                Chest[] chests = root != null ? root.DebugChests : null;
                EditorGUILayout.LabelField("상자", w != null ? "마지막 막힘 " + w.LastChestBlock + " · 마지막 보상 골드 " + w.LastReward.Gold + " · 모험 경험 " + w.LastReward.ExpItems : "-");
                if (chests != null)
                    foreach (var c in chests)
                        if (c != null)
                            EditorGUILayout.LabelField("   " + c.Id, c.Tier + (string.IsNullOrEmpty(c.PuzzleId) ? "" : " · 퍼즐 " + c.PuzzleId) + " · " + (c.Opened ? "열림" : c.Block == ChestBlock.Locked ? "잠김" : "닫힘"));
                if (w != null)
                    foreach (var id in w.Puzzles)
                        EditorGUILayout.LabelField("퍼즐 " + id, (w.IsSolved(id) ? "풀림" : "안 풀림") + " · 압력판 " + w.LitCount(id) + " / " + w.PlateCount(id));

                EditorGUILayout.Space();
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("[상태 변경] 모두 열기")) root.DebugWorldOpenAll();
                    if (GUILayout.Button("[상태 변경] 지점 모두 활성화")) root.DebugWorldActivateAll();
                    if (GUILayout.Button("[상태 변경] 초기화")) root.DebugWorldReset();
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(root != null && root.DebugMapOpen ? "[보기] 지도 닫기" : "[보기] 지도 열기")) root.DebugSetMapOpen(!root.DebugMapOpen);
                    if (GUILayout.Button("[상태 변경] 연못으로")) root.DebugTeleportToPoint("PondWater");
                    if (GUILayout.Button("[상태 변경] 스태미나 바닥")) root.DebugStaminaSet(0f);
                }
                EditorGUILayout.HelpBox("M(패드 Menu)으로 지도 — 켜진 지점을 누르면 순간이동. 지점 · 상자 · 압력판은 다가가면 반응. 물에서는 스태미나가 줄고(질주 수영은 더) 바닥나면 익사 → 체력 일부를 잃고 물가로. 모두 열기는 보상도 준다, 초기화는 재화를 돌려주지 않는다.", MessageType.None);
            }
        }

        static int Count(WorldProgress w, Waypoint[] points)
        {
            int n = 0;
            if (points != null) foreach (var p in points) if (p != null && w.IsActivated(p.Id)) n++;
            return n;
        }
    }
}
