using Moa.Core.Ids;

namespace Moa.Core.Combat
{
    /// <summary>공격 한 동작이 대상을 맞히는 방식 (3-3, 무기 표의 동작마다, 끝에만 추가).</summary>
    public enum HitDelivery
    {
        /// <summary>몸 앞 부채꼴 겹침 검사 (2-1 방식).</summary>
        Melee,
        /// <summary>판정 구간이 열리는 순간 투사체 하나를 쏜다 — 맞은 대상 하나에 같은 피해 · 경직 규칙.</summary>
        Projectile,
    }

    /// <summary>투사체 수치 (WeaponTuning 에셋 → PlayerCombat이 넘긴다, BALANCE 15번).</summary>
    public readonly struct ProjectileRules
    {
        /// <summary>날아가는 빠르기 (m/s, 수평).</summary>
        public readonly float Speed;
        /// <summary>날아가는 시간 (초) — 다 되면 사라진다.</summary>
        public readonly float Lifetime;
        /// <summary>맞음 검사 구 반지름 (m).</summary>
        public readonly float Radius;

        public ProjectileRules(float speed, float lifetime, float radius)
        {
            Speed = speed;
            Lifetime = lifetime;
            Radius = radius;
        }

        /// <summary>닿는 거리 = 빠르기 × 시간 (m).</summary>
        public float Range => Speed * Lifetime;
        public bool Valid => Speed > 0f && Lifetime > 0f;
    }

    /// <summary>쏠 때 정해지는 한 발의 피해 수치 — 맞았을 때 근접과 같은 Report로 넘긴다 (쏜 뒤 교체해도 쏜 캐릭터의 공격력).</summary>
    public readonly struct ProjectileShot
    {
        public readonly float Attack;
        public readonly float Multiplier;
        public readonly float Poise;
        public readonly AttackMode Kind;
        /// <summary>HitReport의 단계 번호 · 휘두르기 번호 (1부터).</summary>
        public readonly int Step;
        public readonly int Swing;

        public ProjectileShot(float attack, float multiplier, float poise, AttackMode kind, int step, int swing)
        {
            Attack = attack;
            Multiplier = multiplier;
            Poise = poise;
            Kind = kind;
            Step = step;
            Swing = swing;
        }
    }

    /// <summary>투사체가 사라진 까닭 (끝에만 추가).</summary>
    public enum ProjectileEnd
    {
        None,
        /// <summary>대상을 맞혔다 (관통 없음 — 한 발에 한 대상).</summary>
        Hit,
        /// <summary>벽 · 땅 같은 대상 아닌 것에 막혔다.</summary>
        Blocked,
        /// <summary>날아가는 시간이 다 됐다.</summary>
        Expired,
        /// <summary>[상태 변경] 모두 지움.</summary>
        Cleared,
    }

    /// <summary>
    /// 투사체 순수 규칙 (3-3) — 쏜 투사체들을 칸(slot)에 담아 다시 쓴다(풀링, 사라진 칸을 먼저 쓴다).
    /// 고정 스텝마다 <see cref="Advance"/>: 칸마다 이번 스텝의 구간(Prev → 지금)을 만든다 — 그 구간의 맞음 검사(구 쓸기)는 Unity 쪽이 하고
    /// 대상이면 <see cref="TryHit"/>(한 발에 한 대상, 맞히면 사라짐), 대상이 아닌 것에 막히면 <see cref="Block"/>.
    /// 날아간 시간이 Lifetime이 되면 그 스텝의 구간까지 검사한 뒤 다음 Advance에서 사라진다(닿는 거리 = Speed × Lifetime 정확히).
    /// 피해 · 경직 · 에너지 규칙은 근접과 같다 — 맞으면 쏠 때 정한 <see cref="ProjectileShot"/>을 그대로 넘긴다.
    /// </summary>
    public sealed class ProjectileSet
    {
        const float Epsilon = 1e-5f;

        struct Slot
        {
            public bool Alive;
            public ProjectileRules Rules;
            public ProjectileShot Shot;
            public float X, Y, Z, PrevX, PrevY, PrevZ, DirX, DirZ;
            public float Elapsed;
            public ProjectileEnd End;
        }

        Slot[] _slots = new Slot[0];

        /// <summary>지금 날아가는 수.</summary>
        public int ActiveCount { get; private set; }
        /// <summary>담을 수 있는 칸 수 (늘어나기만 한다 — 다시 쓰는 칸).</summary>
        public int Capacity => _slots.Length;
        /// <summary>지금까지 쏜 수 · 맞힌 수 · 막힌 수 · 시간이 다 된 수 (디버그 · 확인용).</summary>
        public int FiredCount { get; private set; }
        public int HitCount { get; private set; }
        public int BlockedCount { get; private set; }
        public int ExpiredCount { get; private set; }
        /// <summary>새로 칸을 만든 수 (다시 쓴 칸은 안 셈, 풀링 확인용).</summary>
        public int CreatedCount { get; private set; }

        public bool IsAlive(int slot) => slot >= 0 && slot < _slots.Length && _slots[slot].Alive;
        public ProjectileShot Shot(int slot) => _slots[slot].Shot;
        public ProjectileRules Rules(int slot) => _slots[slot].Rules;
        public ProjectileEnd LastEnd(int slot) => slot >= 0 && slot < _slots.Length ? _slots[slot].End : ProjectileEnd.None;
        public float Elapsed(int slot) => _slots[slot].Elapsed;
        public void Position(int slot, out float x, out float y, out float z) { x = _slots[slot].X; y = _slots[slot].Y; z = _slots[slot].Z; }
        public void PrevPosition(int slot, out float x, out float y, out float z) { x = _slots[slot].PrevX; y = _slots[slot].PrevY; z = _slots[slot].PrevZ; }
        public void Direction(int slot, out float x, out float z) { x = _slots[slot].DirX; z = _slots[slot].DirZ; }
        /// <summary>지금까지 날아간 거리 (m).</summary>
        public float Travelled(int slot) => _slots[slot].Elapsed * _slots[slot].Rules.Speed;

        /// <summary>(x, y, z)에서 수평 방향 (dirX, dirZ)로 한 발. 쓴 칸 번호, 수치가 잘못이면 −1.</summary>
        public int Spawn(ProjectileRules rules, ProjectileShot shot, float x, float y, float z, float dirX, float dirZ)
        {
            float len = (float)System.Math.Sqrt(dirX * dirX + dirZ * dirZ);
            if (!rules.Valid || len < Epsilon) return -1;
            int slot = -1;
            for (int i = 0; i < _slots.Length; i++) if (!_slots[i].Alive) { slot = i; break; }
            if (slot < 0)
            {
                slot = _slots.Length;
                System.Array.Resize(ref _slots, _slots.Length + 1);
                CreatedCount++;
            }
            _slots[slot] = new Slot
            {
                Alive = true, Rules = rules, Shot = shot,
                X = x, Y = y, Z = z, PrevX = x, PrevY = y, PrevZ = z,
                DirX = dirX / len, DirZ = dirZ / len, Elapsed = 0f, End = ProjectileEnd.None,
            };
            ActiveCount++;
            FiredCount++;
            return slot;
        }

        /// <summary>한 고정 스텝: 시간이 다 된 칸은 사라지고(Expired), 나머지는 앞으로 (남은 시간만큼만). 이번 스텝의 구간 = Prev → 지금.</summary>
        public void Advance(float dt)
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                if (!_slots[i].Alive) continue;
                ref Slot s = ref _slots[i];
                float left = s.Rules.Lifetime - s.Elapsed;
                if (left <= Epsilon) { Kill(ref s, ProjectileEnd.Expired); ExpiredCount++; continue; }
                float t = dt < left ? dt : left;
                s.PrevX = s.X; s.PrevY = s.Y; s.PrevZ = s.Z;
                s.X += s.DirX * s.Rules.Speed * t;
                s.Z += s.DirZ * s.Rules.Speed * t;
                s.Elapsed += t;
            }
        }

        /// <summary>이 칸이 target을 맞혔다 — 살아 있고 대상이 맞으면 사라지고(Hit) true (한 발에 한 대상).</summary>
        public bool TryHit(int slot, EntityId target)
        {
            if (!IsAlive(slot) || !target.IsValid) return false;
            Kill(ref _slots[slot], ProjectileEnd.Hit);
            HitCount++;
            return true;
        }

        /// <summary>대상 아닌 것에 막혔다 — (x, y, z)에서 사라진다 (Blocked).</summary>
        public void Block(int slot, float x, float y, float z)
        {
            if (!IsAlive(slot)) return;
            ref Slot s = ref _slots[slot];
            s.X = x; s.Y = y; s.Z = z;
            Kill(ref s, ProjectileEnd.Blocked);
            BlockedCount++;
        }

        /// <summary>[상태 변경] 모두 지운다 (순간이동 · 재시작).</summary>
        public void Clear()
        {
            for (int i = 0; i < _slots.Length; i++)
                if (_slots[i].Alive) Kill(ref _slots[i], ProjectileEnd.Cleared);
        }

        void Kill(ref Slot s, ProjectileEnd end)
        {
            s.Alive = false;
            s.End = end;
            ActiveCount--;
        }
    }
}
