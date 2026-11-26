using System;
using System.Collections.Generic;
using Moa.Core.Gacha;
using Moa.Core.Growth;
using UnityEngine;

namespace Moa.Unity.Data
{
    /// <summary>
    /// 가챠 공통 수치 (3-7): 배너 표(한 줄 = 배너 하나) · 각성 · 재련 최대 · 넘치면 등급마다 재화 · 여러 번 뽑기 수 · 처음부터 가진 것.
    /// 에셋 Assets/_Game/Data/Banners/Gacha.asset, 값은 BALANCE.md 19번. 규칙은 Core GachaAccount. 실제 결제 없음 — 소원석은 상자 · 처치 · 디버그로만.
    /// </summary>
    [CreateAssetMenu(menuName = "MoaRPG/Data/Gacha Tuning", fileName = "Gacha")]
    public sealed class GachaTuning : ScriptableObject
    {
        [Serializable]
        public sealed class OverflowRow
        {
            public int stars;
            public Currency currency;
            public int amount;
        }

        [Tooltip("Banners shown on the gacha screen, in order")]
        [SerializeField] BannerData[] banners = new BannerData[0];
        [Tooltip("Character duplicates raise awakening up to this")]
        [SerializeField] int maxAwakening;
        [Tooltip("Weapon duplicates raise refinement up to this")]
        [SerializeField] int maxRefinement;
        [Tooltip("Pulls of the multi pull button")]
        [SerializeField] int multiPull;
        [Tooltip("Past max awakening / refinement a duplicate turns into this (one row per rarity)")]
        [SerializeField] OverflowRow[] overflow = new OverflowRow[0];
        [Tooltip("A new account owns these (the starting party and their weapons)")]
        [SerializeField] GachaItemData[] startingItems = new GachaItemData[0];

        public int BannerCount => banners != null ? banners.Length : 0;
        public BannerData GetBanner(int i) => banners != null && i >= 0 && i < banners.Length ? banners[i] : null;
        public int MaxAwakening => maxAwakening;
        public int MaxRefinement => maxRefinement;
        public int MultiPull => multiPull;
        public int OverflowRowCount => overflow != null ? overflow.Length : 0;
        public OverflowRow GetOverflowRow(int i) => overflow != null && i >= 0 && i < overflow.Length ? overflow[i] : null;
        public IReadOnlyList<GachaItemData> StartingItems => startingItems ?? new GachaItemData[0];

        public CollectionRules ToRules()
        {
            var rows = new List<(int, Currency, int)>();
            if (overflow != null) foreach (var o in overflow) if (o != null) rows.Add((o.stars, o.currency, o.amount));
            var start = new List<GachaItem>();
            foreach (var s in StartingItems) if (s != null) start.Add(s.ToItem());
            return new CollectionRules(maxAwakening, maxRefinement, multiPull, rows.ToArray(), start.ToArray());
        }

        /// <summary>배너들 · 시작 항목에서 id로 항목 에셋 찾기 (이름 · 무기 연결, 없으면 null).</summary>
        public GachaItemData FindItem(string id)
        {
            for (int i = 0; i < BannerCount; i++)
            {
                var b = GetBanner(i);
                var it = b != null ? b.Find(id) : null;
                if (it != null) return it;
            }
            foreach (var s in StartingItems) if (s != null && s.Id == id) return s;
            return null;
        }
    }
}
