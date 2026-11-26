using System;
using Moa.Unity.Data;
using UnityEngine;

namespace Moa.Unity.Combat
{
    /// <summary>
    /// 그레이박스 무기 모델 고르기 (3-3): 현재 캐릭터 무기의 종류(WeaponTuning.Kind)에 맞는 모델 하나만 보인다 — 표 한 줄 = 종류 하나.
    /// 표에 없는 종류면 첫 줄. 활은 쏘는 동안 시위에 건 화살(nocked)을 숨긴다(날아가는 화살은 ProjectileShooter).
    /// 표현만 한다 — 규칙(PlayerCombat)은 이것을 모른다. 모델이 오면 이것을 뺀다.
    /// </summary>
    public sealed class WeaponGrayboxVisual : MonoBehaviour
    {
        [Serializable]
        public struct Model
        {
            public WeaponKind kind;
            [Tooltip("Root shown while the current weapon is this kind")]
            public GameObject root;
            [Tooltip("Optional: hidden while a projectile attack is in its hit window or recovery (the nocked arrow)")]
            public GameObject nocked;
        }

        [SerializeField] PlayerCombat combat;
        [SerializeField] Model[] models;

        WeaponTuning _shown;
        int _shownIndex = -1;

        /// <summary>지금 보이는 모델의 종류 (디버그 · 확인용).</summary>
        public WeaponKind ShownKind => _shownIndex >= 0 && _shownIndex < models.Length ? models[_shownIndex].kind : WeaponKind.Sword;
        public bool NockedShown => _shownIndex >= 0 && models[_shownIndex].nocked != null && models[_shownIndex].nocked.activeSelf;

        void LateUpdate()
        {
            if (combat == null || models == null || models.Length == 0) return;
            WeaponTuning w = combat.Weapon;
            if (w != _shown) Show(w);
            if (_shownIndex < 0) return;
            GameObject nocked = models[_shownIndex].nocked;
            if (nocked == null) return;
            // 쏜 뒤 후딜 동안은 시위가 빈다 (판정 구간이 지나면) — 조준 중 · 쉬는 중에는 걸려 있다
            var a = combat.Attacks;
            bool released = a != null && combat.CurrentDelivery == Moa.Core.Combat.HitDelivery.Projectile && !combat.Aiming
                && ((a.Combo.Attacking && a.Combo.Elapsed >= a.Combo.CurrentStep.Windup)
                    || (a.Charged.Active && a.Charged.SlashIndex >= 0 && a.Charged.Elapsed >= a.Charged.CurrentSlash.Windup)
                    || (a.Entry.Active && a.Entry.Elapsed >= a.Entry.CurrentSlash.Windup));
            if (nocked.activeSelf == released) nocked.SetActive(!released);
        }

        void Show(WeaponTuning w)
        {
            _shown = w;
            int pick = 0;
            if (w != null)
                for (int i = 0; i < models.Length; i++)
                    if (models[i].kind == w.Kind) { pick = i; break; }
            for (int i = 0; i < models.Length; i++)
                if (models[i].root != null) models[i].root.SetActive(i == pick);
            _shownIndex = pick;
        }
    }
}
