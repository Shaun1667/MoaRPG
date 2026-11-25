using Moa.Core.Combat;
using Moa.Unity.Data;
using NUnit.Framework;
using UnityEditor;

namespace Moa.Tests
{
    /// <summary>
    /// 2-1 수치 에셋 검산: Sword.asset · CombatTuning.asset · TrainingDummy.asset 값이 BALANCE.md 6 · 7 · 8번과 같고 서로 맞는지
    /// (값을 바꾸면 BALANCE.md와 이 테스트도 함께 고친다).
    /// </summary>
    public class CombatTuningAssetTests
    {
        const string SwordPath = "Assets/_Game/Data/Weapons/Sword.asset";
        const string CombatPath = "Assets/_Game/Data/Combat/CombatTuning.asset";
        const string DummyPath = "Assets/_Game/Data/Enemies/TrainingDummy.asset";
        const string MovementPath = "Assets/_Game/Data/Player/MovementTuning.asset";
        const float Eps = 1e-4f;
        const float FixedStep = 0.02f;

        // BALANCE 6번 표: 배율 · 선딜 · 판정 · 후딜 · 입력 열림 · 내딛기
        static readonly float[,] SwordTable =
        {
            { 0.45f, 0.10f, 0.10f, 0.20f, 0.10f, 0.30f },
            { 0.45f, 0.10f, 0.10f, 0.22f, 0.10f, 0.30f },
            { 0.60f, 0.12f, 0.10f, 0.24f, 0.12f, 0.40f },
            { 0.65f, 0.12f, 0.12f, 0.26f, 0.12f, 0.40f },
            { 0.85f, 0.16f, 0.14f, 0.40f, 0.16f, 0.60f },
        };

        // BALANCE 6번 강공격 표 (2-2): 배율 · 선딜 · 판정 · 후딜 · 내딛기
        static readonly float[,] ChargedTable =
        {
            { 0.56f, 0.16f, 0.08f, 0.06f, 0.30f },
            { 0.72f, 0.08f, 0.10f, 0.36f, 0.30f },
        };

        static T Load<T>(string path) where T : UnityEngine.Object
        {
            var t = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.That(t, Is.Not.Null, "missing " + path);
            return t;
        }

        [Test]
        public void Sword_MatchesBalance6()
        {
            var w = Load<WeaponTuning>(SwordPath);
            Assert.That(w.BaseAttack, Is.EqualTo(100f).Within(Eps));
            Assert.That(w.ComboResetTime, Is.EqualTo(0.5f).Within(Eps));
            Assert.That(w.AttackMoveFactor, Is.EqualTo(0f).Within(Eps));
            Assert.That(w.HitRange, Is.EqualTo(2.2f).Within(Eps));
            Assert.That(w.HitAngle, Is.EqualTo(120f).Within(Eps));
            Assert.That(w.HitHeight, Is.EqualTo(1.0f).Within(Eps));
            Assert.That(w.StepCount, Is.EqualTo(SwordTable.GetLength(0)));
            for (int i = 0; i < w.StepCount; i++)
            {
                WeaponTuning.Step s = w.GetStep(i);
                string at = "hit " + (i + 1);
                Assert.That(s.multiplier, Is.EqualTo(SwordTable[i, 0]).Within(Eps), at);
                Assert.That(s.windup, Is.EqualTo(SwordTable[i, 1]).Within(Eps), at);
                Assert.That(s.active, Is.EqualTo(SwordTable[i, 2]).Within(Eps), at);
                Assert.That(s.recovery, Is.EqualTo(SwordTable[i, 3]).Within(Eps), at);
                Assert.That(s.inputOpen, Is.EqualTo(SwordTable[i, 4]).Within(Eps), at);
                Assert.That(s.lunge, Is.EqualTo(SwordTable[i, 5]).Within(Eps), at);
            }
        }

        [Test]
        public void Combat_MatchesBalance7()
        {
            var c = Load<CombatTuning>(CombatPath);
            Assert.That(c.DefenseConstant, Is.EqualTo(400f).Within(Eps));
            Assert.That(c.AutoAimRange, Is.EqualTo(5f).Within(Eps));
            Assert.That(c.DamageFontSize, Is.EqualTo(36));
            Assert.That(c.DamageLifetime, Is.EqualTo(0.8f).Within(Eps));
            Assert.That(c.DamageRise, Is.EqualTo(0.8f).Within(Eps));
            Assert.That(c.DamageFadeTime, Is.EqualTo(0.3f).Within(Eps));
            Assert.That(c.DamageSpread, Is.EqualTo(24f).Within(Eps));
        }

        [Test]
        public void Dummy_MatchesBalance8()
        {
            var d = Load<DummyTuning>(DummyPath);
            Assert.That(d.MaxHealth, Is.EqualTo(600));
            Assert.That(d.Defense, Is.EqualTo(100f).Within(Eps));
            Assert.That(d.RespawnDelay, Is.EqualTo(3f).Within(Eps));
            Assert.That(d.KnockbackDistance, Is.EqualTo(0.15f).Within(Eps));
            Assert.That(d.KnockbackTime, Is.EqualTo(0.06f).Within(Eps));
            Assert.That(d.FlashTime, Is.EqualTo(0.1f).Within(Eps));
            Assert.That(d.NumberHeight, Is.EqualTo(2.0f).Within(Eps));
        }

        [Test]
        public void Sword_ChargedAndPlunge_MatchBalance6()
        {
            var w = Load<WeaponTuning>(SwordPath);
            Assert.That(w.ChargedStaminaCost, Is.EqualTo(20f).Within(Eps));
            Assert.That(w.ChargedHitRange, Is.EqualTo(2.6f).Within(Eps));
            Assert.That(w.ChargedHitAngle, Is.EqualTo(180f).Within(Eps));
            Assert.That(w.PlungeLowMultiplier, Is.EqualTo(1.28f).Within(Eps));
            Assert.That(w.PlungeHighMultiplier, Is.EqualTo(1.60f).Within(Eps));
            Assert.That(w.PlungeRange, Is.EqualTo(2.5f).Within(Eps));
            Assert.That(w.ChargedSlashCount, Is.EqualTo(ChargedTable.GetLength(0)));
            for (int i = 0; i < w.ChargedSlashCount; i++)
            {
                WeaponTuning.Step s = w.GetChargedSlash(i);
                string at = "charged slash " + (i + 1);
                Assert.That(s.multiplier, Is.EqualTo(ChargedTable[i, 0]).Within(Eps), at);
                Assert.That(s.windup, Is.EqualTo(ChargedTable[i, 1]).Within(Eps), at);
                Assert.That(s.active, Is.EqualTo(ChargedTable[i, 2]).Within(Eps), at);
                Assert.That(s.recovery, Is.EqualTo(ChargedTable[i, 3]).Within(Eps), at);
                Assert.That(s.lunge, Is.EqualTo(ChargedTable[i, 4]).Within(Eps), at);
            }
        }

        [Test]
        public void Combat_ChargeAndPlunge_MatchBalance7()
        {
            var c = Load<CombatTuning>(CombatPath);
            Assert.That(c.ChargeHoldTime, Is.EqualTo(0.36f).Within(Eps));
            Assert.That(c.PlungeMinHeight, Is.EqualTo(1.0f).Within(Eps));
            Assert.That(c.PlungeHighHeight, Is.EqualTo(5.0f).Within(Eps));
            Assert.That(c.PlungeFallSpeed, Is.EqualTo(20f).Within(Eps));
            Assert.That(c.PlungeLandRecovery, Is.EqualTo(0.40f).Within(Eps));
        }

        [Test]
        public void ChargedAndPlunge_AreConsistent()
        {
            var w = Load<WeaponTuning>(SwordPath);
            var c = Load<CombatTuning>(CombatPath);
            var d = Load<DummyTuning>(DummyPath);
            var m = Load<MovementTuning>(MovementPath);
            float total = 0f;
            int damage = 0;
            for (int i = 0; i < w.ChargedSlashCount; i++)
            {
                WeaponTuning.Step s = w.GetChargedSlash(i);
                string at = "charged slash " + (i + 1);
                Assert.That(s.active, Is.GreaterThanOrEqualTo(FixedStep), at);
                foreach (float v in new[] { s.windup, s.active, s.recovery })
                {
                    float steps = v / FixedStep;
                    Assert.That(steps, Is.EqualTo((float)System.Math.Round(steps)).Within(1e-3f), at + ": times are whole fixed steps");
                }
                total += s.windup + s.active + s.recovery;
                damage += DamageMath.Compute(w.BaseAttack, s.multiplier, d.Defense, c.DefenseConstant);
            }
            Assert.That(total, Is.EqualTo(0.84f).Within(1e-3f));
            Assert.That(damage, Is.EqualTo(45 + 58), "100 x 0.56 / 0.72 against defense 100");
            float holdSteps = c.ChargeHoldTime / FixedStep;
            Assert.That(holdSteps, Is.EqualTo(18f).Within(1e-3f), "hold = 18 fixed steps");
            Assert.That(c.PlungeLandRecovery / FixedStep, Is.EqualTo((float)System.Math.Round(c.PlungeLandRecovery / FixedStep)).Within(1e-3f));
            // 평지 점프 정점(jumpHeight)에서도 낙하가 되고, 높은 낙하는 그보다 훨씬 높다
            Assert.That(c.PlungeMinHeight, Is.LessThan(m.JumpHeight));
            Assert.That(c.PlungeHighHeight, Is.GreaterThan(m.JumpHeight));
            Assert.That(c.PlungeFallSpeed, Is.LessThanOrEqualTo(m.MaxFallSpeed));
            Assert.That(w.PlungeHighMultiplier, Is.GreaterThan(w.PlungeLowMultiplier));
            Assert.That(DamageMath.Compute(w.BaseAttack, w.PlungeLowMultiplier, d.Defense, c.DefenseConstant), Is.EqualTo(102));
            Assert.That(DamageMath.Compute(w.BaseAttack, w.PlungeHighMultiplier, d.Defense, c.DefenseConstant), Is.EqualTo(128));
            Assert.That(w.PlungeRange, Is.GreaterThan(w.HitRange), "landing area is wider than a swing");
        }

        [Test]
        public void Sword_IsConsistent()
        {
            var w = Load<WeaponTuning>(SwordPath);
            var c = Load<CombatTuning>(CombatPath);
            var d = Load<DummyTuning>(DummyPath);
            var m = Load<MovementTuning>(MovementPath);
            float total = 0f;
            int comboDamage = 0;
            for (int i = 0; i < w.StepCount; i++)
            {
                WeaponTuning.Step s = w.GetStep(i);
                string at = "hit " + (i + 1);
                Assert.That(s.active, Is.GreaterThanOrEqualTo(FixedStep), at + ": hit window covers at least one fixed step");
                Assert.That(s.inputOpen, Is.LessThan(s.windup + s.active + s.recovery), at + ": input window is inside the step");
                foreach (float v in new[] { s.windup, s.active, s.recovery })
                {
                    float steps = v / FixedStep;
                    Assert.That(steps, Is.EqualTo((float)System.Math.Round(steps)).Within(1e-3f), at + ": times are whole fixed steps");
                }
                total += s.windup + s.active + s.recovery;
                comboDamage += DamageMath.Compute(w.BaseAttack, s.multiplier, d.Defense, c.DefenseConstant);
            }
            // 원신 한손검 체감: 5타 약 2.5초, 허수아비는 2 ~ 3 콤보에 쓰러진다
            Assert.That(total, Is.EqualTo(2.48f).Within(1e-3f));
            Assert.That(comboDamage, Is.EqualTo(240));
            Assert.That(d.MaxHealth / (float)comboDamage, Is.InRange(2f, 3f));
            Assert.That(w.HitRange, Is.GreaterThan(m.CapsuleRadius * 2f), "reach beyond touching distance");
            Assert.That(c.AutoAimRange, Is.GreaterThan(w.HitRange));
        }
    }
}
