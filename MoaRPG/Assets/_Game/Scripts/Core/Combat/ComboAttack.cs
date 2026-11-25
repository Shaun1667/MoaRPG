using System.Collections.Generic;
using Moa.Core.Ids;

namespace Moa.Core.Combat
{
    /// <summary>
    /// 일반 공격 콤보 한 단계의 수치 (WeaponTuning 에셋 → PlayerCombat이 넘긴다, BALANCE 6번).
    /// 한 단계 = 선딜(Windup) → 판정 구간(Active) → 후딜(Recovery). 시간은 초.
    /// </summary>
    public readonly struct ComboStepRules
    {
        /// <summary>피해 배율 (공격력 × 이 값).</summary>
        public readonly float Multiplier;
        public readonly float Windup;
        public readonly float Active;
        public readonly float Recovery;
        /// <summary>단계 시작부터 이 시간이 지나야 다음 단계 입력을 받는다 (그 전 입력은 버린다).</summary>
        public readonly float InputOpen;
        /// <summary>선딜 + 판정 구간 동안 앞으로 내딛는 거리 (m).</summary>
        public readonly float Lunge;

        public ComboStepRules(float multiplier, float windup, float active, float recovery, float inputOpen, float lunge)
        {
            Multiplier = multiplier;
            Windup = windup;
            Active = active;
            Recovery = recovery;
            InputOpen = inputOpen;
            Lunge = lunge;
        }

        public float Total => Windup + Active + Recovery;
        /// <summary>판정이 끝나는 시각 (단계 시작부터).</summary>
        public float ActiveEnd => Windup + Active;
    }

    /// <summary>무기 하나의 콤보 수치. 단계 수는 표의 줄 수 — 무기 종류별 분기 없음.</summary>
    public sealed class ComboRules
    {
        readonly ComboStepRules[] _steps;

        public ComboRules(ComboStepRules[] steps, float resetTime)
        {
            _steps = steps != null ? (ComboStepRules[])steps.Clone() : new ComboStepRules[0];
            ResetTime = resetTime;
        }

        public int StepCount => _steps.Length;
        public ComboStepRules Step(int index) => _steps[index];
        /// <summary>단계가 끝난 뒤(다음 입력 없이) 이 시간 안에 누르면 다음 단계, 지나면 1타로 돌아간다 (콤보 끊김 시간).</summary>
        public float ResetTime { get; }
    }

    /// <summary>공격을 시작할 때의 몸 상태 (PlayerMotor가 채운다).</summary>
    public readonly struct AttackContext
    {
        public readonly bool Grounded;
        public readonly bool Dodging;
        public readonly bool Climbing;
        public readonly bool Gliding;

        /// <summary>발 아래 땅까지 높이 (m, 낙하 공격 조건, 2-2).</summary>
        public readonly float HeightAboveGround;
        /// <summary>지금 발 높이 (월드 y, 낙하 높이 계산, 2-2).</summary>
        public readonly float FeetY;

        public AttackContext(bool grounded, bool dodging, bool climbing, bool gliding)
            : this(grounded, dodging, climbing, gliding, 0f, 0f) { }

        public AttackContext(bool grounded, bool dodging, bool climbing, bool gliding, float heightAboveGround, float feetY)
        {
            Grounded = grounded;
            Dodging = dodging;
            Climbing = climbing;
            Gliding = gliding;
            HeightAboveGround = heightAboveGround;
            FeetY = feetY;
        }
    }

    /// <summary>공격 입력이 받아지지 않은 까닭 (끝에만 추가).</summary>
    public enum AttackBlock
    {
        None,
        Climbing,
        Gliding,
        Dodging,
        Airborne,
        TooEarly,
        LastStep,
        AlreadyQueued,
        NoSteps,
        /// <summary>공중이지만 낙하 공격을 하기에는 낮다 (2-2).</summary>
        TooLow,
        /// <summary>강공격 · 낙하 공격 중 (2-2).</summary>
        Busy,
    }

    /// <summary>공격(한 단계)이 끝난 까닭 (끝에만 추가).</summary>
    public enum AttackEnd
    {
        None,
        Finished,
        Dodge,
        Jump,
        Airborne,
        Cancelled,
        /// <summary>길게 눌러 강공격으로 넘어갔다 (2-2).</summary>
        Charged,
    }

    public enum ComboPhase
    {
        Idle,
        Windup,
        Active,
        Recovery,
    }

    /// <summary>한 고정 스텝의 결과.</summary>
    public readonly struct ComboTick
    {
        /// <summary>이번 스텝에 새 단계가 시작됐는지 (몸 방향을 정할 때).</summary>
        public readonly bool StepStarted;
        /// <summary>이번 스텝에 앞으로 내딛을 거리 (m).</summary>
        public readonly float Lunge;
        /// <summary>이번 스텝이 판정 구간과 겹치는지 (겹침 검사는 이때만).</summary>
        public readonly bool HitOpen;

        public ComboTick(bool stepStarted, float lunge, bool hitOpen)
        {
            StepStarted = stepStarted;
            Lunge = lunge;
            HitOpen = hitOpen;
        }
    }

    /// <summary>
    /// 일반 공격 콤보 순수 규칙 (2-1). 고정 스텝마다 <see cref="Advance"/> (핵심 설계 규칙 2).
    /// - 대기 중 누르면 다음 단계(<see cref="NextStep"/>)를 바로 시작한다.
    /// - 단계 중에는 InputOpen 이후에 누르면 다음 단계를 예약하고, 후딜이 끝나면 이어진다. 마지막 단계에서는 예약하지 않는다.
    /// - 예약 없이 단계가 끝나면 ResetTime 동안 다음 단계를 기억하고, 지나면 1타로 돌아간다. 마지막 단계 뒤에는 바로 1타.
    /// - 등반 · 활공 · 회피 중 · 공중에서는 입력을 버린다. 회피 · 점프 · 공중으로 끊기면 콤보도 1타로 (원신 방식).
    /// - 휘두르기(단계 하나)마다 같은 대상은 한 번만 맞는다 — 엔티티 ID 기준 (<see cref="TryHit"/>).
    /// 몸 방향 · 실제 이동 · 겹침 검사는 Unity 쪽(PlayerMotor · PlayerCombat)이 한다.
    /// </summary>
    public sealed class ComboAttack
    {
        const float Epsilon = 1e-5f;
        readonly ComboRules _rules;
        readonly HashSet<long> _hitThisSwing = new HashSet<long>();
        bool _justStarted;

        public ComboAttack(ComboRules rules) { _rules = rules; }

        public ComboRules Rules => _rules;
        public ComboPhase Phase { get; private set; }
        public bool Attacking => Phase != ComboPhase.Idle;
        /// <summary>지금(또는 마지막) 단계 번호, 0부터. 시작 전이면 −1.</summary>
        public int StepIndex { get; private set; } = -1;
        /// <summary>대기 중에 누르면 시작할 단계 번호 (0 = 1타).</summary>
        public int NextStep { get; private set; }
        /// <summary>지금 단계가 시작된 뒤 지난 시간 (초).</summary>
        public float Elapsed { get; private set; }
        public bool Queued { get; private set; }
        /// <summary>콤보가 1타로 돌아가기까지 남은 시간 (대기 중, 초).</summary>
        public float ResetLeft { get; private set; }
        public bool IsLastStep => StepIndex >= 0 && StepIndex == _rules.StepCount - 1;
        public ComboStepRules CurrentStep => StepIndex >= 0 ? _rules.Step(StepIndex) : default;
        /// <summary>휘두른 수 — 단계가 하나 시작될 때마다 1 (디버그 · 확인용, 휘두르기 번호로도 쓴다).</summary>
        public int SwingCount { get; private set; }
        /// <summary>콤보가 끊겨(입력 창을 놓쳐) 1타로 돌아간 수.</summary>
        public int BreakCount { get; private set; }
        public AttackBlock LastBlock { get; private set; }
        public AttackEnd LastEnd { get; private set; }
        /// <summary>이번 휘두르기에 맞은 대상 수.</summary>
        public int HitsThisSwing => _hitThisSwing.Count;

        public AttackBlock Check(AttackContext ctx)
        {
            if (_rules.StepCount == 0) return AttackBlock.NoSteps;
            if (ctx.Climbing) return AttackBlock.Climbing;
            if (ctx.Gliding) return AttackBlock.Gliding;
            if (ctx.Dodging) return AttackBlock.Dodging;
            if (!ctx.Grounded) return AttackBlock.Airborne;
            if (Phase == ComboPhase.Idle) return AttackBlock.None;
            if (IsLastStep) return AttackBlock.LastStep;
            if (Queued) return AttackBlock.AlreadyQueued;
            if (Elapsed < CurrentStep.InputOpen - Epsilon) return AttackBlock.TooEarly;
            return AttackBlock.None;
        }

        /// <summary>공격 입력 한 번. 대기 중이면 바로 시작, 단계 중이면 다음 단계 예약. 받아졌으면 true (안 되면 까닭은 LastBlock).</summary>
        public bool Press(AttackContext ctx)
        {
            LastBlock = Check(ctx);
            if (LastBlock != AttackBlock.None) return false;
            if (Phase == ComboPhase.Idle) StartStep(NextStep);
            else Queued = true;
            return true;
        }

        /// <summary>한 고정 스텝 진행.</summary>
        public ComboTick Advance(float dt)
        {
            bool started = _justStarted;
            _justStarted = false;

            if (Phase == ComboPhase.Idle)
            {
                if (ResetLeft > 0f)
                {
                    ResetLeft -= dt;
                    if (ResetLeft <= Epsilon) { ResetLeft = 0f; NextStep = 0; BreakCount++; }
                }
                return new ComboTick(started, 0f, false);
            }

            ComboStepRules s = CurrentStep;
            float before = Elapsed;
            float after = before + dt;

            // 내딛기: [0, ActiveEnd] 구간과 겹치는 시간만큼 (합이 정확히 Lunge)
            float lunge = 0f;
            float moveEnd = s.ActiveEnd;
            if (moveEnd > 0f && before < moveEnd)
            {
                float overlap = (after < moveEnd ? after : moveEnd) - before;
                lunge = s.Lunge * (overlap / moveEnd);
            }
            bool hitOpen = s.Active > 0f && before < s.ActiveEnd - Epsilon && after > s.Windup + Epsilon;

            Elapsed = after;
            if (Elapsed >= s.Total - Epsilon)
            {
                bool chain = Queued && !IsLastStep;
                int next = StepIndex + 1;
                if (chain)
                {
                    StartStep(next);
                    _justStarted = true; // 다음 스텝에 StepStarted로 알린다 — 이번 스텝의 판정은 끝난 단계 몫
                }
                else
                {
                    LastEnd = AttackEnd.Finished;
                    Phase = ComboPhase.Idle;
                    Queued = false;
                    if (IsLastStep) { NextStep = 0; ResetLeft = 0f; }
                    else { NextStep = next; ResetLeft = _rules.ResetTime; }
                }
            }
            else
            {
                Phase = Elapsed < s.Windup - Epsilon ? ComboPhase.Windup
                    : Elapsed < s.ActiveEnd - Epsilon ? ComboPhase.Active
                    : ComboPhase.Recovery;
            }
            return new ComboTick(started, lunge, hitOpen);
        }

        /// <summary>
        /// 이번 휘두르기에서 이 대상을 맞힐 수 있으면 기록하고 true. 이미 맞았거나 공격 중이 아니면 false.
        /// 판정 구간인지는 호출하는 쪽이 <see cref="ComboTick.HitOpen"/>으로 본다.
        /// </summary>
        public bool TryHit(EntityId target)
        {
            if (Phase == ComboPhase.Idle || !target.IsValid) return false;
            return _hitThisSwing.Add(target.Value);
        }

        /// <summary>공격을 끊는다 (회피 · 점프 · 공중 · 순간이동). 콤보도 1타로 돌아간다.</summary>
        public void Interrupt(AttackEnd reason)
        {
            if (Phase == ComboPhase.Idle && ResetLeft <= 0f && NextStep == 0) return;
            if (Phase != ComboPhase.Idle) LastEnd = reason;
            Phase = ComboPhase.Idle;
            Queued = false;
            NextStep = 0;
            ResetLeft = 0f;
            _justStarted = false;
        }

        /// <summary>[상태 변경] 처음 상태로 (기록 수는 그대로).</summary>
        public void Cancel()
        {
            Interrupt(AttackEnd.Cancelled);
            StepIndex = -1;
            Elapsed = 0f;
            _hitThisSwing.Clear();
        }

        void StartStep(int index)
        {
            StepIndex = index;
            Elapsed = 0f;
            Queued = false;
            ResetLeft = 0f;
            Phase = _rules.Step(index).Windup > Epsilon ? ComboPhase.Windup : ComboPhase.Active;
            _hitThisSwing.Clear();
            SwingCount++;
            _justStarted = true;
        }
    }
}
