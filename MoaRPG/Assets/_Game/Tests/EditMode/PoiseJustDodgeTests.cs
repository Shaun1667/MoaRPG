using Moa.Core.Combat;
using Moa.Core.Motion;
using NUnit.Framework;

namespace Moa.Tests
{
    /// <summary>
    /// 2-3 경직 게이지 · 그로기 · 저스트 회피 · 예고 공격 순수 규칙 (Moa.Core.Combat.Poise · JustDodge · EnemyAttackCycle).
    /// 값은 BALANCE 6번(한손검 경직) · 7번(저스트 회피) · 8번(허수아비 경직) · 9번(예고 공격).
    /// </summary>
    public class PoiseJustDodgeTests
    {
        const float Dt = 0.02f;
        const float Eps = 1e-4f;

        static Poise NewPoise() => new Poise(new PoiseRules(100f, 5f, 1.3f, 2f, 25f));
        static JustDodge NewJust() => new JustDodge(new JustDodgeRules(0.16f, 0.3f, 0.8f, 3f, 2f));
        static EnemyAttackCycle NewCycle() => new EnemyAttackCycle(new EnemyAttackRules(2f, 0.8f, 0.6f));
        static Dodge NewDodge() => new Dodge(new DodgeRules(3.5f, 0.3f, 0.2f, 0.2f, 20f, true));

        static void Steps(Poise p, int n) { for (int i = 0; i < n; i++) p.Advance(Dt); }

        // ---- 경직 · 그로기 ----

        [Test]
        public void Poise_Accumulates_BreaksAtMax()
        {
            var p = NewPoise();
            int broken = 0;
            p.Broken += () => broken++;
            Assert.That(p.Apply(54f), Is.False);
            Assert.That(p.Current, Is.EqualTo(54f).Within(Eps));
            Assert.That(p.Apply(45f), Is.False, "99 < 100");
            Assert.That(p.Groggy, Is.False);
            Assert.That(p.Apply(8f), Is.True, "107 >= 100");
            Assert.That(p.Groggy, Is.True);
            Assert.That(p.Current, Is.EqualTo(100f).Within(Eps), "clamped to max");
            Assert.That(p.BreakCount, Is.EqualTo(1));
            Assert.That(broken, Is.EqualTo(1));
        }

        [Test]
        public void Groggy_Lasts5Seconds_ThenGaugeEmpty()
        {
            var p = NewPoise();
            int recovered = 0;
            p.Recovered += () => recovered++;
            p.Apply(100f);
            Steps(p, 249);
            Assert.That(p.Groggy, Is.True, "4.98 s");
            Assert.That(p.GroggyLeft, Is.EqualTo(0.02f).Within(Eps));
            Steps(p, 1);
            Assert.That(p.Groggy, Is.False, "5.00 s");
            Assert.That(p.Current, Is.EqualTo(0f));
            Assert.That(p.RecoverCount, Is.EqualTo(1));
            Assert.That(recovered, Is.EqualTo(1));
        }

        [Test]
        public void Groggy_TakesDamageTimes13_AndNoMorePoise()
        {
            var p = NewPoise();
            Assert.That(p.DamageTakenMultiplier, Is.EqualTo(1f));
            p.Apply(100f);
            Assert.That(p.DamageTakenMultiplier, Is.EqualTo(1.3f).Within(Eps));
            Assert.That(p.Apply(50f), Is.False, "no build-up while groggy");
            Assert.That(p.BreakCount, Is.EqualTo(1));
            Steps(p, 100);
            Assert.That(p.GroggyLeft, Is.EqualTo(3f).Within(1e-3f), "hits do not extend groggy");
        }

        [Test]
        public void Groggy_DamageOnSword_WithDefense100()
        {
            // 한손검 1 ~ 5타가 그로기 허수아비(방어 100)에: 36 · 36 · 48 · 52 · 68 → 47 · 47 · 62 · 68 · 88
            float[] mult = { 0.45f, 0.45f, 0.60f, 0.65f, 0.85f };
            int[] expected = { 47, 47, 62, 68, 88 };
            for (int i = 0; i < mult.Length; i++)
                Assert.That(DamageMath.Compute(100f, mult[i] * 1.3f, 100f, 400f), Is.EqualTo(expected[i]), "hit " + (i + 1));
        }

        [Test]
        public void Regen_WaitsDelay_ThenDrains25PerSecond()
        {
            var p = NewPoise();
            p.Apply(50f);
            Steps(p, 100); // 2.0 s
            Assert.That(p.Current, Is.EqualTo(50f).Within(Eps), "no drain during the 2 s delay");
            Steps(p, 50); // +1.0 s
            Assert.That(p.Current, Is.EqualTo(25f).Within(1e-3f));
            Steps(p, 60);
            Assert.That(p.Current, Is.EqualTo(0f), "floors at 0");
        }

        [Test]
        public void Regen_HitRestartsDelay()
        {
            var p = NewPoise();
            p.Apply(40f);
            Steps(p, 90); // 1.8 s
            p.Apply(10f);
            Steps(p, 90);
            Assert.That(p.Current, Is.EqualTo(50f).Within(Eps), "delay started again on the second hit");
            Assert.That(p.RegenWait, Is.EqualTo(0.2f).Within(1e-3f));
        }

        [Test]
        public void Reset_ClearsGroggyAndGauge()
        {
            var p = NewPoise();
            p.Apply(100f);
            p.Reset();
            Assert.That(p.Groggy, Is.False);
            Assert.That(p.Current, Is.EqualTo(0f));
            Assert.That(p.DamageTakenMultiplier, Is.EqualTo(1f));
            Assert.That(p.Apply(0f), Is.False);
            Assert.That(p.Apply(-5f), Is.False);
            Assert.That(p.Current, Is.EqualTo(0f), "zero or negative poise does nothing");
        }

        [Test]
        public void SwordCombo_BreaksOnSecondCombo_OrFirstWithCounter()
        {
            // BALANCE 6번 경직: 콤보 8 · 8 · 10 · 12 · 16 = 54
            float[] combo = { 8f, 8f, 10f, 12f, 16f };
            var p = NewPoise();
            int hitsToBreak = 0;
            for (int round = 0; round < 2 && !p.Groggy; round++)
                foreach (float v in combo) { hitsToBreak++; if (p.Apply(v)) break; }
            Assert.That(hitsToBreak, Is.EqualTo(10), "first combo 54, second combo breaks on its 5th hit (108)");

            var q = NewPoise();
            int counterHits = 0;
            foreach (float v in combo) { counterHits++; if (q.Apply(v * 2f)) break; }
            Assert.That(counterHits, Is.EqualTo(5), "with the just-dodge counter x2 one combo (108) breaks");
        }

        // ---- 저스트 회피 ----

        [Test]
        public void Classify_HitAvoidedJust()
        {
            Assert.That(JustDodge.Classify(false, 0.02f, 0.16f), Is.EqualTo(DodgeJudgement.Hit), "not invulnerable");
            Assert.That(JustDodge.Classify(true, 0.02f, 0.16f), Is.EqualTo(DodgeJudgement.Just));
            Assert.That(JustDodge.Classify(true, 0.16f, 0.16f), Is.EqualTo(DodgeJudgement.Just), "window edge is inside");
            Assert.That(JustDodge.Classify(true, 0.18f, 0.16f), Is.EqualTo(DodgeJudgement.Avoided));
        }

        [Test]
        public void WithDodge_8StepsJust_9Avoided_10Hit()
        {
            // 회피를 누른 스텝부터 공격이 닿는 스텝까지: 8스텝(0.16초) 저스트, 9스텝 무적만, 10스텝(0.20초)이면 무적이 끝나 맞음
            int[] steps = { 1, 8, 9, 10 };
            DodgeJudgement[] expected = { DodgeJudgement.Just, DodgeJudgement.Just, DodgeJudgement.Avoided, DodgeJudgement.Hit };
            for (int k = 0; k < steps.Length; k++)
            {
                var d = NewDodge();
                Assert.That(d.TryStart(null, true), Is.True);
                for (int i = 0; i < steps[k]; i++) d.Advance(Dt);
                Assert.That(JustDodge.Classify(d.Invulnerable, d.Elapsed, 0.16f), Is.EqualTo(expected[k]), steps[k] + " steps");
            }
            Assert.That(JustDodge.Classify(NewDodge().Invulnerable, 0f, 0.16f), Is.EqualTo(DodgeJudgement.Hit), "no dodge = hit");
        }

        [Test]
        public void Just_StartsSlowAndCounter()
        {
            var j = NewJust();
            Assert.That(j.TimeScale, Is.EqualTo(1f));
            Assert.That(j.PoiseMultiplier, Is.EqualTo(1f));
            Assert.That(j.Judge(true, 0.04f), Is.EqualTo(DodgeJudgement.Just));
            Assert.That(j.JustCount, Is.EqualTo(1));
            Assert.That(j.TimeScale, Is.EqualTo(0.3f).Within(Eps));
            Assert.That(j.PoiseMultiplier, Is.EqualTo(2f).Within(Eps));
            Assert.That(j.SlowLeft, Is.EqualTo(0.8f).Within(Eps));
            Assert.That(j.CounterLeft, Is.EqualTo(3f).Within(Eps));
        }

        [Test]
        public void Slow_CountsRealTime_Counter_CountsGameTime()
        {
            var j = NewJust();
            j.Judge(true, 0.02f);
            for (int i = 0; i < 39; i++) j.AdvanceReal(Dt);
            Assert.That(j.Slowing, Is.True, "0.78 s real");
            j.AdvanceReal(Dt);
            Assert.That(j.Slowing, Is.False, "0.80 s real");
            Assert.That(j.TimeScale, Is.EqualTo(1f));
            Assert.That(j.CounterActive, Is.True, "game-time counter untouched by real time");
            for (int i = 0; i < 149; i++) j.Advance(Dt);
            Assert.That(j.CounterActive, Is.True, "2.98 s");
            j.Advance(Dt);
            Assert.That(j.CounterActive, Is.False, "3.00 s");
            Assert.That(j.PoiseMultiplier, Is.EqualTo(1f));
        }

        [Test]
        public void AvoidedAndHit_GiveNoReward()
        {
            var j = NewJust();
            Assert.That(j.Judge(true, 0.18f), Is.EqualTo(DodgeJudgement.Avoided));
            Assert.That(j.Judge(false, 0f), Is.EqualTo(DodgeJudgement.Hit));
            Assert.That(j.Slowing || j.CounterActive, Is.False);
            Assert.That(j.AvoidCount, Is.EqualTo(1));
            Assert.That(j.HitCount, Is.EqualTo(1));
            Assert.That(j.Last, Is.EqualTo(DodgeJudgement.Hit));
            j.Judge(true, 0.02f);
            j.Cancel();
            Assert.That(j.Slowing || j.CounterActive, Is.False, "cancel clears the reward");
            Assert.That(j.JustCount, Is.EqualTo(1), "counts stay");
        }

        // ---- 예고 공격 ----

        [Test]
        public void Cycle_TelegraphAfterInterval_HitWhenTelegraphEnds()
        {
            var c = NewCycle();
            int hitStep = -1, telegraphStep = -1;
            for (int i = 1; i <= 200 && hitStep < 0; i++)
            {
                bool hit = c.Advance(Dt, true);
                if (telegraphStep < 0 && c.Phase == EnemyAttackPhase.Telegraph) telegraphStep = i;
                if (hit) hitStep = i;
            }
            Assert.That(telegraphStep, Is.EqualTo(100), "warning starts at 2.0 s");
            Assert.That(hitStep, Is.EqualTo(140), "hit lands 0.8 s later (2.8 s)");
            Assert.That(c.Phase, Is.EqualTo(EnemyAttackPhase.Recovery));
            Assert.That(c.AttackCount, Is.EqualTo(1));
        }

        [Test]
        public void Cycle_PeriodIs34Seconds()
        {
            var c = NewCycle();
            int first = -1, second = -1;
            for (int i = 1; i <= 400 && second < 0; i++)
            {
                if (!c.Advance(Dt, true)) continue;
                if (first < 0) first = i; else second = i;
            }
            Assert.That(second - first, Is.EqualTo(170), "0.6 recovery + 2.0 rest + 0.8 warning = 3.4 s");
            Assert.That(c.Rules.Period, Is.EqualTo(3.4f).Within(Eps));
        }

        [Test]
        public void Cycle_TimeToHit_CountsDown()
        {
            var c = NewCycle();
            for (int i = 0; i < 100; i++) c.Advance(Dt, true);
            Assert.That(c.Phase, Is.EqualTo(EnemyAttackPhase.Telegraph));
            Assert.That(c.TimeToHit, Is.EqualTo(0.8f).Within(Eps));
            for (int i = 0; i < 32; i++) c.Advance(Dt, true);
            Assert.That(c.TimeToHit, Is.EqualTo(0.16f).Within(1e-3f), "just window opens 8 steps before the hit");
            Assert.That(c.TelegraphRatio, Is.EqualTo(0.8f).Within(1e-3f));
        }

        [Test]
        public void Cycle_CannotAct_CancelsAndRestarts()
        {
            var c = NewCycle();
            for (int i = 0; i < 120; i++) c.Advance(Dt, true); // 예고 중
            Assert.That(c.Phase, Is.EqualTo(EnemyAttackPhase.Telegraph));
            Assert.That(c.Advance(Dt, false), Is.False);
            Assert.That(c.Phase, Is.EqualTo(EnemyAttackPhase.Idle));
            Assert.That(c.CancelCount, Is.EqualTo(1));
            for (int i = 0; i < 250; i++) Assert.That(c.Advance(Dt, false), Is.False, "groggy: no attack");
            int hitStep = -1;
            for (int i = 1; i <= 200 && hitStep < 0; i++) if (c.Advance(Dt, true)) hitStep = i;
            Assert.That(hitStep, Is.EqualTo(140), "starts again from the rest time");
            Assert.That(c.AttackCount, Is.EqualTo(1));
        }
    }
}
