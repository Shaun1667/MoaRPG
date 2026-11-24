using System;

namespace Moa.Core.Rng
{
    /// <summary>
    /// 판 시드 하나에서 용도별 난수(<see cref="RandomStream"/>)를 만든다.
    /// 용도별 시드 = Mix(판 시드 ^ Mix(용도 번호 + 1)) — 같은 판 시드면 언제나 같은 용도별 시드.
    /// </summary>
    public sealed class RandomStreams
    {
        readonly SeededRandom[] _streams;

        public RandomStreams(ulong masterSeed)
        {
            MasterSeed = masterSeed;
            _streams = new SeededRandom[StreamCount()];
            for (int i = 0; i < _streams.Length; i++)
                _streams[i] = new SeededRandom(DeriveSeed(masterSeed, i));
        }

        public ulong MasterSeed { get; }

        public SeededRandom Get(RandomStream stream) => _streams[(int)stream];

        public static ulong DeriveSeed(ulong masterSeed, int streamIndex)
        {
            return SeededRandom.Mix(masterSeed ^ SeededRandom.Mix((ulong)streamIndex + 1UL));
        }

        /// <summary>판 시드(정수, 인스펙터 값)를 64비트 시드로.</summary>
        public static ulong SeedFromInt(int seed) => unchecked((ulong)(uint)seed);

        static int StreamCount()
        {
            int max = 0;
            foreach (RandomStream s in Enum.GetValues(typeof(RandomStream)))
                if ((int)s > max) max = (int)s;
            return max + 1;
        }
    }
}
