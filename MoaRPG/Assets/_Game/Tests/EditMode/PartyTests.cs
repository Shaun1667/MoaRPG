using Moa.Core.Combat;
using Moa.Core.Motion;
using NUnit.Framework;

namespace Moa.Tests
{
    /// <summary>
    /// 2-6 파티 4인 교체 순수 규칙 (Moa.Core.Combat.Party). 값은 BALANCE 12번 (교체 쿨 1초 · 대기 처치 에너지 ×0.6).
    /// 한 고정 스텝의 순서는 PlayerCombat.Step과 같다: 교체 → 현재 캐릭터 Step → Party.Advance.
    /// </summary>
    public class PartyTests
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

        static PlayerAttack Member(float tapCooldown, float burstCost) =>
            new PlayerAttack(new ComboRules(SwordSteps, 0.5f), new ChargedRules(ChargedSlashes, 20f),
                new PlungeRules(1.0f, 5.0f, 1.28f, 1.60f, 20f, 0.40f), 0.36f,
                new SkillRules(new SkillFormRules(TapSlashes, tapCooldown, 10f), new SkillFormRules(HoldSlashes, 8f, 15f), 0.5f),
                new BurstRules(BurstSlashes, burstCost, 12f, true));

        static Party New() => new Party(new PartyRules(1.0f, 0.6f),
            new[] { Member(5f, 40f), Member(4f, 60f), Member(6f, 50f), Member(5f, 40f) });

        static Stamina Full() => new Stamina(new StaminaRules(240f, 25f, 1f, 60f));

        static readonly AttackContext Ground = new AttackContext(true, false, false, false, 0f, 0f);
        static AttackContext Air(float height) => new AttackContext(false, false, false, false, height, height);
        static AttackInput None => new AttackInput(false, false, false, false, false);
        static AttackInput AttackPress => new AttackInput(true, false, false, false, false);
        static AttackInput SkillTap => new AttackInput(false, false, true, false, false);
        static AttackInput SkillPressHeld => new AttackInput(false, false, true, true, false);
        static AttackInput SkillHeld => new AttackInput(false, false, false, true, false);
        static AttackInput BurstPress => new AttackInput(false, false, false, false, true);

        /// <summary>PlayerCombat.Step과 같은 순서 (switchTo &lt; 0 = 교체 없음). 교체가 받아졌으면 true.</summary>
        static bool Step(Party p, int switchTo, AttackInput input, AttackContext ctx, Stamina s, bool gliding = false)
        {
            bool switched = switchTo >= 0 && p.TrySwitch(switchTo, gliding);
            p.Active.Step(input, ctx, s, Dt);
            p.Advance(Dt);
            return switched;
        }

        static void Idle(Party p, int n, Stamina s)
        {
            for (int i = 0; i < n; i++) Step(p, -1, None, Ground, s);
        }

        // ---- 교체 · 쿨 ----

        [Test]
        public void Starts_OnSlot1_Ready()
        {
            var p = New();
            Assert.That(p.Count, Is.EqualTo(4));
            Assert.That(p.ActiveIndex, Is.EqualTo(0));
            Assert.That(p.Ready, Is.True);
            Assert.That(p.SwitchCount, Is.EqualTo(0));
        }

        [Test]
        public void Switch_ChangesActive_Cooldown50Steps()
        {
            var p = New();
            var s = Full();
            Assert.That(Step(p, 1, None, Ground, s), Is.True);
            Assert.That(p.ActiveIndex, Is.EqualTo(1));
            Assert.That(p.LastFrom, Is.EqualTo(0));
            Assert.That(p.CooldownLeft, Is.EqualTo(1f - Dt).Within(Eps), "cooldown starts at the switch step");
            Idle(p, 48, s); // 49스텝
            Assert.That(p.Ready, Is.False);
            Assert.That(Step(p, 2, None, Ground, s), Is.False, "step 50 starts before the cooldown ends");
            Assert.That(p.LastBlock, Is.EqualTo(SwitchBlock.Cooldown));
            Assert.That(p.Ready, Is.True, "1 s = 50 fixed steps");
            Assert.That(Step(p, 2, None, Ground, s), Is.True);
            Assert.That(p.ActiveIndex, Is.EqualTo(2));
            Assert.That(p.SwitchCount, Is.EqualTo(2));
        }

        [Test]
        public void Switch_SameOrMissingSlot_Refused()
        {
            var p = New();
            Assert.That(p.TrySwitch(0, false), Is.False);
            Assert.That(p.LastBlock, Is.EqualTo(SwitchBlock.SameMember));
            Assert.That(p.TrySwitch(4, false), Is.False);
            Assert.That(p.LastBlock, Is.EqualTo(SwitchBlock.NoMember));
            Assert.That(p.TrySwitch(-1, false), Is.False);
            Assert.That(p.LastBlock, Is.EqualTo(SwitchBlock.NoMember));
            Assert.That(p.Ready, Is.True, "a refused switch does not start the cooldown");
            var small = new Party(new PartyRules(1f, 0.6f), new[] { Member(5f, 40f), Member(5f, 40f) });
            Assert.That(small.TrySwitch(2, false), Is.False, "a 2-member party has no slot 3");
            Assert.That(small.LastBlock, Is.EqualTo(SwitchBlock.NoMember));
        }

        // ---- 공격 · 스킬 · 궁극기 · 낙하 · 활공 중 교체 ----

        [Test]
        public void Switch_DuringCombo_InterruptsAndNewMemberIdle()
        {
            var p = New();
            var s = Full();
            var first = p.Active;
            Step(p, -1, AttackPress, Ground, s);
            Idle(p, 3, s);
            Assert.That(first.Mode, Is.EqualTo(AttackMode.Combo));
            Assert.That(Step(p, 1, None, Ground, s), Is.True);
            Assert.That(p.LastInterrupted, Is.EqualTo(AttackMode.Combo));
            Assert.That(first.Mode, Is.EqualTo(AttackMode.None), "the outgoing combo is cut");
            Assert.That(first.Combo.LastEnd, Is.EqualTo(AttackEnd.Switch));
            Assert.That(first.Combo.NextStep, Is.EqualTo(0), "its combo goes back to the 1st hit");
            Assert.That(p.Active.Mode, Is.EqualTo(AttackMode.None));
            Step(p, -1, AttackPress, Ground, s);
            Assert.That(p.Active.Mode, Is.EqualTo(AttackMode.Combo), "the new member can attack at once");
            Assert.That(p.Active.Combo.StepIndex, Is.EqualTo(0));
        }

        [Test]
        public void Switch_WhileSkillPressing_NoCooldownSpent()
        {
            var p = New();
            var s = Full();
            var first = p.Active;
            Step(p, -1, SkillPressHeld, Ground, s);
            Step(p, -1, SkillHeld, Ground, s);
            Assert.That(first.Skill.Pressing, Is.True);
            Assert.That(Step(p, 1, None, Ground, s), Is.True);
            Assert.That(first.Skill.Pressing, Is.False);
            Assert.That(first.Skill.Ready, Is.True, "cut before the cast: no cooldown");
            Assert.That(first.Skill.LastEnd, Is.EqualTo(AttackEnd.Switch));
        }

        [Test]
        public void Switch_DuringSkillCast_CutsSlash_CooldownKeepsRunningOnBench()
        {
            var p = New();
            var s = Full();
            var first = p.Active;
            Step(p, -1, SkillTap, Ground, s); // 1스텝: 쿨 5 − 0.02
            Assert.That(first.Skill.Casting, Is.True);
            Assert.That(Step(p, 1, None, Ground, s), Is.True); // 2스텝: 대기에서 쿨이 돈다
            Assert.That(first.Skill.Casting, Is.False);
            Assert.That(first.Skill.CooldownLeft, Is.EqualTo(5f - 2 * Dt).Within(Eps), "one tick per step, also on the switch step");
            Idle(p, 247, s); // 249스텝
            Assert.That(first.Skill.Ready, Is.False);
            Idle(p, 1, s); // 250스텝 = 5초
            Assert.That(first.Skill.Ready, Is.True, "the off-field cooldown ends at 5 s like on-field");
        }

        [Test]
        public void Switch_DuringBurst_Refused()
        {
            var p = New();
            var s = Full();
            p.Active.Energy.Fill();
            Step(p, -1, BurstPress, Ground, s);
            Assert.That(p.Active.Burst.Active, Is.True);
            Assert.That(Step(p, 1, None, Ground, s), Is.False);
            Assert.That(p.LastBlock, Is.EqualTo(SwitchBlock.Burst));
            Assert.That(p.ActiveIndex, Is.EqualTo(0));
            Idle(p, 60, s); // 1.2초 뒤 궁극기 끝
            Assert.That(p.Active.Burst.Active, Is.False);
            Assert.That(Step(p, 1, None, Ground, s), Is.True);
        }

        [Test]
        public void Switch_PlungeFallingRefused_LandingRecoveryCut()
        {
            var p = New();
            var s = Full();
            Step(p, -1, AttackPress, Air(5f), s);
            Assert.That(p.Active.Plunge.Falling, Is.True);
            Assert.That(Step(p, 1, None, Air(4f), s), Is.False);
            Assert.That(p.LastBlock, Is.EqualTo(SwitchBlock.Plunge));
            Assert.That(p.Active.Land(0f), Is.True);
            Assert.That(p.Active.Plunge.Active, Is.True, "landing recovery");
            var first = p.Active;
            Assert.That(Step(p, 1, None, Ground, s), Is.True, "the landing recovery can be cut like an attack");
            Assert.That(first.Plunge.Active, Is.False);
            Assert.That(p.LastInterrupted, Is.EqualTo(AttackMode.Plunge));
        }

        [Test]
        public void Switch_WhileGliding_Refused_ClimbingDodgingAirborneAllowed()
        {
            var p = New();
            var s = Full();
            Assert.That(Step(p, 1, None, Air(10f), s, gliding: true), Is.False);
            Assert.That(p.LastBlock, Is.EqualTo(SwitchBlock.Gliding));
            // 공중 · 회피 · 등반은 막지 않는다 (이동 상태는 몸 하나라 그대로 이어진다)
            Assert.That(Step(p, 1, None, Air(10f), s), Is.True);
            Idle(p, 50, s);
            Assert.That(Step(p, 2, None, new AttackContext(true, true, false, false, 0f, 0f), s), Is.True, "dodging");
            Idle(p, 50, s);
            Assert.That(Step(p, 3, None, new AttackContext(false, false, true, false), s), Is.True, "climbing");
        }

        // ---- 캐릭터마다 따로: 에너지 · 쿨 ----

        [Test]
        public void EnergyAndCooldowns_ArePerMember()
        {
            var p = New();
            var s = Full();
            var first = p.Active;
            Step(p, -1, SkillTap, Ground, s);
            first.TakeSkillEnergy();
            Assert.That(first.Energy.Current, Is.EqualTo(10f).Within(Eps));
            Step(p, 1, None, Ground, s);
            var second = p.Active;
            Assert.That(second.Energy.Current, Is.EqualTo(0f));
            Assert.That(second.Skill.Ready, Is.True, "the new member's skill is its own");
            Assert.That(second.Energy.Max, Is.EqualTo(60f), "energy gauge = this member's burst cost");
            Step(p, -1, SkillTap, Ground, s);
            Assert.That(second.Skill.Casting, Is.True);
            Assert.That(second.Skill.CooldownLeft, Is.EqualTo(4f - Dt).Within(Eps));
            Assert.That(first.Skill.CooldownLeft, Is.EqualTo(5f - 3 * Dt).Within(Eps));
        }

        [Test]
        public void BurstCooldown_RunsOnBench()
        {
            var p = New();
            var s = Full();
            var first = p.Active;
            first.Energy.Fill();
            Step(p, -1, BurstPress, Ground, s);
            Idle(p, 60, s); // 궁극기 끝 (61스텝)
            Step(p, 1, None, Ground, s); // 62스텝
            Assert.That(first.Burst.CooldownLeft, Is.EqualTo(12f - 62 * Dt).Within(1e-3f));
            Idle(p, 600 - 62 - 1, s); // 599스텝
            Assert.That(first.Burst.Ready, Is.False);
            Idle(p, 1, s); // 600스텝 = 12초
            Assert.That(first.Burst.Ready, Is.True);
            Assert.That(first.Energy.Current, Is.EqualTo(0f), "the burst spent its energy");
        }

        [Test]
        public void DefeatEnergy_ActiveFull_BenchTimesRatio()
        {
            var p = New();
            float active = p.GainDefeatEnergy(5f, out float bench);
            Assert.That(active, Is.EqualTo(5f).Within(Eps));
            Assert.That(bench, Is.EqualTo(3f * 3f).Within(Eps), "three off-field members get 5 x 0.6 = 3 each");
            Assert.That(p.Member(0).Energy.Current, Is.EqualTo(5f).Within(Eps));
            for (int i = 1; i < 4; i++) Assert.That(p.Member(i).Energy.Current, Is.EqualTo(3f).Within(Eps));
        }

        [Test]
        public void DefeatEnergy_DuringBurst_ActiveGetsNothing_BenchStillGets()
        {
            var p = New();
            var s = Full();
            p.Active.Energy.Fill();
            Step(p, -1, BurstPress, Ground, s);
            float active = p.GainDefeatEnergy(5f, out float bench);
            Assert.That(active, Is.EqualTo(0f), "2-4 rule: no energy during the burst");
            Assert.That(bench, Is.EqualTo(9f).Within(Eps));
        }

        [Test]
        public void Reset_BackToSlot1_CooldownCleared()
        {
            var p = New();
            var s = Full();
            Step(p, 2, None, Ground, s);
            p.Reset();
            Assert.That(p.ActiveIndex, Is.EqualTo(0));
            Assert.That(p.Ready, Is.True);
        }

        [Test]
        public void Rules_ClampRatio_AndRejectEmptyParty()
        {
            Assert.That(new PartyRules(1f, 1.5f).BenchEnergyRatio, Is.EqualTo(1f));
            Assert.That(new PartyRules(-1f, -0.5f).SwitchCooldown, Is.EqualTo(0f));
            Assert.That(new PartyRules(-1f, -0.5f).BenchEnergyRatio, Is.EqualTo(0f));
            Assert.Throws<System.ArgumentException>(() => new Party(new PartyRules(1f, 0.6f), new PlayerAttack[0]));
            Assert.Throws<System.ArgumentException>(() => new Party(new PartyRules(1f, 0.6f), new PlayerAttack[] { Member(5f, 40f), null }));
        }
    }
}
