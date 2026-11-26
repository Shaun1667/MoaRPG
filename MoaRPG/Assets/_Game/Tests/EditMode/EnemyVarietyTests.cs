using System;
using Moa.Core.Combat;
using Moa.Core.Motion;
using NUnit.Framework;

namespace Moa.Tests
{
    /// <summary>
    /// 3-4 적 추가 · 길찾기 (Core): 공격 표(한 줄 = 공격 하나, 차례로) · 원거리(거리 유지 · 투사체) · 정예(내려찍기 넘어짐 · 돌진 · 안 멈춤) ·
    /// 길찾기(IEnemyPath — 없으면 곧장 = 2-5) · 플레이어 넘어짐(PartyVitals). 수치는 BALANCE 16번과 같은 값을 직접 넣는다 — 에셋 검산은 EnemyVarietyAssetTests.
    /// </summary>
    public class EnemyVarietyTests
    {
        const float Dt = 0.02f;
        const float Eps = 1e-3f;

        static EnemyAttackSpec Melee(float mult, float start, float range, float angle, float interval, float telegraph, float recovery,
            float active = 0f, float lunge = 0f, float knockdown = 0f)
            => new EnemyAttackSpec(mult, start, range, angle, new EnemyAttackRules(interval, telegraph, recovery, active), lunge, knockdown, HitDelivery.Melee, default);

        static EnemyAttackSpec Arrow() => new EnemyAttackSpec(0.9f, 12f, 15f, 0f, new EnemyAttackRules(1.0f, 1.0f, 0.6f), 0f, 0f,
            HitDelivery.Projectile, new ProjectileRules(15f, 1.0f, 0.2f));

        static EnemyAttackSpec Slam() => Melee(2.0f, 3.0f, 3.5f, 360f, 0.8f, 1.2f, 1.2f, knockdown: 1.2f);
        static EnemyAttackSpec Charge() => Melee(1.5f, 9.0f, 1.8f, 120f, 0.6f, 0.8f, 1.0f, active: 0.6f, lunge: 7.2f);

        static EnemyAiRules Archer() => new EnemyAiRules(14f, 120f, 3f, 18f, 2f, 20f, 1.5f, 3.0f, 4f, 360f, 2f, 0.3f, 0.3f, 5f, 5f, 2.5f, new[] { Arrow() });
        static EnemyAiRules Brute() => new EnemyAiRules(10f, 120f, 4f, 15f, 3f, 20f, 1.2f, 2.6f, 4f, 240f, 2f, 0.4f, 0f, 8f, 0f, 0f, new[] { Slam(), Charge() });
        static EnemyAiRules Grunt() => new EnemyAiRules(10f, 120f, 3f, 15f, 2f, 20f, 1.5f, 3.5f, 4f, 360f, 2f, 0.3f, 0.3f, 5f, 0f, 0f,
            new[] { Melee(1.25f, 2f, 2.6f, 120f, 0.6f, 0.8f, 0.8f) });

        sealed class FakePath : IEnemyPath
        {
            public Func<Planar, Planar, Planar> F;
            public int Calls;
            public Planar Steer(Planar self, Planar destination) { Calls++; return F(self, destination); }
        }

        sealed class Sim
        {
            public EnemyBrain B;
            public Planar Self;
            public float Yaw;
            public Planar Target;
            public int Landed, Hits, Fired;

            public Sim(EnemyAiRules rules, Planar home, float yaw = 0f)
            {
                B = new EnemyBrain(rules, home, Planar.Zero);
                Self = home;
                Yaw = yaw;
                Target = new Planar(0f, 100f);
            }

            public EnemyTick Step()
            {
                var t = B.Step(Dt, new EnemySense(Self, Yaw, Target, 0f, true), false);
                Self = Self + t.Velocity * Dt;
                Yaw = t.Yaw;
                if (t.AttackLanded) Landed++;
                if (t.AttackHits) Hits++;
                if (t.FireProjectile) Fired++;
                return t;
            }

            public int Until(Func<EnemyTick, bool> cond, int max)
            {
                for (int i = 1; i <= max; i++) if (cond(Step())) return i;
                return -1;
            }
        }

        static float Dist(Planar a, Planar b) => (a - b).Length;

        // ---- 길찾기 ----

        [Test]
        public void Path_SteersToNextCorner_AtChaseSpeed()
        {
            var sim = new Sim(Grunt(), Planar.Zero);
            var corner = new Planar(-3f, 4f);
            var path = new FakePath { F = (self, dest) => self.Z < corner.Z - 0.01f ? corner : dest };
            sim.B.Path = path;
            sim.Target = new Planar(0f, 9f); // 앞 9m — 시야 안
            var t = sim.Step();
            Assert.That(sim.B.State, Is.EqualTo(EnemyState.Chase));
            Assert.That(t.Velocity.Length, Is.EqualTo(3.5f).Within(Eps), "chase speed from EnemyTuning, unchanged");
            Planar dir = t.Velocity.Normalized;
            Planar want = corner.Normalized;
            Assert.That(dir.X, Is.EqualTo(want.X).Within(Eps), "heads for the corner, not the player");
            Assert.That(dir.Z, Is.EqualTo(want.Z).Within(Eps));
            Assert.That(sim.B.LastDestination.Z, Is.EqualTo(9f).Within(Eps), "Core still chose the player as the destination");
            Assert.That(sim.B.LastWaypoint.X, Is.EqualTo(corner.X).Within(Eps));
            Assert.That(path.Calls, Is.GreaterThan(0));
        }

        [Test]
        public void NoPath_OrStraightPath_IsExactly25()
        {
            // 길찾기가 없거나 목적지를 그대로 돌려주면 2-5와 같은 움직임 (NavMesh 없이 곧장)
            var a = new Sim(Grunt(), Planar.Zero);
            var b = new Sim(Grunt(), Planar.Zero);
            b.B.Path = new FakePath { F = (self, dest) => dest };
            a.Target = b.Target = new Planar(1f, 8f);
            for (int i = 0; i < 400; i++)
            {
                var ta = a.Step();
                var tb = b.Step();
                Assert.That(tb.Velocity.X, Is.EqualTo(ta.Velocity.X).Within(1e-5f), "step " + i);
                Assert.That(tb.Velocity.Z, Is.EqualTo(ta.Velocity.Z).Within(1e-5f), "step " + i);
                Assert.That(tb.Yaw, Is.EqualTo(ta.Yaw).Within(1e-4f), "step " + i);
                Assert.That(tb.AttackLanded, Is.EqualTo(ta.AttackLanded), "step " + i);
            }
            Assert.That(a.Landed, Is.GreaterThan(0));
        }

        [Test]
        public void Path_ReturnHome_FollowsCorners()
        {
            var sim = new Sim(Grunt(), Planar.Zero);
            sim.Self = new Planar(0f, 10f);
            var corner = new Planar(3f, 5f);
            sim.B.Path = new FakePath { F = (self, dest) => self.Z > corner.Z + 0.01f ? corner : dest };
            sim.B.ForceState(EnemyState.Return);
            var t = sim.Step();
            Assert.That(t.Velocity.Length, Is.EqualTo(4f).Within(Eps), "return speed");
            Assert.That(t.Velocity.X, Is.GreaterThan(0f), "goes round via the corner");
            int n = sim.Until(x => x.Healed, 1000);
            Assert.That(n, Is.GreaterThan(0), "reaches home");
            Assert.That(Dist(sim.Self, Planar.Zero), Is.LessThanOrEqualTo(0.3f + Eps));
        }

        // ---- 원거리 ----

        [Test]
        public void Ranged_StopsAtStartRange_FiresWhenWarningEnds()
        {
            var sim = new Sim(Archer(), Planar.Zero);
            sim.Target = new Planar(0f, 10f); // 감지 14 · 시작 12 안
            int n = sim.Until(t => t.FireProjectile, 300);
            Assert.That(n, Is.EqualTo(100), "interval 1.0 + warning 1.0 = 100 steps");
            Assert.That(sim.Landed, Is.EqualTo(0), "a projectile row never lands as melee");
            Assert.That(sim.B.FireCount, Is.EqualTo(1));
            Assert.That(sim.B.State, Is.EqualTo(EnemyState.Attack));
            Assert.That(Dist(sim.Self, Planar.Zero), Is.LessThan(Eps), "stands still beyond retreat range");
            // 다음 발: 자세 0.6 + 쉬기 1.0 + 예고 1.0 = 130스텝
            int m = sim.Until(t => t.FireProjectile, 300);
            Assert.That(m, Is.EqualTo(130));
        }

        [Test]
        public void Ranged_AimsDuringWarning()
        {
            var sim = new Sim(Archer(), Planar.Zero);
            sim.Target = new Planar(0f, 10f);
            sim.Until(t => sim.B.Cycle.Phase == EnemyAttackPhase.Telegraph, 200);
            sim.Target = new Planar(6f, 8f); // 예고 중 옆으로
            sim.Until(t => t.FireProjectile, 100);
            float want = MoveMath.YawOf(sim.Target - sim.Self);
            Assert.That(Math.Abs(MoveMath.DeltaAngle(sim.Yaw, want)), Is.LessThan(1f), "turned to the player during the warning");
        }

        [Test]
        public void Ranged_TooClose_BacksAway_FacingPlayer_AtRetreatSpeed()
        {
            var sim = new Sim(Archer(), Planar.Zero);
            sim.Target = new Planar(0f, 3f); // 물러나기 5m 안 (가까이 알아챔 3m)
            var t = sim.Step();
            Assert.That(sim.B.State, Is.EqualTo(EnemyState.Attack));
            Assert.That(sim.B.Retreating, Is.True);
            Assert.That(t.Velocity.Length, Is.EqualTo(2.5f).Within(Eps));
            Assert.That(t.Velocity.Z, Is.LessThan(0f), "away from the player");
            Assert.That(Math.Abs(MoveMath.DeltaAngle(t.Yaw, 0f)), Is.LessThan(Eps), "keeps facing the player");
            sim.Until(x => !sim.B.Retreating, 200);
            Assert.That(Dist(sim.Self, sim.Target), Is.EqualTo(5f).Within(0.06f), "stops at retreat range");
        }

        [Test]
        public void Ranged_DoesNotRetreatDuringWarning()
        {
            var sim = new Sim(Archer(), Planar.Zero);
            sim.Target = new Planar(0f, 8f);
            sim.Until(t => sim.B.Cycle.Phase == EnemyAttackPhase.Telegraph, 200);
            sim.Target = new Planar(0f, 2f); // 예고 중에 다가옴
            var tick = sim.Step();
            Assert.That(tick.Velocity.Length, Is.LessThan(Eps), "commits to the shot");
        }

        // ---- 정예 ----

        [Test]
        public void Elite_AlternatesSlamAndCharge()
        {
            var sim = new Sim(Brute(), Planar.Zero);
            sim.Target = new Planar(0f, 2.5f);
            int n = sim.Until(t => t.AttackLanded, 300);
            Assert.That(n, Is.EqualTo(100), "slam: interval 0.8 + warning 1.2");
            Assert.That(sim.Hits, Is.EqualTo(1), "2.5 m inside the 3.5 m circle");
            Assert.That(sim.B.CurrentAttackIndex, Is.EqualTo(0));
            sim.Until(t => sim.B.CurrentAttackIndex == 1, 100);
            Assert.That(sim.B.CurrentAttack.Sustained, Is.True, "next row = charge");
            int c = sim.Until(t => sim.B.Cycle.Phase == EnemyAttackPhase.Active, 200);
            Assert.That(c, Is.GreaterThan(0));
            Assert.That(sim.B.LungeCount, Is.EqualTo(1));
            sim.Until(t => sim.B.CurrentAttackIndex == 0, 300);
            Assert.That(sim.B.CurrentAttackIndex, Is.EqualTo(0), "back to the slam");
        }

        [Test]
        public void Charge_Covers72m_In06s_AndMissIsReportedAtEnd()
        {
            var rules = new EnemyAiRules(10f, 120f, 4f, 15f, 3f, 20f, 1.2f, 2.6f, 4f, 240f, 2f, 0.4f, 0f, 8f, 0f, 0f, new[] { Charge() });
            var sim = new Sim(rules, Planar.Zero);
            sim.Target = new Planar(0f, 8f);
            sim.Until(t => sim.B.Cycle.Phase == EnemyAttackPhase.Telegraph, 200);
            sim.Target = new Planar(5f, 8f); // 예고 중 옆으로 비킴 (몸은 안 돈다)
            Planar start = sim.Self;
            int moving = 0;
            EnemyTick end = default;
            for (int i = 0; i < 200; i++)
            {
                var t = sim.Step();
                if (t.Velocity.Length > Eps) { moving++; Assert.That(t.Velocity.Length, Is.EqualTo(12f).Within(Eps), "7.2 m / 0.6 s"); }
                if (t.AttackLanded) { end = t; break; }
            }
            Assert.That(moving, Is.EqualTo(30), "0.6 s = 30 steps");
            Assert.That(Dist(sim.Self, start), Is.EqualTo(7.2f).Within(0.01f));
            Assert.That(end.AttackLanded, Is.True);
            Assert.That(end.AttackHits, Is.False, "sidestepped the charge");
            Assert.That(sim.B.AttackMissCount, Is.EqualTo(1));
        }

        [Test]
        public void Charge_HitsOnlyOnce()
        {
            var rules = new EnemyAiRules(10f, 120f, 4f, 15f, 3f, 20f, 1.2f, 2.6f, 4f, 240f, 2f, 0.4f, 0f, 8f, 0f, 0f, new[] { Charge() });
            var sim = new Sim(rules, Planar.Zero);
            sim.Target = new Planar(0f, 6f);
            sim.Until(t => sim.B.Cycle.Phase == EnemyAttackPhase.Recovery, 400);
            Assert.That(sim.Hits, Is.EqualTo(1), "passes through the player but hits once");
            Assert.That(sim.Landed, Is.EqualTo(1));
            Assert.That(sim.B.AttackHitCount, Is.EqualTo(1));
            Assert.That(sim.B.AttackMissCount, Is.EqualTo(0));
        }

        [Test]
        public void Elite_DoesNotFlinch_ButNoticesWhenHit()
        {
            var sim = new Sim(Brute(), Planar.Zero, 180f); // 등 뒤
            sim.Target = new Planar(0f, 8f);
            sim.Step();
            Assert.That(sim.B.State, Is.EqualTo(EnemyState.Idle), "player behind, beyond notice range");
            Assert.That(sim.B.NotifyHit(), Is.False, "hit stun 0 = no flinch");
            Assert.That(sim.B.State, Is.EqualTo(EnemyState.Chase), "but notices");
            sim.Target = new Planar(0f, 2.5f);
            sim.Self = Planar.Zero;
            sim.Until(t => sim.B.State == EnemyState.Attack, 100);
            sim.Step();
            float elapsed = sim.B.Cycle.Elapsed;
            Assert.That(sim.B.NotifyHit(), Is.False);
            Assert.That(sim.B.State, Is.EqualTo(EnemyState.Attack), "keeps attacking");
            sim.Step();
            Assert.That(sim.B.Cycle.Elapsed, Is.GreaterThan(elapsed), "cycle not reset by the hit");
            Assert.That(sim.B.StaggerCount, Is.EqualTo(0));
        }

        [Test]
        public void Grunt_StillFlinches()
        {
            var sim = new Sim(Grunt(), Planar.Zero);
            sim.Target = new Planar(0f, 8f);
            sim.Step();
            Assert.That(sim.B.NotifyHit(), Is.True);
            Assert.That(sim.B.State, Is.EqualTo(EnemyState.Stagger));
        }

        [Test]
        public void Charge_IsArmored_WhileActive()
        {
            var rules = new EnemyAiRules(10f, 120f, 4f, 15f, 3f, 20f, 1.2f, 2.6f, 4f, 240f, 2f, 0.4f, 0.3f, 8f, 0f, 0f, new[] { Charge() });
            var sim = new Sim(rules, Planar.Zero);
            sim.Target = new Planar(0f, 8f);
            sim.Until(t => sim.B.Cycle.Phase == EnemyAttackPhase.Active, 200);
            Assert.That(sim.B.NotifyHit(), Is.False, "no stagger during the charge (even with hit stun)");
            Assert.That(sim.B.ArmorCount, Is.EqualTo(1));
        }

        [Test]
        public void Respawn_StartsFromFirstRow()
        {
            var sim = new Sim(Brute(), Planar.Zero);
            sim.Target = new Planar(0f, 2.5f);
            sim.Until(t => sim.B.CurrentAttackIndex == 1, 300);
            sim.B.Die();
            sim.Until(t => t.Respawned, 1000);
            Assert.That(sim.B.CurrentAttackIndex, Is.EqualTo(0));
            Assert.That(sim.B.Cycle.Phase, Is.EqualTo(EnemyAttackPhase.Idle));
        }

        [Test]
        public void Cycle_WithActive_PeriodAndPhases()
        {
            var c = new EnemyAttackCycle(new EnemyAttackRules(0.6f, 0.8f, 1.0f, 0.6f));
            Assert.That(c.Rules.Period, Is.EqualTo(3.0f).Within(Eps));
            int start = -1, recovery = -1, idle = -1;
            for (int i = 1; i <= 200; i++)
            {
                var before = c.Phase;
                if (c.Advance(Dt, true)) start = i;
                if (before == EnemyAttackPhase.Active && c.Phase == EnemyAttackPhase.Recovery) recovery = i;
                if (before == EnemyAttackPhase.Recovery && c.Phase == EnemyAttackPhase.Idle) { idle = i; break; }
            }
            Assert.That(start, Is.EqualTo(70));
            Assert.That(recovery, Is.EqualTo(100));
            Assert.That(idle, Is.EqualTo(150));
            Assert.That(new EnemyAttackRules(0.6f, 0.8f, 0.8f).Active, Is.EqualTo(0f), "2-5 three-value rules have no active time");
        }

        // ---- 플레이어 넘어짐 ----

        static PartyVitals OneMember() => new PartyVitals(new VitalsRules(0.3f, 0.5f, 1f, 0.6f, 0.8f, 0.5f, 0.5f), new[] { 1000 }, new[] { 100f }, null);

        [Test]
        public void Knockdown_Lasts12s_DodgeCannotEndIt()
        {
            var v = OneMember();
            PlayerHit h = v.TakeHit(100f, 2.0f, 400f, true, 1.2f);
            Assert.That(h.Requested, Is.EqualTo(160), "slam 200 x 0.8 on the hero");
            Assert.That(h.KnockedDown, Is.True);
            Assert.That(h.Stunned, Is.True);
            Assert.That(v.KnockedDown, Is.True);
            Assert.That(v.InputBlocked, Is.True);
            Assert.That(v.EndStun(), Is.False, "a dodge cannot end a knockdown");
            Assert.That(v.Stunned, Is.True);
            for (int i = 0; i < 59; i++) v.Advance(Dt);
            Assert.That(v.KnockedDown, Is.True, "59 steps");
            v.Advance(Dt);
            Assert.That(v.KnockedDown, Is.False, "60 steps = 1.2 s");
            Assert.That(v.Stunned, Is.False);
        }

        [Test]
        public void Knockdown_PushesOnlyForHitStun()
        {
            var v = OneMember();
            v.TakeHit(100f, 2.0f, 400f, true, 1.2f);
            for (int i = 0; i < 14; i++) v.Advance(Dt);
            Assert.That(v.Pushing, Is.True, "14 steps");
            v.Advance(Dt);
            Assert.That(v.Pushing, Is.False, "15 steps = 0.3 s - same 0.5 m as a normal hit");
            Assert.That(v.KnockedDown, Is.True, "then lies still");
        }

        [Test]
        public void Knockdown_NotInAir_NotWhenInvulnerable_NormalHitUnchanged()
        {
            var air = OneMember();
            PlayerHit a = air.TakeHit(100f, 2.0f, 400f, false, 1.2f);
            Assert.That(a.Applied, Is.EqualTo(160), "damage only in the air");
            Assert.That(a.KnockedDown, Is.False);
            Assert.That(air.Stunned, Is.False);

            var inv = OneMember();
            inv.Invulnerable = true;
            Assert.That(inv.TakeHit(100f, 2.0f, 400f, true, 1.2f).KnockedDown, Is.False);

            var n = OneMember();
            PlayerHit g = n.TakeHit(100f, 1.25f, 400f, true);
            Assert.That(g.Requested, Is.EqualTo(100), "3-1 Grunt hit unchanged");
            Assert.That(g.KnockedDown, Is.False);
            Assert.That(n.StunLeft, Is.EqualTo(0.3f).Within(Eps));
            Assert.That(n.EndStun(), Is.True, "a normal stun still ends with a dodge");
        }

        [Test]
        public void HitWhileKnockedDown_DoesNotShortenIt()
        {
            var v = OneMember();
            v.TakeHit(100f, 2.0f, 400f, true, 1.2f);
            for (int i = 0; i < 10; i++) v.Advance(Dt);
            v.TakeHit(100f, 1.25f, 400f, true); // Grunt 한 대 (경직 0.3 s)
            Assert.That(v.KnockedDown, Is.True);
            Assert.That(v.KnockdownLeft, Is.EqualTo(1.0f).Within(Eps), "1.2 - 0.2 s, not cut to 0.3");
            Assert.That(v.StunLeft, Is.EqualTo(1.0f).Within(Eps));
        }

        [Test]
        public void DebugKnockdown_NoDamage()
        {
            var v = OneMember();
            Assert.That(v.Knockdown(1.2f, true), Is.True);
            Assert.That(v.Active.Current, Is.EqualTo(1000));
            Assert.That(v.KnockedDown, Is.True);
            Assert.That(OneMember().Knockdown(1.2f, false), Is.False, "not in the air");
        }
    }
}
