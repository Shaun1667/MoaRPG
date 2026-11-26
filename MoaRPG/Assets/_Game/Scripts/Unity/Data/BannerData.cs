using System.Collections.Generic;
using Moa.Core.Gacha;
using UnityEngine;

namespace Moa.Unity.Data
{
    /// <summary>
    /// 가챠 배너 하나 (3-7). 에셋 Assets/_Game/Data/Banners/&lt;배너&gt;.asset — 새 배너 = 에셋 하나 + GachaTuning 배너 표에 한 줄 (BALANCE 19번).
    /// 풀(등급은 항목 에셋) · 확률(★5 · ★4 기본 · 상승 시작 · 회차당 상승 · 확정) · 천장(종류 = BannerKind, 같은 종류끼리 이어짐) · 픽업 ★5와 픽업 확률(50/50).
    /// 규칙은 Core BannerRules · GachaRoll. 코드에 기본값 없음.
    /// </summary>
    [CreateAssetMenu(menuName = "MoaRPG/Data/Gacha Banner", fileName = "Banner")]
    public sealed class BannerData : ScriptableObject
    {
        [Tooltip("Banner id (debug / logs)")]
        [SerializeField] string bannerId;
        [Tooltip("Pity is counted per banner kind (banners of the same kind share it)")]
        [SerializeField] BannerKind kind;
        [Tooltip("Text table key of the banner name")]
        [SerializeField] string nameKey;
        [Tooltip("Wish stones per pull")]
        [SerializeField] int cost;

        [Header("5 stars")]
        [Tooltip("Base chance per pull (0.006 = 0.6%)")]
        [SerializeField] float fiveBaseRate;
        [Tooltip("From this pull (since the last 5 star, 1 = first) the chance rises")]
        [SerializeField] int fiveSoftPityStart;
        [Tooltip("Chance added per pull from the soft pity start")]
        [SerializeField] float fiveSoftPityStep;
        [Tooltip("This pull is always a 5 star")]
        [SerializeField] int fiveHardPity;

        [Header("4 stars")]
        [SerializeField] float fourBaseRate;
        [SerializeField] int fourSoftPityStart;
        [SerializeField] float fourSoftPityStep;
        [Tooltip("A 4 star or better within this many pulls")]
        [SerializeField] int fourHardPity;

        [Header("Pool")]
        [Tooltip("Featured 5 stars (empty = no 50/50)")]
        [SerializeField] GachaItemData[] featured = new GachaItemData[0];
        [Tooltip("Chance that a 5 star is featured when the featured one is not guaranteed")]
        [SerializeField] float featuredChance;
        [Tooltip("All other items (rarity comes from each item)")]
        [SerializeField] GachaItemData[] pool = new GachaItemData[0];

        public string BannerId => bannerId;
        public BannerKind Kind => kind;
        public string NameKey => nameKey;
        public int Cost => cost;
        public float FiveBaseRate => fiveBaseRate;
        public int FiveSoftPityStart => fiveSoftPityStart;
        public float FiveSoftPityStep => fiveSoftPityStep;
        public int FiveHardPity => fiveHardPity;
        public float FourBaseRate => fourBaseRate;
        public int FourSoftPityStart => fourSoftPityStart;
        public float FourSoftPityStep => fourSoftPityStep;
        public int FourHardPity => fourHardPity;
        public float FeaturedChance => featuredChance;
        public IReadOnlyList<GachaItemData> Featured => featured ?? new GachaItemData[0];
        public IReadOnlyList<GachaItemData> Pool => pool ?? new GachaItemData[0];

        /// <summary>규칙 (확률은 float → double, 4자리에서 반올림해 0.006이 0.006이 되게).</summary>
        public BannerRules ToRules()
        {
            var p = new List<GachaItem>();
            foreach (var it in Pool) if (it != null) p.Add(it.ToItem());
            var f = new List<GachaItem>();
            foreach (var it in Featured) if (it != null) f.Add(it.ToItem());
            return new BannerRules(bannerId, kind, cost,
                new RateCurve(Round(fiveBaseRate), fiveSoftPityStart, Round(fiveSoftPityStep), fiveHardPity),
                new RateCurve(Round(fourBaseRate), fourSoftPityStart, Round(fourSoftPityStep), fourHardPity),
                Round(featuredChance), p.ToArray(), f.ToArray());
        }

        static double Round(float v) => System.Math.Round((double)v, 6);

        /// <summary>id로 항목 에셋 찾기 (픽업 · 풀, 없으면 null).</summary>
        public GachaItemData Find(string id)
        {
            foreach (var it in Featured) if (it != null && it.Id == id) return it;
            foreach (var it in Pool) if (it != null && it.Id == id) return it;
            return null;
        }
    }
}
