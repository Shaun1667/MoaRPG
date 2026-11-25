using Moa.Core.Combat;
using NUnit.Framework;

namespace Moa.Tests
{
    /// <summary>2-1 피해 공식 (DamageMath) · 체력 (Health). 값은 BALANCE 6 · 7 · 8번.</summary>
    public class DamageTests
    {
        const float DefenseConstant = 400f;

        [Test]
        public void Formula_AttackTimesMultiplier_ThenDefense()
        {
            // 100 × 0.45 = 45 → 방어 100 / (100 + 400) = 20% 감소 → 36
            Assert.That(DamageMath.Raw(100f, 0.45f), Is.EqualTo(45f).Within(1e-4f));
            Assert.That(DamageMath.DefenseReduction(100f, DefenseConstant), Is.EqualTo(0.2f).Within(1e-6f));
            Assert.That(DamageMath.Compute(100f, 0.45f, 100f, DefenseConstant), Is.EqualTo(36));
        }

        [Test]
        public void Formula_SwordCombo_OnDummy()
        {
            // BALANCE 8번 검산: 36 · 36 · 48 · 52 · 68 = 240 (허수아비 600 = 2.5 콤보)
            float[] mult = { 0.45f, 0.45f, 0.60f, 0.65f, 0.85f };
            int[] expected = { 36, 36, 48, 52, 68 };
            int sum = 0;
            for (int i = 0; i < mult.Length; i++)
            {
                int d = DamageMath.Compute(100f, mult[i], 100f, DefenseConstant);
                Assert.That(d, Is.EqualTo(expected[i]), "hit " + (i + 1));
                sum += d;
            }
            Assert.That(sum, Is.EqualTo(240));
        }

        [Test]
        public void Formula_DefenseEqualToConstant_Halves()
        {
            Assert.That(DamageMath.Compute(100f, 1f, DefenseConstant, DefenseConstant), Is.EqualTo(50));
        }

        [Test]
        public void Formula_NoDefense_IsRaw_RoundedHalfUp()
        {
            Assert.That(DamageMath.Compute(10f, 0.25f, 0f, DefenseConstant), Is.EqualTo(3)); // 2.5 → 3
            Assert.That(DamageMath.Compute(10f, 0.24f, 0f, DefenseConstant), Is.EqualTo(2)); // 2.4 → 2
        }

        [Test]
        public void Formula_MinimumOne_ZeroAttackIsZero()
        {
            Assert.That(DamageMath.Compute(1f, 0.1f, 100000f, DefenseConstant), Is.EqualTo(1));
            Assert.That(DamageMath.Compute(0f, 1f, 0f, DefenseConstant), Is.EqualTo(0));
            Assert.That(DamageMath.Compute(100f, 0f, 0f, DefenseConstant), Is.EqualTo(0));
        }

        [Test]
        public void Health_Damage_RaisesEvent_AndReduces()
        {
            var h = new Health(600);
            int got = -1, applied = -1, left = -1;
            h.Damaged += (a, b, c) => { got = a; applied = b; left = c; };
            Assert.That(h.TakeDamage(36), Is.EqualTo(36));
            Assert.That(h.Current, Is.EqualTo(564));
            Assert.That(got, Is.EqualTo(36));
            Assert.That(applied, Is.EqualTo(36));
            Assert.That(left, Is.EqualTo(564));
        }

        [Test]
        public void Health_ReachesZero_DiesOnce()
        {
            var h = new Health(100);
            int deaths = 0;
            h.Died += () => deaths++;
            h.TakeDamage(60);
            Assert.That(h.IsDead, Is.False);
            Assert.That(h.TakeDamage(60), Is.EqualTo(40), "overkill only takes what is left");
            Assert.That(h.Current, Is.EqualTo(0));
            Assert.That(h.IsDead, Is.True);
            Assert.That(deaths, Is.EqualTo(1));
            Assert.That(h.TakeDamage(10), Is.EqualTo(0), "dead: no more damage");
            Assert.That(deaths, Is.EqualTo(1));
        }

        [Test]
        public void Health_Restore_Revives()
        {
            var h = new Health(100);
            h.TakeDamage(100);
            h.Restore();
            Assert.That(h.IsDead, Is.False);
            Assert.That(h.Current, Is.EqualTo(100));
        }

        [Test]
        public void Health_Invulnerable_EventButNoLoss()
        {
            var h = new Health(100) { Invulnerable = true };
            int got = 0;
            h.Damaged += (a, b, c) => got = a;
            Assert.That(h.TakeDamage(50), Is.EqualTo(0));
            Assert.That(h.Current, Is.EqualTo(100));
            Assert.That(got, Is.EqualTo(50));
        }
    }
}
