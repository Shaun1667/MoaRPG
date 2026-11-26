using Moa.Core.Combat;
using Moa.Unity;
using Moa.Unity.Combat;
using UnityEditor;
using UnityEngine;

namespace Moa.Editor.Debugging
{
    /// <summary>적 AI (2-5): 적마다 상태 · 목표 · 거리 · 체력 · 경직 · 공격 주기, [상태 변경] 상태 바꾸기 · 쓰러뜨리기 · 다시 세우기 · 멈추기 · 무적 · 그로기.</summary>
    public sealed class EnemySection : IDebugSection
    {
        const string EnemiesPointName = "Enemies"; // 시험 지형 순간이동 지점 (Setup 배치 표)

        public string Title => "적 AI";

        public void Draw(GameRoot root)
        {
            if (root != null && root.DebugEnemyCount == 0)
            {
                EditorGUILayout.HelpBox("이 씬에는 적이 없습니다 (Assets/_Game/Scenes/TestField.unity, 메뉴 MoaRPG > Setup > Test Field).", MessageType.None);
                return;
            }

            using (new EditorGUI.DisabledScope(root == null))
            {
                EnemyAgent[] enemies = root != null ? root.DebugEnemies : null;
                if (enemies != null)
                {
                    for (int i = 0; i < enemies.Length; i++)
                    {
                        var e = enemies[i];
                        if (e == null || e.Brain == null) continue;
                        DrawEnemy(root, i, e);
                    }
                }

                EditorGUILayout.Space();
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("[상태 변경] 모두 다시 세우기")) root.DebugRespawnEnemies();
                    bool paused = root != null && root.DebugEnemiesPaused;
                    if (GUILayout.Button(paused ? "[상태 변경] 모두 다시 움직이기" : "[상태 변경] 모두 멈추기")) root.DebugSetEnemiesPaused(!paused);
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    bool inv = root != null && root.DebugEnemiesInvulnerable;
                    if (GUILayout.Button(inv ? "[상태 변경] 무적 끄기" : "[상태 변경] 무적")) root.DebugSetEnemiesInvulnerable(!inv);
                    if (GUILayout.Button("[상태 변경] 모두 그로기")) root.DebugBreakEnemies();
                    if (GUILayout.Button("[상태 변경] 지점 Enemies로")) root.DebugTeleportToPoint(EnemiesPointName);
                }
                EditorGUILayout.HelpBox("적: 대기 · 순찰 → 앞 시야(또는 아주 가까이)에서 플레이어를 보면 쫓고, 사거리 안에서 몸이 빨개지며 앞 부채꼴 예고 → 공격(저스트 회피 가능). 벽 뒤로 숨거나 멀어지면 집으로 돌아가 체력이 찬다. 맞으면 잠깐 멈춤(예고 중에는 안 멈춤), 그로기면 멈춤, 쓰러지면 잠시 뒤 집에서 다시 난다.", MessageType.None);
            }
        }

        static void DrawEnemy(GameRoot root, int index, EnemyAgent e)
        {
            EnemyBrain b = e.Brain;
            EditorGUILayout.LabelField(e.name, b.State + " " + b.StateTime.ToString("0.00") + "초"
                + (b.Paused ? " · 멈춤" : "")
                + (b.State == EnemyState.Dead ? " · 다시 나기까지 " + b.RespawnLeft.ToString("0.00") + "초" : ""));
            using (new EditorGUI.IndentLevelScope())
            {
                string target = !e.Bound ? "없음" : b.Engaged ? "플레이어 (쫓는 중)" : b.LastCanSee ? "플레이어 (보임)" : "플레이어 (못 봄)";
                EditorGUILayout.LabelField("목표", target
                    + " · 거리 " + (float.IsInfinity(b.LastDistance) ? "-" : b.LastDistance.ToString("0.00") + "m")
                    + " · 시야선 " + (b.LastLineOfSight ? "있음" : "막힘")
                    + (b.State == EnemyState.Chase ? " · 막힌 지 " + b.LostSightTime.ToString("0.00") + "초" : ""));
                EditorGUILayout.LabelField("집", "거리 " + b.LastHomeDistance.ToString("0.00") + "m (한계 " + b.Rules.LeashRange.ToString("0") + "m)"
                    + (b.HasPatrol ? " · 순찰 목적지 (" + b.PatrolTarget.X.ToString("0.0") + ", " + b.PatrolTarget.Z.ToString("0.0") + ")" : " · 순찰 없음")
                    + " · 마지막 복귀 까닭 " + b.LastReturnReason);
                var c = b.Cycle;
                EditorGUILayout.LabelField("공격", c.Phase + " " + c.Elapsed.ToString("0.00") + "초"
                    + (c.Phase == EnemyAttackPhase.Telegraph ? " · 닿기까지 " + c.TimeToHit.ToString("0.00") + "초" : "")
                    + " · 공격 " + b.AttackCount + " (맞힘 " + b.AttackHitCount + " · 빗나감 " + b.AttackMissCount + ")"
                    + (e.JudgedCount > 0 ? " · 마지막 판정 " + e.LastJudgement : ""));
                EditorGUILayout.LabelField("기록", "알아챔 " + b.DetectCount + " · 경직 " + b.StaggerCount + " · 예고 중 맞음 " + b.ArmorCount
                    + " · 복귀 " + b.ReturnCount + " · 쓰러짐 " + b.DeathCount + " · 다시 남 " + b.RespawnCount);
                if (e.Health != null)
                {
                    Rect r = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
                    EditorGUI.ProgressBar(r, e.Health.Ratio, "체력 " + e.Health.Current + " / " + e.Health.Max + (e.Health.Invulnerable ? " (무적)" : ""));
                }
                if (e.Poise != null)
                {
                    var p = e.Poise;
                    Rect r = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
                    EditorGUI.ProgressBar(r, p.Groggy ? p.GroggyLeft / Mathf.Max(p.Rules.GroggyDuration, 1e-4f) : p.Ratio,
                        "경직 " + p.Current.ToString("0.#") + " / " + p.Rules.Max.ToString("0") + (p.Groggy ? " · 그로기 " + p.GroggyLeft.ToString("0.00") + "초" : ""));
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("[상태 변경] 대기")) root.DebugSetEnemyState(index, EnemyState.Idle);
                    using (new EditorGUI.DisabledScope(!b.HasPatrol))
                        if (GUILayout.Button("[상태 변경] 순찰")) root.DebugSetEnemyState(index, EnemyState.Patrol);
                    if (GUILayout.Button("[상태 변경] 추적")) root.DebugSetEnemyState(index, EnemyState.Chase);
                    if (GUILayout.Button("[상태 변경] 복귀")) root.DebugSetEnemyState(index, EnemyState.Return);
                    if (GUILayout.Button("[상태 변경] 경직")) root.DebugSetEnemyState(index, EnemyState.Stagger);
                    if (GUILayout.Button("[상태 변경] 쓰러뜨리기")) root.DebugKillEnemy(index);
                    if (GUILayout.Button("[상태 변경] 다시 세우기")) e.Respawn();
                }
            }
        }
    }
}
