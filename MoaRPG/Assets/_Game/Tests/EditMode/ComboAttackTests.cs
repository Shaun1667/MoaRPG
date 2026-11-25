using Moa.Core.Combat;
using Moa.Core.Ids;
using NUnit.Framework;

namespace Moa.Tests
{
    /// <summary>2-1 일반 공격 콤보 순수 규칙 (Moa.Core.Combat.ComboAttack). 값은 BALANCE 6번 한손검.</summary>
    public class ComboAttackTests
    {
        const float Dt = 0.02f;
        const float Eps = 1e-4f;

        static readonly ComboStepRules[] SwordSteps =
        {
            new ComboStepRules(0.45f, 0.10f, 0.10f, 0.20f, 0.10f, 0.30f),
            new ComboStepRules(0.45f, 0.10f, 0.10f, 0.22f, 0.10f, 0.30f),
            new ComboStepRules(0.60f, 0.12f, 0.10f, 0.24f, 0.12f, 0.40f),
            new ComboStepRules(0.65f, 0.12f, 0.12f, 0.26f, 0.12f, 0.40f),
            new ComboStepRules(0.85f, 0.16f, 0.14f, 0.40f, 0.16f, 0.60f),
        };
        static ComboRules Sword => new ComboRules(SwordSteps, 0.5f);

        static readonly AttackContext Ground = new AttackContext(true, false, false, false);

        /// <summary>조건이 될 때까지 진행 (최대 steps 스텝). 진행한 스텝 수.</summary>
        static int AdvanceUntil(ComboAttack c, System.Func<ComboAttack, bool> done, int steps = 500)
        {
            int n = 0;
            while (!done(c) && n < steps) { c.Advance(Dt); n++; }
            return n;
        }

        static void AdvanceSeconds(ComboAttack c, float seconds)
        {
            int n = (int)System.Math.Round(seconds / Dt);
            for (int i = 0; i < n; i++) c.Advance(Dt);
        }

        [Test]
        public void Press_FromIdle_StartsStep1_InWindup()
        {
            var c = new ComboAttack(Sword);
            Assert.That(c.Press(Ground), Is.True);
            Assert.That(c.StepIndex, Is.EqualTo(0));
            Assert.That(c.Phase, Is.EqualTo(ComboPhase.Windup));
            ComboTick t = c.Advance(Dt);
            Assert.That(t.StepStarted, Is.True);
            Assert.That(c.Advance(Dt).StepStarted, Is.False);
        }

        [Test]
        public void Step_Phases_FollowWindupActiveRecovery()
        {
            var c = new ComboAttack(Sword);
            c.Press(Ground);
            AdvanceSeconds(c, 0.08f);
            Assert.That(c.Phase, Is.EqualTo(ComboPhase.Windup));
            AdvanceSeconds(c, 0.04f); // 0.12
            Assert.That(c.Phase, Is.EqualTo(ComboPhase.Active));
            AdvanceSeconds(c, 0.10f); // 0.22
            Assert.That(c.Phase, Is.EqualTo(ComboPhase.Recovery));
            AdvanceSeconds(c, 0.18f); // 0.40 = total
            Assert.That(c.Phase, Is.EqualTo(ComboPhase.Idle));
            Assert.That(c.LastEnd, Is.EqualTo(AttackEnd.Finished));
            Assert.That(c.NextStep, Is.EqualTo(1));
        }

        [Test]
        public void HitOpen_OnlyDuringActiveWindow()
        {
            var c = new ComboAttack(Sword);
            c.Press(Ground);
            int open = 0, firstOpen = -1;
            for (int i = 0; i < 20; i++)
            {
                if (c.Advance(Dt).HitOpen) { open++; if (firstOpen < 0) firstOpen = i; }
            }
            Assert.That(open, Is.EqualTo(5), "0.10 s window = 5 fixed steps");
            Assert.That(firstOpen, Is.EqualTo(5), "opens after the 0.10 s windup");
        }

        [Test]
        public void Queued_InWindow_ChainsAllFiveSteps_ThenBackToStep1()
        {
            var c = new ComboAttack(Sword);
            c.Press(Ground);
            for (int step = 0; step < 5; step++)
            {
                Assert.That(c.StepIndex, Is.EqualTo(step));
                AdvanceSeconds(c, SwordSteps[step].InputOpen);
                bool accepted = c.Press(Ground);
                if (step < 4)
                {
                    Assert.That(accepted, Is.True, "step " + (step + 1) + " queues the next");
                    AdvanceUntil(c, x => x.StepIndex != step);
                }
                else
                {
                    Assert.That(accepted, Is.False);
                    Assert.That(c.LastBlock, Is.EqualTo(AttackBlock.LastStep));
                }
            }
            AdvanceUntil(c, x => !x.Attacking);
            Assert.That(c.SwingCount, Is.EqualTo(5));
            Assert.That(c.NextStep, Is.EqualTo(0), "after the last hit the next press is hit 1");
            Assert.That(c.Press(Ground), Is.True);
            Assert.That(c.StepIndex, Is.EqualTo(0));
        }

        [Test]
        public void Queued_NextStep_StartsRightAfterRecovery()
        {
            var c = new ComboAttack(Sword);
            c.Press(Ground);
            AdvanceSeconds(c, 0.10f);
            c.Press(Ground);
            int steps = AdvanceUntil(c, x => x.StepIndex == 1);
            Assert.That((steps + 5) * Dt, Is.EqualTo(SwordSteps[0].Total).Within(Eps));
            Assert.That(c.Advance(Dt).StepStarted, Is.True);
        }

        [Test]
        public void Press_BeforeInputOpen_IsDropped()
        {
            var c = new ComboAttack(Sword);
            c.Press(Ground);
            AdvanceSeconds(c, 0.04f);
            Assert.That(c.Press(Ground), Is.False);
            Assert.That(c.LastBlock, Is.EqualTo(AttackBlock.TooEarly));
            AdvanceUntil(c, x => !x.Attacking);
            Assert.That(c.StepIndex, Is.EqualTo(0), "no chain from a dropped press");
        }

        [Test]
        public void PressWithinResetTime_AfterStep_ContinuesCombo()
        {
            var c = new ComboAttack(Sword);
            c.Press(Ground);
            AdvanceUntil(c, x => !x.Attacking);
            AdvanceSeconds(c, 0.4f); // < 0.5
            Assert.That(c.Press(Ground), Is.True);
            Assert.That(c.StepIndex, Is.EqualTo(1));
        }

        [Test]
        public void MissingTheWindow_BreaksCombo_BackToStep1()
        {
            var c = new ComboAttack(Sword);
            c.Press(Ground);
            AdvanceUntil(c, x => !x.Attacking);
            Assert.That(c.ResetLeft, Is.EqualTo(0.5f).Within(Eps));
            AdvanceSeconds(c, 0.5f);
            Assert.That(c.NextStep, Is.EqualTo(0));
            Assert.That(c.BreakCount, Is.EqualTo(1));
            Assert.That(c.Press(Ground), Is.True);
            Assert.That(c.StepIndex, Is.EqualTo(0));
        }

        [Test]
        public void Lunge_SumsToStepLunge_DuringWindupAndActive()
        {
            var c = new ComboAttack(Sword);
            c.Press(Ground);
            float total = 0f, afterActive = 0f;
            while (c.Attacking)
            {
                bool recovery = c.Phase == ComboPhase.Recovery;
                float l = c.Advance(Dt).Lunge;
                total += l;
                if (recovery) afterActive += l;
            }
            Assert.That(total, Is.EqualTo(0.30f).Within(1e-4f));
            Assert.That(afterActive, Is.EqualTo(0f).Within(1e-6f));
        }

        [Test]
        public void OneSwing_HitsEachTargetOnce()
        {
            var c = new ComboAttack(Sword);
            var a = new EntityId(5);
            var b = new EntityId(6);
            c.Press(Ground);
            AdvanceSeconds(c, 0.12f);
            Assert.That(c.TryHit(a), Is.True);
            Assert.That(c.TryHit(b), Is.True, "another target in the same swing");
            Assert.That(c.TryHit(a), Is.False, "same target, same swing");
            Assert.That(c.TryHit(b), Is.False);
            Assert.That(c.HitsThisSwing, Is.EqualTo(2));
            Assert.That(c.TryHit(EntityId.None), Is.False, "no id");

            c.Press(Ground);
            AdvanceUntil(c, x => x.StepIndex == 1);
            Assert.That(c.TryHit(a), Is.True, "next swing hits again");
        }

        [Test]
        public void TryHit_WhenIdle_IsFalse()
        {
            var c = new ComboAttack(Sword);
            Assert.That(c.TryHit(new EntityId(1)), Is.False);
        }

        [Test]
        public void Dodge_InterruptsAttack_AndResetsCombo()
        {
            var c = new ComboAttack(Sword);
            c.Press(Ground);
            AdvanceSeconds(c, 0.12f);
            c.Press(Ground);
            c.Interrupt(AttackEnd.Dodge);
            Assert.That(c.Attacking, Is.False);
            Assert.That(c.Queued, Is.False);
            Assert.That(c.LastEnd, Is.EqualTo(AttackEnd.Dodge));
            Assert.That(c.NextStep, Is.EqualTo(0));
            var dodging = new AttackContext(true, true, false, false);
            Assert.That(c.Press(dodging), Is.False);
            Assert.That(c.LastBlock, Is.EqualTo(AttackBlock.Dodging));
            Assert.That(c.Press(Ground), Is.True);
            Assert.That(c.StepIndex, Is.EqualTo(0));
        }

        [Test]
        public void Climbing_Gliding_Airborne_CannotAttack()
        {
            var c = new ComboAttack(Sword);
            Assert.That(c.Press(new AttackContext(false, false, true, false)), Is.False);
            Assert.That(c.LastBlock, Is.EqualTo(AttackBlock.Climbing));
            Assert.That(c.Press(new AttackContext(false, false, false, true)), Is.False);
            Assert.That(c.LastBlock, Is.EqualTo(AttackBlock.Gliding));
            Assert.That(c.Press(new AttackContext(false, false, false, false)), Is.False);
            Assert.That(c.LastBlock, Is.EqualTo(AttackBlock.Airborne));
            Assert.That(c.Attacking, Is.False);
            Assert.That(c.SwingCount, Is.EqualTo(0));
        }

        [Test]
        public void StepCount_ComesFromRules_NoWeaponBranch()
        {
            var three = new ComboRules(new[] { SwordSteps[0], SwordSteps[1], SwordSteps[4] }, 0.5f);
            var c = new ComboAttack(three);
            c.Press(Ground);
            for (int i = 0; i < 2; i++)
            {
                AdvanceSeconds(c, c.CurrentStep.InputOpen);
                Assert.That(c.Press(Ground), Is.True);
                int from = c.StepIndex;
                AdvanceUntil(c, x => x.StepIndex != from);
            }
            Assert.That(c.IsLastStep, Is.True);
            Assert.That(c.CurrentStep.Multiplier, Is.EqualTo(0.85f).Within(Eps));
        }
    }
}
