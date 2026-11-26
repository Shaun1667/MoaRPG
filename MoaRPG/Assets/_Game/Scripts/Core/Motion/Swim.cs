namespace Moa.Core.Motion
{
    /// <summary>수영 수치 (SwimTuning 에셋 → PlayerMotor가 넘긴다, BALANCE 18번).</summary>
    public readonly struct SwimRules
    {
        /// <summary>물 표면에서 발까지 깊이가 이만큼 이상이면 수영이 된다 (m).</summary>
        public readonly float EnterDepth;
        /// <summary>수영 중 깊이가 이보다 얕아지면(바닥을 딛고 올라옴) 수영이 끝난다 (m, EnterDepth보다 작게).</summary>
        public readonly float ExitDepth;
        /// <summary>수영 중 떠 있는 깊이 — 발이 표면 아래 이만큼 (m).</summary>
        public readonly float FloatDepth;
        /// <summary>떠 있는 깊이로 맞추는 빠르기 (1/s) · 그때 최대 수직 속력 (m/s).</summary>
        public readonly float FloatSharpness;
        public readonly float FloatMaxSpeed;
        /// <summary>수평 속력 (m/s) · 질주 수영 속력 · 가속 (m/s², 감속도 같은 값).</summary>
        public readonly float Speed;
        public readonly float SprintSpeed;
        public readonly float Acceleration;
        /// <summary>물에 있는 동안 초당 스태미나 (가만히 있어도) · 질주 수영 중 초당.</summary>
        public readonly float DrainPerSecond;
        public readonly float SprintDrainPerSecond;
        /// <summary>익사하면 필드 캐릭터가 잃는 체력 (최대 체력 비율, 1은 남김).</summary>
        public readonly float DrownHealthRatio;

        public SwimRules(float enterDepth, float exitDepth, float floatDepth, float floatSharpness, float floatMaxSpeed,
            float speed, float sprintSpeed, float acceleration, float drainPerSecond, float sprintDrainPerSecond, float drownHealthRatio)
        {
            EnterDepth = enterDepth;
            ExitDepth = exitDepth;
            FloatDepth = floatDepth;
            FloatSharpness = floatSharpness;
            FloatMaxSpeed = floatMaxSpeed;
            Speed = speed;
            SprintSpeed = sprintSpeed;
            Acceleration = acceleration;
            DrainPerSecond = drainPerSecond;
            SprintDrainPerSecond = sprintDrainPerSecond;
            DrownHealthRatio = drownHealthRatio;
        }
    }

    /// <summary>수영이 끝난 까닭 (끝에만 추가).</summary>
    public enum SwimEnd
    {
        None,
        /// <summary>얕아져 걸어 나옴.</summary>
        Exited,
        /// <summary>스태미나가 바닥나 익사 → 물가로.</summary>
        Drowned,
        /// <summary>벽에 붙음 (물에서 기어오르기).</summary>
        Climbed,
        /// <summary>순간이동 등.</summary>
        Cancelled,
    }

    /// <summary>
    /// 수영 순수 규칙 (3-6): 들어가고 나오는 깊이(사이를 두어 떨리지 않게) · 물에 있는 동안 스태미나(가만히 있어도, 질주 수영은 더) ·
    /// 바닥나면 익사 · 떠 있는 깊이로 맞추는 수직 속도 · 익사 피해. 스태미나는 이동 · 회피와 같은 <see cref="Stamina"/> 하나 (1-2).
    /// 물 깊이 재기 · 실제 이동 · 물가 찾기는 PlayerMotor. 수영 중에는 공격 · 회피 · 활공 · 점프가 없다.
    /// 지침(바닥난 뒤 회복 전)이면 계속 쓰기가 안 되므로 물에 들어가면 바로 익사한다 (등반 · 활공이 못 붙고 못 펴는 것과 같은 규칙).
    /// </summary>
    public sealed class Swim
    {
        readonly SwimRules _rules;

        public Swim(SwimRules rules) { _rules = rules; }

        public SwimRules Rules => _rules;
        public bool Active { get; private set; }
        /// <summary>지난 스텝에 질주 수영이었는지.</summary>
        public bool Sprinting { get; private set; }
        public SwimEnd LastEnd { get; private set; }
        /// <summary>물에 들어간 수 · 익사한 수 (디버그 · 확인용).</summary>
        public int Count { get; private set; }
        public int DrownCount { get; private set; }

        /// <summary>지금 깊이에서 수영을 시작해야 하는지.</summary>
        public bool ShouldEnter(float depth) => !Active && depth >= _rules.EnterDepth;

        /// <summary>수영 중 이 깊이면 끝나야 하는지 (바닥을 딛고 올라옴).</summary>
        public bool ShouldExit(float depth) => Active && depth < _rules.ExitDepth;

        public void Enter()
        {
            if (Active) return;
            Active = true;
            Sprinting = false;
            LastEnd = SwimEnd.None;
            Count++;
        }

        /// <summary>
        /// 물에 있는 고정 스텝: 초당 DrainPerSecond(움직이며 질주면 SprintDrainPerSecond)를 쓴다. 쓸 수 없거나 이번에 바닥나면 익사(Drowned)로 끝나고 false.
        /// </summary>
        public bool Hold(Stamina stamina, bool wantSprint, bool moving, float dt)
        {
            if (!Active) return false;
            Sprinting = wantSprint && moving && (stamina == null || stamina.CanDrain);
            float rate = Sprinting ? _rules.SprintDrainPerSecond : _rules.DrainPerSecond;
            if (stamina != null && !stamina.Drain(rate, dt))
            {
                Sprinting = false;
                End(SwimEnd.Drowned);
                DrownCount++;
                return false;
            }
            return true;
        }

        /// <summary>수평 목표 속력 (입력 길이 0 ~ 1 비례, 질주면 SprintSpeed).</summary>
        public float TargetSpeed(float inputLength)
        {
            if (inputLength <= 0f) return 0f;
            if (inputLength > 1f) inputLength = 1f;
            return (Sprinting ? _rules.SprintSpeed : _rules.Speed) * inputLength;
        }

        /// <summary>수직 속도 (위 = 양수): 깊이가 FloatDepth가 되도록, 차이 × FloatSharpness를 ±FloatMaxSpeed로 자른다.</summary>
        public float Vertical(float depth)
        {
            float v = (depth - _rules.FloatDepth) * _rules.FloatSharpness;
            if (v > _rules.FloatMaxSpeed) v = _rules.FloatMaxSpeed;
            if (v < -_rules.FloatMaxSpeed) v = -_rules.FloatMaxSpeed;
            return v;
        }

        public void End(SwimEnd reason)
        {
            if (!Active) return;
            Active = false;
            Sprinting = false;
            LastEnd = reason;
        }

        /// <summary>[상태 변경] 수영을 멈춘다 (순간이동 등).</summary>
        public void Cancel() => End(SwimEnd.Cancelled);

        /// <summary>익사 피해: 최대 체력 × ratio (올림), 남은 체력보다 크면 1을 남긴다 (익사로는 쓰러지지 않는다). 0 이하면 0.</summary>
        public static int DrownDamage(int maxHealth, int currentHealth, float ratio)
        {
            if (maxHealth <= 0 || currentHealth <= 1 || ratio <= 0f) return 0;
            int d = (int)System.Math.Ceiling(maxHealth * (double)ratio - 1e-9);
            if (d > currentHealth - 1) d = currentHealth - 1;
            return d < 0 ? 0 : d;
        }
    }
}
