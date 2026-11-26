using System;
using Moa.Core.Combat;
using Moa.Core.Motion;
using NUnit.Framework;

namespace Moa.Tests
{
    /// <summary>
    /// 2-5 근접 적 AI 상태 기계 (Core EnemyBrain): 대기 · 순찰 → 감지 → 추적 → 예고 공격 → 복귀 · 경직 · 그로기 · 쓰러짐 · 다시 남.
    /// 수치는 BALANCE 11번(Grunt.asset)과 같은 값을 직접 넣는다 — 에셋 값 검산은 EnemyTuningAssetTests.
    /// </summary>
    public class EnemyBrainTests
    {
        const float Dt = 0.02f;
        const float Eps = 1e-3f;

        static EnemyAiRules Rules() => new EnemyAiRules(
            detectRange: 10f, viewAngle: 120f, noticeRange: 3f, loseRange: 15f, loseSightTime: 2f, leashRange: 20f,
            attackRange: 2f, hitRange: 2.6f, hitAngle: 120f, patrolSpeed: 1.5f, chaseSpeed: 3.5f, returnSpeed: 4f, turnSpeed: 360f,
            patrolWait: 2f, arriveDistance: 0.3f, hitStunTime: 0.3f, respawnDelay: 5f,
            attack: new EnemyAttackRules(0.6f, 0.8f, 0.8f));

        /// <summary>적 하나를 고정 스텝으로 움직여 보는 시험대 (Unity 쪽 이동 대신 속도 × dt).</summary>
        sealed class Sim
        {
            public EnemyBrain B;
            public Planar Self;
            public float Yaw;
            public Planar Target;
            public bool HasTarget = true;
            public bool Los = true;
            public bool Groggy;
            public int Landed, Hits, Grazed;

            public Sim(Planar home, Planar patrol, float yaw = 0f)
            {
                B = new EnemyBrain(Rules(), home, patrol);
                Self = home;
                Yaw = yaw;
                Target = new Planar(0f, 100f); // 멀리
            }

            public EnemyTick Step()
            {
                var s = HasTarget ? new EnemySense(Self, Yaw, Target, 0f, Los) : EnemySense.NoTarget(Self, Yaw);
                var t = B.Step(Dt, s, Groggy);
                Self = Self + t.Velocity * Dt;
                Yaw = t.Yaw;
                if (t.Respawned) Self = B.Home;
                if (t.AttackLanded) Landed++;
                if (t.AttackHits) Hits++;
                if (t.AttackGrazed) Grazed++;
                return t;
            }

            /// <summary>조건이 될 때까지 (최대 max 스텝). 조건이 된 스텝 번호(1부터), 안 되면 −1.</summary>
            public int Until(Func<EnemyTick, bool> cond, int max)
            {
                for (int i = 1; i <= max; i++) if (cond(Step())) return i;
                return -1;
            }

            public void Steps(int n) { for (int i = 0; i < n; i++) Step(); }
        }

        static float Dist(Planar a, Planar b) => (a - b).Length;

        [Test]
        public void Idle_NoPatrol_StaysIdle_WhenNothingSeen()
        {
            var sim = new Sim(Planar.Zero, Planar.Zero);
            sim.Steps(500);
            Assert.That(sim.B.State, Is.EqualTo(EnemyState.Idle));
            Assert.That(Dist(sim.Self, Planar.Zero), Is.LessThan(Eps));
        }

        [Test]
        public void Patrol_AfterWait_WalksToPointAtPatrolSpeed_ThenBackHome()
        {
            var sim = new Sim(Planar.Zero, new Planar(4f, 0f));
            int n = sim.Until(t => t.State == EnemyState.Patrol, 200);
            Assert.That(n, Is.EqualTo(100), "patrolWait 2 s = 100 steps");
            Assert.That(sim.B.PatrolTarget.X, Is.EqualTo(4f).Within(Eps));
            sim.Step();
            Assert.That(sim.B.State, Is.EqualTo(EnemyState.Patrol));
            // 몸을 돌리면서도 1.5 m/s
            Assert.That((sim.Self - Planar.Zero).Length / (2 * Dt), Is.EqualTo(1.5f).Within(Eps));
            int arrive = sim.Until(t => t.State == EnemyState.Idle, 500);
            Assert.That(arrive, Is.GreaterThan(0));
            Assert.That(sim.Self.X, Is.EqualTo(4f).Within(0.31f));
            sim.Until(t => t.State == EnemyState.Patrol, 200);
            Assert.That(sim.B.PatrolTarget.X, Is.EqualTo(0f).Within(Eps), "then back home");
            sim.Until(t => t.State == EnemyState.Idle, 500);
            Assert.That(Dist(sim.Self, Planar.Zero), Is.LessThan(0.31f));
        }

        [Test]
        public void Sees_FrontConeWithinDetectRange_OrCloseFromBehind_OnlyWithLineOfSight()
        {
            var r = Rules();
            var self = Planar.Zero;
            // 앞(+Z)으로 9.9m · 옆 59° 안 → 봄, 10.1m → 못 봄
            Assert.That(EnemyBrain.CanSee(r, new EnemySense(self, 0f, new Planar(0f, 9.9f), 0f, true)), Is.True);
            Assert.That(EnemyBrain.CanSee(r, new EnemySense(self, 0f, new Planar(0f, 10.1f), 0f, true)), Is.False);
            float a59 = 59f * MathF.PI / 180f, a61 = 61f * MathF.PI / 180f;
            Assert.That(EnemyBrain.CanSee(r, new EnemySense(self, 0f, new Planar(MathF.Sin(a59) * 8f, MathF.Cos(a59) * 8f), 0f, true)), Is.True);
            Assert.That(EnemyBrain.CanSee(r, new EnemySense(self, 0f, new Planar(MathF.Sin(a61) * 8f, MathF.Cos(a61) * 8f), 0f, true)), Is.False);
            // 뒤 2.9m → 봄(가까이), 뒤 3.1m → 못 봄
            Assert.That(EnemyBrain.CanSee(r, new EnemySense(self, 0f, new Planar(0f, -2.9f), 0f, true)), Is.True);
            Assert.That(EnemyBrain.CanSee(r, new EnemySense(self, 0f, new Planar(0f, -3.1f), 0f, true)), Is.False);
            // 시야선이 막히면 앞 · 가까이 모두 못 봄
            Assert.That(EnemyBrain.CanSee(r, new EnemySense(self, 0f, new Planar(0f, 5f), 0f, false)), Is.False);
            Assert.That(EnemyBrain.CanSee(r, new EnemySense(self, 0f, new Planar(0f, 1f), 0f, false)), Is.False);
            Assert.That(EnemyBrain.CanSee(r, EnemySense.NoTarget(self, 0f)), Is.False);
        }

        [Test]
        public void Detect_FromIdleAndPatrol_ChasesAtChaseSpeed()
        {
            var sim = new Sim(Planar.Zero, Planar.Zero) { Target = new Planar(0f, 8f) };
            var t = sim.Step();
            Assert.That(t.State, Is.EqualTo(EnemyState.Chase));
            Assert.That(t.Velocity.Z, Is.EqualTo(3.5f).Within(Eps));
            Assert.That(sim.B.DetectCount, Is.EqualTo(1));

            var p = new Sim(Planar.Zero, new Planar(4f, 0f));
            p.Until(x => x.State == EnemyState.Patrol, 200);
            p.Target = p.Self + new Planar(0f, 2.5f); // 가까이 (뒤여도 보임, 사거리 2m 밖)
            Assert.That(p.Step().State, Is.EqualTo(EnemyState.Chase));
        }

        [Test]
        public void Attack_InRange_WarnsAfterInterval_HitLandsOn70thStep_AndHitsInFront()
        {
            var sim = new Sim(Planar.Zero, Planar.Zero) { Target = new Planar(0f, 1.5f) };
            int n = sim.Until(t => t.AttackLanded, 200);
            Assert.That(n, Is.EqualTo(70), "interval 0.6 s (30) + telegraph 0.8 s (40)");
            Assert.That(sim.B.State, Is.EqualTo(EnemyState.Attack));
            Assert.That(sim.Hits, Is.EqualTo(1));
            Assert.That(sim.Grazed, Is.EqualTo(0), "a hit is not a graze");
            Assert.That(sim.B.Cycle.Phase, Is.EqualTo(EnemyAttackPhase.Recovery));
            // 다음 공격: 자세 0.8 + 쉬기 0.6 + 예고 0.8 = 2.2 s (110스텝)
            Assert.That(sim.Until(t => t.AttackLanded, 300), Is.EqualTo(110));
            Assert.That(sim.B.AttackCount, Is.EqualTo(2));
        }

        [Test]
        public void Attack_DoesNotTurnDuringWarning_SidestepMisses()
        {
            var sim = new Sim(Planar.Zero, Planar.Zero) { Target = new Planar(0f, 1.5f) };
            sim.Until(t => sim.B.Cycle.Phase == EnemyAttackPhase.Telegraph, 100);
            float yaw = sim.Yaw;
            sim.Target = new Planar(1.5f, -0.5f); // 옆 · 조금 뒤 (부채꼴 밖, 거리 1.58)
            int n = sim.Until(t => t.AttackLanded, 100);
            Assert.That(n, Is.GreaterThan(0), "a started warning finishes even when the target moves");
            Assert.That(sim.Yaw, Is.EqualTo(yaw).Within(Eps), "no turning while warning");
            Assert.That(sim.Hits, Is.EqualTo(0));
            Assert.That(sim.B.AttackMissCount, Is.EqualTo(1));
            Assert.That(sim.Grazed, Is.EqualTo(1), "was in the area during the warning (a dodge out still gets judged)");
        }

        [Test]
        public void Attack_TargetLeavesRangeWhileResting_ChasesAgain_ButWarningIsCommitted()
        {
            var sim = new Sim(Planar.Zero, Planar.Zero) { Target = new Planar(0f, 1.5f) };
            sim.Steps(10); // 쉬는 중
            sim.Target = new Planar(0f, 5f);
            Assert.That(sim.Step().State, Is.EqualTo(EnemyState.Chase));

            var c = new Sim(Planar.Zero, Planar.Zero) { Target = new Planar(0f, 1.5f) };
            c.Until(t => c.B.Cycle.Phase == EnemyAttackPhase.Telegraph, 100);
            c.Target = new Planar(0f, 5f);
            c.Until(t => t.AttackLanded, 100);
            Assert.That(c.B.State, Is.EqualTo(EnemyState.Attack), "telegraph + recovery are not cut");
            Assert.That(c.Hits, Is.EqualTo(0), "5 m is out of hitRange 2.6");
            Assert.That(c.Grazed, Is.EqualTo(1));
            int back = c.Until(t => t.State == EnemyState.Chase, 100);
            Assert.That(back, Is.EqualTo(41), "after recovery 0.8 s (40) the next resting step chases");
        }

        [Test]
        public void Return_AfterLineOfSightBlocked2Seconds()
        {
            var sim = new Sim(Planar.Zero, Planar.Zero) { Target = new Planar(0f, 9f) };
            sim.Step();
            sim.Los = false;
            // 플레이어는 9m 앞에서 같이 물러난다 (사거리 · 너무 멀어짐에 안 걸리게)
            int n = sim.Until(t => { sim.Target = sim.Self + new Planar(0f, 9f); return t.State == EnemyState.Return; }, 300);
            Assert.That(n, Is.EqualTo(100), "loseSightTime 2 s");
            Assert.That(sim.B.LastReturnReason, Is.EqualTo(EnemyReturnReason.LostSight));
        }

        [Test]
        public void Return_WhenTargetTooFar_OrLeashFromHome()
        {
            var far = new Sim(Planar.Zero, Planar.Zero) { Target = new Planar(0f, 8f) };
            far.Step();
            far.Target = new Planar(0f, 15.5f) + far.Self;
            Assert.That(far.Step().State, Is.EqualTo(EnemyState.Return));
            Assert.That(far.B.LastReturnReason, Is.EqualTo(EnemyReturnReason.TooFar));

            // 플레이어가 3.5 m/s로 계속 도망 — 거리는 그대로, 집에서 20m를 넘으면 복귀
            var leash = new Sim(Planar.Zero, Planar.Zero) { Target = new Planar(0f, 8f) };
            int n = leash.Until(t => { leash.Target = leash.Target + new Planar(0f, 3.5f * Dt); return t.State == EnemyState.Return; }, 1000);
            Assert.That(n, Is.GreaterThan(0));
            Assert.That(leash.B.LastReturnReason, Is.EqualTo(EnemyReturnReason.Leash));
            Assert.That(leash.B.LastHomeDistance, Is.GreaterThan(20f).And.LessThan(20.1f));
        }

        [Test]
        public void Return_WalksHomeAt4_IgnoresTarget_HealsOnArrival()
        {
            var sim = new Sim(Planar.Zero, Planar.Zero) { Target = new Planar(0f, 8f) };
            sim.Steps(100);
            sim.B.ForceState(EnemyState.Return);
            sim.Target = sim.Self + new Planar(0f, 1f); // 바로 앞이어도
            var t = sim.Step();
            Assert.That(t.State, Is.EqualTo(EnemyState.Return));
            Assert.That(t.Velocity.Length, Is.EqualTo(4f).Within(Eps));
            sim.Target = new Planar(0f, 100f);
            bool healed = false;
            int n = sim.Until(x => { healed |= x.Healed; return x.State == EnemyState.Idle; }, 1000);
            Assert.That(n, Is.GreaterThan(0));
            Assert.That(healed, Is.True);
            Assert.That(Dist(sim.Self, Planar.Zero), Is.LessThanOrEqualTo(0.3f + Eps));
            Assert.That(sim.B.ReturnCount, Is.EqualTo(1));
        }

        [Test]
        public void Hit_Staggers03Seconds_ThenChases_EvenFromIdleUnseen()
        {
            var sim = new Sim(Planar.Zero, Planar.Zero) { Target = new Planar(0f, -6f) }; // 등 뒤 6m — 못 봄
            sim.Steps(5);
            Assert.That(sim.B.State, Is.EqualTo(EnemyState.Idle));
            Assert.That(sim.B.NotifyHit(), Is.True);
            Assert.That(sim.B.State, Is.EqualTo(EnemyState.Stagger));
            int n = sim.Until(t => t.State != EnemyState.Stagger, 100);
            Assert.That(n, Is.EqualTo(15), "hitStunTime 0.3 s");
            Assert.That(sim.B.State, Is.EqualTo(EnemyState.Chase));
            Assert.That(sim.B.StaggerCount, Is.EqualTo(1));
        }

        [Test]
        public void Hit_DuringWarning_DoesNotStagger_AttackStillLands()
        {
            var sim = new Sim(Planar.Zero, Planar.Zero) { Target = new Planar(0f, 1.5f) };
            sim.Until(t => sim.B.Cycle.Phase == EnemyAttackPhase.Telegraph, 100);
            Assert.That(sim.B.NotifyHit(), Is.False);
            Assert.That(sim.B.ArmorCount, Is.EqualTo(1));
            Assert.That(sim.Until(t => t.AttackLanded, 100), Is.EqualTo(40));
            Assert.That(sim.Hits, Is.EqualTo(1));
            // 자세 중에 맞으면 경직
            Assert.That(sim.B.NotifyHit(), Is.True);
            Assert.That(sim.B.State, Is.EqualTo(EnemyState.Stagger));
        }

        [Test]
        public void Hit_WhileResting_ResetsTheAttackCycle()
        {
            var sim = new Sim(Planar.Zero, Planar.Zero) { Target = new Planar(0f, 1.5f) };
            sim.Steps(20);
            sim.B.NotifyHit();
            // 경직 15스텝(끝 스텝에 추적 → 사거리 안이라 바로 공격 주기 1스텝) → 다시 쉬기 30 + 예고 40
            Assert.That(sim.Until(t => t.AttackLanded, 300), Is.EqualTo(14 + 70));
        }

        [Test]
        public void Groggy_StandsAndCancelsWarning_ThenChases()
        {
            var sim = new Sim(Planar.Zero, Planar.Zero) { Target = new Planar(0f, 1.5f) };
            sim.Until(t => sim.B.Cycle.Phase == EnemyAttackPhase.Telegraph, 100);
            sim.Groggy = true;
            var t0 = sim.Step();
            Assert.That(t0.State, Is.EqualTo(EnemyState.Groggy));
            Assert.That(sim.B.Cycle.Phase, Is.EqualTo(EnemyAttackPhase.Idle));
            Assert.That(sim.B.Cycle.CancelCount, Is.EqualTo(1));
            Assert.That(sim.Until(t => t.AttackLanded || t.Velocity.Length > 0f, 250), Is.EqualTo(-1), "no attack, no movement while groggy");
            Assert.That(sim.B.NotifyHit(), Is.False, "no stagger while groggy");
            sim.Groggy = false;
            var t1 = sim.Step();
            Assert.That(t1.State, Is.EqualTo(EnemyState.Attack), "recovers into chase, already in range");
        }

        [Test]
        public void Die_Respawns5SecondsLater_AtHome_Idle()
        {
            var sim = new Sim(new Planar(2f, 3f), Planar.Zero) { Target = new Planar(2f, 9f) };
            sim.Steps(50);
            Assert.That(sim.B.State, Is.EqualTo(EnemyState.Chase).Or.EqualTo(EnemyState.Attack));
            sim.B.Die();
            Assert.That(sim.B.State, Is.EqualTo(EnemyState.Dead));
            Assert.That(sim.B.NotifyHit(), Is.False);
            Assert.That(sim.B.ForceState(EnemyState.Chase), Is.False);
            int n = sim.Until(t => t.Respawned, 400);
            Assert.That(n, Is.EqualTo(250), "respawnDelay 5 s");
            Assert.That(sim.B.State, Is.EqualTo(EnemyState.Idle));
            Assert.That(Dist(sim.Self, new Planar(2f, 3f)), Is.LessThan(Eps));
            Assert.That(sim.B.DeathCount, Is.EqualTo(1));
            Assert.That(sim.B.RespawnCount, Is.EqualTo(1));
        }

        [Test]
        public void Paused_StandsStill_NoAttack()
        {
            var sim = new Sim(Planar.Zero, Planar.Zero) { Target = new Planar(0f, 1.5f) };
            sim.B.Paused = true;
            Assert.That(sim.Until(t => t.AttackLanded || t.Velocity.Length > 0f, 300), Is.EqualTo(-1));
            sim.B.Paused = false;
            Assert.That(sim.Until(t => t.AttackLanded, 200), Is.EqualTo(70));
        }

        [Test]
        public void ForceState_OnlyDebugStates()
        {
            var sim = new Sim(Planar.Zero, Planar.Zero);
            Assert.That(sim.B.ForceState(EnemyState.Patrol), Is.False, "no patrol point");
            Assert.That(sim.B.ForceState(EnemyState.Attack), Is.False);
            Assert.That(sim.B.ForceState(EnemyState.Groggy), Is.False);
            Assert.That(sim.B.ForceState(EnemyState.Dead), Is.False);
            Assert.That(sim.B.ForceState(EnemyState.Chase), Is.True);
            sim.HasTarget = false;
            Assert.That(sim.Step().State, Is.EqualTo(EnemyState.Return), "nothing to chase");
            Assert.That(sim.B.LastReturnReason, Is.EqualTo(EnemyReturnReason.NoTarget));
            Assert.That(sim.B.ForceState(EnemyState.Idle), Is.True);
            Assert.That(sim.B.State, Is.EqualTo(EnemyState.Idle));
        }

        [Test]
        public void HitArea_RangeConeAndHeight()
        {
            var r = Rules();
            Assert.That(EnemyBrain.InHitArea(r, Planar.Zero, 0f, new Planar(0f, 2.6f), 0f), Is.True);
            Assert.That(EnemyBrain.InHitArea(r, Planar.Zero, 0f, new Planar(0f, 2.7f), 0f), Is.False);
            Assert.That(EnemyBrain.InHitArea(r, Planar.Zero, 0f, new Planar(2f, 0.1f), 0f), Is.False, "beside = outside the 120 degree cone");
            Assert.That(EnemyBrain.InHitArea(r, Planar.Zero, 90f, new Planar(2f, 0.1f), 0f), Is.True);
            Assert.That(EnemyBrain.InHitArea(r, Planar.Zero, 0f, new Planar(0f, 1f), 2.7f), Is.False, "too high (gliding over)");
            Assert.That(EnemyBrain.InHitArea(r, Planar.Zero, 0f, Planar.Zero, 0f), Is.True);
        }
    }
}
