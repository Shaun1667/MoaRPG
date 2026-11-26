using Moa.Core.World;
using Moa.Unity.Data;
using UnityEngine;

namespace Moa.Unity.World
{
    /// <summary>
    /// 상자 (3-6): 플레이어가 반지름 안에 오면 열린다(Core WorldProgress.TryOpen — 등급 보상이 지갑에, 열린 상태는 저장). 퍼즐 상자는 퍼즐이 풀리기 전에는 잠김.
    /// 상호작용 키 없이 다가가면 열린다 (정한 것). 색 = 닫힘(등급) / 잠김 / 열림, 열리면 뚜껑이 젖혀진다. 배치는 TestFieldSetup 표 한 줄.
    /// </summary>
    public sealed class Chest : MonoBehaviour
    {
        [Tooltip("Save id (do not change once shipped)")]
        [SerializeField] string id;
        [SerializeField] ChestTier tier;
        [Tooltip("Puzzle that must be solved first (empty = none)")]
        [SerializeField] string puzzleId;
        [SerializeField] Transform lid;
        [SerializeField] Renderer[] paint = new Renderer[0];
        [SerializeField] Color closedColor;
        [SerializeField] Color lockedColor;
        [SerializeField] Color openColor;
        [Tooltip("Lid angle when open (degrees around local X, visual only)")]
        [SerializeField] float openLidAngle;

        WorldProgress _world;
        Transform _player;
        ExplorationTuning _tuning;
        MaterialPropertyBlock _block;
        Quaternion _lidClosed = Quaternion.identity;
        bool _lidRead;
        int _shown = -1;

        public string Id => id;
        public ChestTier Tier => tier;
        public string PuzzleId => puzzleId;
        public bool Opened => _world != null && _world.IsOpened(id);
        public ChestBlock Block => _world != null ? _world.CheckOpen(id) : ChestBlock.Unknown;
        /// <summary>지금 보이는 상태 (0 닫힘 · 1 잠김 · 2 열림) — 확인용.</summary>
        public int ShownState => _shown;

        public void Bind(WorldProgress world, Transform player, ExplorationTuning tuning)
        {
            _world = world;
            _player = player;
            _tuning = tuning;
            _shown = -1;
        }

        void FixedUpdate()
        {
            if (_world == null || _player == null || _tuning == null) return;
            if (_world.CheckOpen(id) != ChestBlock.None) return;
            if (WorldReach.Inside(_player.position, transform.position, _tuning.ChestRadius, _tuning.ReachHeight)) _world.TryOpen(id);
        }

        void LateUpdate()
        {
            if (_world == null) return;
            ChestBlock b = _world.CheckOpen(id);
            int want = b == ChestBlock.AlreadyOpen ? 2 : b == ChestBlock.Locked ? 1 : 0;
            if (want == _shown) return;
            _shown = want;
            WorldReach.Paint(paint, want == 2 ? openColor : want == 1 ? lockedColor : closedColor, ref _block);
            if (lid != null)
            {
                if (!_lidRead) { _lidClosed = lid.localRotation; _lidRead = true; }
                lid.localRotation = want == 2 ? Quaternion.Euler(-openLidAngle, 0f, 0f) * _lidClosed : _lidClosed;
            }
        }
    }
}
