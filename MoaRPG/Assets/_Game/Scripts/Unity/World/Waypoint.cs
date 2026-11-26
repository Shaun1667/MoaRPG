using Moa.Core.World;
using Moa.Unity.Data;
using UnityEngine;

namespace Moa.Unity.World
{
    /// <summary>
    /// 순간이동 지점 (3-6): 플레이어가 반지름(ExplorationTuning) 안에 오면 활성화(Core WorldProgress.TryActivate) — 저장된다.
    /// 활성화된 지점은 지도 화면(M)에서 골라 이동한다(GameRoot.TeleportToWaypoint). 색 = 꺼짐 / 켜짐 (Setup 표).
    /// id는 저장 키 — 바꾸면 저장의 활성화가 풀린다. 이름은 텍스트 키(nameKey). 배치는 TestFieldSetup 표 한 줄.
    /// </summary>
    public sealed class Waypoint : MonoBehaviour
    {
        [Tooltip("Save id (do not change once shipped)")]
        [SerializeField] string id;
        [Tooltip("Text table key of the name shown on the map")]
        [SerializeField] string nameKey;
        [SerializeField] Renderer[] marks = new Renderer[0];
        [SerializeField] Color inactiveColor;
        [SerializeField] Color activeColor;
        [Tooltip("Arrival spot: this far in front of the waypoint (local +Z, m, layout only)")]
        [SerializeField] float arrivalDistance;

        WorldProgress _world;
        Transform _player;
        ExplorationTuning _tuning;
        MaterialPropertyBlock _block;
        int _shown = -1;

        public string Id => id;
        public string NameKey => nameKey;
        public bool Activated => _world != null && _world.IsActivated(id);
        /// <summary>순간이동해 서는 자리 (지점 앞 arrivalDistance, 바닥 조금 위 — 기둥에 가리지 않게).</summary>
        public Vector3 ArrivalPosition => transform.position + transform.forward * arrivalDistance + Vector3.up * 0.05f;

        public void Bind(WorldProgress world, Transform player, ExplorationTuning tuning)
        {
            _world = world;
            _player = player;
            _tuning = tuning;
            _shown = -1;
        }

        void FixedUpdate()
        {
            if (_world == null || _player == null || _tuning == null || _world.IsActivated(id)) return;
            if (WorldReach.Inside(_player.position, transform.position, _tuning.WaypointRadius, _tuning.ReachHeight)) _world.TryActivate(id);
        }

        void LateUpdate()
        {
            int want = Activated ? 1 : 0;
            if (want == _shown) return;
            _shown = want;
            WorldReach.Paint(marks, want == 1 ? activeColor : inactiveColor, ref _block);
        }
    }
}
