using Moa.Core.Combat;
using Moa.Unity.Data;
using NUnit.Framework;
using UnityEditor;

namespace Moa.Tests
{
    /// <summary>
    /// 3-3 수치 에셋 검산: 대검(Claymore.asset) · 활(Bow.asset)이 BALANCE.md 15번과 같고, 한손검과의 관계가 맞는지
    /// (대검 1타 피해 · 경직 &gt; 한손검, 활 한 발 &lt; 한손검, DPS 비교), 캐릭터 무기(영웅 한손검 그대로 · 수호자 대검 · 떠돌이 활)와 바뀐 E · Q.
    /// 값을 바꾸면 BALANCE.md와 이 테스트도 함께 고친다.
    /// </summary>
    public class WeaponKindAssetTests
    {
        const string SwordPath = "Assets/_Game/Data/Weapons/Sword.asset";
        const string ClaymorePath = "Assets/_Game/Data/Weapons/Claymore.asset";
        const string BowPath = "Assets/_Game/Data/Weapons/Bow.asset";
        const string PartyPath = "Assets/_Game/Data/Characters/Party.asset";
        const string CombatPath = "Assets/_Game/Data/Combat/CombatTuning.asset";
        const string DummyPath = "Assets/_Game/Data/Enemies/TrainingDummy.asset";
        const string GruntPath = "Assets/_Game/Data/Enemies/Grunt.asset";
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

        static int Hit(float attack, float multiplier)
        {
            var c = Load<CombatTuning>(CombatPath);
            var d = Load<DummyTuning>(DummyPath);
            return DamageMath.Compute(attack, multiplier, d.Defense, c.DefenseConstant);
        }

        static float ComboTime(WeaponTuning w) { float t = 0f; for (int i = 0; i < w.StepCount; i++) { var s = w.GetStep(i); t += s.windup + s.active + s.recovery; } return t; }
        static float ComboMultiplier(WeaponTuning w) { float m = 0f; for (int i = 0; i < w.StepCount; i++) m += w.GetStep(i).multiplier; return m; }
        static float ComboPoise(WeaponTuning w) { float p = 0f; for (int i = 0; i < w.StepCount; i++) p += w.ComboPoise(i); return p; }

        static void AllWholeSteps(WeaponTuning w)
        {
            for (int i = 0; i < w.StepCount; i++) { var s = w.GetStep(i); Assert.That(WholeSteps(s.windup) && WholeSteps(s.active) && WholeSteps(s.recovery) && WholeSteps(s.inputOpen), Is.True, w.name + " step " + (i + 1)); }
            for (int i = 0; i < w.ChargedSlashCount; i++) { var s = w.GetChargedSlash(i); Assert.That(WholeSteps(s.windup) && WholeSteps(s.active) && WholeSteps(s.recovery), Is.True, w.name + " charged " + (i + 1)); }
            var e = w.EntrySlash;
            Assert.That(WholeSteps(e.windup) && WholeSteps(e.active) && WholeSteps(e.recovery), Is.True, w.name + " entry");
            Assert.That(WholeSteps(w.ChargedAimTime) && WholeSteps(w.ProjectileLifetime), Is.True, w.name);
        }

        [Test]
        public void Claymore_MatchesBalance15()
        {
            var w = Load<WeaponTuning>(ClaymorePath);
            Assert.That(w.Kind, Is.EqualTo(WeaponKind.Claymore));
            Assert.That(w.BaseAttack, Is.EqualTo(40f).Within(Eps));
            Assert.That(w.StepCount, Is.EqualTo(4));
            float[] m = { 0.80f, 0.85f, 1.00f, 1.35f };
            float[] poise = { 18f, 20f, 24f, 32f };
            for (int i = 0; i < 4; i++)
            {
                Assert.That(w.GetStep(i).multiplier, Is.EqualTo(m[i]).Within(Eps), "hit " + (i + 1));
                Assert.That(w.ComboPoise(i), Is.EqualTo(poise[i]).Within(Eps), "hit " + (i + 1));
            }
            Assert.That(ComboTime(w), Is.EqualTo(3.26f).Within(1e-3f));
            Assert.That(w.ComboDelivery, Is.EqualTo(HitDelivery.Melee));
            Assert.That(w.ChargedDelivery, Is.EqualTo(HitDelivery.Melee));
            Assert.That(w.EntryDelivery, Is.EqualTo(HitDelivery.Melee));
            Assert.That(w.UsesProjectiles, Is.False);
            // 회전 베기
            Assert.That(w.ChargedSpinDrainPerSecond, Is.EqualTo(40f).Within(Eps), "Genshin claymore: 40 / s");
            Assert.That(w.ChargedAimTime, Is.EqualTo(0f));
            Assert.That(w.ChargedStaminaCost, Is.EqualTo(10f).Within(Eps));
            Assert.That(w.ChargedSlashCount, Is.EqualTo(2), "spin row + finisher");
            Assert.That(w.GetChargedSlash(0).multiplier, Is.EqualTo(0.50f).Within(Eps));
            Assert.That(w.GetChargedSlash(1).multiplier, Is.EqualTo(1.10f).Within(Eps));
            Assert.That(w.ChargedHitAngle, Is.EqualTo(360f).Within(Eps));
            var r = w.ToChargedRules();
            Assert.That(r.Spins, Is.True);
            Assert.That(r.FinisherIndex, Is.EqualTo(1));
            Assert.That(w.EntrySlash.multiplier, Is.EqualTo(1.60f).Within(Eps));
            Assert.That(w.EntryPoise, Is.EqualTo(40f).Within(Eps));
            AllWholeSteps(w);
        }

        [Test]
        public void Bow_MatchesBalance15()
        {
            var w = Load<WeaponTuning>(BowPath);
            Assert.That(w.Kind, Is.EqualTo(WeaponKind.Bow));
            Assert.That(w.BaseAttack, Is.EqualTo(40f).Within(Eps));
            float[] m = { 0.30f, 0.30f, 0.38f, 0.40f, 0.52f };
            float[] poise = { 5f, 5f, 6f, 7f, 9f };
            Assert.That(w.StepCount, Is.EqualTo(5));
            for (int i = 0; i < 5; i++)
            {
                Assert.That(w.GetStep(i).multiplier, Is.EqualTo(m[i]).Within(Eps), "shot " + (i + 1));
                Assert.That(w.ComboPoise(i), Is.EqualTo(poise[i]).Within(Eps), "shot " + (i + 1));
            }
            Assert.That(ComboTime(w), Is.EqualTo(1.98f).Within(1e-3f));
            Assert.That(w.ComboDelivery, Is.EqualTo(HitDelivery.Projectile));
            Assert.That(w.ChargedDelivery, Is.EqualTo(HitDelivery.Projectile));
            Assert.That(w.EntryDelivery, Is.EqualTo(HitDelivery.Projectile));
            Assert.That(w.Delivery(AttackMode.Plunge), Is.EqualTo(HitDelivery.Melee), "plunge is always a landing area");
            var p = w.ToProjectileRules();
            Assert.That(p.Speed, Is.EqualTo(30f).Within(Eps));
            Assert.That(p.Lifetime, Is.EqualTo(0.6f).Within(Eps));
            Assert.That(p.Radius, Is.EqualTo(0.15f).Within(Eps));
            Assert.That(p.Range, Is.EqualTo(18f).Within(1e-3f));
            Assert.That(w.AutoAimRange, Is.EqualTo(15f).Within(Eps));
            Assert.That(p.Range, Is.GreaterThan(w.AutoAimRange), "an auto-aimed target is in reach");
            // 조준 사격
            Assert.That(w.ChargedAimTime, Is.EqualTo(0.8f).Within(Eps));
            Assert.That(w.ChargedStaminaCost, Is.EqualTo(0f), "bow aiming costs no stamina (Genshin)");
            Assert.That(w.ChargedSpinDrainPerSecond, Is.EqualTo(0f));
            Assert.That(w.ChargedSlashCount, Is.EqualTo(1));
            Assert.That(w.GetChargedSlash(0).multiplier, Is.EqualTo(1.60f).Within(Eps));
            Assert.That(w.ChargedPoise(0), Is.EqualTo(30f).Within(Eps));
            Assert.That(w.ToChargedRules().Aimed, Is.True);
            // 등장: 뒤로 뛰며 쏘기
            Assert.That(w.EntrySlash.multiplier, Is.EqualTo(1.00f).Within(Eps));
            Assert.That(w.EntrySlash.lunge, Is.EqualTo(-2.0f).Within(Eps), "hops back 2 m");
            AllWholeSteps(w);
        }

        [Test]
        public void Claymore_HitsHarder_Bow_HitsLighter_ThanSword()
        {
            var sword = Load<WeaponTuning>(SwordPath);
            var claymore = Load<WeaponTuning>(ClaymorePath);
            var bow = Load<WeaponTuning>(BowPath);
            Assert.That(sword.Kind, Is.EqualTo(WeaponKind.Sword));
            Assert.That(sword.UsesProjectiles, Is.False, "the sword stays melee");
            // 1타: 피해 · 경직
            Assert.That(claymore.GetStep(0).multiplier, Is.GreaterThan(sword.GetStep(0).multiplier));
            Assert.That(claymore.ComboPoise(0), Is.GreaterThan(sword.ComboPoise(0)));
            Assert.That(Hit(100f, claymore.GetStep(0).multiplier), Is.EqualTo(64), "sword hit 1 is 36");
            Assert.That(ComboPoise(claymore), Is.EqualTo(94f).Within(Eps));
            Assert.That(ComboPoise(claymore), Is.GreaterThan(ComboPoise(sword)));
            Assert.That(claymore.GetStep(0).windup, Is.GreaterThan(sword.GetStep(0).windup), "slower");
            // 활 한 발은 한손검 한 번보다 약하다
            for (int i = 0; i < bow.StepCount; i++)
                Assert.That(bow.GetStep(i).multiplier, Is.LessThan(sword.GetStep(System.Math.Min(i, sword.StepCount - 1)).multiplier), "shot " + (i + 1));
            // DPS (배율 / 초): 대검 ≈ 한손검, 활 연사 < 한손검, 활 조준 > 활 연사
            float swordDps = ComboMultiplier(sword) / ComboTime(sword);
            float claymoreDps = ComboMultiplier(claymore) / ComboTime(claymore);
            float bowDps = ComboMultiplier(bow) / ComboTime(bow);
            var c = Load<CombatTuning>(CombatPath);
            var aim = bow.GetChargedSlash(0);
            float aimCycle = c.ChargeHoldTime + bow.ChargedAimTime + aim.windup + aim.active + aim.recovery;
            float aimDps = aim.multiplier / aimCycle;
            Assert.That(swordDps, Is.EqualTo(1.21f).Within(0.01f));
            Assert.That(claymoreDps, Is.EqualTo(1.23f).Within(0.01f));
            Assert.That(bowDps, Is.EqualTo(0.96f).Within(0.01f));
            Assert.That(aimDps, Is.EqualTo(1.07f).Within(0.01f));
            Assert.That(bowDps, Is.LessThan(swordDps), "range costs damage");
            Assert.That(aimDps, Is.GreaterThan(bowDps), "aimed shots pay off");
            Assert.That(System.Math.Abs(claymoreDps - swordDps), Is.LessThan(0.05f), "claymore: same DPS, bigger hits");
        }

        [Test]
        public void ClaymoreCombo_GroggiesGrunt_OnHit3()
        {
            var w = Load<WeaponTuning>(ClaymorePath);
            var g = Load<EnemyTuning>(GruntPath);
            Assert.That(w.ComboPoise(0) + w.ComboPoise(1), Is.LessThan(g.MaxPoise));
            Assert.That(w.ComboPoise(0) + w.ComboPoise(1) + w.ComboPoise(2), Is.GreaterThanOrEqualTo(g.MaxPoise), "62 >= 60 (the sword needs one full combo + 1)");
        }

        [Test]
        public void Characters_Weapons_Slot1SwordUnchanged_GuardianClaymore_WandererBow()
        {
            var p = Load<PartyTuning>(PartyPath);
            Assert.That(p.GetMember(0).Weapon, Is.SameAs(Load<WeaponTuning>(SwordPath)), "hero keeps the sword");
            Assert.That(p.GetMember(1).Weapon, Is.SameAs(Load<WeaponTuning>(ClaymorePath)));
            Assert.That(p.GetMember(2).Weapon, Is.SameAs(Load<WeaponTuning>(BowPath)));
            Assert.That(p.GetMember(3).Weapon, Is.SameAs(Load<WeaponTuning>(SwordPath)));

            var hero = p.GetMember(0);
            var combo = new int[hero.Weapon.StepCount];
            for (int i = 0; i < combo.Length; i++) combo[i] = Hit(hero.TotalAttack, hero.Weapon.GetStep(i).multiplier);
            Assert.That(combo, Is.EqualTo(new[] { 36, 36, 48, 52, 68 }));
            Assert.That(Hit(hero.TotalAttack, hero.Skills.Tap.slashes[0].multiplier), Is.EqualTo(136));
            Assert.That(Hit(hero.TotalAttack, hero.Skills.GetBurstSlash(0).multiplier), Is.EqualTo(320));

            var guardian = p.GetMember(1);
            var gc = new int[guardian.Weapon.StepCount];
            for (int i = 0; i < gc.Length; i++) gc[i] = Hit(guardian.TotalAttack, guardian.Weapon.GetStep(i).multiplier);
            Assert.That(gc, Is.EqualTo(new[] { 64, 68, 80, 108 }));
            Assert.That(Hit(guardian.TotalAttack, guardian.Weapon.GetChargedSlash(0).multiplier), Is.EqualTo(40), "spin");
            Assert.That(Hit(guardian.TotalAttack, guardian.Weapon.GetChargedSlash(1).multiplier), Is.EqualTo(88), "finisher");
            Assert.That(Hit(guardian.TotalAttack, guardian.Weapon.EntrySlash.multiplier), Is.EqualTo(128), "entry smash");

            var wanderer = p.GetMember(2);
            var wc = new int[wanderer.Weapon.StepCount];
            for (int i = 0; i < wc.Length; i++) wc[i] = Hit(wanderer.TotalAttack, wanderer.Weapon.GetStep(i).multiplier);
            Assert.That(wc, Is.EqualTo(new[] { 26, 26, 33, 35, 46 }));
            Assert.That(Hit(wanderer.TotalAttack, wanderer.Weapon.GetChargedSlash(0).multiplier), Is.EqualTo(141), "aimed shot");
            Assert.That(Hit(wanderer.TotalAttack, wanderer.Weapon.EntrySlash.multiplier), Is.EqualTo(88), "backstep shot");
        }

        [Test]
        public void Skills_FitTheWeapons()
        {
            var p = Load<PartyTuning>(PartyPath);
            var g = p.GetMember(1);
            var w = p.GetMember(2);
            // 수호자(대검): 내려찍기 E 128 · 경직 55, 길게 112 + 192, Q 304 · 경직 100 · 5.5m
            Assert.That(Hit(g.TotalAttack, g.Skills.Tap.slashes[0].multiplier), Is.EqualTo(128));
            Assert.That(g.Skills.SkillPoise(SkillForm.Tap, 0), Is.EqualTo(55f).Within(Eps));
            Assert.That(Hit(g.TotalAttack, g.Skills.Hold.slashes[0].multiplier), Is.EqualTo(112));
            Assert.That(Hit(g.TotalAttack, g.Skills.Hold.slashes[1].multiplier), Is.EqualTo(192));
            Assert.That(Hit(g.TotalAttack, g.Skills.GetBurstSlash(0).multiplier), Is.EqualTo(304));
            Assert.That(g.Skills.BurstPoise(0), Is.EqualTo(100f).Within(Eps));
            Assert.That(g.Skills.BurstHitRange, Is.EqualTo(5.5f).Within(Eps));
            // 떠돌이(활): 앞 원거리 부채꼴 E 150 (8m 60°, 뒤로 1m), 길게 97 + 158 (10m 40°), Q 370 (9m 둘레)
            Assert.That(Hit(w.TotalAttack, w.Skills.Tap.slashes[0].multiplier), Is.EqualTo(150));
            Assert.That(w.Skills.Tap.hitRange, Is.EqualTo(8f).Within(Eps));
            Assert.That(w.Skills.Tap.hitAngle, Is.EqualTo(60f).Within(Eps));
            Assert.That(w.Skills.Tap.slashes[0].lunge, Is.EqualTo(-1f).Within(Eps));
            Assert.That(Hit(w.TotalAttack, w.Skills.Hold.slashes[0].multiplier), Is.EqualTo(97));
            Assert.That(Hit(w.TotalAttack, w.Skills.Hold.slashes[1].multiplier), Is.EqualTo(158));
            Assert.That(w.Skills.Hold.hitRange, Is.EqualTo(10f).Within(Eps));
            Assert.That(Hit(w.TotalAttack, w.Skills.GetBurstSlash(0).multiplier), Is.EqualTo(370));
            Assert.That(w.Skills.BurstHitRange, Is.EqualTo(9f).Within(Eps));
            Assert.That(w.Skills.Tap.hitRange, Is.LessThanOrEqualTo(w.Weapon.AutoAimRange), "the bow faces targets that far before the skill");
        }
    }
}
