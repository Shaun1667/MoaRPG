using Moa.Core.Combat;
using Moa.Core.Ids;
using Moa.Core.Motion;
using NUnit.Framework;

namespace Moa.Tests
{
    /// <summary>
    /// 2-4 스킬(E) · 궁극기(Q) · 에너지 순수 규칙 (Moa.Core.Combat.PlayerAttack · SkillAttack · BurstAttack · Energy).
    /// 값은 BALANCE 10번 (짧게 1베기 0.52초 · 쿨 5 · 에너지 10 / 길게 2베기 0.84초 · 쿨 8 · 에너지 15 · 기준 0.5초 / 궁극기 비용 40 · 쿨 12 · 1.2초 무적).
    /// </summary>
    public class SkillBurstTests
    {
        const float Dt = 0.02f;
        const float Eps = 1e-4f;
        const int HoldSteps = 25; // 0.5초

        static readonly ComboStepRules[] SwordSteps =
        {
            new ComboStepRules(0.45f, 0.10f, 0.10f, 0.20f, 0.10f, 0.30f),
            new ComboStepRules(0.45f, 0.10f, 0.10f, 0.22f, 0.10f, 0.30f),
            new ComboStepRules(0.60f, 0.12f, 0.10f, 0.24f, 0.12f, 0.40f),
            new ComboStepRules(0.65f, 0.12f, 0.12f, 0.26f, 0.12f, 0.40f),
            new ComboStepRules(0.85f, 0.16f, 0.14f, 0.40f, 0.16f, 0.60f),
        };
        static readonly ComboStepRules[] ChargedSlashes =
        {
            new ComboStepRules(0.56f, 0.16f, 0.08f, 0.06f, 0f, 0.30f),
            new ComboStepRules(0.72f, 0.08f, 0.10f, 0.36f, 0f, 0.30f),
        };
        static readonly ComboStepRules[] TapSlashes = { new ComboStepRules(1.70f, 0.12f, 0.10f, 0.30f, 0f, 0.40f) };
        static readonly ComboStepRules[] HoldSlashes =
        {
            new ComboStepRules(1.20f, 0.10f, 0.08f, 0.04f, 0f, 0.50f),
            new ComboStepRules(2.00f, 0.10f, 0.12f, 0.40f, 0f, 1.00f),
        };
        static readonly ComboStepRules[] BurstSlashes = { new ComboStepRules(4.00f, 0.60f, 0.10f, 0.50f, 0f, 0f) };

        static SkillRules Skills() => new SkillRules(new SkillFormRules(TapSlashes, 5f, 10f), new SkillFormRules(HoldSlashes, 8f, 15f), 0.5f);
        static BurstRules Burst() => new BurstRules(BurstSlashes, 40f, 12f, true);

        static PlayerAttack New() =>
            new PlayerAttack(new ComboRules(SwordSteps, 0.5f), new ChargedRules(ChargedSlashes, 20f),
                new PlungeRules(1.0f, 5.0f, 1.28f, 1.60f, 20f, 0.40f), 0.36f, Skills(), Burst());

        static Stamina Full() => new Stamina(new StaminaRules(240f, 25f, 1f, 60f));

        static readonly AttackContext Ground = new AttackContext(true, false, false, false, 0f, 0f);
        static readonly AttackContext Dodging = new AttackContext(true, true, false, false, 0f, 0f);
        static AttackContext Air(float height) => new AttackContext(false, false, false, false, height, height);

        static AttackInput None => new AttackInput(false, false, false, false, false);
        static AttackInput SkillPress(bool held) => new AttackInput(false, false, true, held, false);
        static AttackInput SkillHold(bool held) => new AttackInput(false, false, false, held, false);
        static AttackInput BurstPress => new AttackInput(false, false, false, false, true);
        static AttackInput AttackPress => new AttackInput(true, false, false, false, false);

        static AttackTick Step(PlayerAttack a, AttackInput input, AttackContext ctx, Stamina s) => a.Step(input, ctx, s, Dt);

        static void Idle(PlayerAttack a, int n, Stamina s, AttackContext ctx)
        {
            for (int i = 0; i < n; i++) a.Step(None, ctx, s, Dt);
        }

        // ---- 스킬: 짧게 · 길게 ----

        [Test]
        public void Tap_PressAndRelease_SameStep_CastsTapAtOnce()
        {
            var a = New();
            var s = Full();
            AttackTick t = Step(a, SkillPress(false), Ground, s);
            Assert.That(t.SkillStarted, Is.True);
            Assert.That(t.Swing.StepStarted, Is.True, "the slash starts this step (facing)");
            Assert.That(a.Skill.Form, Is.EqualTo(SkillForm.Tap));
            Assert.That(a.Mode, Is.EqualTo(AttackMode.Skill));
            Assert.That(a.Skill.CooldownLeft, Is.EqualTo(5f - Dt).Within(Eps), "cooldown starts at the cast");
            Assert.That(a.Skill.TapCount, Is.EqualTo(1));
        }

        [Test]
        public void Hold_25StepsAfterPress_CastsHold_24DoesNot()
        {
            var a = New();
            var s = Full();
            Step(a, SkillPress(true), Ground, s);
            Assert.That(a.Skill.Pressing, Is.True);
            Assert.That(a.Mode, Is.EqualTo(AttackMode.Skill), "standing still while pressing");
            for (int i = 0; i < HoldSteps - 1; i++) Assert.That(Step(a, SkillHold(true), Ground, s).SkillStarted, Is.False);
            Assert.That(a.Skill.Count, Is.EqualTo(0), "0.48 s held: nothing yet, no cooldown");
            Assert.That(a.Skill.Ready, Is.True);
            AttackTick t = Step(a, SkillHold(true), Ground, s);
            Assert.That(t.SkillStarted, Is.True, "0.50 s = 25 fixed steps after the press step");
            Assert.That(a.Skill.Form, Is.EqualTo(SkillForm.Hold));
            Assert.That(a.Skill.HoldCount, Is.EqualTo(1));
            Assert.That(a.Skill.CooldownLeft, Is.EqualTo(8f - Dt).Within(Eps));
        }

        [Test]
        public void Release_BeforeHoldTime_CastsTapOnRelease()
        {
            var a = New();
            var s = Full();
            Step(a, SkillPress(true), Ground, s);
            for (int i = 0; i < 10; i++) Step(a, SkillHold(true), Ground, s);
            AttackTick t = Step(a, SkillHold(false), Ground, s);
            Assert.That(t.SkillStarted, Is.True);
            Assert.That(a.Skill.Form, Is.EqualTo(SkillForm.Tap));
            Assert.That(a.Skill.HoldCount, Is.EqualTo(0));
        }

        [Test]
        public void TapSlash_052Seconds_HitWindow5Steps_Lunge04m()
        {
            var a = New();
            var s = Full();
            AttackTick t = Step(a, SkillPress(false), Ground, s);
            int steps = 1, open = t.Swing.HitOpen ? 1 : 0;
            float lunge = t.Swing.Lunge;
            while (a.Skill.Casting && steps < 200)
            {
                t = Step(a, None, Ground, s);
                steps++;
                if (t.Swing.HitOpen) open++;
                lunge += t.Swing.Lunge;
            }
            Assert.That(steps, Is.EqualTo(26), "0.52 s");
            Assert.That(open, Is.EqualTo(5), "0.10 s hit window");
            Assert.That(lunge, Is.EqualTo(0.40f).Within(1e-3f));
            Assert.That(a.Skill.LastEnd, Is.EqualTo(AttackEnd.Finished));
            Assert.That(a.Mode, Is.EqualTo(AttackMode.None));
        }

        [Test]
        public void HoldForm_TwoSlashes_084Seconds_Lunge15m()
        {
            var a = New();
            var s = Full();
            Step(a, SkillPress(true), Ground, s);
            AttackTick t = default;
            for (int i = 0; i < HoldSteps; i++) t = Step(a, SkillHold(true), Ground, s);
            Assert.That(t.SkillStarted, Is.True);
            int steps = 1, starts = 1;
            float lunge = t.Swing.Lunge;
            while (a.Skill.Casting && steps < 200)
            {
                t = Step(a, SkillHold(false), Ground, s);
                steps++;
                if (t.Swing.StepStarted) starts++;
                lunge += t.Swing.Lunge;
            }
            Assert.That(steps, Is.EqualTo(42), "0.22 + 0.62 = 0.84 s");
            Assert.That(starts, Is.EqualTo(2), "two slashes");
            Assert.That(lunge, Is.EqualTo(1.5f).Within(1e-3f));
            Assert.That(a.Skill.TapCount, Is.EqualTo(0), "releasing after the hold cast does not add a tap");
        }

        [Test]
        public void Cooldown_BlocksUntil5Seconds()
        {
            var a = New();
            var s = Full();
            Step(a, SkillPress(false), Ground, s);
            Idle(a, 100, s, Ground);
            Step(a, SkillPress(false), Ground, s);
            Assert.That(a.Skill.LastBlock, Is.EqualTo(SkillBlock.Cooldown));
            Assert.That(a.Skill.Count, Is.EqualTo(1));
            int steps = 102; // 쓴 스텝 + 100 + 막힌 누름 스텝
            while (!a.Skill.Ready && steps < 1000) { Step(a, None, Ground, s); steps++; }
            Assert.That(steps * Dt, Is.EqualTo(5f).Within(Dt + 1e-3f), "ready again 5 s (250 fixed steps) after the cast");
            Step(a, SkillPress(false), Ground, s);
            Assert.That(a.Skill.Count, Is.EqualTo(2));
        }

        [Test]
        public void Skill_CutsCombo_AndAttackDuringSkillIsBusy()
        {
            var a = New();
            var s = Full();
            Step(a, AttackPress, Ground, s);
            Idle(a, 3, s, Ground);
            Assert.That(a.Mode, Is.EqualTo(AttackMode.Combo));
            Step(a, SkillPress(false), Ground, s);
            Assert.That(a.Combo.LastEnd, Is.EqualTo(AttackEnd.Skill));
            Assert.That(a.Combo.NextStep, Is.EqualTo(0), "combo back to hit 1");
            Assert.That(a.Mode, Is.EqualTo(AttackMode.Skill));
            Step(a, AttackPress, Ground, s);
            Assert.That(a.LastBlock, Is.EqualTo(AttackBlock.Busy));
            Assert.That(a.Combo.Attacking, Is.False);
        }

        [Test]
        public void Skill_CutsChargedAttack()
        {
            var a = New();
            var s = Full();
            a.Step(true, true, Ground, s, Dt);
            for (int i = 0; i < 18; i++) a.Step(false, true, Ground, s, Dt);
            Assert.That(a.Charged.Active, Is.True);
            Step(a, SkillPress(false), Ground, s);
            Assert.That(a.Charged.Active, Is.False);
            Assert.That(a.Charged.LastEnd, Is.EqualTo(AttackEnd.Skill));
            Assert.That(a.Skill.Casting, Is.True);
        }

        [Test]
        public void DodgeWhilePressing_DropsPress_NoCooldown()
        {
            var a = New();
            var s = Full();
            Step(a, SkillPress(true), Ground, s);
            Step(a, SkillHold(true), Ground, s);
            a.Interrupt(AttackEnd.Dodge);
            Assert.That(a.Skill.Pressing, Is.False);
            Assert.That(a.Skill.LastEnd, Is.EqualTo(AttackEnd.Dodge));
            Assert.That(a.Skill.Count, Is.EqualTo(0));
            Assert.That(a.Skill.Ready, Is.True);
            // 누르는 중에 회피 상태가 되어도 버린다
            Step(a, SkillPress(true), Ground, s);
            Step(a, SkillHold(true), Dodging, s);
            Assert.That(a.Skill.Pressing, Is.False);
            Assert.That(a.Skill.Count, Is.EqualTo(0));
        }

        [Test]
        public void DodgeCutsSkillCast_CooldownStays()
        {
            var a = New();
            var s = Full();
            Step(a, SkillPress(false), Ground, s);
            a.Interrupt(AttackEnd.Dodge);
            Assert.That(a.Skill.Casting, Is.False);
            Assert.That(a.Skill.LastEnd, Is.EqualTo(AttackEnd.Dodge));
            Assert.That(a.Skill.CooldownLeft, Is.GreaterThan(4.9f));
        }

        [Test]
        public void Skill_NotInAir_Climbing_Gliding_Dodging()
        {
            var a = New();
            var s = Full();
            Step(a, SkillPress(false), Air(3f), s);
            Assert.That(a.Skill.LastBlock, Is.EqualTo(SkillBlock.Airborne));
            Step(a, SkillPress(false), new AttackContext(false, false, true, false), s);
            Assert.That(a.Skill.LastBlock, Is.EqualTo(SkillBlock.Climbing));
            Step(a, SkillPress(false), new AttackContext(false, false, false, true, 5f, 5f), s);
            Assert.That(a.Skill.LastBlock, Is.EqualTo(SkillBlock.Gliding));
            Step(a, SkillPress(false), Dodging, s);
            Assert.That(a.Skill.LastBlock, Is.EqualTo(SkillBlock.Dodging));
            Assert.That(a.Skill.Count, Is.EqualTo(0));
        }

        // ---- 에너지 ----

        [Test]
        public void SkillEnergy_OncePerCast_Tap10_Hold15()
        {
            var a = New();
            var s = Full();
            Step(a, SkillPress(false), Ground, s);
            Assert.That(a.TakeSkillEnergy(), Is.EqualTo(10f).Within(Eps));
            Assert.That(a.TakeSkillEnergy(), Is.EqualTo(0f), "only the first hit of a cast");
            Assert.That(a.Energy.Current, Is.EqualTo(10f).Within(Eps));
            a.Skill.ResetCooldown();
            Idle(a, 30, s, Ground);
            Step(a, SkillPress(true), Ground, s);
            for (int i = 0; i < HoldSteps; i++) Step(a, SkillHold(true), Ground, s);
            Assert.That(a.Skill.Form, Is.EqualTo(SkillForm.Hold));
            Assert.That(a.TakeSkillEnergy(), Is.EqualTo(15f).Within(Eps));
            Assert.That(a.Energy.Current, Is.EqualTo(25f).Within(Eps));
        }

        [Test]
        public void Energy_ClampsAtMax_SpendNeedsFull()
        {
            var e = new Energy(40f);
            Assert.That(e.Add(30f), Is.EqualTo(30f).Within(Eps));
            Assert.That(e.Full, Is.False);
            Assert.That(e.TrySpend(40f), Is.False);
            Assert.That(e.Add(30f), Is.EqualTo(10f).Within(Eps), "stops at the max");
            Assert.That(e.Full, Is.True);
            Assert.That(e.TrySpend(40f), Is.True);
            Assert.That(e.Current, Is.EqualTo(0f));
            Assert.That(e.Add(-5f), Is.EqualTo(0f));
        }

        [Test]
        public void FourTaps_FillTheBurst()
        {
            var a = New();
            var s = Full();
            for (int i = 0; i < 4; i++)
            {
                a.Skill.ResetCooldown();
                Step(a, SkillPress(false), Ground, s);
                a.TakeSkillEnergy();
                Idle(a, 30, s, Ground);
            }
            Assert.That(a.Energy.Full, Is.True, "4 x 10 = 40");
        }

        // ---- 궁극기 ----

        [Test]
        public void Burst_NeedsFullEnergy()
        {
            var a = New();
            var s = Full();
            a.Energy.Set(39f);
            Step(a, BurstPress, Ground, s);
            Assert.That(a.Burst.LastBlock, Is.EqualTo(BurstBlock.NoEnergy));
            Assert.That(a.Energy.Current, Is.EqualTo(39f).Within(Eps));
            a.Energy.Fill();
            AttackTick t = Step(a, BurstPress, Ground, s);
            Assert.That(t.BurstStarted, Is.True);
            Assert.That(t.Swing.StepStarted, Is.True);
            Assert.That(a.Energy.Current, Is.EqualTo(0f));
            Assert.That(a.Burst.CooldownLeft, Is.EqualTo(12f - Dt).Within(Eps));
            Assert.That(a.Mode, Is.EqualTo(AttackMode.Burst));
        }

        [Test]
        public void Burst_Invulnerable_WholeTime_12Seconds_NotCutByDodgeOrJump()
        {
            var a = New();
            var s = Full();
            a.Energy.Fill();
            AttackTick t = Step(a, BurstPress, Ground, s);
            int steps = 1, open = t.Swing.HitOpen ? 1 : 0, invul = a.Invulnerable ? 1 : 0;
            while (a.Burst.Active && steps < 200)
            {
                if (steps == 5) { a.Interrupt(AttackEnd.Dodge); a.InterruptSwing(AttackEnd.Jump); }
                t = Step(a, None, Ground, s);
                steps++;
                if (t.Swing.HitOpen) open++;
                if (a.Burst.Active && a.Invulnerable) invul++;
            }
            Assert.That(steps, Is.EqualTo(60), "0.60 + 0.10 + 0.50 = 1.20 s");
            Assert.That(open, Is.EqualTo(5));
            Assert.That(invul, Is.EqualTo(59), "invulnerable every step it is active");
            Assert.That(a.Burst.LastEnd, Is.EqualTo(AttackEnd.Finished));
            Assert.That(a.Invulnerable, Is.False);
        }

        [Test]
        public void Burst_CutsSkillAndCombo()
        {
            var a = New();
            var s = Full();
            a.Energy.Fill();
            Step(a, SkillPress(false), Ground, s);
            Step(a, BurstPress, Ground, s);
            Assert.That(a.Skill.Casting, Is.False);
            Assert.That(a.Skill.LastEnd, Is.EqualTo(AttackEnd.Burst));
            Assert.That(a.Burst.Active, Is.True);
            Step(a, SkillPress(false), Ground, s);
            Assert.That(a.Skill.LastBlock, Is.EqualTo(SkillBlock.Busy), "no skill during the burst");
            Step(a, AttackPress, Ground, s);
            Assert.That(a.LastBlock, Is.EqualTo(AttackBlock.Busy));
        }

        [Test]
        public void Burst_NoEnergyGain_WhileActive_Cooldown12()
        {
            var a = New();
            var s = Full();
            a.Energy.Fill();
            Step(a, BurstPress, Ground, s);
            Assert.That(a.GainEnergy(5f), Is.EqualTo(0f), "no energy during the burst");
            Idle(a, 70, s, Ground);
            Assert.That(a.GainEnergy(5f), Is.EqualTo(5f).Within(Eps));
            a.Energy.Fill();
            Step(a, BurstPress, Ground, s);
            Assert.That(a.Burst.LastBlock, Is.EqualTo(BurstBlock.Cooldown));
            Assert.That(a.Energy.Full, Is.True, "energy kept");
        }

        [Test]
        public void Burst_NotInAir_NorDuringPlunge()
        {
            var a = New();
            var s = Full();
            a.Energy.Fill();
            Step(a, BurstPress, Air(3f), s);
            Assert.That(a.Burst.LastBlock, Is.EqualTo(BurstBlock.Airborne));
            a.Step(true, false, Air(3f), s, Dt);
            Assert.That(a.Plunge.Active, Is.True);
            Step(a, BurstPress, Ground, s);
            Assert.That(a.Burst.LastBlock, Is.EqualTo(BurstBlock.Busy));
            Assert.That(a.Energy.Full, Is.True);
        }

        [Test]
        public void SwingHits_OncePerSlash_ForSkillAndBurst()
        {
            var a = New();
            var s = Full();
            var id = new EntityId(7);
            Step(a, SkillPress(false), Ground, s);
            Assert.That(a.TryHitSwing(id), Is.True);
            Assert.That(a.TryHitSwing(id), Is.False);
            Assert.That(a.CurrentSwing.Multiplier, Is.EqualTo(1.70f).Within(Eps));
            a.Energy.Fill();
            Step(a, BurstPress, Ground, s);
            Assert.That(a.TryHitSwing(id), Is.True, "a new swing (burst)");
            Assert.That(a.CurrentSwing.Multiplier, Is.EqualTo(4.00f).Within(Eps));
        }

        [Test]
        public void NoSkillTable_NoFormsNoSlashes_OldConstructor()
        {
            var a = new PlayerAttack(new ComboRules(SwordSteps, 0.5f), new ChargedRules(ChargedSlashes, 20f),
                new PlungeRules(1.0f, 5.0f, 1.28f, 1.60f, 20f, 0.40f), 0.36f);
            var s = Full();
            Step(a, SkillPress(false), Ground, s);
            Assert.That(a.Skill.LastBlock, Is.EqualTo(SkillBlock.NoForms));
            Step(a, BurstPress, Ground, s);
            Assert.That(a.Burst.LastBlock, Is.EqualTo(BurstBlock.NoSlashes));
            Assert.That(a.Energy.Max, Is.EqualTo(0f));
            Assert.That(a.Mode, Is.EqualTo(AttackMode.None));
        }
    }
}
