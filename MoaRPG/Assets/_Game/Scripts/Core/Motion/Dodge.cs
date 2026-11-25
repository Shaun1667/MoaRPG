namespace Moa.Core.Motion
{
    /// <summary>회피 수치 (StaminaTuning 에셋 → PlayerMotor가 넘긴다, BALANCE 3번).</summary>
    public readonly struct DodgeRules
    {
        /// <summary>한 번에 가는 거리 (m).</summary>
        public readonly float Distance;
        /// <summary>대시 시간 (초). 이동은 이 시간 동안 같은 빠르기.</summary>
        public readonly float Duration;
        /// <summary>시작부터 무적인 시간 (초).</summary>
        public readonly float InvulnerableTime;
        /// <summary>끝난 뒤 다시 회피할 수 있기까지 (초).</summary>
        public readonly float Cooldown;
        /// <summary>스태미나 비용.</summary>
        public readonly float Cost;
        /// <summary>땅 위에서만 (원신 방식).</summary>
        public readonly bool GroundOnly;

        public DodgeRules(float distance, float duration, float invulnerableTime, float cooldown, float cost, bool groundOnly)
        {
            Distance = distance;
            Duration = duration;
            InvulnerableTime = invulnerableTime;
            Cooldown = cooldown;
            Cost = cost;
            GroundOnly = groundOnly;
        }

        /// <summary>대시 속력 = 거리 ÷ 시간 (m/s).</summary>
        public float Speed => Duration > 0f ? Distance / Duration : 0f;
    }

    /// <summary>회피를 못 한 까닭 (끝에만 추가).</summary>
    public enum DodgeBlock
    {
        None,
        Busy,
        Cooldown,
        Airborne,
        NoStamina,
    }

    /// <summary>
    /// 회피 순수 규칙 (1-2): 시작 조건(쿨 · 공중 · 스태미나) · 대시 시간 · 무적 시간 · 쿨.
    /// 방향 · 실제 이동은 PlayerMotor가 한다 — 매 고정 스텝 <see cref="Advance"/>가 돌려준 시간 × 속력만큼 민다
    /// (스텝 경계와 상관없이 합이 정확히 Distance).
    /// 무적은 지금은 상태 플래그뿐 (피해가 생기면 히트 판정이 이 값을 본다).
    /// </summary>
    public sealed class Dodge
    {
        const float Epsilon = 1e-5f;
        readonly DodgeRules _rules;

        public Dodge(DodgeRules rules) { _rules = rules; }

        public DodgeRules Rules => _rules;
        public bool Active { get; private set; }
        /// <summary>이번 회피가 시작된 뒤 지난 시간 (초).</summary>
        public float Elapsed { get; private set; }
        public float CooldownLeft { get; private set; }
        public bool Invulnerable => Active && Elapsed < _rules.InvulnerableTime - Epsilon;
        public DodgeBlock LastBlock { get; private set; }
        /// <summary>시작한 회피 수 (디버그 · 확인용).</summary>
        public int Count { get; private set; }

        public DodgeBlock Check(Stamina stamina, bool grounded)
        {
            if (Active) return DodgeBlock.Busy;
            if (CooldownLeft > 0f) return DodgeBlock.Cooldown;
            if (_rules.GroundOnly && !grounded) return DodgeBlock.Airborne;
            if (stamina != null && !stamina.CanAfford(_rules.Cost)) return DodgeBlock.NoStamina;
            return DodgeBlock.None;
        }

        /// <summary>조건이 되면 스태미나를 쓰고 시작한다. 안 되면 쓰지 않고 false (까닭은 LastBlock).</summary>
        public bool TryStart(Stamina stamina, bool grounded)
        {
            LastBlock = Check(stamina, grounded);
            if (LastBlock != DodgeBlock.None) return false;
            if (stamina != null && !stamina.TrySpend(_rules.Cost)) { LastBlock = DodgeBlock.NoStamina; return false; }
            Active = true;
            Elapsed = 0f;
            Count++;
            return true;
        }

        /// <summary>
        /// 한 고정 스텝 진행. 이 스텝에서 대시한 시간(0 ~ dt)을 돌려준다 — 이동 거리 = 그 시간 × Rules.Speed.
        /// 대시가 끝나면 쿨이 시작된다.
        /// </summary>
        public float Advance(float dt)
        {
            if (!Active)
            {
                if (CooldownLeft > 0f) { CooldownLeft -= dt; if (CooldownLeft <= Epsilon) CooldownLeft = 0f; }
                return 0f;
            }
            float remaining = _rules.Duration - Elapsed;
            float used = remaining < dt ? remaining : dt;
            if (used < 0f) used = 0f;
            Elapsed += used;
            if (_rules.Duration - Elapsed <= Epsilon)
            {
                Active = false;
                CooldownLeft = _rules.Cooldown;
            }
            return used;
        }

        /// <summary>[상태 변경] 진행 중인 회피를 멈춘다 (순간이동 등). 쿨은 없다.</summary>
        public void Cancel()
        {
            Active = false;
            Elapsed = 0f;
            CooldownLeft = 0f;
        }
    }
}
