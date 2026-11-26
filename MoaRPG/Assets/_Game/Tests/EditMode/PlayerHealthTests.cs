using Moa.Core.Combat;
using Moa.Core.Motion;
using NUnit.Framework;

namespace Moa.Tests
{
    /// <summary>
    /// 3-1 플레이어 체력 · 쓰러짐 순수 규칙 (Moa.Core.Combat.PartyVitals · Party 쓰러짐 · Health.Heal/Revive). 값은 BALANCE 13번
    /// (경직 0.3초 · 밀려남 0.5m · 어두워짐 1.0 / 0.6 / 0.8초 · 부활 50% · 회복 초당 50%), 캐릭터 체력 · 방어는 12번 표의 4명, Grunt 100 × 1.25.
    /// </summary>
    public class PlayerHealthTests
    {
        const float Dt = 0.02f;
        const float Eps = 1e-4f;
        const float DefenseConstant = 400f;
        const float GruntAttack = 100f, GruntMultiplier = 1.25f;

        static readonly int[] MaxHealth = { 1000, 1300, 850, 900 };
        static readonly float[] Defense = { 100f, 200f, 80f, 100f };

        static VitalsRules Rules() => new VitalsRules(0.30f, 0.5f, 1.0f, 0.6f, 0.8f, 0.5f, 0.5f);

        static readonly ComboStepRules[] SwordSteps =
        {
            new ComboStepRules(0.45f, 0.10f, 0.10f, 0.20f, 0.10f, 0.30f),
            new ComboStepRules(0.45f, 0.10f, 0.10f, 0.22f, 0.10f, 0.30f),
        };
        static readonly ComboStepRules[] Slashes = { new ComboStepRules(1.0f, 0.10f, 0.10f, 0.10f, 0f, 0.30f) };

        static PlayerAttack Member() =>
            new PlayerAttack(new ComboRules(SwordSteps, 0.5f), new ChargedRules(Slashes, 20f),
                new PlungeRules(1.0f, 5.0f, 1.28f, 1.60f, 20f, 0.40f), 0.36f,
                new SkillRules(new SkillFormRules(Slashes, 5f, 10f), new SkillFormRules(Slashes, 8f, 15f), 0.5f),
                new BurstRules(Slashes, 40f, 12f, true));

        static Party NewParty() => new Party(new PartyRules(1.0f, 0.6f), new[] { Member(), Member(), Member(), Member() });

        static PartyVitals New(out Party party)
        {
            party = NewParty();
            return new PartyVitals(Rules(), MaxHealth, Defense, party);
        }

        static PlayerHit Grunt(PartyVitals v, bool ground = true) => v.TakeHit(GruntAttack, GruntMultiplier, DefenseConstant, ground);

        [Test]
        public void GruntHit_UsesDamageMath_AndTheFieldCharactersDefense()
        {
            var v = New(out var party);
            PlayerHit h = Grunt(v);
            Assert.That(h.Requested, Is.EqualTo(100), "125 x (1 - 100/500)");
            Assert.That(h.Applied, Is.EqualTo(100));
            Assert.That(v.Member(0).Current, Is.EqualTo(900));
            Assert.That(party.TrySwitch(1, false), Is.True);
            Assert.That(Grunt(v).Requested, Is.EqualTo(83), "guardian: 125 x (1 - 200/600)");
            Assert.That(v.Member(1).Current, Is.EqualTo(1300 - 83));
        }

        [Test]
        public void OnlyTheFieldCharacter_LosesHealth()
        {
            var v = New(out _);
            for (int i = 0; i < 5; i++) Grunt(v);
            Assert.That(v.Member(0).Current, Is.EqualTo(500));
            for (int i = 1; i < 4; i++) Assert.That(v.Member(i).Current, Is.EqualTo(MaxHealth[i]), "bench " + i);
        }

        [Test]
        public void Stun_Lasts15Steps_OnTheGroundOnly()
        {
            var v = New(out _);
            Assert.That(Grunt(v).Stunned, Is.True);
            Assert.That(v.InputBlocked, Is.True);
            Assert.That(v.KnockbackSpeed, Is.EqualTo(0.5f / 0.3f).Within(Eps), "0.5 m over 0.3 s");
            for (int i = 0; i < 14; i++) v.Advance(Dt);
            Assert.That(v.Stunned, Is.True, "14 steps");
            v.Advance(Dt);
            Assert.That(v.Stunned, Is.False, "15 steps = 0.3 s");

            PlayerHit air = Grunt(v, false);
            Assert.That(air.Stunned, Is.False, "in the air / climbing / gliding: damage only");
            Assert.That(air.Applied, Is.EqualTo(100));
            Grunt(v);
            v.EndStun();
            Assert.That(v.Stunned, Is.False, "a dodge ends the stun");
        }

        [Test]
        public void Invulnerable_NoDamage_NoStun()
        {
            var v = New(out _);
            v.Invulnerable = true;
            PlayerHit h = Grunt(v);
            Assert.That(h.Requested, Is.EqualTo(100));
            Assert.That(h.Applied, Is.EqualTo(0));
            Assert.That(h.Stunned, Is.False);
            Assert.That(v.Member(0).Current, Is.EqualTo(1000));
            v.Invulnerable = false;
            Assert.That(Grunt(v).Applied, Is.EqualTo(100));
        }

        [Test]
        public void Downed_SwitchesToTheNextAliveMember_IgnoringTheCooldown()
        {
            var v = New(out var party);
            Assert.That(party.TrySwitch(1, false), Is.True);
            float cooldown = party.CooldownLeft;
            Assert.That(cooldown, Is.GreaterThan(0f));
            int switches = party.SwitchCount;
            PlayerHit h = v.TakeDamage(1300, true);
            Assert.That(h.Downed, Is.True);
            Assert.That(h.Stunned, Is.False, "no stun when downed");
            Assert.That(h.SwitchedTo, Is.EqualTo(2));
            Assert.That(h.AllDown, Is.False);
            Assert.That(party.ActiveIndex, Is.EqualTo(2));
            Assert.That(party.SwitchCount, Is.EqualTo(switches + 1));
            Assert.That(party.CooldownLeft, Is.EqualTo(cooldown).Within(Eps), "the forced switch starts no new cooldown");
            Assert.That(party.IsDowned(1), Is.True);
            Assert.That(v.Member(2).Current, Is.EqualTo(850), "the new field character is untouched");
        }

        [Test]
        public void NextAlive_SkipsDownedMembers_AndWrapsAround()
        {
            var v = New(out var party);
            Assert.That(party.TrySwitch(2, false), Is.True);
            Assert.That(v.DownActive().SwitchedTo, Is.EqualTo(3));
            Assert.That(v.DownActive().SwitchedTo, Is.EqualTo(0), "wraps to slot 1");
            Assert.That(v.DownActive().SwitchedTo, Is.EqualTo(1), "skips the downed 3 and 4");
            PlayerHit last = v.DownActive();
            Assert.That(last.SwitchedTo, Is.EqualTo(-1));
            Assert.That(last.AllDown, Is.True);
            Assert.That(v.AllDown, Is.True);
            Assert.That(v.Wipe, Is.EqualTo(WipePhase.FadeOut));
        }

        [Test]
        public void DownedMember_CannotBeSwitchedTo_UntilRevived()
        {
            var v = New(out var party);
            v.DownActive();
            Assert.That(party.ActiveIndex, Is.EqualTo(1));
            party.ResetCooldown();
            Assert.That(party.TrySwitch(0, false), Is.False);
            Assert.That(party.LastBlock, Is.EqualTo(SwitchBlock.Downed));
            Assert.That(party.TrySwitch(2, false), Is.True, "alive members still switch");
            v.RestoreAll();
            party.ResetCooldown();
            Assert.That(party.TrySwitch(0, false), Is.True, "revived");
        }

        [Test]
        public void ForcedSwitch_InterruptsTheDownedMembersAttack()
        {
            var v = New(out var party);
            var stamina = new Stamina(new StaminaRules(240f, 25f, 1f, 60f));
            party.Active.Step(new AttackInput(true, false, false, false, false), new AttackContext(true, false, false, false, 0f, 0f), stamina, Dt);
            Assert.That(party.Member(0).Mode, Is.EqualTo(AttackMode.Combo));
            v.DownActive();
            Assert.That(party.Member(0).Mode, Is.EqualTo(AttackMode.None));
            Assert.That(party.LastInterrupted, Is.EqualTo(AttackMode.Combo));
            Assert.That(party.Member(0).Combo.LastEnd, Is.EqualTo(AttackEnd.Switch));
        }

        [Test]
        public void EveryoneDown_DarkensThenRevivesAtHalf_ThenComesBack()
        {
            var v = New(out var party);
            Grunt(v);
            PlayerHit h = v.DownAll();
            Assert.That(h.AllDown, Is.True);
            Assert.That(v.WipeCount, Is.EqualTo(1));
            Assert.That(v.InputBlocked, Is.True);
            Assert.That(Grunt(v).Ignored, Is.True, "no hits while the screen goes dark");
            Assert.That(v.TakeDamage(10, true).Ignored, Is.True);

            int respawnStep = -1, endStep = -1;
            for (int step = 1; step <= 200 && endStep < 0; step++)
            {
                VitalsTick t = v.Advance(Dt);
                if (step == 25) Assert.That(v.Darkness, Is.EqualTo(0.5f).Within(1e-3f), "half way through the fade out");
                if (t.Respawned) respawnStep = step;
                if (t.WipeEnded) endStep = step;
                if (step == respawnStep)
                {
                    Assert.That(v.Darkness, Is.EqualTo(1f).Within(Eps));
                    for (int i = 0; i < 4; i++)
                    {
                        Assert.That(v.Member(i).Current, Is.EqualTo(MaxHealth[i] / 2), "revived at 50% " + i);
                        Assert.That(party.IsDowned(i), Is.False);
                    }
                }
            }
            Assert.That(respawnStep, Is.EqualTo(50), "1.0 s fade out");
            Assert.That(endStep, Is.EqualTo(120), "+ 0.6 s dark + 0.8 s fade in");
            Assert.That(v.Wiping, Is.False);
            Assert.That(v.Darkness, Is.EqualTo(0f));
            Assert.That(v.RespawnCount, Is.EqualTo(1));
            Assert.That(Grunt(v).Applied, Is.GreaterThan(0), "takes hits again");
        }

        [Test]
        public void HealingPoint_RevivesDowned_AndHealsEveryone()
        {
            var v = New(out var party);
            v.TakeDamage(500, false);
            party.ResetCooldown();
            Assert.That(party.TrySwitch(1, false), Is.True);
            v.DownActive(); // 수호자 쓰러짐 → 3번
            Assert.That(party.ActiveIndex, Is.EqualTo(2));
            int healed = v.Heal(Dt);
            Assert.That(v.Member(0).Current, Is.EqualTo(510), "1000 x 0.5/s x 0.02 = 10");
            Assert.That(v.Member(1).Current, Is.EqualTo(13), "revived with one step's heal (1300 x 0.01)");
            Assert.That(party.IsDowned(1), Is.False);
            Assert.That(healed, Is.EqualTo(10 + 13));
            for (int i = 0; i < 99; i++) v.Heal(Dt);
            for (int i = 0; i < 4; i++) Assert.That(v.Member(i).Current, Is.EqualTo(MaxHealth[i]), "full after 2 s " + i);
            Assert.That(v.Heal(Dt), Is.EqualTo(0), "nothing to heal");
        }

        [Test]
        public void HealingPoint_DoesNothing_WhileWiping()
        {
            var v = New(out _);
            v.DownAll();
            Assert.That(v.Heal(Dt), Is.EqualTo(0));
            Assert.That(v.AllDown, Is.True);
        }

        [Test]
        public void WithoutAParty_OneMember_DownStartsTheWipe()
        {
            var v = new PartyVitals(Rules(), new[] { 1000 }, new[] { 100f }, null);
            Assert.That(v.Count, Is.EqualTo(1));
            Assert.That(Grunt(v).Applied, Is.EqualTo(100));
            PlayerHit h = v.DownActive();
            Assert.That(h.AllDown, Is.True);
            Assert.That(v.Wiping, Is.True);
            for (int i = 0; i < 50; i++) v.Advance(Dt);
            Assert.That(v.Member(0).Current, Is.EqualTo(500));
        }

        [Test]
        public void Downed_ClearsTheStun()
        {
            var v = New(out _);
            Assert.That(Grunt(v).Stunned, Is.True);
            v.DownActive();
            Assert.That(v.Stunned, Is.False, "the next character comes in standing");
        }

        [Test]
        public void Health_Heal_AndRevive()
        {
            var h = new Health(100);
            h.TakeDamage(30);
            Assert.That(h.Heal(50), Is.EqualTo(30), "up to max");
            Assert.That(h.Current, Is.EqualTo(100));
            h.TakeDamage(100);
            Assert.That(h.Heal(10), Is.EqualTo(0), "dead: heal does nothing");
            h.Revive(0);
            Assert.That(h.Current, Is.EqualTo(1), "at least 1");
            h.Revive(500);
            Assert.That(h.Current, Is.EqualTo(1), "alive: revive does nothing");
            h.TakeDamage(1);
            h.Revive(500);
            Assert.That(h.Current, Is.EqualTo(100), "at most max");
        }
    }
}
