using System;
using Moa.Core.World;
using UnityEngine;

namespace Moa.Unity.Data
{
    /// <summary>
    /// 탐험 수치 (3-6): 순간이동 지점 · 상자 · 압력판이 반응하는 거리, 상자 등급마다 보상(표 한 줄 = 등급 하나), 순간이동하면 회복하는지.
    /// 에셋 Assets/_Game/Data/World/Exploration.asset, 값은 BALANCE.md 18번. 코드에는 기본값을 두지 않는다.
    /// </summary>
    [CreateAssetMenu(menuName = "MoaRPG/Data/Exploration Tuning", fileName = "Exploration")]
    public sealed class ExplorationTuning : ScriptableObject
    {
        [Serializable]
        public sealed class ChestRow
        {
            public ChestTier tier;
            public int gold;
            public int expItems;
            /// <summary>3-7: 소원석 (가챠).</summary>
            public int wishes;
        }

        [Tooltip("A waypoint activates when the player comes this close (m, flat distance)")]
        [SerializeField] float waypointRadius;
        [Tooltip("A chest opens when the player comes this close (m, flat distance)")]
        [SerializeField] float chestRadius;
        [Tooltip("A pressure plate lights when the player stands this close to its center (m)")]
        [SerializeField] float plateRadius;
        [Tooltip("Max height difference for waypoints / chests / plates to react (m)")]
        [SerializeField] float reachHeight;
        [Tooltip("Teleporting to a waypoint heals and revives the party (like a healing point)")]
        [SerializeField] bool teleportHealsParty;
        [Tooltip("One row per chest tier (gold, adventure exp items)")]
        [SerializeField] ChestRow[] chests = new ChestRow[0];

        public float WaypointRadius => waypointRadius;
        public float ChestRadius => chestRadius;
        public float PlateRadius => plateRadius;
        public float ReachHeight => reachHeight;
        public bool TeleportHealsParty => teleportHealsParty;
        public int ChestRowCount => chests != null ? chests.Length : 0;
        public ChestRow GetChestRow(int i) => chests != null && i >= 0 && i < chests.Length ? chests[i] : null;

        /// <summary>등급마다 보상 (표에 없는 등급은 0).</summary>
        public ExplorationRules ToRules()
        {
            int n = Enum.GetValues(typeof(ChestTier)).Length;
            var rewards = new ChestReward[n];
            if (chests != null)
                foreach (var row in chests)
                {
                    if (row == null) continue;
                    int i = (int)row.tier;
                    if (i >= 0 && i < n) rewards[i] = new ChestReward(row.gold, row.expItems, row.wishes);
                }
            return new ExplorationRules(rewards, teleportHealsParty);
        }
    }
}
