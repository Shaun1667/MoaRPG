using Moa.Core.World;
using Moa.Unity.Data;
using UnityEngine;

namespace Moa.Unity.World
{
    /// <summary>순간이동 지점 · 상자 · 압력판이 같이 쓰는 "플레이어가 가까이 있나" (평면 거리 · 높이 차).</summary>
    public static class WorldReach
    {
        public static bool Inside(Vector3 player, Vector3 point, float radius, float height)
        {
            Vector3 d = player - point;
            return Mathf.Abs(d.y) <= height && d.x * d.x + d.z * d.z <= radius * radius;
        }

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        /// <summary>렌더러 색을 MaterialPropertyBlock으로 (머티리얼은 그대로).</summary>
        public static void Paint(Renderer[] renderers, Color c, ref MaterialPropertyBlock block)
        {
            if (renderers == null) return;
            if (block == null) block = new MaterialPropertyBlock();
            foreach (var r in renderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(block);
                block.SetColor(BaseColorId, c);
                r.SetPropertyBlock(block);
            }
        }
    }
}
