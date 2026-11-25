using System.Collections.Generic;
using Moa.Core.Ids;

namespace Moa.Core.Combat
{
    /// <summary>
    /// 베기 표 하나를 차례로 진행한다 (2-4 스킬 · 궁극기 공용 — 강공격 ChargedAttack과 같은 방식).
    /// 각 베기 = 선딜 → 판정 → 후딜, 내딛기는 선딜 + 판정 구간 동안 같은 빠르기(합이 정확히 표 값). 베기마다 같은 대상은 한 번만 (<see cref="TryHit"/>).
    /// 다음 베기는 앞 베기가 끝난 다음 스텝에 StepStarted로 알린다.
    /// </summary>
    public sealed class SlashSequence
    {
        const float Epsilon = 1e-5f;
        static readonly ComboStepRules[] Empty = new ComboStepRules[0];

        readonly HashSet<long> _hitThisSlash = new HashSet<long>();
        ComboStepRules[] _slashes = Empty;
        bool _justStarted;

        public ComboPhase Phase { get; private set; }
        public bool Active => Phase != ComboPhase.Idle;
        /// <summary>지금(또는 마지막) 베기 번호, 0부터. 시작 전이면 −1.</summary>
        public int SlashIndex { get; private set; } = -1;
        public float Elapsed { get; private set; }
        public int SlashCount => _slashes.Length;
        public ComboStepRules CurrentSlash => SlashIndex >= 0 && SlashIndex < _slashes.Length ? _slashes[SlashIndex] : default;
        public AttackEnd LastEnd { get; private set; }
        public int HitsThisSlash => _hitThisSlash.Count;

        /// <summary>첫 베기를 시작한다 (표가 비면 false — 바로 끝).</summary>
        public bool Start(ComboStepRules[] slashes)
        {
            _slashes = slashes ?? Empty;
            if (_slashes.Length == 0)
            {
                Phase = ComboPhase.Idle;
                LastEnd = AttackEnd.Finished;
                return false;
            }
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
                if (SlashIndex + 1 < _slashes.Length) StartSlash(SlashIndex + 1);
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

        /// <summary>끊는다. 진행 중이었으면 true.</summary>
        public bool Interrupt(AttackEnd reason)
        {
            if (Phase == ComboPhase.Idle) return false;
            LastEnd = reason;
            Phase = ComboPhase.Idle;
            _justStarted = false;
            return true;
        }

        void StartSlash(int index)
        {
            SlashIndex = index;
            Elapsed = 0f;
            Phase = _slashes[index].Windup > Epsilon ? ComboPhase.Windup : ComboPhase.Active;
            _hitThisSlash.Clear();
            _justStarted = true;
        }
    }
}
