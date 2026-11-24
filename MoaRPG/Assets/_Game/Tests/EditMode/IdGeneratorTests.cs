using System.Collections.Generic;
using Moa.Core.Ids;
using NUnit.Framework;

namespace Moa.Tests
{
    /// <summary>고유 ID 발급기 (핵심 설계 규칙 5).</summary>
    public class IdGeneratorTests
    {
        const int SampleCount = 1000;

        [Test]
        public void Ids_AreUniqueAndValid()
        {
            var gen = new IdGenerator();
            var seen = new HashSet<EntityId>();
            for (int i = 0; i < SampleCount; i++)
            {
                var id = gen.Next();
                Assert.That(id.IsValid, Is.True);
                Assert.That(seen.Add(id), Is.True, "duplicate " + id);
            }
            Assert.That(gen.IssuedCount, Is.EqualTo(SampleCount));
        }

        [Test]
        public void FirstId_IsFirstValue_AndNoneIsInvalid()
        {
            Assert.That(new IdGenerator().Next().Value, Is.EqualTo(IdGenerator.FirstValue));
            Assert.That(EntityId.None.IsValid, Is.False);
        }

        [Test]
        public void SameStart_SameIds()
        {
            var a = new IdGenerator();
            var b = new IdGenerator();
            for (int i = 0; i < SampleCount; i++) Assert.That(a.Next(), Is.EqualTo(b.Next()));
        }

        [Test]
        public void Restore_FromNextValue_ContinuesWithoutReuse()
        {
            var gen = new IdGenerator();
            var issued = new HashSet<EntityId>();
            for (int i = 0; i < SampleCount; i++) issued.Add(gen.Next());
            var restored = new IdGenerator(gen.NextValue);
            for (int i = 0; i < SampleCount; i++)
            {
                var id = restored.Next();
                Assert.That(issued.Contains(id), Is.False, "reused " + id);
                Assert.That(id, Is.EqualTo(gen.Next()));
            }
        }

        [Test]
        public void Restore_BelowFirstValue_Throws()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new IdGenerator(IdGenerator.FirstValue - 1));
        }
    }
}
