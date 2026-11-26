using System;
using System.Collections.Generic;
using Moa.Core.Combat;
using UnityEngine;

namespace Moa.Unity.Combat
{
    /// <summary>
    /// 적 투사체 실행 (3-4) — 규칙(빠르기 · 수명 · 한 발에 한 대상 · 풀링)은 3-3과 같은 Core <see cref="ProjectileSet"/>, 여기는 맞음 검사(구 쓸기)와 그레이박스 표현.
    /// EnemyAgent가 예고가 끝나는 스텝에 <see cref="Fire"/>, 고정 스텝마다 <see cref="Step"/>: 칸마다 이번 스텝 구간을 SphereCast로 쓸어 가장 가까운 것 —
    /// 플레이어(PlayerDefense)면 <see cref="PlayerHit"/> 이벤트(EnemyAgent가 근접과 같은 PlayerDefense.ReceiveEnemyAttack으로 판정 — 회피 · 저스트 · 무적 · 보호막 규칙이 같다),
    /// 다른 적 · 허수아비(IHitTarget)는 지나가고, 그 밖의 콜라이더(벽 · 땅)면 막힘. 자기(owner) 콜라이더 · 트리거는 뺀다.
    /// 쏠 때의 공격 줄 번호는 ProjectileShot.Step(1부터)에 담는다 — 배율 · 넘어짐은 그 줄에서.
    /// 표현: 칸마다 arrowTemplate을 복제한 오브젝트를 다시 쓴다(사라진 칸은 숨김).
    /// </summary>
    public sealed class EnemyProjectiles : MonoBehaviour
    {
        const int MaxCastHits = 16;

        [Tooltip("Inactive template object copied for each pooled projectile (points along local +Z)")]
        [SerializeField] GameObject arrowTemplate;
        [Tooltip("Colliders under this are never hit (the shooter's own body). Empty = this object")]
        [SerializeField] Transform owner;

        readonly ProjectileSet _set = new ProjectileSet();
        readonly List<GameObject> _visuals = new List<GameObject>();
        readonly List<Vector3> _origins = new List<Vector3>();
        readonly RaycastHit[] _hits = new RaycastHit[MaxCastHits];
        Transform _pool;

        /// <summary>투사체 규칙 (Core).</summary>
        public ProjectileSet Set => _set;
        public int ActiveCount => _set.ActiveCount;
        public int FiredCount => _set.FiredCount;
        public int HitCount => _set.HitCount;
        public int BlockedCount => _set.BlockedCount;
        public int PooledVisuals => _visuals.Count;
        /// <summary>마지막으로 맞은 · 막힌 자리 · 이름 (디버그).</summary>
        public Vector3 LastImpact { get; private set; }
        public string LastImpactName { get; private set; } = "-";

        /// <summary>플레이어에게 닿았다 (대상, 쏠 때 정한 수치, 쏜 자리 = 밀려나는 기준).</summary>
        public event Action<PlayerDefense, ProjectileShot, Vector3> PlayerHit;

        Transform Owner => owner != null ? owner : transform;

        /// <summary>origin에서 수평 방향 dir로 한 발. 칸 번호 (−1 = 수치가 비어 안 나감).</summary>
        public int Fire(ProjectileRules rules, ProjectileShot shot, Vector3 origin, Vector3 dir, Vector3 shooterFeet)
        {
            int slot = _set.Spawn(rules, shot, origin.x, origin.y, origin.z, dir.x, dir.z);
            if (slot < 0) { Debug.LogWarning("EnemyProjectiles: projectile values are empty (speed / lifetime) - nothing fired"); return -1; }
            while (_origins.Count <= slot) _origins.Add(Vector3.zero);
            _origins[slot] = shooterFeet;
            ShowVisual(slot);
            return slot;
        }

        /// <summary>한 고정 스텝: Core가 칸을 옮기고, 이번 구간을 쓸어 맞음 · 막힘을 정한다.</summary>
        public void Step(float dt)
        {
            if (_set.Capacity == 0) return;
            _set.Advance(dt);
            for (int i = 0; i < _set.Capacity; i++)
            {
                if (!_set.IsAlive(i)) { HideVisual(i); continue; }
                _set.PrevPosition(i, out float px, out float py, out float pz);
                _set.Position(i, out float x, out float y, out float z);
                Vector3 from = new Vector3(px, py, pz), to = new Vector3(x, y, z);
                Sweep(i, from, to);
                if (_set.IsAlive(i)) PlaceVisual(i, to);
                else HideVisual(i);
            }
        }

        void Sweep(int slot, Vector3 from, Vector3 to)
        {
            Vector3 d = to - from;
            float dist = d.magnitude;
            if (dist < 1e-5f) return;
            float radius = _set.Rules(slot).Radius;
            int n = Physics.SphereCastNonAlloc(from, radius, d / dist, _hits, dist, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            Array.Sort(_hits, 0, n, HitDistance.Instance);
            Transform me = Owner;
            for (int k = 0; k < n; k++)
            {
                Collider c = _hits[k].collider;
                if (c == null || c.transform.IsChildOf(me)) continue; // 자기 몸 제외
                Vector3 at = _hits[k].distance > 0f ? from + d / dist * _hits[k].distance : from;
                var player = c.GetComponentInParent<PlayerDefense>();
                if (player != null)
                {
                    ProjectileShot shot = _set.Shot(slot);
                    if (!_set.TryHit(slot, player.Id.IsValid ? player.Id : new Moa.Core.Ids.EntityId(-1))) continue;
                    LastImpact = at;
                    LastImpactName = c.name;
                    PlayerHit?.Invoke(player, shot, slot < _origins.Count ? _origins[slot] : from);
                    return;
                }
                if (c.GetComponentInParent<IHitTarget>() != null) continue; // 다른 적 · 허수아비는 지나간다
                _set.Block(slot, at.x, at.y, at.z);
                LastImpact = at;
                LastImpactName = c.name;
                return;
            }
        }

        /// <summary>[상태 변경] 날아가는 것을 모두 지운다 (다시 나기 · 전멸 뒤).</summary>
        public void Clear()
        {
            _set.Clear();
            for (int i = 0; i < _visuals.Count; i++) HideVisual(i);
        }

        // ---- 표현 (풀) ----

        void ShowVisual(int slot)
        {
            if (arrowTemplate == null) return;
            if (_pool == null) _pool = new GameObject(name + "_Projectiles").transform; // 적을 따라 움직이지 않게 씬 루트에
            while (_visuals.Count <= slot)
            {
                var go = Instantiate(arrowTemplate, _pool);
                go.name = arrowTemplate.name + "_" + _visuals.Count;
                foreach (var col in go.GetComponentsInChildren<Collider>()) Destroy(col);
                go.SetActive(false);
                _visuals.Add(go);
            }
            _set.Position(slot, out float x, out float y, out float z);
            PlaceVisual(slot, new Vector3(x, y, z));
            _visuals[slot].SetActive(true);
        }

        void PlaceVisual(int slot, Vector3 at)
        {
            if (slot >= _visuals.Count || _visuals[slot] == null) return;
            _set.Direction(slot, out float dx, out float dz);
            _visuals[slot].transform.SetPositionAndRotation(at, Quaternion.LookRotation(new Vector3(dx, 0f, dz)));
        }

        void HideVisual(int slot)
        {
            if (slot < _visuals.Count && _visuals[slot] != null && _visuals[slot].activeSelf) _visuals[slot].SetActive(false);
        }

        void OnDestroy()
        {
            if (_pool != null) Destroy(_pool.gameObject);
        }

        sealed class HitDistance : IComparer<RaycastHit>
        {
            public static readonly HitDistance Instance = new HitDistance();
            public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
        }
    }
}
