using System;
using System.Collections.Generic;
using Moa.Core.Growth;

namespace Moa.Core.World
{
    /// <summary>상자 등급 (3-6) — 보상은 등급마다 표 한 줄(ExplorationRules). 에셋 · 씬에 정수로 들어가므로 enum은 끝에만 추가.</summary>
    public enum ChestTier
    {
        Common = 0,
        Exquisite = 1,
    }

    /// <summary>상자 보상 (골드 · 모험 경험).</summary>
    public readonly struct ChestReward
    {
        public readonly int Gold;
        public readonly int ExpItems;

        public ChestReward(int gold, int expItems)
        {
            Gold = gold;
            ExpItems = expItems;
        }
    }

    /// <summary>탐험 규칙 수치 (ExplorationTuning 에셋 → GameRoot가 넘긴다, BALANCE 18번).</summary>
    public sealed class ExplorationRules
    {
        readonly ChestReward[] _rewards;

        /// <param name="rewards">등급(ChestTier 정수)마다 보상 한 줄.</param>
        /// <param name="teleportHealsParty">순간이동하면 파티를 회복하는지.</param>
        public ExplorationRules(ChestReward[] rewards, bool teleportHealsParty)
        {
            _rewards = rewards ?? throw new ArgumentNullException(nameof(rewards));
            TeleportHealsParty = teleportHealsParty;
        }

        public bool TeleportHealsParty { get; }
        public int TierCount => _rewards.Length;

        /// <summary>등급의 보상 (표에 없으면 0).</summary>
        public ChestReward Reward(ChestTier tier)
        {
            int i = (int)tier;
            return i >= 0 && i < _rewards.Length ? _rewards[i] : new ChestReward(0, 0);
        }
    }

    /// <summary>순간이동이 막힌 까닭 (끝에만 추가).</summary>
    public enum WaypointBlock
    {
        None,
        Unknown,
        NotActivated,
        /// <summary>지금은 못 함 (전원 쓰러짐 연출 중 등 — 부르는 쪽이 알려 준다).</summary>
        Busy,
    }

    /// <summary>상자를 열 수 없는 까닭 (끝에만 추가).</summary>
    public enum ChestBlock
    {
        None,
        Unknown,
        AlreadyOpen,
        /// <summary>퍼즐이 아직 안 풀림.</summary>
        Locked,
    }

    /// <summary>압력판을 밟은 결과.</summary>
    public enum PlateResult
    {
        None,
        Unknown,
        /// <summary>이미 켜져 있음 (또는 퍼즐이 이미 풀림).</summary>
        AlreadyLit,
        /// <summary>새로 켜짐 (아직 다 안 켜짐).</summary>
        Lit,
        /// <summary>마지막 판이 켜져 퍼즐이 풀림.</summary>
        Solved,
    }

    /// <summary>
    /// 월드 진행 (3-6) 한 곳 — 순간이동 지점 활성화 · 상자 열림 · 퍼즐(1차: 압력판을 모두 밟기) 풀림.
    /// 지점 · 상자 · 퍼즐은 씬이 id로 등록한다(Register*). 상자를 열면 등급 보상을 지갑(3-5 Wallet)에 넣는다.
    /// 저장은 <see cref="ToSave"/> · <see cref="Load"/> (SaveSnapshot.world) — 저장에는 지점 · 상자 · 퍼즐 id만, 압력판 하나하나는 저장하지 않는다(풀기 전에 끄면 처음부터).
    /// 지금 씬에 없는 id도 지우지 않고 들고 있다가 다시 쓴다(다른 지역). 바뀌면 <see cref="Changed"/>.
    /// </summary>
    public sealed class WorldProgress
    {
        sealed class ChestInfo
        {
            public ChestTier Tier;
            public string PuzzleId;
        }

        sealed class PuzzleInfo
        {
            public bool[] Plates;
        }

        readonly Wallet _wallet;
        readonly List<string> _waypointOrder = new List<string>();
        readonly List<string> _chestOrder = new List<string>();
        readonly List<string> _puzzleOrder = new List<string>();
        readonly Dictionary<string, ChestInfo> _chests = new Dictionary<string, ChestInfo>();
        readonly Dictionary<string, PuzzleInfo> _puzzles = new Dictionary<string, PuzzleInfo>();
        readonly HashSet<string> _activated = new HashSet<string>();
        readonly HashSet<string> _opened = new HashSet<string>();
        readonly HashSet<string> _solved = new HashSet<string>();

        /// <param name="wallet">상자 보상을 넣을 지갑 (null이면 보상 없음).</param>
        public WorldProgress(ExplorationRules rules, Wallet wallet)
        {
            Rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _wallet = wallet;
        }

        public ExplorationRules Rules { get; }

        /// <summary>무엇이든 바뀌면 (활성화 · 열림 · 판 · 풀림 · 읽기 · 초기화).</summary>
        public event Action Changed;
        /// <summary>새로 활성화된 지점 id.</summary>
        public event Action<string> WaypointActivated;
        /// <summary>열린 상자 id · 보상.</summary>
        public event Action<string, ChestReward> ChestOpened;
        /// <summary>압력판 (퍼즐 id, 결과, 켜진 수, 판 수).</summary>
        public event Action<string, PlateResult, int, int> PlatePressed;

        public IReadOnlyList<string> Waypoints => _waypointOrder;
        public IReadOnlyList<string> Chests => _chestOrder;
        public IReadOnlyList<string> Puzzles => _puzzleOrder;
        public WaypointBlock LastTeleportBlock { get; private set; }
        public ChestBlock LastChestBlock { get; private set; }
        public ChestReward LastReward { get; private set; }
        public int TeleportCount { get; private set; }

        // ---- 등록 (씬이 시작할 때) ----

        public void RegisterWaypoint(string id)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("waypoint without id", nameof(id));
            if (_waypointOrder.Contains(id)) throw new ArgumentException("waypoint id " + id + " twice", nameof(id));
            _waypointOrder.Add(id);
        }

        /// <param name="puzzleId">이 퍼즐이 풀려야 열린다 (null · 빈 값 = 퍼즐 없음).</param>
        public void RegisterChest(string id, ChestTier tier, string puzzleId)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("chest without id", nameof(id));
            if (_chests.ContainsKey(id)) throw new ArgumentException("chest id " + id + " twice", nameof(id));
            _chests[id] = new ChestInfo { Tier = tier, PuzzleId = string.IsNullOrEmpty(puzzleId) ? null : puzzleId };
            _chestOrder.Add(id);
        }

        public void RegisterPuzzle(string id, int plateCount)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("puzzle without id", nameof(id));
            if (_puzzles.ContainsKey(id)) throw new ArgumentException("puzzle id " + id + " twice", nameof(id));
            if (plateCount < 1) throw new ArgumentException("puzzle " + id + " needs a plate", nameof(plateCount));
            _puzzles[id] = new PuzzleInfo { Plates = new bool[plateCount] };
            _puzzleOrder.Add(id);
        }

        // ---- 순간이동 지점 ----

        public bool IsActivated(string id) => id != null && _activated.Contains(id);

        /// <summary>다가가면 활성화. 새로 됐으면 true (모르는 id · 이미 활성화면 false).</summary>
        public bool TryActivate(string id)
        {
            if (id == null || !_waypointOrder.Contains(id) || !_activated.Add(id)) return false;
            WaypointActivated?.Invoke(id);
            Changed?.Invoke();
            return true;
        }

        public WaypointBlock CheckTeleport(string id, bool busy)
        {
            if (id == null || !_waypointOrder.Contains(id)) return WaypointBlock.Unknown;
            if (!_activated.Contains(id)) return WaypointBlock.NotActivated;
            if (busy) return WaypointBlock.Busy;
            return WaypointBlock.None;
        }

        /// <summary>순간이동해도 되는지 확인하고 세기 (실제 이동은 부르는 쪽). 되면 true, 안 되면 LastTeleportBlock.</summary>
        public bool TryTeleport(string id, bool busy)
        {
            LastTeleportBlock = CheckTeleport(id, busy);
            if (LastTeleportBlock != WaypointBlock.None) return false;
            TeleportCount++;
            return true;
        }

        // ---- 상자 ----

        public bool IsOpened(string id) => id != null && _opened.Contains(id);
        public ChestTier TierOf(string id) => id != null && _chests.TryGetValue(id, out var c) ? c.Tier : ChestTier.Common;
        public string PuzzleOf(string id) => id != null && _chests.TryGetValue(id, out var c) ? c.PuzzleId : null;

        public ChestBlock CheckOpen(string id)
        {
            if (id == null || !_chests.TryGetValue(id, out var c)) return ChestBlock.Unknown;
            if (_opened.Contains(id)) return ChestBlock.AlreadyOpen;
            if (c.PuzzleId != null && !_solved.Contains(c.PuzzleId)) return ChestBlock.Locked;
            return ChestBlock.None;
        }

        /// <summary>상자를 연다 — 등급 보상을 지갑에. 되면 true, 안 되면 LastChestBlock.</summary>
        public bool TryOpen(string id)
        {
            LastChestBlock = CheckOpen(id);
            if (LastChestBlock != ChestBlock.None) return false;
            _opened.Add(id);
            ChestReward r = Rules.Reward(_chests[id].Tier);
            LastReward = r;
            if (_wallet != null)
            {
                if (r.Gold > 0) _wallet.Add(Currency.Gold, r.Gold);
                if (r.ExpItems > 0) _wallet.Add(Currency.AdventureExp, r.ExpItems);
            }
            ChestOpened?.Invoke(id, r);
            Changed?.Invoke();
            return true;
        }

        // ---- 퍼즐 (압력판) ----

        public bool IsSolved(string puzzleId) => puzzleId != null && _solved.Contains(puzzleId);
        public int PlateCount(string puzzleId) => puzzleId != null && _puzzles.TryGetValue(puzzleId, out var p) ? p.Plates.Length : 0;

        /// <summary>켜진 판 수 (풀렸으면 판 수 전부).</summary>
        public int LitCount(string puzzleId)
        {
            if (puzzleId == null || !_puzzles.TryGetValue(puzzleId, out var p)) return 0;
            if (_solved.Contains(puzzleId)) return p.Plates.Length;
            int n = 0;
            foreach (bool b in p.Plates) if (b) n++;
            return n;
        }

        public bool IsPlateLit(string puzzleId, int plate)
        {
            if (puzzleId == null || !_puzzles.TryGetValue(puzzleId, out var p) || plate < 0 || plate >= p.Plates.Length) return false;
            return _solved.Contains(puzzleId) || p.Plates[plate];
        }

        /// <summary>압력판을 밟았다 — 한 번 켜지면 켜진 채로, 모두 켜지면 풀린다.</summary>
        public PlateResult PressPlate(string puzzleId, int plate)
        {
            if (puzzleId == null || !_puzzles.TryGetValue(puzzleId, out var p) || plate < 0 || plate >= p.Plates.Length) return PlateResult.Unknown;
            if (_solved.Contains(puzzleId) || p.Plates[plate]) return PlateResult.AlreadyLit;
            p.Plates[plate] = true;
            int lit = LitCount(puzzleId);
            PlateResult result = PlateResult.Lit;
            if (lit >= p.Plates.Length)
            {
                _solved.Add(puzzleId);
                result = PlateResult.Solved;
            }
            PlatePressed?.Invoke(puzzleId, result, lit, p.Plates.Length);
            Changed?.Invoke();
            return result;
        }

        // ---- 디버그 ----

        /// <summary>[상태 변경] 모든 지점 활성화.</summary>
        public void ActivateAll()
        {
            foreach (var id in _waypointOrder) _activated.Add(id);
            Changed?.Invoke();
        }

        /// <summary>[상태 변경] 모든 퍼즐을 풀고 모든 상자를 연다 (하나씩 연 것과 같음 — 보상 포함). 연 수.</summary>
        public int OpenAll()
        {
            foreach (var id in _puzzleOrder) _solved.Add(id);
            int n = 0;
            foreach (var id in _chestOrder) if (TryOpen(id)) n++;
            Changed?.Invoke();
            return n;
        }

        /// <summary>[상태 변경] 모두 처음으로 (지점 · 상자 · 퍼즐 · 판, 다른 지역 기록도).</summary>
        public void ResetAll()
        {
            _activated.Clear();
            _opened.Clear();
            _solved.Clear();
            foreach (var p in _puzzles.Values) Array.Clear(p.Plates, 0, p.Plates.Length);
            Changed?.Invoke();
        }

        // ---- 저장 ----

        public WorldSave ToSave()
        {
            return new WorldSave
            {
                waypoints = Sorted(_activated),
                chests = Sorted(_opened),
                puzzles = Sorted(_solved),
            };
        }

        static string[] Sorted(HashSet<string> set)
        {
            var a = new string[set.Count];
            set.CopyTo(a);
            Array.Sort(a, StringComparer.Ordinal);
            return a;
        }

        /// <summary>
        /// 저장을 읽는다 (null이면 처음 상태). 빈 id · 같은 id 두 번은 버리고 그 수를 돌려준다. 지금 씬에 없는 id도 들고 있다(저장하면 그대로 남는다).
        /// 판은 처음으로 (풀린 퍼즐은 모두 켜진 것으로 보인다).
        /// </summary>
        public int Load(WorldSave save)
        {
            _activated.Clear();
            _opened.Clear();
            _solved.Clear();
            foreach (var p in _puzzles.Values) Array.Clear(p.Plates, 0, p.Plates.Length);
            int fixes = 0;
            if (save != null)
            {
                fixes += Fill(_activated, save.waypoints);
                fixes += Fill(_opened, save.chests);
                fixes += Fill(_solved, save.puzzles);
            }
            Changed?.Invoke();
            return fixes;
        }

        static int Fill(HashSet<string> set, string[] ids)
        {
            if (ids == null) return 0;
            int fixes = 0;
            foreach (var id in ids)
                if (string.IsNullOrEmpty(id) || !set.Add(id)) fixes++;
            return fixes;
        }
    }
}
