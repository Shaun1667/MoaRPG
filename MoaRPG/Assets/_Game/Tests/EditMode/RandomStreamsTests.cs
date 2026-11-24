using System;
using Moa.Core.Rng;
using NUnit.Framework;

namespace Moa.Tests
{
    /// <summary>시드 난수: 같은 시드 = 같은 결과, 용도별 스트림끼리 독립 (핵심 설계 규칙 4).</summary>
    public class RandomStreamsTests
    {
        const int SampleCount = 64;
        const ulong SeedA = 12345UL;
        const ulong SeedB = 67890UL;

        static ulong[] Take(SeededRandom r, int n)
        {
            var a = new ulong[n];
            for (int i = 0; i < n; i++) a[i] = r.NextULong();
            return a;
        }

        [Test]
        public void SameSeed_SameSequence()
        {
            Assert.That(Take(new SeededRandom(SeedA), SampleCount), Is.EqualTo(Take(new SeededRandom(SeedA), SampleCount)));
        }

        [Test]
        public void DifferentSeed_DifferentSequence()
        {
            Assert.That(Take(new SeededRandom(SeedA), SampleCount), Is.Not.EqualTo(Take(new SeededRandom(SeedB), SampleCount)));
        }

        [Test]
        public void State_RoundTrip_ContinuesSameSequence()
        {
            var r = new SeededRandom(SeedA);
            Take(r, SampleCount);
            var restored = SeededRandom.FromState(r.State);
            Assert.That(Take(restored, SampleCount), Is.EqualTo(Take(r, SampleCount)));
        }

        [Test]
        public void Streams_SameMasterSeed_SameResultPerStream()
        {
            var a = new RandomStreams(SeedA);
            var b = new RandomStreams(SeedA);
            foreach (RandomStream s in Enum.GetValues(typeof(RandomStream)))
                Assert.That(Take(a.Get(s), SampleCount), Is.EqualTo(Take(b.Get(s), SampleCount)), s.ToString());
        }

        [Test]
        public void Streams_AreDifferentFromEachOther()
        {
            var streams = new RandomStreams(SeedA);
            var values = (RandomStream[])Enum.GetValues(typeof(RandomStream));
            var firsts = new ulong[values.Length][];
            for (int i = 0; i < values.Length; i++) firsts[i] = Take(streams.Get(values[i]), SampleCount);
            for (int i = 0; i < values.Length; i++)
                for (int j = i + 1; j < values.Length; j++)
                    Assert.That(firsts[i], Is.Not.EqualTo(firsts[j]), values[i] + " vs " + values[j]);
        }

        [Test]
        public void Streams_ConsumingOneStream_DoesNotChangeOthers()
        {
            var values = (RandomStream[])Enum.GetValues(typeof(RandomStream));
            foreach (var consumed in values)
            {
                var reference = new RandomStreams(SeedA);
                var touched = new RandomStreams(SeedA);
                Take(touched.Get(consumed), SampleCount * SampleCount);
                foreach (var other in values)
                {
                    if (other == consumed) continue;
                    Assert.That(Take(touched.Get(other), SampleCount), Is.EqualTo(Take(reference.Get(other), SampleCount)),
                        "consuming " + consumed + " changed " + other);
                }
            }
        }

        [Test]
        public void Range_StaysInBounds()
        {
            const int min = -3;
            const int max = 7;
            var r = new SeededRandom(SeedA);
            for (int i = 0; i < SampleCount * SampleCount; i++)
            {
                int v = r.Range(min, max);
                Assert.That(v, Is.GreaterThanOrEqualTo(min).And.LessThan(max));
            }
        }

        [Test]
        public void NextDouble_InUnitInterval()
        {
            var r = new SeededRandom(SeedB);
            for (int i = 0; i < SampleCount * SampleCount; i++)
            {
                double d = r.NextDouble();
                Assert.That(d, Is.GreaterThanOrEqualTo(0.0).And.LessThan(1.0));
            }
        }

        [Test]
        public void Range_InvalidBounds_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SeededRandom(SeedA).Range(1, 1));
        }
    }
}
