using UnityEngine;

namespace Moa.Unity.World
{
    /// <summary>
    /// 물 영역 (3-6): 축에 맞춘 상자 하나(가운데 = 이 오브젝트 자리, 크기 size) — 윗면이 물 표면. 충돌체는 없다(물리 · 레이에 걸리지 않는다).
    /// PlayerMotor가 고정 스텝마다 발 깊이를 재서 수영(Core Swim)을 정한다. 배치는 TestFieldSetup의 물 표 한 줄, GameRoot가 PlayerMotor에 넘긴다.
    /// 회전은 쓰지 않는다 (요만 돌려도 축 상자로 본다).
    /// </summary>
    public sealed class WaterVolume : MonoBehaviour
    {
        [Tooltip("Box size (m): x/z = area, y = depth from the bottom to the surface")]
        [SerializeField] Vector3 size;

        public Vector3 Size => size;
        public float SurfaceY => transform.position.y + size.y * 0.5f;
        public float BottomY => transform.position.y - size.y * 0.5f;
        public Vector3 Min => transform.position - size * 0.5f;
        public Vector3 Max => transform.position + size * 0.5f;

        /// <summary>평면 자리가 물 위(안)인지.</summary>
        public bool ContainsFlat(Vector3 p)
        {
            Vector3 a = Min, b = Max;
            return p.x >= a.x && p.x <= b.x && p.z >= a.z && p.z <= b.z;
        }

        /// <summary>발 자리의 물 깊이 (표면 − 발, m). 물 밖(평면 밖 · 표면 위 · 바닥보다 많이 아래)이면 0.</summary>
        public float DepthAt(Vector3 feet)
        {
            if (!ContainsFlat(feet)) return 0f;
            float surface = SurfaceY;
            if (feet.y > surface || feet.y < BottomY - size.y) return 0f;
            return surface - feet.y;
        }

        /// <summary>물가 후보: 평면 자리에서 가장 가까운 네 가장자리 점을 margin만큼 바깥으로 (가까운 순).</summary>
        public Vector3[] ShoreCandidates(Vector3 p, float margin)
        {
            Vector3 a = Min, b = Max;
            float x = Mathf.Clamp(p.x, a.x, b.x), z = Mathf.Clamp(p.z, a.z, b.z);
            var c = new[]
            {
                new Vector3(a.x - margin, p.y, z),
                new Vector3(b.x + margin, p.y, z),
                new Vector3(x, p.y, a.z - margin),
                new Vector3(x, p.y, b.z + margin),
            };
            System.Array.Sort(c, (u, v) => FlatDistance(p, u).CompareTo(FlatDistance(p, v)));
            return c;
        }

        static float FlatDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x, dz = a.z - b.z;
            return dx * dx + dz * dz;
        }

        /// <summary>물 영역들 중 발 자리의 가장 깊은 깊이와 그 물 (없으면 0 · null).</summary>
        public static float Deepest(WaterVolume[] volumes, Vector3 feet, out WaterVolume water)
        {
            water = null;
            float best = 0f;
            if (volumes == null) return 0f;
            foreach (var v in volumes)
            {
                if (v == null || !v.isActiveAndEnabled) continue;
                float d = v.DepthAt(feet);
                if (d > best) { best = d; water = v; }
            }
            return best;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 0.5f, 1f, 0.35f);
            Gizmos.DrawWireCube(transform.position, size);
        }
    }
}
