using Moa.Core.Combat;
using Moa.Core.Ids;
using Moa.Core.Motion;
using NUnit.Framework;

namespace Moa.Tests
{
    /// <summary>
    /// 3-2 교체 연계 순수 규칙: 교체 등장 공격(EntryAttack · Party가 창을 연다) · 스킬 직후 교체 강화(HandoffRules · HandoffBuff) · 보호막(PartyVitals).
    /// 값은 BALANCE 14번 (등장 창 0.8초 · 스킬 뒤 교체 창 3초 · 그로기 ×1.5, 돌진 베기 1.2 · 0.08/0.10/0.32 · 2m).
    /// 한 고정 스텝의 순서는 PlayerCombat.Step과 같다: 교체 → 현재 캐릭터 Step → Party.Advance.
    /// </summary>
    public class SwitchComboTests
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
        static readonly ComboStepRules[] EntrySlash = { new ComboStepRules(1.20f, 0.08f, 0.10f, 0.32f, 0f, 2.0f) };

        // 칸: 영웅 · 수호자 · 떠돌이 · 현자 (BALANCE 14번)
        static readonly HandoffRules[] Handoffs =
        {
            new HandoffRules(0.15f, 0f, 0, 0f, 6f),
            new HandoffRules(0f, 0.30f, 300, 0f, 8f),
            new HandoffRules(0.10f, 0f, 0, 0f, 5f),
            new HandoffRules(0.25f, 0f, 0, 15f, 8f),
        };

        static PlayerAttack Member(float tapCooldown, float burstCost, ComboStepRules[] entry) =>
            new PlayerAttack(new ComboRules(SwordSteps, 0.5f), new ChargedRules(ChargedSlashes, 20f),
                new PlungeRules(1.0f, 5.0f, 1.28f, 1.60f, 20f, 0.40f), 0.36f,
                new SkillRules(new SkillFormRules(TapSlashes, tapCooldown, 10f), new SkillFormRules(HoldSlashes, 8f, 15f), 0.5f),
                new BurstRules(BurstSlashes, burstCost, 12f, true), entry);

        static PlayerAttack[] Members() => new[] { Member(5f, 40f, EntrySlash), Member(4f, 60f, EntrySlash), Member(6f, 50f, EntrySlash), Member(5f, 40f, EntrySlash) };

        static Party New() => new Party(new PartyRules(1.0f, 0.6f, 0.8f, 3.0f, 1.5f), Members(), Handoffs);

        static Stamina Full() => new Stamina(new StaminaRules(240f, 25f, 1f, 60f));

        static readonly AttackContext Ground = new AttackContext(true, false, false, false, 0f, 0f);
        static readonly AttackContext Dodging = new AttackContext(true, true, false, false, 0f, 0f);
        static AttackInput None => new AttackInput(false, false, false, false, false);
        static AttackInput AttackPress => new AttackInput(true, false, false, false, false);
        static AttackInput SkillTap => new AttackInput(false, false, true, false, false);

        static AttackTick Step(Party p, int switchTo, AttackInput input, AttackContext ctx, Stamina s)
        {
            if (switchTo >= 0) p.TrySwitch(switchTo, false);
            AttackTick t = p.Active.Step(input, ctx, s, Dt);
            p.Advance(Dt);
            return t;
        }

        static void Idle(Party p, int n, Stamina s)
        {
            for (int i = 0; i < n; i++) Step(p, -1, None, Ground, s);
        }

        static VitalsRules Vitals() => new VitalsRules(0.30f, 0.5f, 1.0f, 0.6f, 0.8f, 0.5f, 0.5f);

        // ---- 교체 등장 공격 ----

        [Test]
        public void Switch_OpensEntryWindow_AttackOnLastOf40Steps_IsEntry()
        {
            var p = New();
            var s = Full();
            Step(p, 1, None, Ground, s); // 1스텝 (교체)
            Assert.That(p.Active.Entry.WindowOpen, Is.True);
            Assert.That(p.Active.Entry.WindowLeft, Is.EqualTo(0.8f - Dt).Within(Eps));
            Idle(p, 38, s); // 39스텝
            AttackTick t = Step(p, -1, AttackPress, Ground, s); // 40스텝 = 0.8초
            Assert.That(t.EntryStarted, Is.True, "0.8 s = 40 fixed steps, counting the switch step");
            Assert.That(p.Active.Mode, Is.EqualTo(AttackMode.Entry));
            Assert.That(p.Active.Entry.WindowOpen, Is.False, "used once, then closed");
        }

        [Test]
        public void EntryWindow_Missed_NormalComboHit1()
        {
            var p = New();
            var s = Full();
            Step(p, 1, None, Ground, s);
            Idle(p, 39, s); // 40스텝 — 창이 닫힘
            Assert.That(p.Active.Entry.WindowOpen, Is.False);
            Assert.That(p.Active.Entry.ExpiredCount, Is.EqualTo(1));
            AttackTick t = Step(p, -1, AttackPress, Ground, s);
            Assert.That(t.EntryStarted, Is.False);
            Assert.That(p.Active.Mode, Is.EqualTo(AttackMode.Combo));
            Assert.That(p.Active.Combo.StepIndex, Is.EqualTo(0));
        }

        [Test]
        public void Entry_OneDashSlash_Lunge2m_Busy_ThenComboStartsAtHit1()
        {
            var p = New();
            var s = Full();
            AttackTick t = Step(p, 1, AttackPress, Ground, s); // 교체한 스텝에 바로 공격
            Assert.That(t.EntryStarted && t.Swing.StepStarted, Is.True);
            float lunge = t.Swing.Lunge;
            int steps = 1, hitSteps = t.Swing.HitOpen ? 1 : 0;
            while (p.Active.Entry.Active)
            {
                AttackInput input = steps == 5 ? AttackPress : None; // 베는 중 누름은 Busy
                t = Step(p, -1, input, Ground, s);
                lunge += t.Swing.Lunge;
                if (t.Swing.HitOpen) hitSteps++;
                steps++;
                Assert.That(steps, Is.LessThan(100));
            }
            Assert.That(steps, Is.EqualTo(25), "0.08 + 0.10 + 0.32 = 0.50 s");
            Assert.That(lunge, Is.EqualTo(2.0f).Within(Eps), "dash 2 m during windup + hit window");
            Assert.That(hitSteps, Is.EqualTo(5), "hit window 0.10 s = 5 steps");
            Assert.That(p.Active.LastBlock, Is.EqualTo(AttackBlock.Busy));
            Assert.That(p.Active.Entry.LastEnd, Is.EqualTo(AttackEnd.Finished));
            Step(p, -1, AttackPress, Ground, s);
            Assert.That(p.Active.Mode, Is.EqualTo(AttackMode.Combo));
            Assert.That(p.Active.Combo.StepIndex, Is.EqualTo(0), "the combo starts from hit 1 after the entry attack");
        }

        [Test]
        public void Entry_HitsEachTargetOnce()
        {
            var p = New();
            var s = Full();
            Step(p, 1, AttackPress, Ground, s);
            var ids = new IdGenerator();
            EntityId a = ids.Next(), b = ids.Next();
            Assert.That(p.Active.TryHitSwing(a), Is.True);
            Assert.That(p.Active.TryHitSwing(a), Is.False);
            Assert.That(p.Active.TryHitSwing(b), Is.True);
            Assert.That(p.Active.CurrentSwing.Multiplier, Is.EqualTo(1.2f).Within(Eps));
        }

        [Test]
        public void NoSwitch_NoEntry_SoloAndOldRulesAreAsBefore()
        {
            // 한 캐릭터 (파티 없음) — 창이 열린 적이 없어 일반 공격
            var solo = Member(5f, 40f, EntrySlash);
            var s = Full();
            solo.Step(AttackPress, Ground, s, Dt);
            Assert.That(solo.Mode, Is.EqualTo(AttackMode.Combo));
            Assert.That(solo.CurrentSwing.Multiplier, Is.EqualTo(0.45f).Within(Eps));

            // 2-6 규칙(창 0) — 교체해도 일반 공격
            var old = new Party(new PartyRules(1.0f, 0.6f), Members());
            Step(old, 1, AttackPress, Ground, s);
            Assert.That(old.Active.Mode, Is.EqualTo(AttackMode.Combo));
            Assert.That(old.Buff.Active, Is.False);
            Assert.That(old.DamageMultiplier, Is.EqualTo(1f));
            Assert.That(old.PoiseMultiplier, Is.EqualTo(1f));
        }

        [Test]
        public void NoEntrySlash_WindowDoesNotOpen()
        {
            var p = new Party(new PartyRules(1.0f, 0.6f, 0.8f, 3.0f, 1.5f),
                new[] { Member(5f, 40f, null), Member(4f, 60f, null) }, null);
            var s = Full();
            Step(p, 1, AttackPress, Ground, s);
            Assert.That(p.Active.Entry.HasSlash, Is.False);
            Assert.That(p.Active.Mode, Is.EqualTo(AttackMode.Combo));
        }

        [Test]
        public void ForceSwitch_NoEntryWindow_NoHandoff()
        {
            var p = New();
            var s = Full();
            Step(p, -1, SkillTap, Ground, s); // 영웅 E
            Assert.That(p.HandoffLeft(0), Is.EqualTo(3f).Within(Eps));
            Assert.That(p.ForceSwitch(2), Is.True); // 쓰러져 바뀜
            Assert.That(p.Active.Entry.WindowOpen, Is.False);
            Assert.That(p.Buff.Active, Is.False);
        }

        [Test]
        public void SkillOrBurst_InWindow_ClosesIt()
        {
            var p = New();
            var s = Full();
            Step(p, 1, None, Ground, s);
            Step(p, -1, SkillTap, Ground, s);
            Assert.That(p.Active.Entry.WindowOpen, Is.False, "E closes the entry window");
            Assert.That(p.Active.Mode, Is.EqualTo(AttackMode.Skill));
        }

        [Test]
        public void Entry_DodgeInterrupts_AndDodgingPressIsRefused()
        {
            var p = New();
            var s = Full();
            Step(p, 1, None, Ground, s);
            Step(p, -1, AttackPress, Dodging, s); // 회피 중 누름 — 등장 공격 안 나감, 창은 그대로
            Assert.That(p.Active.Entry.LastBlock, Is.EqualTo(EntryBlock.Dodging));
            Assert.That(p.Active.Entry.WindowOpen, Is.True);
            Step(p, -1, AttackPress, Ground, s);
            Assert.That(p.Active.Mode, Is.EqualTo(AttackMode.Entry));
            p.Active.Interrupt(AttackEnd.Dodge);
            Assert.That(p.Active.Entry.Active, Is.False);
            Assert.That(p.Active.Entry.LastEnd, Is.EqualTo(AttackEnd.Dodge));
        }

        // ---- 스킬 직후 교체 강화 ----

        [Test]
        public void Handoff_SwitchWithin3sAfterE_GivesBuff_ThenNot()
        {
            var p = New();
            var s = Full();
            Step(p, -1, SkillTap, Ground, s); // 영웅 E (이 스텝부터 3초)
            Idle(p, 149, s);
            Assert.That(p.HandoffLeft(0), Is.GreaterThan(0f));
            Step(p, 2, None, Ground, s); // 150스텝 뒤 = 3.0초 — 떠돌이로
            Assert.That(p.Buff.Active, Is.True);
            Assert.That(p.Buff.From, Is.EqualTo(0));
            Assert.That(p.Buff.To, Is.EqualTo(2));
            Assert.That(p.DamageMultiplier, Is.EqualTo(1.15f).Within(Eps));
            Assert.That(p.HandoffCount, Is.EqualTo(1));

            var q = New();
            Step(q, -1, SkillTap, Ground, s);
            Idle(q, 150, s); // 151스텝 뒤
            Assert.That(q.HandoffLeft(0), Is.EqualTo(0f));
            Step(q, 2, None, Ground, s);
            Assert.That(q.Buff.Active, Is.False, "too late");
            Assert.That(q.DamageMultiplier, Is.EqualTo(1f));
        }

        [Test]
        public void Handoff_NoSkill_NoBuff()
        {
            var p = New();
            var s = Full();
            Step(p, 1, None, Ground, s);
            Assert.That(p.Buff.Active, Is.False);
        }

        [Test]
        public void GuardianHandoff_PoisePlus30_Shield300_Lasts8s()
        {
            var p = New();
            var s = Full();
            Step(p, 1, None, Ground, s);   // 수호자로
            Idle(p, 50, s);                 // 교체 쿨
            Step(p, -1, SkillTap, Ground, s);
            Step(p, 2, None, Ground, s);    // 떠돌이로 (1스텝 뒤)
            Assert.That(p.Buff.Active, Is.True);
            Assert.That(p.PoiseMultiplier, Is.EqualTo(1.3f).Within(Eps));
            Assert.That(p.DamageMultiplier, Is.EqualTo(1f));
            Assert.That(p.Buff.ShieldLeft, Is.EqualTo(300));
            Idle(p, 398, s); // 교체 스텝 포함 399스텝
            Assert.That(p.Buff.Active, Is.True);
            Idle(p, 1, s);   // 400스텝 = 8초
            Assert.That(p.Buff.Active, Is.False);
            Assert.That(p.Buff.LastEnd, Is.EqualTo(HandoffEnd.Expired));
            Assert.That(p.Buff.ShieldLeft, Is.EqualTo(0));
            Assert.That(p.PoiseMultiplier, Is.EqualTo(1f));
        }

        [Test]
        public void Buff_EndsWhenReceiverLeavesField()
        {
            var p = New();
            var s = Full();
            Step(p, -1, SkillTap, Ground, s);
            Step(p, 2, None, Ground, s);
            Assert.That(p.Buff.Active, Is.True);
            Idle(p, 50, s);
            Step(p, 1, None, Ground, s); // 떠돌이가 나감 (E를 안 써서 새 버프 없음)
            Assert.That(p.Buff.Active, Is.False);
            Assert.That(p.Buff.LastEnd, Is.EqualTo(HandoffEnd.Left));
            Assert.That(p.DamageMultiplier, Is.EqualTo(1f));
        }

        [Test]
        public void SageHandoff_Energy15Once_Damage25()
        {
            var p = New();
            var s = Full();
            Step(p, 3, None, Ground, s); // 현자로
            Idle(p, 50, s);
            Step(p, -1, SkillTap, Ground, s);
            float before = p.Member(2).Energy.Current;
            Step(p, 2, None, Ground, s); // 떠돌이로
            Assert.That(p.Member(2).Energy.Current - before, Is.EqualTo(15f).Within(Eps));
            Assert.That(p.LastHandoffEnergy, Is.EqualTo(15f).Within(Eps));
            Assert.That(p.DamageMultiplier, Is.EqualTo(1.25f).Within(Eps));
            Assert.That(p.HandoffLeft(3), Is.EqualTo(0f), "used up by the switch");
        }

        [Test]
        public void GrantHandoff_Debug_AppliesToActive()
        {
            var p = New();
            Assert.That(p.GrantHandoff(1), Is.True);
            Assert.That(p.Buff.To, Is.EqualTo(0));
            Assert.That(p.Buff.ShieldLeft, Is.EqualTo(300));
            p.ClearHandoff();
            Assert.That(p.Buff.Active, Is.False);
            Assert.That(p.Buff.LastEnd, Is.EqualTo(HandoffEnd.Cleared));
        }

        [Test]
        public void Shield_AbsorbsFirst_NoStunUntilItBreaks()
        {
            var p = New();
            var v = new PartyVitals(Vitals(), new[] { 1000, 1300, 850, 900 }, new[] { 100f, 200f, 80f, 100f }, p);
            var s = Full();
            Step(p, 1, None, Ground, s);
            Idle(p, 50, s);
            Step(p, -1, SkillTap, Ground, s); // 수호자 E
            Step(p, 2, None, Ground, s);      // 떠돌이 (방어 80) — Grunt 한 대 104
            PlayerHit h = v.TakeHit(100f, 1.25f, 400f, true);
            Assert.That(h.Requested, Is.EqualTo(104));
            Assert.That(h.Absorbed, Is.EqualTo(104));
            Assert.That(h.Applied, Is.EqualTo(0));
            Assert.That(h.Stunned, Is.False, "fully blocked: no hit stun");
            Assert.That(v.Member(2).Current, Is.EqualTo(850));
            v.TakeHit(100f, 1.25f, 400f, true);
            Assert.That(p.Buff.ShieldLeft, Is.EqualTo(92));
            h = v.TakeHit(100f, 1.25f, 400f, true);
            Assert.That(h.Absorbed, Is.EqualTo(92));
            Assert.That(h.Applied, Is.EqualTo(12));
            Assert.That(h.Stunned, Is.True);
            Assert.That(v.Member(2).Current, Is.EqualTo(838));
            Assert.That(p.Buff.Active, Is.True, "the poise bonus stays after the shield breaks");
        }

        [Test]
        public void Shield_NotUsedByDebugDown_OrWhenInvulnerable()
        {
            var p = New();
            var v = new PartyVitals(Vitals(), new[] { 1000, 1300, 850, 900 }, new[] { 100f, 200f, 80f, 100f }, p);
            p.GrantHandoff(1);
            v.Invulnerable = true;
            v.TakeHit(100f, 1.25f, 400f, true);
            Assert.That(p.Buff.ShieldLeft, Is.EqualTo(300));
            v.Invulnerable = false;
            PlayerHit h = v.DownActive();
            Assert.That(h.Downed, Is.True);
            Assert.That(h.Absorbed, Is.EqualTo(0));
        }

        [Test]
        public void Reset_ClearsBuffAndWindows()
        {
            var p = New();
            var s = Full();
            Step(p, -1, SkillTap, Ground, s);
            Step(p, 2, None, Ground, s);
            p.Reset();
            Assert.That(p.Buff.Active, Is.False);
            Assert.That(p.HandoffLeft(0), Is.EqualTo(0f));
            Assert.That(p.Active.Entry.WindowOpen, Is.False);
        }
    }
}
