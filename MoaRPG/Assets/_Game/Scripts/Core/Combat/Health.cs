using System;

namespace Moa.Core.Combat
{
    /// <summary>
    /// 체력 (2-1). 피해 · 죽음 이벤트. 플레이어 · 적 · 허수아비가 같은 것을 쓴다.
    /// 무적(<see cref="Invulnerable"/>)이면 피해 이벤트는 오지만 체력은 줄지 않는다 (디버그 · 시험용).
    /// </summary>
    public sealed class Health
    {
        public Health(int max)
        {
            if (max < 1) throw new ArgumentOutOfRangeException(nameof(max), "max must be >= 1");
            Max = max;
            Current = max;
        }

        public int Max { get; }
        public int Current { get; private set; }
        public bool IsDead => Current <= 0;
        public bool Invulnerable { get; set; }
        public float Ratio => (float)Current / Max;

        /// <summary>피해를 받았을 때 (요청한 피해, 실제로 준 양, 남은 체력).</summary>
        public event Action<int, int, int> Damaged;
        public event Action Died;

        /// <summary>피해를 준다. 실제로 줄어든 양을 돌려준다 (죽어 있으면 0, 이벤트 없음).</summary>
        public int TakeDamage(int amount)
        {
            if (IsDead || amount <= 0) return 0;
            int applied = Invulnerable ? 0 : (amount < Current ? amount : Current);
            Current -= applied;
            Damaged?.Invoke(amount, applied, Current);
            if (IsDead) Died?.Invoke();
            return applied;
        }

        /// <summary>[상태 변경] 가득 채운다 (죽어 있어도 되살린다).</summary>
        public void Restore() => Current = Max;
    }
}
