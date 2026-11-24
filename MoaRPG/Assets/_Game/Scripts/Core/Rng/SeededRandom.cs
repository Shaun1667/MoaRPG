using System;

namespace Moa.Core.Rng
{
    /// <summary>
    /// 시드 난수 하나 (SplitMix64). 같은 시드 = 같은 수열.
    /// 상태는 64비트 하나라 스냅샷에 그대로 저장 · 복원할 수 있다 (<see cref="State"/>, <see cref="FromState"/>).
    /// 아래 상수는 알고리즘 상수이고 게임 수치가 아니다.
    /// </summary>
    public sealed class SeededRandom
    {
        const ulong GoldenGamma = 0x9E3779B97F4A7C15UL;
        const ulong MixMul1 = 0xBF58476D1CE4E5B9UL;
        const ulong MixMul2 = 0x94D049BB133111EBUL;
        const int MixShift1 = 30;
        const int MixShift2 = 27;
        const int MixShift3 = 31;
        const int UpperHalfShift = 32;
        const int DoubleMantissaShift = 11;
        const double DoubleUnit = 1.0 / (1UL << 53);

        ulong _state;

        public SeededRandom(ulong seed)
        {
            _state = seed;
        }

        /// <summary>지금 상태 (저장용). 같은 상태에서 이어 뽑으면 같은 수열이 이어진다.</summary>
        public ulong State => _state;

        /// <summary>저장한 상태에서 다시 만든다.</summary>
        public static SeededRandom FromState(ulong state) => new SeededRandom(state);

        /// <summary>64비트 값을 고르게 섞는다 (시드 파생용).</summary>
        public static ulong Mix(ulong z)
        {
            z = (z ^ (z >> MixShift1)) * MixMul1;
            z = (z ^ (z >> MixShift2)) * MixMul2;
            return z ^ (z >> MixShift3);
        }

        public ulong NextULong()
        {
            _state += GoldenGamma;
            return Mix(_state);
        }

        public uint NextUInt() => (uint)(NextULong() >> UpperHalfShift);

        /// <summary>[0, 1) 실수.</summary>
        public double NextDouble() => (NextULong() >> DoubleMantissaShift) * DoubleUnit;

        /// <summary>[minInclusive, maxExclusive) 정수. 치우침 없음 (거절 샘플링).</summary>
        public int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
                throw new ArgumentOutOfRangeException(nameof(maxExclusive), "maxExclusive must be greater than minInclusive");
            ulong span = (ulong)((long)maxExclusive - minInclusive);
            ulong limit = ulong.MaxValue - (ulong.MaxValue % span);
            ulong v;
            do { v = NextULong(); } while (v >= limit);
            return (int)((long)minInclusive + (long)(v % span));
        }

        /// <summary>확률 p(0~1)로 참.</summary>
        public bool Chance(double p) => NextDouble() < p;
    }
}
