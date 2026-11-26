using Moa.Unity;
using Moa.Unity.Data;
using UnityEditor;
using UnityEngine;

namespace Moa.Editor.Debugging
{
    /// <summary>
    /// 무기 종류 (3-3): 현재 캐릭터 무기 · 종류 · 동작마다 판정 방식 · 투사체 수(날아가는 · 쏜 · 맞힌 · 풀) · 조준 · 회전,
    /// [상태 변경] 무기 바꿔 끼우기(Data/Weapons의 에셋마다 버튼, 테스트용 — 에셋은 그대로) · 원래 무기로.
    /// </summary>
    public sealed class WeaponSection : IDebugSection
    {
        const string WeaponFolder = "Assets/_Game/Data/Weapons";
        WeaponTuning[] _weapons;

        public string Title => "무기 종류";

        public void Draw(GameRoot root)
        {
            if (root != null && !root.DebugHasCombat)
            {
                EditorGUILayout.HelpBox("이 씬에는 PlayerCombat이 연결된 플레이어가 없습니다 (메뉴 MoaRPG > Setup > Test Field).", MessageType.None);
                return;
            }
            if (_weapons == null) _weapons = LoadWeapons();

            using (new EditorGUI.DisabledScope(root == null))
            {
                WeaponTuning w = root != null ? root.DebugWeapon : null;
                var c = root != null && root.DebugHasParty ? root.DebugPartyCharacter(root.DebugPartyActiveSlot) : null;
                EditorGUILayout.LabelField("현재 무기", w != null
                    ? w.name + " (" + w.Kind + ") · 공격력 " + w.BaseAttack.ToString("0.#") + (c != null && c.Weapon != w ? " · 바꿔 끼움 (데이터: " + (c.Weapon != null ? c.Weapon.name : "-") + ")" : "")
                    : "-");
                if (w != null)
                {
                    EditorGUILayout.LabelField("판정 방식", "콤보 " + w.ComboDelivery + " · 강공격 " + w.ChargedDelivery + " · 등장 " + w.EntryDelivery + " · 지금 " + root.DebugCurrentDelivery);
                    string style = w.ChargedAimTime > 0f ? "조준 " + w.ChargedAimTime.ToString("0.00") + "초"
                        : w.ChargedSpinDrainPerSecond > 0f ? "회전 " + w.ChargedSpinDrainPerSecond.ToString("0.#") + "/초"
                        : "베기 표";
                    EditorGUILayout.LabelField("강공격", style + " · 시작 비용 " + w.ChargedStaminaCost.ToString("0.#") + " · 자동 조준 " + (w.AutoAimRange > 0f ? w.AutoAimRange.ToString("0.#") + "m" : "공통"));
                    if (w.UsesProjectiles)
                        EditorGUILayout.LabelField("투사체 수치", w.ProjectileSpeed.ToString("0.#") + " m/s · " + w.ProjectileLifetime.ToString("0.00") + "초 (" + (w.ProjectileSpeed * w.ProjectileLifetime).ToString("0.#") + "m) · 반지름 " + w.ProjectileRadius.ToString("0.00"));
                }

                var shooter = root != null ? root.DebugProjectileShooter : null;
                EditorGUILayout.LabelField("투사체", root != null
                    ? "날아가는 " + root.DebugProjectileCount + " · 쏜 " + root.DebugProjectileFired + " · 맞힘 " + root.DebugProjectileHits
                      + (shooter != null ? " · 풀 " + shooter.PooledVisuals + " · 막힘 " + shooter.Set.BlockedCount + " · 사라짐 " + shooter.Set.ExpiredCount + " · 마지막 " + shooter.LastImpactName : " · (ProjectileShooter 없음)")
                    : "-");
                if (root != null && root.DebugHasCombat)
                {
                    EditorGUILayout.LabelField("조준", (root.DebugAiming ? "조준 중 " : "아님 ") + root.DebugAimElapsed.ToString("0.00") + "초");
                    EditorGUILayout.LabelField("회전", (root.DebugSpinning ? "회전 중 · " : "아님 · ") + "이번 " + root.DebugSpinsThisCharge + "바퀴 · 쓴 스태미나 " + root.DebugSpinStaminaSpent.ToString("0.#"));
                }

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("[상태 변경] 무기 바꿔 끼우기 (현재 캐릭터, 테스트용)");
                using (new EditorGUILayout.HorizontalScope())
                {
                    foreach (var weapon in _weapons)
                        if (weapon != null && GUILayout.Button("[상태 변경] " + weapon.name)) root.DebugSetWeapon(weapon);
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("[상태 변경] 원래 무기로")) root.DebugResetWeapon();
                    if (GUILayout.Button("무기 목록 다시 읽기")) _weapons = LoadWeapons();
                }
                EditorGUILayout.HelpBox("활: 클릭 = 화살 연사, 길게 누르기 = 조준(카메라 쪽), 다 조준한 뒤 떼면 강한 한 발. 대검: 길게 누르기 = 회전 베기(스태미나를 계속 씀), 떼거나 바닥나면 마무리.", MessageType.None);
            }
        }

        static WeaponTuning[] LoadWeapons()
        {
            string[] guids = AssetDatabase.FindAssets("t:WeaponTuning", new[] { WeaponFolder });
            var list = new WeaponTuning[guids.Length];
            for (int i = 0; i < guids.Length; i++) list[i] = AssetDatabase.LoadAssetAtPath<WeaponTuning>(AssetDatabase.GUIDToAssetPath(guids[i]));
            return list;
        }
    }
}
