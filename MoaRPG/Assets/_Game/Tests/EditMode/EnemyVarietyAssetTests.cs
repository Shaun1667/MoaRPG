using Moa.Core.Combat;
using Moa.Unity.Data;
using NUnit.Framework;
using UnityEditor;

namespace Moa.Tests
{
    /// <summary>
    /// 3-4 수치 에셋 검산: Grunt(공격 한 줄로 옮겨도 2-5 값 그대로) · Archer · Brute가 BALANCE.md 16번과 같고 서로 · 플레이어 수치와 맞는지.
    /// </summary>
    public class EnemyVarietyAssetTests
    {
        const string GruntPath = "Assets/_Game/Data/Enemies/Grunt.asset";
        const string ArcherPath = "Assets/_Game/Data/Enemies/Archer.asset";
        const string BrutePath = "Assets/_Game/Data/Enemies/Brute.asset";
        const string SwordPath = "Assets/_Game/Data/Weapons/Sword.asset";
        const string HeroPath = "Assets/_Game/Data/Characters/SwordHero.asset";
        const string CombatPath = "Assets/_Game/Data/Combat/CombatTuning.asset";
        const string MovementPath = "Assets/_Game/Data/Player/MovementTuning.asset";
        const string StaminaPath = "Assets/_Game/Data/Player/StaminaTuning.asset";
        const string NavMeshPath = "Assets/_Game/Scenes/TestField/NavMesh-Environment.asset";
        const float Eps = 1e-4f;
        const float FixedStep = 0.02f;

        static T Load<T>(string path) where T : UnityEngine.Object
        {
            var t = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.That(t, Is.Not.Null, "missing " + path);
            return t;
        }

        static bool WholeSteps(float seconds)
        {
            float steps = seconds / FixedStep;
            return System.Math.Abs(steps - System.Math.Round(steps)) < 1e-3;
        }

        static void Row(EnemyTuning.AttackRow r, float mult, float start, float range, float angle, float interval, float telegraph,
            float active, float lunge, float recovery, float knockdown, HitDelivery delivery)
        {
            Assert.That(r, Is.Not.Null);
            Assert.That(r.multiplier, Is.EqualTo(mult).Within(Eps), r.debugName + " multiplier");
            Assert.That(r.startRange, Is.EqualTo(start).Within(Eps), r.debugName + " start");
            Assert.That(r.hitRange, Is.EqualTo(range).Within(Eps), r.debugName + " range");
            Assert.That(r.hitAngle, Is.EqualTo(angle).Within(Eps), r.debugName + " angle");
            Assert.That(r.interval, Is.EqualTo(interval).Within(Eps), r.debugName + " interval");
            Assert.That(r.telegraph, Is.EqualTo(telegraph).Within(Eps), r.debugName + " telegraph");
            Assert.That(r.activeTime, Is.EqualTo(active).Within(Eps), r.debugName + " active");
            Assert.That(r.lungeDistance, Is.EqualTo(lunge).Within(Eps), r.debugName + " lunge");
            Assert.That(r.recovery, Is.EqualTo(recovery).Within(Eps), r.debugName + " recovery");
            Assert.That(r.knockdownTime, Is.EqualTo(knockdown).Within(Eps), r.debugName + " knockdown");
            Assert.That(r.delivery, Is.EqualTo(delivery), r.debugName + " delivery");
        }

        [Test]
        public void Grunt_OneRow_SameAs25()
        {
            var g = Load<EnemyTuning>(GruntPath);
            Assert.That(g.Rank, Is.EqualTo(EnemyRank.Common));
            Assert.That(g.AttackCount, Is.EqualTo(1));
            Row(g.GetAttack(0), 1.25f, 2f, 2.6f, 120f, 0.6f, 0.8f, 0f, 0f, 0.8f, 0f, HitDelivery.Melee);
            Assert.That(g.RetreatRange, Is.EqualTo(0f));
            Assert.That(g.HitStunTime, Is.EqualTo(0.3f).Within(Eps));
            var rules = g.ToAiRules();
            Assert.That(rules.AttackRange, Is.EqualTo(2f).Within(Eps));
            Assert.That(rules.Attack.Period, Is.EqualTo(2.2f).Within(Eps), "2-5: next attack every 2.2 s");
        }

        [Test]
        public void Archer_MatchesBalance16()
        {
            var a = Load<EnemyTuning>(ArcherPath);
            Assert.That(a.Rank, Is.EqualTo(EnemyRank.Common));
            Assert.That(a.MaxHealth, Is.EqualTo(350));
            Assert.That(a.Defense, Is.EqualTo(100f).Within(Eps));
            Assert.That(a.EnergyOnDefeat, Is.EqualTo(5f).Within(Eps));
            Assert.That(a.CapsuleHeight, Is.EqualTo(1.4f).Within(Eps));
            Assert.That(a.CapsuleRadius, Is.EqualTo(0.4f).Within(Eps));
            Assert.That(a.MaxPoise, Is.EqualTo(40f).Within(Eps));
            Assert.That(a.GroggyDuration, Is.EqualTo(5f).Within(Eps));
            Assert.That(a.DetectRange, Is.EqualTo(14f).Within(Eps));
            Assert.That(a.LoseRange, Is.EqualTo(18f).Within(Eps));
            Assert.That(a.ChaseSpeed, Is.EqualTo(3f).Within(Eps));
            Assert.That(a.RetreatRange, Is.EqualTo(5f).Within(Eps));
            Assert.That(a.RetreatSpeed, Is.EqualTo(2.5f).Within(Eps));
            Assert.That(a.Attack, Is.EqualTo(100f).Within(Eps));
            Assert.That(a.HitStunTime, Is.EqualTo(0.3f).Within(Eps));
            Assert.That(a.AttackCount, Is.EqualTo(1));
            var r = a.GetAttack(0);
            Row(r, 0.9f, 12f, 15f, 0f, 1.0f, 1.0f, 0f, 0f, 0.6f, 0f, HitDelivery.Projectile);
            Assert.That(r.projectileSpeed, Is.EqualTo(15f).Within(Eps));
            Assert.That(r.projectileLifetime, Is.EqualTo(1f).Within(Eps));
            Assert.That(r.projectileRadius, Is.EqualTo(0.2f).Within(Eps));
        }

        [Test]
        public void Brute_MatchesBalance16()
        {
            var b = Load<EnemyTuning>(BrutePath);
            Assert.That(b.Rank, Is.EqualTo(EnemyRank.Elite));
            Assert.That(b.MaxHealth, Is.EqualTo(1600));
            Assert.That(b.Defense, Is.EqualTo(100f).Within(Eps));
            Assert.That(b.EnergyOnDefeat, Is.EqualTo(10f).Within(Eps));
            Assert.That(b.CapsuleHeight, Is.EqualTo(2.6f).Within(Eps));
            Assert.That(b.CapsuleRadius, Is.EqualTo(0.75f).Within(Eps));
            Assert.That(b.MaxPoise, Is.EqualTo(150f).Within(Eps));
            Assert.That(b.ChaseSpeed, Is.EqualTo(2.6f).Within(Eps));
            Assert.That(b.TurnSpeed, Is.EqualTo(240f).Within(Eps));
            Assert.That(b.HitStunTime, Is.EqualTo(0f), "elite: hits do not stop it");
            Assert.That(b.RespawnDelay, Is.EqualTo(8f).Within(Eps));
            Assert.That(b.AttackCount, Is.EqualTo(2));
            Row(b.GetAttack(0), 2.0f, 3f, 3.5f, 360f, 0.8f, 1.2f, 0f, 0f, 1.2f, 1.2f, HitDelivery.Melee);
            Row(b.GetAttack(1), 1.5f, 9f, 1.8f, 120f, 0.6f, 0.8f, 0.6f, 7.2f, 1.0f, 0f, HitDelivery.Melee);
        }

        [Test]
        public void Brute_HeavierPoise_SameGroggy()
        {
            var g = Load<EnemyTuning>(GruntPath);
            var b = Load<EnemyTuning>(BrutePath);
            var w = Load<WeaponTuning>(SwordPath);
            Assert.That(b.GroggyDuration, Is.EqualTo(g.GroggyDuration).Within(Eps), "groggy time the same (GDD 5 s)");
            Assert.That(b.GroggyDamageTaken, Is.EqualTo(g.GroggyDamageTaken).Within(Eps));
            float combo = 0f;
            for (int i = 0; i < w.StepCount; i++) combo += w.ComboPoise(i);
            Assert.That(combo * 2f, Is.LessThan(b.MaxPoise), "two sword combos are not enough");
            Assert.That(combo * 3f, Is.GreaterThanOrEqualTo(b.MaxPoise), "three are");
            Assert.That(b.CapsuleHeight, Is.GreaterThan(g.CapsuleHeight));
        }

        [Test]
        public void Damage_OnHero_AndKnockdownFits()
        {
            var hero = Load<CharacterData>(HeroPath);
            var c = Load<CombatTuning>(CombatPath);
            var a = Load<EnemyTuning>(ArcherPath);
            var b = Load<EnemyTuning>(BrutePath);
            Assert.That(DamageMath.Compute(a.Attack, a.GetAttack(0).multiplier, hero.Defense, c.DefenseConstant), Is.EqualTo(72), "arrow");
            Assert.That(DamageMath.Compute(b.Attack, b.GetAttack(0).multiplier, hero.Defense, c.DefenseConstant), Is.EqualTo(160), "slam");
            Assert.That(DamageMath.Compute(b.Attack, b.GetAttack(1).multiplier, hero.Defense, c.DefenseConstant), Is.EqualTo(120), "charge");
            // 넘어짐은 다음 공격(돌진)이 나가기 전에 끝난다 — 넘어진 채 또 맞지 않게
            var slam = b.GetAttack(0);
            var charge = b.GetAttack(1);
            Assert.That(slam.knockdownTime, Is.LessThan(slam.recovery + charge.interval + charge.telegraph));
            Assert.That(slam.knockdownTime, Is.GreaterThan(Load<PlayerHealthTuning>("Assets/_Game/Data/Player/PlayerHealthTuning.asset").HitStunTime));
        }

        [Test]
        public void Rows_WholeSteps_AndRangesConsistent()
        {
            var m = Load<MovementTuning>(MovementPath);
            var s = Load<StaminaTuning>(StaminaPath);
            var c = Load<CombatTuning>(CombatPath);
            foreach (var path in new[] { GruntPath, ArcherPath, BrutePath })
            {
                var t = Load<EnemyTuning>(path);
                Assert.That(t.ChaseSpeed, Is.LessThan(m.RunSpeed), path + ": the player can outrun it");
                for (int i = 0; i < t.AttackCount; i++)
                {
                    var r = t.GetAttack(i);
                    foreach (float v in new[] { r.interval, r.telegraph, r.activeTime, r.recovery, r.knockdownTime })
                        Assert.That(WholeSteps(v), Is.True, path + " " + r.debugName + ": " + v + " s = whole fixed steps");
                    Assert.That(r.telegraph, Is.GreaterThan(c.JustDodgeWindow), path + " " + r.debugName + ": warning longer than the just window");
                    if (r.delivery == HitDelivery.Projectile)
                        Assert.That(r.projectileSpeed * r.projectileLifetime, Is.GreaterThanOrEqualTo(r.startRange), "arrow reaches from where it shoots");
                }
            }
            var a = Load<EnemyTuning>(ArcherPath);
            Assert.That(a.RetreatRange, Is.LessThan(a.AttackRange), "backs off inside its shooting range");
            Assert.That(a.RetreatSpeed, Is.LessThan(m.RunSpeed), "the player can close in");
            var charge = Load<EnemyTuning>(BrutePath).GetAttack(1);
            Assert.That(charge.lungeDistance + charge.hitRange, Is.GreaterThanOrEqualTo(charge.startRange), "the charge reaches from where it starts");
            Assert.That(s.DodgeDistance, Is.GreaterThan(charge.hitRange), "one dodge sideways leaves the charge lane");
        }

        [Test]
        public void TestField_NavMesh_IsBaked()
        {
            var data = Load<UnityEngine.AI.NavMeshData>(NavMeshPath);
            Assert.That(data.sourceBounds.size.x, Is.GreaterThan(50f), "covers the ground (Setup bakes Environment)");
        }
    }
}
