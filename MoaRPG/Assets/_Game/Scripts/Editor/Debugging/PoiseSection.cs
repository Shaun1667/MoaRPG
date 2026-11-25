using Moa.Unity;
using Moa.Unity.Combat;
using UnityEditor;
using UnityEngine;

namespace Moa.Editor.Debugging
{
    /// <summary>경직 · 그로기 · 저스트 회피 (2-3): 허수아비 경직 막대 · 그로기 남은 시간, 공격 허수아비 단계, 저스트 판정 · 슬로모션 · 반격 보너스, [상태 변경] 그로기 만들기 · 게이지 비우기 · 공격 멈춤.</summary>
    public sealed class PoiseSection : IDebugSection
    {
        public string Title => "경직 · 그로기 · 저스트 회피";

        public void Draw(GameRoot root)
        {
            if (root != null && !root.DebugHasCombat)
            {
                EditorGUILayout.HelpBox("이 씬에는 전투가 연결된 플레이어가 없습니다 (Assets/_Game/Scenes/TestField.unity, 메뉴 MoaRPG > Setup > Test Field).", MessageType.None);
                return;
            }

            using (new EditorGUI.DisabledScope(root == null))
            {
                TrainingDummy[] dummies = root != null ? root.DebugDummies : null;
                if (dummies != null)
                {
                    foreach (var d in dummies)
                    {
                        if (d == null || d.Poise == null) continue;
                        var p = d.Poise;
                        Rect r = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
                        string label = d.name + "  경직 " + p.Current.ToString("0.#") + " / " + p.Rules.Max.ToString("0")
                            + (p.Groggy ? " · 그로기 " + p.GroggyLeft.ToString("0.00") + "초 (받는 피해 ×" + p.DamageTakenMultiplier.ToString("0.00") + ")" : "")
                            + (!p.Groggy && p.Current > 0f ? " · 줄기까지 " + p.RegenWait.ToString("0.00") + "초" : "")
                            + " · 그로기 " + p.BreakCount + "번";
                        EditorGUI.ProgressBar(r, p.Groggy ? p.GroggyLeft / Mathf.Max(p.Rules.GroggyDuration, 1e-4f) : p.Ratio, label);
                    }
                }
                EditorGUILayout.LabelField("경직 막대", root != null ? "보임 " + root.DebugPoiseBarsVisible : "-");

                DummyAttacker[] attackers = root != null ? root.DebugAttackers : null;
                if (attackers != null)
                {
                    foreach (var a in attackers)
                    {
                        if (a == null || a.Cycle == null) continue;
                        var c = a.Cycle;
                        string now = c.Phase + " " + c.Elapsed.ToString("0.00") + "초"
                            + (c.Phase == Moa.Core.Combat.EnemyAttackPhase.Telegraph ? " · 닿기까지 " + c.TimeToHit.ToString("0.00") + "초" : "");
                        EditorGUILayout.LabelField(a.name, now + " · 공격 " + c.AttackCount + " · 취소 " + c.CancelCount + " · 빗나감 " + a.MissCount
                            + " · 마지막 " + (a.LastMissed ? "범위 밖" : a.LastJudgement.ToString()) + (a.Paused ? " · 멈춤" : ""));
                    }
                }

                EditorGUILayout.LabelField("저스트 회피", root != null
                    ? "마지막 " + root.DebugJustLast + " · 저스트 " + root.DebugJustCount + " · 피함 " + root.DebugAvoidCount + " · 맞음 " + root.DebugPlayerHitCount
                      + " · 창 " + root.DebugJustWindow.ToString("0.00") + "초"
                    : "-");
                EditorGUILayout.LabelField("보상", root != null
                    ? "슬로모션 " + root.DebugJustSlowLeft.ToString("0.00") + "초 (timeScale " + root.DebugTimeScale.ToString("0.00") + ") · 반격 " + root.DebugJustCounterLeft.ToString("0.00")
                      + "초 (경직 ×" + root.DebugPoiseMultiplier.ToString("0.0") + ")"
                    : "-");

                EditorGUILayout.Space();
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("[상태 변경] 허수아비 모두 그로기")) root.DebugBreakDummies();
                    if (GUILayout.Button("[상태 변경] 경직 게이지 비우기")) root.DebugResetPoise();
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    bool paused = root != null && root.DebugAttackersPaused;
                    if (GUILayout.Button(paused ? "[상태 변경] 공격 허수아비 다시" : "[상태 변경] 공격 허수아비 멈춤")) root.DebugSetAttackersPaused(!paused);
                    if (GUILayout.Button("[상태 변경] 공격 주기 처음으로")) root.DebugResetAttackers();
                    if (GUILayout.Button("[상태 변경] 슬로모션 · 보너스 끄기")) root.DebugCancelJustDodge();
                }
                EditorGUILayout.HelpBox("경직: 공격마다 쌓이고 가득 차면 그로기(행동 불가 · 받는 피해 증가), 맞지 않으면 잠시 뒤 줄어든다. 저스트 회피: 순간이동 지점 AttackDummy에서 바닥 원이 빨개져 닿기 직전에 회피(오른쪽 클릭) — 슬로모션 + 잠시 경직 보너스.", MessageType.None);
            }
        }
    }
}
