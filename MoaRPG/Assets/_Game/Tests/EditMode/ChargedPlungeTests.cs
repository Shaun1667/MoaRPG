using Moa.Core.Combat;
using Moa.Core.Ids;
using Moa.Core.Motion;
using NUnit.Framework;

namespace Moa.Tests
{
    /// <summary>
    /// 2-2 강공격 · 낙하 공격 순수 규칙 (Moa.Core.Combat.PlayerAttack · ChargedAttack · PlungeAttack).
    /// 값은 BALANCE 6번(한손검 강공격 · 낙하 배율) · 7번(길게 누르기 · 낙하 높이 · 속력 · 경직).
    /// </summary>
    public class ChargedPlungeTests
    {
        const float Dt = 0.02f;
        const float Eps = 1e-4f;
        const float HoldTime = 0.36f;
        const int HoldSteps = 18;

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
        static PlungeRules Plunge(float landRecovery = 0.40f) => new PlungeRules(1.0f, 5.0f, 1.28f, 1.60f, 20f, landRecovery);

        static PlayerAttack New(float landRecovery = 0.40f) =>
            new PlayerAttack(new ComboRules(SwordSteps, 0.5f), new ChargedRules(ChargedSlashes, 20f), Plunge(landRecovery), HoldTime);

        static Stamina Full() => new Stamina(new StaminaRules(240f, 25f, 1f, 60f));

        static readonly AttackContext Ground = new AttackContext(true, false, false, false, 0f, 0f);
        static AttackContext Air(float height, float feetY) => new AttackContext(false, false, false, false, height, feetY);

        static AttackTick Press(PlayerAttack a, AttackContext ctx, Stamina s, bool held = true) => a.Step(true, held, ctx, s, Dt);

        static void Steps(PlayerAttack a, int n, bool held, AttackContext ctx, Stamina s)
        {
            for (int i = 0; i < n; i++) a.Step(false, held, ctx, s, Dt);
        }

        // ---- 길게 누르기 ----

        [Test]
        public void Hold_18StepsAfterPress_StartsCharged_17DoesNot()
        {
            var a = New();
            var s = Full();
            Press(a, Ground, s);
            Steps(a, HoldSteps - 1, true, Ground, s);
            Assert.That(a.Charged.Active, Is.False, "0.34 s held is not enough");
            Assert.That(a.Holding, Is.True);
            AttackTick t = a.Step(false, true, Ground, s, Dt);
            Assert.That(t.ChargedStarted, Is.True, "0.36 s = 18 fixed steps after the press step");
            Assert.That(t.Swing.StepStarted, Is.True, "first slash starts this step");
            Assert.That(a.Charged.Active, Is.True);
            Assert.That(a.Mode, Is.EqualTo(AttackMode.Charged));
            Assert.That(a.LastChargeBlock, Is.EqualTo(ChargeBlock.None));
        }

        [Test]
        public void Release_BeforeHoldTime_NoCharged()
        {
            var a = New();
            var s = Full();
            Press(a, Ground, s);
            Steps(a, 10, true, Ground, s);
            Steps(a, 40, false, Ground, s);
            Assert.That(a.Charged.Count, Is.EqualTo(0));
            Assert.That(a.Combo.LastEnd, Is.EqualTo(AttackEnd.Finished), "the normal hit plays out");
            Assert.That(s.Current, Is.EqualTo(240f).Within(Eps));
        }

        [Test]
        public void HoldFromIdle_IsN1C_Hit1ThenCharged()
        {
            var a = New();
            var s = Full();
            Press(a, Ground, s);
            Assert.That(a.Combo.StepIndex, Is.EqualTo(0), "the press starts hit 1 at once");
            int open = 0;
            for (int i = 0; i < HoldSteps; i++) if (a.Step(false, true, Ground, s, Dt).Swing.HitOpen && !a.Charged.Active) open++;
            Assert.That(open, Is.EqualTo(5), "hit 1 hit window (0.10 s) went by before the charged attack");
            Assert.That(a.Charged.Active, Is.True);
            Assert.That(a.Combo.Attacking, Is.False, "hit 1 is cut in its recovery");
            Assert.That(a.Combo.LastEnd, Is.EqualTo(AttackEnd.Charged));
            Assert.That(a.Combo.NextStep, Is.EqualTo(0), "combo back to hit 1");
            Assert.That(a.Combo.SwingCount, Is.EqualTo(1));
        }

        [Test]
        public void Charged_Spends20_TwoSlashes_084Seconds_LungeSum()
        {
            var a = New();
            var s = Full();
            Press(a, Ground, s);
            Steps(a, HoldSteps - 1, true, Ground, s);
            int steps = 0, starts = 0, open1 = 0, open2 = 0;
            float lunge = 0f;
            AttackTick t = a.Step(false, true, Ground, s, Dt);
            Assert.That(s.Current, Is.EqualTo(220f).Within(Eps), "stamina 240 - 20");
            while (true)
            {
                steps++;
                if (t.Swing.StepStarted) starts++;
                if (t.Swing.HitOpen) { if (a.Charged.SlashIndex == 0) open1++; else open2++; }
                lunge += t.Swing.Lunge;
                if (!a.Charged.Active || steps > 200) break;
                t = a.Step(false, false, Ground, s, Dt);
            }
            Assert.That(steps, Is.EqualTo(42), "0.30 + 0.54 s");
            Assert.That(starts, Is.EqualTo(2));
            Assert.That(open1, Is.EqualTo(4), "slash 1 window 0.08 s");
            Assert.That(open2, Is.EqualTo(5), "slash 2 window 0.10 s");
            Assert.That(lunge, Is.EqualTo(0.60f).Within(1e-4f));
            Assert.That(a.Charged.LastEnd, Is.EqualTo(AttackEnd.Finished));
            Assert.That(a.Charged.Count, Is.EqualTo(1));
        }

        [Test]
        public void NotEnoughStamina_NothingHappens_ComboGoesOn()
        {
            var a = New();
            var s = Full();
            s.Set(19f);
            Press(a, Ground, s);
            Steps(a, HoldSteps, true, Ground, s);
            Assert.That(a.Charged.Active, Is.False);
            Assert.That(a.LastChargeBlock, Is.EqualTo(ChargeBlock.NoStamina));
            Assert.That(s.Current, Is.EqualTo(19f).Within(Eps), "nothing spent");
            Assert.That(a.Combo.Attacking, Is.True, "hit 1 is not cut");
            Steps(a, 10, true, Ground, s);
            Assert.That(a.Combo.LastEnd, Is.EqualTo(AttackEnd.Finished));
            Assert.That(a.Charged.Count, Is.EqualTo(0), "holding on does not retry");

            s.Set(20f);
            Steps(a, 30, false, Ground, s);
            Press(a, Ground, s);
            Steps(a, HoldSteps, true, Ground, s);
            Assert.That(a.Charged.Active, Is.True, "exactly 20 is enough");
            Assert.That(s.Current, Is.EqualTo(0f).Within(Eps));
        }

        [Test]
        public void ChargedDuringCombo_CutsTheStep_BackToHit1()
        {
            var a = New();
            var s = Full();
            Press(a, Ground, s, false);
            // 입력 열림마다 눌러 3타까지
            int guard = 0;
            while (a.Combo.StepIndex < 2 && guard++ < 200)
            {
                bool press = a.Combo.Attacking && !a.Combo.Queued && a.Combo.Elapsed >= a.Combo.CurrentStep.InputOpen - Eps;
                a.Step(press, false, Ground, s, Dt);
            }
            Assert.That(a.Combo.StepIndex, Is.EqualTo(2));
            while (a.Combo.Elapsed < a.Combo.CurrentStep.InputOpen - Eps) a.Step(false, false, Ground, s, Dt);
            Press(a, Ground, s);
            Steps(a, HoldSteps, true, Ground, s);
            Assert.That(a.Charged.Active, Is.True);
            Assert.That(a.Combo.Attacking, Is.False);
            Assert.That(a.Combo.LastEnd, Is.EqualTo(AttackEnd.Charged));
            Assert.That(a.Combo.NextStep, Is.EqualTo(0));
            Assert.That(a.Combo.Queued, Is.False);

            Steps(a, 42, false, Ground, s);
            Assert.That(a.Charged.Active, Is.False);
            Press(a, Ground, s, false);
            Assert.That(a.Combo.StepIndex, Is.EqualTo(0), "after the charged attack the combo starts again at hit 1");
        }

        [Test]
        public void PressDuringCharged_IsBusy()
        {
            var a = New();
            var s = Full();
            Press(a, Ground, s);
            Steps(a, HoldSteps, true, Ground, s);
            Press(a, Ground, s, false);
            Assert.That(a.LastBlock, Is.EqualTo(AttackBlock.Busy));
            Assert.That(a.BusyCount, Is.EqualTo(1));
            Assert.That(a.Combo.Attacking, Is.False);
            Assert.That(a.Holding, Is.False, "a busy press does not start a new hold");
        }

        [Test]
        public void Dodge_InterruptsCharged_AndDropsTheHold()
        {
            var a = New();
            var s = Full();
            Press(a, Ground, s);
            Steps(a, HoldSteps, true, Ground, s);
            Steps(a, 5, false, Ground, s);
            a.Interrupt(AttackEnd.Dodge);
            Assert.That(a.Charged.Active, Is.False);
            Assert.That(a.Charged.LastEnd, Is.EqualTo(AttackEnd.Dodge));
            Assert.That(a.Mode, Is.EqualTo(AttackMode.None));

            // 길게 누르는 중 회피 → 강공격 안 나감
            var b = New();
            Press(b, Ground, s);
            Steps(b, 5, true, Ground, s);
            b.Interrupt(AttackEnd.Dodge);
            var dodging = new AttackContext(true, true, false, false, 0f, 0f);
            Steps(b, HoldSteps, true, dodging, s);
            Assert.That(b.Charged.Count, Is.EqualTo(0));
        }

        [Test]
        public void Jump_DuringHold_NoCharged()
        {
            var a = New();
            var s = Full();
            Press(a, Ground, s);
            Steps(a, 5, true, Ground, s);
            Steps(a, HoldSteps, true, Air(0.5f, 0.5f), s);
            Assert.That(a.Charged.Count, Is.EqualTo(0));
            Assert.That(a.Holding, Is.False);
        }

        // ---- 낙하 공격 ----

        [Test]
        public void AirPress_StartsPlunge_FromMinHeight1m()
        {
            var a = New();
            var s = Full();
            AttackTick low = Press(a, Air(0.99f, 0.99f), s);
            Assert.That(low.PlungeStarted, Is.False);
            Assert.That(a.LastBlock, Is.EqualTo(AttackBlock.TooLow));
            Assert.That(a.Combo.Attacking, Is.False, "no normal attack in the air");

            AttackTick t = Press(a, Air(1.0f, 1.0f), s);
            Assert.That(t.PlungeStarted, Is.True, "a flat-ground jump apex (1.28 m) is high enough");
            Assert.That(a.Mode, Is.EqualTo(AttackMode.Plunge));
            Assert.That(a.Plunge.Falling, Is.True);
            Assert.That(a.Plunge.StartY, Is.EqualTo(1.0f).Within(Eps));
            Assert.That(a.Holding, Is.False, "air presses never charge");

            var g = New();
            Assert.That(Press(g, Ground, s).PlungeStarted, Is.False, "on the ground the press goes to the combo");
            Assert.That(g.Combo.Attacking, Is.True);
        }

        [Test]
        public void Climbing_Dodging_Block_Gliding_Allows()
        {
            var s = Full();
            var a = New();
            Press(a, new AttackContext(false, false, true, false, 5f, 5f), s);
            Assert.That(a.LastBlock, Is.EqualTo(AttackBlock.Climbing));
            Assert.That(a.Plunge.Active, Is.False);
            Press(a, new AttackContext(false, true, false, false, 5f, 5f), s);
            Assert.That(a.LastBlock, Is.EqualTo(AttackBlock.Dodging));
            Assert.That(a.Plunge.Active, Is.False);

            AttackTick t = Press(a, new AttackContext(false, false, false, true, 3f, 3f), s);
            Assert.That(t.PlungeStarted, Is.True, "plunge from a glide (the caller folds the glider)");
        }

        [Test]
        public void FallHeight_PicksLowOrHighMultiplier_At5m()
        {
            var r = Plunge();
            Assert.That(PlungeAttack.MultiplierFor(r, 4.99f, out bool h1), Is.EqualTo(1.28f).Within(Eps));
            Assert.That(h1, Is.False);
            Assert.That(PlungeAttack.MultiplierFor(r, 5.0f, out bool h2), Is.EqualTo(1.60f).Within(Eps));
            Assert.That(h2, Is.True);
            Assert.That(PlungeAttack.MultiplierFor(r, 1.28f, out bool h3), Is.EqualTo(1.28f).Within(Eps), "a jump plunge is low");
            Assert.That(h3, Is.False);

            var s = Full();
            var a = New();
            Press(a, Air(10f, 10f), s);
            Assert.That(a.Land(0f), Is.True);
            Assert.That(a.Plunge.FallHeight, Is.EqualTo(10f).Within(Eps));
            Assert.That(a.Plunge.High, Is.True);
            Assert.That(a.Plunge.Multiplier, Is.EqualTo(1.60f).Within(Eps));

            var b = New();
            Press(b, Air(1.2f, 1.2f), s);
            b.Land(0f);
            Assert.That(b.Plunge.High, Is.False);
            Assert.That(b.Plunge.Multiplier, Is.EqualTo(1.28f).Within(Eps));
        }

        [Test]
        public void Landing_HitsEachTargetOnce_OnlyAfterLanding()
        {
            var s = Full();
            var a = New();
            var x = new EntityId(7);
            var y = new EntityId(8);
            Press(a, Air(10f, 10f), s);
            Assert.That(a.Plunge.TryHit(x), Is.False, "no hit while falling");
            Assert.That(a.Land(0f), Is.True);
            Assert.That(a.Land(0f), Is.False, "lands once");
            Assert.That(a.Plunge.TryHit(x), Is.True);
            Assert.That(a.Plunge.TryHit(y), Is.True);
            Assert.That(a.Plunge.TryHit(x), Is.False, "same target, same plunge");
            Assert.That(a.Plunge.LandCount, Is.EqualTo(1));
        }

        [Test]
        public void LandRecovery_040s_IsBusy_ThenFree()
        {
            var s = Full();
            var a = New();
            Press(a, Air(10f, 10f), s);
            a.Land(0f);
            Steps(a, 19, false, Ground, s);
            Assert.That(a.Plunge.Phase, Is.EqualTo(PlungePhase.Landing));
            Press(a, Ground, s, false);
            Assert.That(a.LastBlock, Is.EqualTo(AttackBlock.Busy), "press during landing recovery");
            Assert.That(a.Combo.Attacking, Is.False);
            Assert.That(a.Plunge.Active, Is.False, "0.40 s = 20 steps (the 20th is the press step above)");
            Assert.That(a.Plunge.LastEnd, Is.EqualTo(AttackEnd.Finished));
            Assert.That(a.Plunge.TryHit(new EntityId(9)), Is.False, "no hit after the landing ends");
            Press(a, Ground, s, false);
            Assert.That(a.Combo.Attacking, Is.True);
        }

        [Test]
        public void LandRecovery_CanBeCutByDodgeOrJump()
        {
            var s = Full();
            var a = New();
            Press(a, Air(10f, 10f), s);
            a.Land(0f);
            Steps(a, 3, false, Ground, s);
            a.Interrupt(AttackEnd.Dodge);
            Assert.That(a.Plunge.Active, Is.False);
            Assert.That(a.Plunge.LastEnd, Is.EqualTo(AttackEnd.Dodge));

            var b = New();
            Press(b, Air(10f, 10f), s);
            b.Land(0f);
            b.Interrupt(AttackEnd.Jump);
            Assert.That(b.Plunge.LastEnd, Is.EqualTo(AttackEnd.Jump));
        }

        [Test]
        public void ZeroLandRecovery_StillLandsAndHits_ThenEnds()
        {
            var s = Full();
            var a = New(0f);
            Press(a, Air(10f, 10f), s);
            Assert.That(a.Land(0f), Is.True);
            Assert.That(a.Plunge.Phase, Is.EqualTo(PlungePhase.Landing), "the landing hit check still happens");
            Assert.That(a.Plunge.TryHit(new EntityId(3)), Is.True);
            a.Step(false, false, Ground, s, Dt);
            Assert.That(a.Plunge.Active, Is.False);
        }

        [Test]
        public void PressWhileFalling_IsBusy()
        {
            var s = Full();
            var a = New();
            Press(a, Air(10f, 10f), s);
            AttackTick t = Press(a, Air(8f, 8f), s);
            Assert.That(t.PlungeStarted, Is.False);
            Assert.That(a.LastBlock, Is.EqualTo(AttackBlock.Busy));
            Assert.That(a.Plunge.StartY, Is.EqualTo(10f).Within(Eps), "start height is kept");
            Assert.That(a.Plunge.Count, Is.EqualTo(1));
        }
    }
}
