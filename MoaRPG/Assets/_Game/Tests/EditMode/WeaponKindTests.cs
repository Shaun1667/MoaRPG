using Moa.Core.Combat;
using Moa.Core.Ids;
using Moa.Core.Motion;
using NUnit.Framework;

namespace Moa.Tests
{
    /// <summary>
    /// 3-3 무기 종류 순수 규칙: 투사체(Moa.Core.Combat.ProjectileSet — 빠르기 · 수명 · 한 발에 한 대상 · 풀링) ·
    /// 조준형 강공격(활: 누르면 조준, 다 조준한 뒤 떼면 한 발) · 회전형 강공격(대검: 누르는 동안 회전 + 초당 스태미나, 떼거나 바닥나면 마무리) ·
    /// 무기 바꿔 끼기(PlayerAttack.ChangeWeapon). 값은 BALANCE 15번.
    /// </summary>
    public class WeaponKindTests
    {
        const float Dt = 0.02f;
        const float Eps = 1e-4f;
        const float HoldTime = 0.36f;
        const int HoldSteps = 18;

        static readonly ProjectileRules Arrow = new ProjectileRules(30f, 0.6f, 0.15f);
        static ProjectileShot Shot(float m = 0.3f) => new ProjectileShot(110f, m, 5f, AttackMode.Combo, 1, 1);

        static readonly ComboStepRules[] BowSteps =
        {
            new ComboStepRules(0.30f, 0.08f, 0.02f, 0.20f, 0.08f, 0f),
            new ComboStepRules(0.30f, 0.08f, 0.02f, 0.22f, 0.08f, 0f),
        };
        static readonly ComboStepRules[] AimShot = { new ComboStepRules(1.60f, 0.02f, 0.02f, 0.30f, 0f, 0f) };
        static readonly ComboStepRules[] ClaymoreSteps = { new ComboStepRules(0.80f, 0.20f, 0.12f, 0.36f, 0.20f, 0.40f) };
        static readonly ComboStepRules[] Spin =
        {
            new ComboStepRules(0.50f, 0.04f, 0.12f, 0.30f, 0f, 0f),  // 회전 (되풀이)
            new ComboStepRules(1.10f, 0.10f, 0.12f, 0.42f, 0f, 0.30f), // 마무리
        };
        static PlungeRules Plunge() => new PlungeRules(1.0f, 5.0f, 1.28f, 1.60f, 20f, 0.40f);
        static readonly AttackContext Ground = new AttackContext(true, false, false, false, 0f, 0f);
        static readonly AttackContext Dodging = new AttackContext(true, true, false, false, 0f, 0f);
        static Stamina Full() => new Stamina(new StaminaRules(240f, 25f, 1f, 60f));

        static PlayerAttack Bow() => new PlayerAttack(new ComboRules(BowSteps, 0.5f), new ChargedRules(AimShot, 0f, 0.8f, 0f), Plunge(), HoldTime);
        static PlayerAttack Claymore() => new PlayerAttack(new ComboRules(ClaymoreSteps, 0.6f), new ChargedRules(Spin, 10f, 0f, 40f), Plunge(), HoldTime);

        static void Steps(PlayerAttack a, int n, bool held, AttackContext ctx, Stamina s)
        {
            for (int i = 0; i < n; i++) a.Step(false, held, ctx, s, Dt);
        }

        /// <summary>누른 뒤 18스텝 길게 → 강공격 시작까지.</summary>
        static void HoldIntoCharged(PlayerAttack a, Stamina s)
        {
            a.Step(true, true, Ground, s, Dt);
            Steps(a, HoldSteps, true, Ground, s);
            Assert.That(a.Charged.Active, Is.True, "charged started after 18 held steps");
        }

        // ---- 투사체 ----

        [Test]
        public void Projectile_FliesSpeedTimesLifetime_ThenExpires()
        {
            var set = new ProjectileSet();
            int slot = set.Spawn(Arrow, Shot(), 0f, 1f, 0f, 0f, 2f); // 방향은 정규화
            Assert.That(slot, Is.EqualTo(0));
            Assert.That(set.ActiveCount, Is.EqualTo(1));
            set.Advance(Dt);
            set.PrevPosition(slot, out _, out _, out float pz);
            set.Position(slot, out float x, out float y, out float z);
            Assert.That(pz, Is.EqualTo(0f).Within(Eps));
            Assert.That(z, Is.EqualTo(0.6f).Within(Eps), "30 m/s x 0.02 s");
            Assert.That(y, Is.EqualTo(1f).Within(Eps), "flies level");
            Assert.That(x, Is.EqualTo(0f).Within(Eps));
            for (int i = 1; i < 30; i++) set.Advance(Dt); // 0.6 s = 30 steps
            Assert.That(set.IsAlive(slot), Is.True, "the last segment is still checked");
            set.Position(slot, out _, out _, out z);
            Assert.That(z, Is.EqualTo(18f).Within(1e-3f), "range = 30 x 0.6 = 18 m exactly");
            set.Advance(Dt);
            Assert.That(set.IsAlive(slot), Is.False);
            Assert.That(set.LastEnd(slot), Is.EqualTo(ProjectileEnd.Expired));
            Assert.That(set.ActiveCount, Is.EqualTo(0));
            Assert.That(set.ExpiredCount, Is.EqualTo(1));
        }

        [Test]
        public void Projectile_HitsOneTargetOnce_ThenGone()
        {
            var set = new ProjectileSet();
            int slot = set.Spawn(Arrow, Shot(0.52f), 0f, 1f, 0f, 1f, 0f);
            set.Advance(Dt);
            Assert.That(set.Shot(slot).Multiplier, Is.EqualTo(0.52f).Within(Eps), "the shot keeps the values set when fired");
            Assert.That(set.TryHit(slot, new EntityId(7)), Is.True);
            Assert.That(set.TryHit(slot, new EntityId(8)), Is.False, "no piercing: one target per shot");
            Assert.That(set.TryHit(slot, new EntityId(7)), Is.False);
            Assert.That(set.LastEnd(slot), Is.EqualTo(ProjectileEnd.Hit));
            Assert.That(set.HitCount, Is.EqualTo(1));
            Assert.That(set.ActiveCount, Is.EqualTo(0));
            Assert.That(set.TryHit(set.Spawn(Arrow, Shot(), 0f, 0f, 0f, 1f, 0f), EntityId.None), Is.False, "no id = not a target");
        }

        [Test]
        public void Projectile_Block_StopsAtThePoint()
        {
            var set = new ProjectileSet();
            int slot = set.Spawn(Arrow, Shot(), 0f, 1f, 0f, 1f, 0f);
            set.Advance(Dt);
            set.Block(slot, 0.4f, 1f, 0f);
            Assert.That(set.IsAlive(slot), Is.False);
            Assert.That(set.LastEnd(slot), Is.EqualTo(ProjectileEnd.Blocked));
            set.Position(slot, out float x, out _, out _);
            Assert.That(x, Is.EqualTo(0.4f).Within(Eps));
            Assert.That(set.BlockedCount, Is.EqualTo(1));
        }

        [Test]
        public void Projectile_Pool_ReusesDeadSlots()
        {
            var set = new ProjectileSet();
            int a = set.Spawn(Arrow, Shot(), 0f, 1f, 0f, 1f, 0f);
            int b = set.Spawn(Arrow, Shot(), 0f, 1f, 0f, 1f, 0f);
            Assert.That(set.Capacity, Is.EqualTo(2));
            set.TryHit(a, new EntityId(1));
            int c = set.Spawn(Arrow, Shot(), 0f, 1f, 0f, 1f, 0f);
            Assert.That(c, Is.EqualTo(a), "the freed slot is used first");
            Assert.That(set.Capacity, Is.EqualTo(2));
            for (int i = 0; i < 40; i++) set.Advance(Dt); // 모두 사라짐
            for (int i = 0; i < 2; i++) set.Spawn(Arrow, Shot(), 0f, 1f, 0f, 1f, 0f);
            Assert.That(set.Capacity, Is.EqualTo(2), "no new slots while old ones are free");
            Assert.That(set.CreatedCount, Is.EqualTo(2));
            Assert.That(set.FiredCount, Is.EqualTo(5));
            Assert.That(b, Is.EqualTo(1));
        }

        [Test]
        public void Projectile_EmptyRulesOrDirection_DoesNotFire()
        {
            var set = new ProjectileSet();
            Assert.That(set.Spawn(new ProjectileRules(0f, 0.6f, 0.1f), Shot(), 0f, 0f, 0f, 1f, 0f), Is.EqualTo(-1));
            Assert.That(set.Spawn(Arrow, Shot(), 0f, 0f, 0f, 0f, 0f), Is.EqualTo(-1));
            Assert.That(set.FiredCount, Is.EqualTo(0));
            set.Spawn(Arrow, Shot(), 0f, 0f, 0f, 1f, 0f);
            set.Clear();
            Assert.That(set.ActiveCount, Is.EqualTo(0));
            Assert.That(set.LastEnd(0), Is.EqualTo(ProjectileEnd.Cleared));
        }

        // ---- 조준형 강공격 (활) ----

        [Test]
        public void Aim_HoldAims_ReleaseAfterAimTime_FiresOneShot()
        {
            var a = Bow();
            var s = Full();
            HoldIntoCharged(a, s);
            Assert.That(a.Aiming, Is.True);
            Assert.That(a.Mode, Is.EqualTo(AttackMode.Charged));
            Assert.That(a.Combo.NextStep, Is.EqualTo(0), "the combo goes back to hit 1");
            Assert.That(a.Charged.SlashIndex, Is.EqualTo(-1), "nothing fired while aiming");
            Assert.That(s.Current, Is.EqualTo(240f).Within(Eps), "bow aiming costs no stamina");
            Steps(a, 39, true, Ground, s);
            Assert.That(a.Charged.AimReady, Is.True, "0.8 s = 40 steps (the first one is the start step)");
            Steps(a, 60, true, Ground, s);
            Assert.That(a.Aiming, Is.True, "keeps aiming while held");
            AttackTick release = a.Step(false, false, Ground, s, Dt);
            Assert.That(a.Aiming, Is.False);
            Assert.That(release.Swing.HitOpen, Is.False);
            AttackTick t1 = a.Step(false, false, Ground, s, Dt);
            Assert.That(t1.Swing.StepStarted, Is.True, "the shot starts the step after the release");
            AttackTick t2 = a.Step(false, false, Ground, s, Dt);
            Assert.That(t2.Swing.HitOpen, Is.True, "hit window after a 0.02 s windup");
            Assert.That(a.CurrentSwing.Multiplier, Is.EqualTo(1.60f).Within(Eps));
            Assert.That(a.Charged.ShotCount, Is.EqualTo(1));
            Steps(a, 20, false, Ground, s);
            Assert.That(a.Charged.Active, Is.False);
            Assert.That(a.Charged.LastEnd, Is.EqualTo(AttackEnd.Finished));
        }

        [Test]
        public void Aim_ReleasedTooEarly_NothingFires()
        {
            var a = Bow();
            var s = Full();
            HoldIntoCharged(a, s);
            Steps(a, 20, true, Ground, s); // 0.42 s 조준
            Assert.That(a.Charged.AimReady, Is.False);
            a.Step(false, false, Ground, s, Dt);
            Assert.That(a.Charged.Active, Is.False);
            Assert.That(a.Charged.LastEnd, Is.EqualTo(AttackEnd.AimCancelled));
            Assert.That(a.Charged.ShotCount, Is.EqualTo(0));
            Assert.That(a.Charged.AimCancelCount, Is.EqualTo(1));
        }

        [Test]
        public void Aim_PressWhileAiming_Busy_DodgeCancels()
        {
            var a = Bow();
            var s = Full();
            HoldIntoCharged(a, s);
            a.Step(true, true, Ground, s, Dt);
            Assert.That(a.LastBlock, Is.EqualTo(AttackBlock.Busy));
            a.Interrupt(AttackEnd.Dodge);
            Assert.That(a.Aiming, Is.False);
            Assert.That(a.Charged.LastEnd, Is.EqualTo(AttackEnd.Dodge));
            Steps(a, 60, false, Dodging, s);
            Assert.That(a.Charged.ShotCount, Is.EqualTo(0), "a dodge-cancelled aim never fires");
        }

        // ---- 회전형 강공격 (대검) ----

        [Test]
        public void Spin_RepeatsWhileHeld_Drains40PerSecond()
        {
            var a = Claymore();
            var s = Full();
            HoldIntoCharged(a, s);
            Assert.That(a.Charged.Spinning, Is.True);
            Assert.That(s.Current, Is.EqualTo(229.2f).Within(1e-3f), "10 to start, the first spin step drains 0.8");
            Assert.That(a.Charged.SpinsThisCharge, Is.EqualTo(1));
            float before = s.Current;
            Steps(a, 50, true, Ground, s); // 1 s
            Assert.That(before - s.Current, Is.EqualTo(40f).Within(1e-2f), "40 stamina per second while spinning");
            Assert.That(a.Charged.Spinning, Is.True);
            Assert.That(a.Charged.SpinsThisCharge, Is.EqualTo(3), "0.46 s per spin: 1 s later the third spin");
        }

        [Test]
        public void Spin_EachSpinHitsAgain_ReleaseGoesToFinisher()
        {
            var a = Claymore();
            var s = Full();
            HoldIntoCharged(a, s);
            Steps(a, 4, true, Ground, s);
            Assert.That(a.Charged.TryHit(new EntityId(3)), Is.True);
            Assert.That(a.Charged.TryHit(new EntityId(3)), Is.False, "once per spin");
            Steps(a, 23, true, Ground, s); // 다음 회전
            Assert.That(a.Charged.SpinsThisCharge, Is.EqualTo(2));
            Assert.That(a.Charged.TryHit(new EntityId(3)), Is.True, "a new spin hits the same target again");
            // 떼면 이번 회전이 끝나고 마무리
            Steps(a, 30, false, Ground, s);
            Assert.That(a.Charged.SlashIndex, Is.EqualTo(1), "finisher row");
            Assert.That(a.CurrentSwing.Multiplier, Is.EqualTo(1.10f).Within(Eps));
            Assert.That(a.Charged.Spinning, Is.False);
            Steps(a, 40, false, Ground, s);
            Assert.That(a.Charged.Active, Is.False);
            Assert.That(a.Charged.LastEnd, Is.EqualTo(AttackEnd.Finished));
            Assert.That(a.Charged.SpinsThisCharge, Is.EqualTo(2));
        }

        [Test]
        public void Spin_StaminaRunsOut_FinisherEvenIfHeld()
        {
            var a = Claymore();
            var s = Full();
            s.Set(12f);
            HoldIntoCharged(a, s); // 10을 쓰고 2 남음
            Steps(a, 40, true, Ground, s);
            Assert.That(s.Current, Is.EqualTo(0f).Within(Eps));
            Assert.That(s.Exhausted, Is.True);
            Assert.That(a.Charged.SpinsThisCharge, Is.EqualTo(1), "no second spin without stamina");
            Assert.That(a.Charged.SlashIndex, Is.EqualTo(1), "the finisher still comes out");
            Steps(a, 40, true, Ground, s);
            Assert.That(a.Charged.Active, Is.False);
        }

        [Test]
        public void Spin_NotEnoughToStart_NoStamina()
        {
            var a = Claymore();
            var s = Full();
            s.Set(9f);
            a.Step(true, true, Ground, s, Dt);
            Steps(a, HoldSteps, true, Ground, s);
            Assert.That(a.Charged.Active, Is.False);
            Assert.That(a.LastChargeBlock, Is.EqualTo(ChargeBlock.NoStamina));
        }

        [Test]
        public void SwordStyle_HeldDoesNotChangeTheSlashes()
        {
            var r = new ChargedRules(Spin, 20f); // 2-2 생성자 = 조준 · 회전 없음
            Assert.That(r.Aimed, Is.False);
            Assert.That(r.Spins, Is.False);
            Assert.That(r.FinisherIndex, Is.EqualTo(-1));
            var c = new ChargedAttack(r);
            c.TryStart(Ground, null);
            int ticks = 0;
            while (c.Active && ticks < 200) { c.Advance(Dt, true, null); ticks++; }
            Assert.That(ticks, Is.EqualTo(55), "both rows once: 0.46 + 0.64 = 1.10 s = 55 steps, held or not");
            Assert.That(new ChargedRules(new[] { Spin[0] }, 0f, 0f, 40f).Spins, Is.False, "a spin needs a finisher row");
        }

        // ---- 무기 바꿔 끼기 ----

        [Test]
        public void ChangeWeapon_SwapsComboAndCharged_KeepsSkillEnergy()
        {
            var a = new PlayerAttack(new ComboRules(BowSteps, 0.5f), new ChargedRules(AimShot, 0f, 0.8f, 0f), Plunge(), HoldTime,
                new SkillRules(new SkillFormRules(new[] { new ComboStepRules(1.7f, 0.12f, 0.1f, 0.3f, 0f, 0f) }, 5f, 10f), null, 0.5f),
                new BurstRules(new[] { new ComboStepRules(4f, 0.6f, 0.1f, 0.5f, 0f, 0f) }, 40f, 12f, true), null);
            a.Energy.Set(25f);
            a.ChangeWeapon(new ComboRules(ClaymoreSteps, 0.6f), new ChargedRules(Spin, 10f, 0f, 40f), Plunge(), null);
            Assert.That(a.Combo.Rules.StepCount, Is.EqualTo(1));
            Assert.That(a.Charged.Rules.Spins, Is.True);
            Assert.That(a.Charged.Rules.Aimed, Is.False);
            Assert.That(a.Energy.Current, Is.EqualTo(25f).Within(Eps), "energy belongs to the character");
            Assert.That(a.Skill.Rules, Is.Not.Null);
            a.Step(true, false, Ground, Full(), Dt);
            Assert.That(a.CurrentSwing.Multiplier, Is.EqualTo(0.80f).Within(Eps), "the new weapon's hit 1");
        }
    }
}
