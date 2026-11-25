using System.Collections.Generic;
using Moa.Core.Ids;
using Moa.Core.Motion;

namespace Moa.Core.Combat
{
    /// <summary>강공격 수치 (WeaponTuning 에셋 → PlayerCombat이 넘긴다, BALANCE 6번). 베기 수 = 표의 줄 수.</summary>
    public sealed class ChargedRules
    {
        readonly ComboStepRules[] _slashes;

        public ChargedRules(ComboStepRules[] slashes, float staminaCost)
        {
            _slashes = slashes != null ? (ComboStepRules[])slashes.Clone() : new ComboStepRules[0];
            StaminaCost = staminaCost;
        }

        public int SlashCount => _slashes.Length;
        public ComboStepRules Slash(int index) => _slashes[index];
        /// <summary>강공격 한 번에 쓰는 스태미나 (모자라면 나가지 않는다).</summary>
        public float StaminaCost { get; }

        public float Total
        {
            get { float t = 0f; foreach (var s in _slashes) t += s.Total; return t; }
        }
    }

    /// <summary>강공격이 나가지 않은 까닭 (끝에만 추가).</summary>
    public enum ChargeBlock
    {
        None,
        NoSteps,
        Climbing,
        Gliding,
        Dodging,
        Airborne,
        Busy,
        NoStamina,
    }

    /// <summary>
    /// 강공격 순수 규칙 (2-2): 스태미나를 한 번에 쓰고, 베기 표를 차례로 (각 베기 = 선딜 → 판정 → 후딜, 내딛기는 콤보와 같은 방식).
    /// 베기마다 같은 대상은 한 번만 맞는다 (<see cref="TryHit"/>). 언제 나가는지(길게 누르기)는 <see cref="PlayerAttack"/>가 정한다.
    /// </summary>
    public sealed class ChargedAttack
    {
        const float Epsilon = 1e-5f;
        readonly ChargedRules _rules;
        readonly HashSet<long> _hitThisSlash = new HashSet<long>();
        bool _justStarted;

        public ChargedAttack(ChargedRules rules) { _rules = rules; }

        public ChargedRules Rules => _rules;
        public ComboPhase Phase { get; private set; }
        public bool Active => Phase != ComboPhase.Idle;
        /// <summary>지금(또는 마지막) 베기 번호, 0부터. 시작 전이면 −1.</summary>
        public int SlashIndex { get; private set; } = -1;
        public float Elapsed { get; private set; }
        public ComboStepRules CurrentSlash => SlashIndex >= 0 && SlashIndex < _rules.SlashCount ? _rules.Slash(SlashIndex) : default;
        /// <summary>강공격이 나간 수.</summary>
        public int Count { get; private set; }
        public ChargeBlock LastBlock { get; private set; }
        public AttackEnd LastEnd { get; private set; }
        public int HitsThisSlash => _hitThisSlash.Count;

        public ChargeBlock Check(AttackContext ctx, Stamina stamina)
        {
            if (_rules.SlashCount == 0) return ChargeBlock.NoSteps;
            if (ctx.Climbing) return ChargeBlock.Climbing;
            if (ctx.Gliding) return ChargeBlock.Gliding;
            if (ctx.Dodging) return ChargeBlock.Dodging;
            if (!ctx.Grounded) return ChargeBlock.Airborne;
            if (Active) return ChargeBlock.Busy;
            if (stamina != null && !stamina.CanAfford(_rules.StaminaCost)) return ChargeBlock.NoStamina;
            return ChargeBlock.None;
        }

        /// <summary>조건이 되면 스태미나를 쓰고 첫 베기를 시작한다. 안 되면 false (스태미나 그대로, 까닭은 LastBlock).</summary>
        public bool TryStart(AttackContext ctx, Stamina stamina)
        {
            LastBlock = Check(ctx, stamina);
            if (LastBlock != ChargeBlock.None) return false;
            if (stamina != null && !stamina.TrySpend(_rules.StaminaCost)) { LastBlock = ChargeBlock.NoStamina; return false; }
            Count++;
            StartSlash(0);
            return true;
        }

        /// <summary>한 고정 스텝 진행 (콤보와 같은 ComboTick).</summary>
        public ComboTick Advance(float dt)
        {
            bool started = _justStarted;
            _justStarted = false;
            if (Phase == ComboPhase.Idle) return new ComboTick(started, 0f, false);

            ComboStepRules s = CurrentSlash;
            float before = Elapsed;
            float after = before + dt;
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
                if (SlashIndex + 1 < _rules.SlashCount)
                {
                    StartSlash(SlashIndex + 1); // 다음 베기는 다음 스텝에 StepStarted로 알린다
                }
                else
                {
                    LastEnd = AttackEnd.Finished;
                    Phase = ComboPhase.Idle;
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

        /// <summary>이번 베기에서 이 대상을 맞힐 수 있으면 기록하고 true.</summary>
        public bool TryHit(EntityId target)
        {
            if (Phase == ComboPhase.Idle || !target.IsValid) return false;
            return _hitThisSlash.Add(target.Value);
        }

        /// <summary>끊는다 (회피 · 점프 · 공중 · 순간이동).</summary>
        public void Interrupt(AttackEnd reason)
        {
            if (Phase == ComboPhase.Idle) return;
            LastEnd = reason;
            Phase = ComboPhase.Idle;
            _justStarted = false;
        }

        void StartSlash(int index)
        {
            SlashIndex = index;
            Elapsed = 0f;
            Phase = _rules.Slash(index).Windup > Epsilon ? ComboPhase.Windup : ComboPhase.Active;
            _hitThisSlash.Clear();
            _justStarted = true;
        }
    }
}
