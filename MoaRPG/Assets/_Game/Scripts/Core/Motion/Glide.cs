namespace Moa.Core.Motion
{
    /// <summary>활공 수치 (GlideTuning 에셋 → PlayerMotor가 넘긴다, BALANCE 5번).</summary>
    public readonly struct GlideRules
    {
        /// <summary>땅에서 이 높이 이상이어야 펼친다 (m).</summary>
        public readonly float MinHeight;
        /// <summary>수평 목표 속력 (m/s) · 가속 (m/s², 감속도 같은 값).</summary>
        public readonly float HorizontalSpeed;
        public readonly float HorizontalAcceleration;
        /// <summary>낙하 목표 속력 (m/s, 양수) · 그 속력으로 맞추는 수직 가속 (m/s²).</summary>
        public readonly float FallSpeed;
        public readonly float VerticalAcceleration;
        /// <summary>펼친 동안 초당 스태미나.</summary>
        public readonly float DrainPerSecond;

        public GlideRules(float minHeight, float horizontalSpeed, float horizontalAcceleration, float fallSpeed, float verticalAcceleration, float drainPerSecond)
        {
            MinHeight = minHeight;
            HorizontalSpeed = horizontalSpeed;
            HorizontalAcceleration = horizontalAcceleration;
            FallSpeed = fallSpeed;
            VerticalAcceleration = verticalAcceleration;
            DrainPerSecond = drainPerSecond;
        }
    }

    /// <summary>펼치지 못한 까닭 (끝에만 추가).</summary>
    public enum GlideBlock
    {
        None,
        Active,
        Grounded,
        TooLow,
        NoStamina,
    }

    /// <summary>활공이 끝난 까닭 (끝에만 추가).</summary>
    public enum GlideEnd
    {
        None,
        Toggled,
        Exhausted,
        Landed,
        Wall,
        Cancelled,
    }

    /// <summary>
    /// 활공 순수 규칙 (1-3): 펼치는 조건(공중 · 높이 · 스태미나) · 펼친 동안 소모 · 바닥나면 접힘 · 수평/수직 속도 맞추기.
    /// 높이 검사(레이) · 실제 이동 · 착지 · 벽은 PlayerMotor가 보고 <see cref="Close"/>에 까닭을 넘긴다.
    /// </summary>
    public sealed class Glide
    {
        const float Epsilon = 1e-5f;
        readonly GlideRules _rules;

        public Glide(GlideRules rules) { _rules = rules; }

        public GlideRules Rules => _rules;
        public bool Active { get; private set; }
        public GlideBlock LastBlock { get; private set; }
        public GlideEnd LastEnd { get; private set; }
        /// <summary>펼친 수 (디버그 · 확인용).</summary>
        public int Count { get; private set; }

        public GlideBlock Check(Stamina stamina, bool grounded, float heightAboveGround)
        {
            if (Active) return GlideBlock.Active;
            if (grounded) return GlideBlock.Grounded;
            if (heightAboveGround < _rules.MinHeight - Epsilon) return GlideBlock.TooLow;
            if (stamina != null && !stamina.CanDrain) return GlideBlock.NoStamina;
            return GlideBlock.None;
        }

        /// <summary>조건이 되면 펼친다 (스태미나는 펼친 동안 <see cref="Hold"/>가 쓴다). 안 되면 false (까닭은 LastBlock).</summary>
        public bool TryOpen(Stamina stamina, bool grounded, float heightAboveGround)
        {
            LastBlock = Check(stamina, grounded, heightAboveGround);
            if (LastBlock != GlideBlock.None) return false;
            Active = true;
            LastEnd = GlideEnd.None;
            Count++;
            return true;
        }

        /// <summary>펼친 고정 스텝: 초당 DrainPerSecond를 쓴다. 바닥나면 Exhausted로 접히고 false.</summary>
        public bool Hold(Stamina stamina, float dt)
        {
            if (!Active) return false;
            if (stamina != null && !stamina.Drain(_rules.DrainPerSecond, dt)) { Close(GlideEnd.Exhausted); return false; }
            return true;
        }

        public void Close(GlideEnd reason)
        {
            if (!Active) return;
            Active = false;
            LastEnd = reason;
        }

        /// <summary>수직 속도(위 = 양수)를 −FallSpeed 쪽으로 VerticalAcceleration만큼 맞춘다.</summary>
        public float Vertical(float current, float dt)
        {
            float target = -_rules.FallSpeed;
            float step = _rules.VerticalAcceleration * dt;
            if (current > target) return current - step < target ? target : current - step;
            return current + step > target ? target : current + step;
        }

        /// <summary>수평 속도를 direction(길이 1) × HorizontalSpeed 쪽으로 HorizontalAcceleration만큼 맞춘다.</summary>
        public Planar Horizontal(Planar current, Planar direction, float dt)
        {
            return MoveMath.Approach(current, direction * _rules.HorizontalSpeed, _rules.HorizontalAcceleration, _rules.HorizontalAcceleration, dt);
        }

        /// <summary>[상태 변경] 활공을 멈춘다 (순간이동 등).</summary>
        public void Cancel()
        {
            if (Active) LastEnd = GlideEnd.Cancelled;
            Active = false;
        }
    }
}
