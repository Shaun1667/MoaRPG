using System;

namespace Moa.Core.Growth
{
    /// <summary>
    /// 재화 (3-5). 저장은 이름으로 하므로 순서가 바뀌어도 되지만, 에셋 · 표에 정수로 들어갈 수 있어 enum은 끝에만 추가한다.
    /// Gold = 돈 (스킬 레벨 · 돌파), AdventureExp = 경험치 재료 "모험 경험" (캐릭터 · 무기 레벨). 둘 다 적 처치로 얻는다(EnemyTuning).
    /// </summary>
    public enum Currency
    {
        Gold = 0,
        AdventureExp = 1,
    }

    /// <summary>재화 지갑 (3-5) — 개수만 든다. 바뀔 때마다 <see cref="Changed"/>.</summary>
    public sealed class Wallet
    {
        public static readonly int CurrencyCount = Enum.GetValues(typeof(Currency)).Length;

        readonly int[] _amounts = new int[CurrencyCount];

        public event Action Changed;

        public int Get(Currency c) => _amounts[(int)c];

        public bool Has(Currency c, int amount) => amount <= 0 || _amounts[(int)c] >= amount;

        /// <summary>amount(0보다 크면)만큼 더한다. int 최대에서 멈춘다.</summary>
        public void Add(Currency c, int amount)
        {
            if (amount <= 0) return;
            long v = (long)_amounts[(int)c] + amount;
            _amounts[(int)c] = v > int.MaxValue ? int.MaxValue : (int)v;
            Changed?.Invoke();
        }

        /// <summary>있으면 쓰고 true, 모자라면 그대로 false.</summary>
        public bool TrySpend(Currency c, int amount)
        {
            if (amount < 0 || !Has(c, amount)) return false;
            if (amount == 0) return true;
            _amounts[(int)c] -= amount;
            Changed?.Invoke();
            return true;
        }

        /// <summary>[상태 변경] 값 정하기 (음수면 0).</summary>
        public void Set(Currency c, int amount)
        {
            _amounts[(int)c] = amount < 0 ? 0 : amount;
            Changed?.Invoke();
        }

        /// <summary>[상태 변경] 모두 0.</summary>
        public void Clear()
        {
            for (int i = 0; i < _amounts.Length; i++) _amounts[i] = 0;
            Changed?.Invoke();
        }
    }
}
