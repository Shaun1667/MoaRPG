using System.Collections.Generic;
using Moa.Core.Ids;
using Moa.Core.Motion;

namespace Moa.Core.Combat
{
    /// <summary>
    /// 강공격 수치 (WeaponTuning 에셋 → PlayerCombat이 넘긴다, BALANCE 6 · 15번). 베기 수 = 표의 줄 수.
    /// 3-3: 같은 표로 세 가지를 쓴다 — 무기 종류별 분기 없이 값으로 고른다.
    /// - 기본(한손검): 길게 누르기가 되면 표를 차례로 (AimTime · SpinDrainPerSecond 0).
    /// - 조준(활, AimTime &gt; 0): 길게 누르기가 되면 조준 — 누르고 있는 동안 조준, 떼면 AimTime 이상 조준했을 때만 표를 쏜다(아니면 아무것도 안 나감).
    /// - 회전(대검, SpinDrainPerSecond &gt; 0, 줄 2개 이상): 마지막 줄 = 마무리, 그 앞 줄들 = 회전. 회전하는 동안 초당 스태미나를 쓰고,
    ///   한 줄이 끝날 때 누르고 있고 스태미나가 있으면 회전 줄을 되풀이, 아니면 마무리.
    /// </summary>
    public sealed class ChargedRules
    {
        readonly ComboStepRules[] _slashes;

        public ChargedRules(ComboStepRules[] slashes, float staminaCost) : this(slashes, staminaCost, 0f, 0f) { }

        /// <summary>3-3: aimTime = 조준을 다 하는 시간(0 = 조준 없음), spinDrainPerSecond = 회전 중 초당 스태미나(0 = 회전 없음).</summary>
        public ChargedRules(ComboStepRules[] slashes, float staminaCost, float aimTime, float spinDrainPerSecond)
        {
            _slashes = slashes != null ? (ComboStepRules[])slashes.Clone() : new ComboStepRules[0];
            StaminaCost = staminaCost;
            AimTime = aimTime > 0f ? aimTime : 0f;
            SpinDrainPerSecond = spinDrainPerSecond > 0f && _slashes.Length >= 2 ? spinDrainPerSecond : 0f;
        }

        public int SlashCount => _slashes.Length;
        public ComboStepRules Slash(int index) => _slashes[index];
        /// <summary>강공격 한 번에 쓰는 스태미나 (모자라면 나가지 않는다, 회전형은 시작할 때).</summary>
        public float StaminaCost { get; }
        /// <summary>조준을 다 하는 시간 (초, 0 = 조준 없음, 3-3).</summary>
        public float AimTime { get; }
        /// <summary>회전 중 초당 스태미나 (0 = 회전 없음, 3-3).</summary>
        public float SpinDrainPerSecond { get; }
        public bool Aimed => AimTime > 0f;
        public bool Spins => SpinDrainPerSecond > 0f;
        /// <summary>마무리 줄 번호 (회전형만, 아니면 −1).</summary>
        public int FinisherIndex => Spins ? _slashes.Length - 1 : -1;

        /// <summary>표를 한 번씩 (회전형은 회전 줄 한 바퀴 + 마무리, 조준 시간은 빼고).</summary>
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
    /// 강공격 순수 규칙 (2-2 · 3-3): 스태미나를 한 번에 쓰고, 베기 표를 차례로 (각 베기 = 선딜 → 판정 → 후딜, 내딛기는 콤보와 같은 방식).
    /// 베기마다 같은 대상은 한 번만 맞는다 (<see cref="TryHit"/>). 언제 나가는지(길게 누르기)는 <see cref="PlayerAttack"/>가 정한다.
    /// 3-3: 조준형 · 회전형은 <see cref="ChargedRules"/> 설명대로 — 누르고 있는지 · 스태미나를 <see cref="Advance(float, bool, Stamina)"/>로 받는다.
    /// </summary>
    public sealed class ChargedAttack
    {
        const float Epsilon = 1e-5f;
        readonly ChargedRules _rules;
        readonly HashSet<long> _hitThisSlash = new HashSet<long>();
        bool _justStarted;
        bool _spinOut; // 회전 중 스태미나가 바닥남 — 이번 줄이 끝나면 마무리

        public ChargedAttack(ChargedRules rules) { _rules = rules; }

        public ChargedRules Rules => _rules;
        public ComboPhase Phase { get; private set; }
        public bool Active => Phase != ComboPhase.Idle;
        /// <summary>지금(또는 마지막) 베기 번호, 0부터. 시작 전 · 조준 중이면 −1.</summary>
        public int SlashIndex { get; private set; } = -1;
        public float Elapsed { get; private set; }
        public ComboStepRules CurrentSlash => SlashIndex >= 0 && SlashIndex < _rules.SlashCount ? _rules.Slash(SlashIndex) : default;
        /// <summary>강공격이 나간 수 (조준형은 조준을 시작한 수).</summary>
        public int Count { get; private set; }
        public ChargeBlock LastBlock { get; private set; }
        public AttackEnd LastEnd { get; private set; }
        public int HitsThisSlash => _hitThisSlash.Count;

        /// <summary>조준 중 (3-3, 조준형만) · 조준한 시간 · 다 조준했는지.</summary>
        public bool Aiming { get; private set; }
        public float AimElapsed { get; private set; }
        public bool AimReady => _rules.Aimed && AimElapsed >= _rules.AimTime - Epsilon;
        /// <summary>조준 뒤 쏜 수 · 다 조준하기 전에 떼서 안 나간 수 (3-3).</summary>
        public int ShotCount { get; private set; }
        public int AimCancelCount { get; private set; }
        /// <summary>회전 줄 중 (3-3, 회전형만) · 지금까지 회전 줄을 시작한 수 · 회전 중 쓴 스태미나 합.</summary>
        public bool Spinning => _rules.Spins && Active && SlashIndex >= 0 && SlashIndex < _rules.FinisherIndex;
        public int SpinCount { get; private set; }
        public float SpinStaminaSpent { get; private set; }
        /// <summary>이번 강공격에서 회전 줄을 시작한 수.</summary>
        public int SpinsThisCharge { get; private set; }

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

        /// <summary>조건이 되면 스태미나를 쓰고 첫 베기(조준형은 조준)를 시작한다. 안 되면 false (스태미나 그대로, 까닭은 LastBlock).</summary>
        public bool TryStart(AttackContext ctx, Stamina stamina)
        {
            LastBlock = Check(ctx, stamina);
            if (LastBlock != ChargeBlock.None) return false;
            if (stamina != null && !stamina.TrySpend(_rules.StaminaCost)) { LastBlock = ChargeBlock.NoStamina; return false; }
            Count++;
            _spinOut = false;
            SpinsThisCharge = 0;
            if (_rules.Aimed)
            {
                // 조준 시작: 베기는 아직 (떼면 쏜다). 몸 방향은 부르는 쪽이 조준 방향으로
                Aiming = true;
                AimElapsed = 0f;
                SlashIndex = -1;
                Elapsed = 0f;
                Phase = ComboPhase.Windup;
                _hitThisSlash.Clear();
                _justStarted = false;
                return true;
            }
            StartSlash(0);
            return true;
        }

        /// <summary>한 고정 스텝 진행 (누르고 있지 않음 · 스태미나 없음으로).</summary>
        public ComboTick Advance(float dt) => Advance(dt, false, null);

        /// <summary>한 고정 스텝 진행 (콤보와 같은 ComboTick). held = 공격을 누르고 있는지, stamina = 회전 소모 (null이면 소모 없음).</summary>
        public ComboTick Advance(float dt, bool held, Stamina stamina)
        {
            bool started = _justStarted;
            _justStarted = false;
            if (Phase == ComboPhase.Idle) return new ComboTick(started, 0f, false);

            if (Aiming)
            {
                if (held) { AimElapsed += dt; return new ComboTick(false, 0f, false); }
                Aiming = false;
                if (AimReady)
                {
                    ShotCount++;
                    StartSlash(0); // 다음 스텝에 StepStarted · 판정
                }
                else
                {
                    AimCancelCount++;
                    LastEnd = AttackEnd.AimCancelled;
                    Phase = ComboPhase.Idle;
                }
                return new ComboTick(false, 0f, false);
            }

            // 회전 (3-3): 회전 줄 동안 초당 소모 — 바닥나면 이번 줄 뒤 마무리
            if (Spinning && stamina != null && !_spinOut)
            {
                float before0 = stamina.Current;
                if (!stamina.Drain(_rules.SpinDrainPerSecond, dt)) _spinOut = true;
                SpinStaminaSpent += before0 - stamina.Current;
            }

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
                int next = NextSlash(held, stamina);
                if (next >= 0) StartSlash(next); // 다음 베기는 다음 스텝에 StepStarted로 알린다
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

        /// <summary>지금 베기가 끝났을 때 다음 베기 (−1 = 끝).</summary>
        int NextSlash(bool held, Stamina stamina)
        {
            if (!_rules.Spins) return SlashIndex + 1 < _rules.SlashCount ? SlashIndex + 1 : -1;
            int finisher = _rules.FinisherIndex;
            if (SlashIndex >= finisher) return -1;
            bool more = held && !_spinOut && (stamina == null || stamina.CanDrain);
            if (!more) return finisher;
            return SlashIndex + 1 < finisher ? SlashIndex + 1 : 0;
        }

        /// <summary>이번 베기에서 이 대상을 맞힐 수 있으면 기록하고 true.</summary>
        public bool TryHit(EntityId target)
        {
            if (Phase == ComboPhase.Idle || Aiming || !target.IsValid) return false;
            return _hitThisSlash.Add(target.Value);
        }

        /// <summary>끊는다 (회피 · 점프 · 공중 · 순간이동).</summary>
        public void Interrupt(AttackEnd reason)
        {
            if (Phase == ComboPhase.Idle) return;
            LastEnd = reason;
            Phase = ComboPhase.Idle;
            Aiming = false;
            _justStarted = false;
        }

        void StartSlash(int index)
        {
            SlashIndex = index;
            Elapsed = 0f;
            Phase = _rules.Slash(index).Windup > Epsilon ? ComboPhase.Windup : ComboPhase.Active;
            _hitThisSlash.Clear();
            _justStarted = true;
            if (_rules.Spins && index < _rules.FinisherIndex) { SpinCount++; SpinsThisCharge++; }
        }
    }
}
