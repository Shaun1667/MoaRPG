using Moa.Core.Rng;
using NUnit.Framework;

namespace Moa.Tests
{
    /// <summary>규칙 코드(Moa.Core)는 엔진을 참조하지 않는다 (핵심 설계 규칙 1, MY_DEV_GUIDE 0번).</summary>
    public class CoreAssemblyTests
    {
        [Test]
        public void MoaCore_DoesNotReferenceUnity()
        {
            var asm = typeof(SeededRandom).Assembly;
            Assert.That(asm.GetName().Name, Is.EqualTo("Moa.Core"));
            foreach (var r in asm.GetReferencedAssemblies())
                Assert.That(r.Name.StartsWith("Unity"), Is.False, "Moa.Core references " + r.Name);
        }
    }
}
