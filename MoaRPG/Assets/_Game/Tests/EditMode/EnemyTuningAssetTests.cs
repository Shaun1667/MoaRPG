using Moa.Core.Combat;
using Moa.Unity.Data;
using NUnit.Framework;
using UnityEditor;

namespace Moa.Tests
{
    /// <summary>
    /// 2-5 수치 에셋 검산: 근접 졸병 Grunt.asset이 BALANCE.md 11번과 같고, 한손검 · 플레이어 수치와 맞는지 (값을 바꾸면 BALANCE.md와 이 테스트도 함께 고친다).
    /// </summary>
    public class EnemyTuningAssetTests
    {
        const string GruntPath = "Assets/_Game/Data/Enemies/Grunt.asset";
        const string SwordPath = "Assets/_Game/Data/Weapons/Sword.asset";
        const string HeroPath = "Assets/_Game/Data/Characters/SwordHero.asset"; // 2-6: 공격력 = 캐릭터 + 무기
        const string CombatPath = "Assets/_Game/Data/Combat/CombatTuning.asset";
        const string MovementPath = "Assets/_Game/Data/Player/MovementTuning.asset";
        const string StaminaPath = "Assets/_Game/Data/Player/StaminaTuning.asset";
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

        [Test]
        public void Grunt_MatchesBalance11()
        {
            var g = Load<EnemyTuning>(GruntPath);
            Assert.That(g.MaxHealth, Is.EqualTo(500));
            Assert.That(g.Defense, Is.EqualTo(100f).Within(Eps));
            Assert.That(g.EnergyOnDefeat, Is.EqualTo(5f).Within(Eps));
            Assert.That(g.CapsuleHeight, Is.EqualTo(1.4f).Within(Eps));
            Assert.That(g.CapsuleRadius, Is.EqualTo(0.4f).Within(Eps));
            Assert.That(g.EyeHeight, Is.EqualTo(1.1f).Within(Eps));
            Assert.That(g.Gravity, Is.EqualTo(25f).Within(Eps));
            Assert.That(g.FlashTime, Is.EqualTo(0.1f).Within(Eps));
            Assert.That(g.KnockbackDistance, Is.EqualTo(0.15f).Within(Eps));
            Assert.That(g.KnockbackTime, Is.EqualTo(0.06f).Within(Eps));
            Assert.That(g.NumberHeight, Is.EqualTo(1.7f).Within(Eps));
            Assert.That(g.MaxPoise, Is.EqualTo(60f).Within(Eps));
            Assert.That(g.GroggyDuration, Is.EqualTo(5f).Within(Eps), "GDD: groggy 5 s");
            Assert.That(g.GroggyDamageTaken, Is.EqualTo(1.3f).Within(Eps), "GDD: +30% damage");
            Assert.That(g.PoiseRegenDelay, Is.EqualTo(2f).Within(Eps));
            Assert.That(g.PoiseRegenPerSecond, Is.EqualTo(25f).Within(Eps));
            Assert.That(g.DetectRange, Is.EqualTo(10f).Within(Eps));
            Assert.That(g.ViewAngle, Is.EqualTo(120f).Within(Eps));
            Assert.That(g.NoticeRange, Is.EqualTo(3f).Within(Eps));
            Assert.That(g.LoseRange, Is.EqualTo(15f).Within(Eps));
            Assert.That(g.LoseSightTime, Is.EqualTo(2f).Within(Eps));
            Assert.That(g.LeashRange, Is.EqualTo(20f).Within(Eps));
            Assert.That(g.PatrolSpeed, Is.EqualTo(1.5f).Within(Eps));
            Assert.That(g.ChaseSpeed, Is.EqualTo(3.5f).Within(Eps));
            Assert.That(g.ReturnSpeed, Is.EqualTo(4f).Within(Eps));
            Assert.That(g.TurnSpeed, Is.EqualTo(360f).Within(Eps));
            Assert.That(g.PatrolWait, Is.EqualTo(2f).Within(Eps));
            Assert.That(g.ArriveDistance, Is.EqualTo(0.3f).Within(Eps));
            Assert.That(g.AttackRange, Is.EqualTo(2f).Within(Eps));
            Assert.That(g.HitRange, Is.EqualTo(2.6f).Within(Eps));
            Assert.That(g.HitAngle, Is.EqualTo(120f).Within(Eps));
            Assert.That(g.AttackInterval, Is.EqualTo(0.6f).Within(Eps));
            Assert.That(g.TelegraphTime, Is.EqualTo(0.8f).Within(Eps));
            Assert.That(g.RecoveryTime, Is.EqualTo(0.8f).Within(Eps));
            Assert.That(g.HitStunTime, Is.EqualTo(0.3f).Within(Eps));
            Assert.That(g.RespawnDelay, Is.EqualTo(5f).Within(Eps));
        }

        [Test]
        public void Grunt_IsConsistentWithSwordAndPlayer()
        {
            var g = Load<EnemyTuning>(GruntPath);
            var w = Load<WeaponTuning>(SwordPath);
            var c = Load<CombatTuning>(CombatPath);
            var m = Load<MovementTuning>(MovementPath);
            var s = Load<StaminaTuning>(StaminaPath);

            // 한손검 콤보 36 · 36 · 48 · 52 · 68 (허수아비와 같은 방어) — 2콤보(480) + 1타에 쓰러짐
            int[] expected = { 36, 36, 48, 52, 68 };
            int combo = 0;
            for (int i = 0; i < w.StepCount; i++)
            {
                int d = DamageMath.Compute(Load<CharacterData>(HeroPath).TotalAttack, w.GetStep(i).multiplier, g.Defense, c.DefenseConstant);
                Assert.That(d, Is.EqualTo(expected[i]), "hit " + (i + 1));
                combo += d;
            }
            Assert.That(combo * 2, Is.LessThan(g.MaxHealth));
            Assert.That(combo * 3, Is.GreaterThanOrEqualTo(g.MaxHealth));

            // 경직: 콤보 한 번(54)으로는 안 되고 다음 1타(+8)에 그로기
            float poise = 0f;
            for (int i = 0; i < w.StepCount; i++) poise += w.ComboPoise(i);
            Assert.That(poise, Is.LessThan(g.MaxPoise));
            Assert.That(poise + w.ComboPoise(0), Is.GreaterThanOrEqualTo(g.MaxPoise));

            // 쫓는 빠르기 < 플레이어 뛰기 (뛰면 떼어 낼 수 있다), 회피 한 번이면 공격 범위 밖
            Assert.That(g.ChaseSpeed, Is.LessThan(m.RunSpeed));
            Assert.That(g.PatrolSpeed, Is.LessThan(g.ChaseSpeed));
            Assert.That(g.AttackRange + s.DodgeDistance, Is.GreaterThan(g.HitRange));
            // 사거리 < 닿는 거리 (멈춘 자리에서 닿는다) · 가까이 알아챔 < 감지 < 놓침
            Assert.That(g.AttackRange, Is.LessThan(g.HitRange));
            Assert.That(g.NoticeRange, Is.LessThan(g.DetectRange));
            Assert.That(g.DetectRange, Is.LessThan(g.LoseRange));
            Assert.That(g.AttackRange, Is.GreaterThan(g.CapsuleRadius + m.CapsuleRadius), "stops before touching");
            // 예고는 저스트 창보다 길고, 시간은 고정 스텝 배수
            Assert.That(g.TelegraphTime, Is.GreaterThan(c.JustDodgeWindow));
            foreach (float v in new[] { g.AttackInterval, g.TelegraphTime, g.RecoveryTime, g.HitStunTime, g.PatrolWait, g.LoseSightTime, g.RespawnDelay, g.GroggyDuration })
                Assert.That(WholeSteps(v), Is.True, v + " s = whole fixed steps");
            // 경직은 공격 쉬기보다 짧다 (맞고 나서 다시 쫓는다)
            Assert.That(g.HitStunTime, Is.LessThan(g.AttackInterval));
        }
    }
}
