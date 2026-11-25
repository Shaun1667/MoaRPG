using System.Collections.Generic;
using Moa.Core.Ids;

namespace Moa.Core.Combat
{
    /// <summary>낙하 공격 수치 (배율 · 범위는 WeaponTuning, 높이 · 속도 · 경직은 CombatTuning → PlayerCombat이 넘긴다, BALANCE 6 · 7번).</summary>
    public readonly struct PlungeRules
    {
        /// <summary>발 아래 땅까지 이 높이(m) 이상인 공중에서만 시작한다.</summary>
        public readonly float MinHeight;
        /// <summary>떨어진 높이(시작 발 높이 − 착지 발 높이)가 이 값 이상이면 높은 낙하, 아니면 낮은 낙하 (m).</summary>
        public readonly float HighHeight;
        public readonly float LowMultiplier;
        public readonly float HighMultiplier;
        /// <summary>낙하 속력 (m/s, 아래로). 이미 더 빠르게 떨어지고 있으면 그 속력.</summary>
        public readonly float FallSpeed;
        /// <summary>착지 뒤 경직 (초).</summary>
        public readonly float LandRecovery;

        public PlungeRules(float minHeight, float highHeight, float lowMultiplier, float highMultiplier, float fallSpeed, float landRecovery)
        {
            MinHeight = minHeight;
            HighHeight = highHeight;
            LowMultiplier = lowMultiplier;
            HighMultiplier = highMultiplier;
            FallSpeed = fallSpeed;
            LandRecovery = landRecovery;
        }
    }

    public enum PlungePhase
    {
        None,
        Falling,
        Landing,
    }

    /// <summary>
    /// 낙하 공격 순수 규칙 (2-2, 원신 방식): 공중(높이 MinHeight 이상, 활공 중 포함)에서 공격을 누르면 빠르게 떨어지고,
    /// 착지할 때(<see cref="Land"/>) 떨어진 높이로 낮은/높은 낙하 배율을 정해 원형 범위 판정을 한 번 한다(대상마다 한 번, <see cref="TryHit"/>).
    /// 착지 뒤 LandRecovery 동안 경직. 실제 낙하 · 착지 검사 · 범위 겹침은 Unity 쪽(PlayerMotor · PlayerCombat).
    /// </summary>
    public sealed class PlungeAttack
    {
        const float Epsilon = 1e-5f;
        readonly PlungeRules _rules;
        readonly HashSet<long> _hit = new HashSet<long>();

        public PlungeAttack(PlungeRules rules) { _rules = rules; }

        public PlungeRules Rules => _rules;
        public PlungePhase Phase { get; private set; }
        public bool Active => Phase != PlungePhase.None;
        public bool Falling => Phase == PlungePhase.Falling;
        /// <summary>시작한 발 높이 (월드 y).</summary>
        public float StartY { get; private set; }
        /// <summary>마지막 착지에서 떨어진 높이 (m).</summary>
        public float FallHeight { get; private set; }
        public bool High { get; private set; }
        /// <summary>마지막 착지의 배율 (낮은/높은 낙하).</summary>
        public float Multiplier { get; private set; }
        /// <summary>경직이 끝나기까지 남은 초.</summary>
        public float RecoveryLeft { get; private set; }
        public int Count { get; private set; }
        public int LandCount { get; private set; }
        public AttackBlock LastBlock { get; private set; }
        public AttackEnd LastEnd { get; private set; }

        /// <summary>떨어진 높이 → 배율 (HighHeight 이상이면 높은 낙하).</summary>
        public static float MultiplierFor(PlungeRules rules, float fallHeight, out bool high)
        {
            high = fallHeight >= rules.HighHeight - Epsilon;
            return high ? rules.HighMultiplier : rules.LowMultiplier;
        }

        public AttackBlock Check(AttackContext ctx)
        {
            if (Active) return AttackBlock.Busy;
            if (ctx.Climbing) return AttackBlock.Climbing;
            if (ctx.Dodging) return AttackBlock.Dodging;
            if (ctx.Grounded) return AttackBlock.None; // 땅 위면 낙하가 아니다 — 부르는 쪽이 일반 공격으로
            if (ctx.HeightAboveGround < _rules.MinHeight - Epsilon) return AttackBlock.TooLow;
            return AttackBlock.None;
        }

        /// <summary>공중에서 시작 (땅 위 · 낮음 · 등반 · 회피 중이면 false, 까닭은 LastBlock). 활공 중이면 부르는 쪽이 활공을 접는다.</summary>
        public bool TryStart(AttackContext ctx)
        {
            LastBlock = ctx.Grounded ? AttackBlock.None : Check(ctx);
            if (ctx.Grounded || LastBlock != AttackBlock.None) return false;
            Phase = PlungePhase.Falling;
            StartY = ctx.FeetY;
            RecoveryLeft = 0f;
            _hit.Clear();
            Count++;
            return true;
        }

        /// <summary>떨어지는 중 땅에 닿았다: 떨어진 높이로 배율을 정하고 경직을 시작한다. 낙하 중이 아니면 false.</summary>
        public bool Land(float feetY)
        {
            if (Phase != PlungePhase.Falling) return false;
            FallHeight = StartY - feetY;
            if (FallHeight < 0f) FallHeight = 0f;
            Multiplier = MultiplierFor(_rules, FallHeight, out bool high);
            High = high;
            Phase = PlungePhase.Landing;
            RecoveryLeft = _rules.LandRecovery;
            LandCount++;
            return true;
        }

        /// <summary>한 고정 스텝 진행 (경직만 줄어든다).</summary>
        public void Advance(float dt)
        {
            if (Phase != PlungePhase.Landing) return;
            RecoveryLeft -= dt;
            if (RecoveryLeft <= Epsilon) Finish();
        }

        /// <summary>착지 판정에서 이 대상을 맞힐 수 있으면 기록하고 true (낙하 한 번에 대상마다 한 번).</summary>
        public bool TryHit(EntityId target)
        {
            if (Phase != PlungePhase.Landing || !target.IsValid) return false;
            return _hit.Add(target.Value);
        }

        public void Interrupt(AttackEnd reason)
        {
            if (Phase == PlungePhase.None) return;
            LastEnd = reason;
            Phase = PlungePhase.None;
            RecoveryLeft = 0f;
        }

        void Finish()
        {
            RecoveryLeft = 0f;
            Phase = PlungePhase.None;
            LastEnd = AttackEnd.Finished;
        }
    }
}
