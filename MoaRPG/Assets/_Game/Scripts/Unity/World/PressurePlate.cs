using Moa.Core.World;
using Moa.Unity.Data;
using UnityEngine;

namespace Moa.Unity.World
{
    /// <summary>
    /// 압력판 (3-6, 1차 퍼즐 1종): 플레이어가 판 위(plateRadius 안)에 서면 켜진다 — 한 번 켜지면 켜진 채, 같은 퍼즐의 판이 모두 켜지면 풀린다(Core WorldProgress.PressPlate).
    /// 풀린 것만 저장된다. 색 = 꺼짐 / 켜짐. 배치는 TestFieldSetup 표 한 줄(퍼즐 id · 판 번호).
    /// </summary>
    public sealed class PressurePlate : MonoBehaviour
    {
        [SerializeField] string puzzleId;
        [SerializeField] int index;
        [SerializeField] Renderer[] paint = new Renderer[0];
        [SerializeField] Color idleColor;
        [SerializeField] Color litColor;

        WorldProgress _world;
        Transform _player;
        ExplorationTuning _tuning;
        MaterialPropertyBlock _block;
        int _shown = -1;

        public string PuzzleId => puzzleId;
        public int Index => index;
        public bool Lit => _world != null && _world.IsPlateLit(puzzleId, index);

        public void Bind(WorldProgress world, Transform player, ExplorationTuning tuning)
        {
            _world = world;
            _player = player;
            _tuning = tuning;
            _shown = -1;
        }

        void FixedUpdate()
        {
            if (_world == null || _player == null || _tuning == null || _world.IsPlateLit(puzzleId, index)) return;
            if (WorldReach.Inside(_player.position, transform.position, _tuning.PlateRadius, _tuning.ReachHeight)) _world.PressPlate(puzzleId, index);
        }

        void LateUpdate()
        {
            int want = Lit ? 1 : 0;
            if (want == _shown) return;
            _shown = want;
            WorldReach.Paint(paint, want == 1 ? litColor : idleColor, ref _block);
        }
    }
}
