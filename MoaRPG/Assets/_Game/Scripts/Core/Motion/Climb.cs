using System;

namespace Moa.Core.Motion
{
    /// <summary>등반 수치 (ClimbTuning 에셋 → PlayerMotor가 넘긴다, BALANCE 4번).</summary>
    public readonly struct ClimbRules
    {
        /// <summary>붙을 수 있는 벽 기울기 (도, 위쪽과 벽 법선 사이 — 90 = 수직, 120 = 앞으로 기운 벽).</summary>
        public readonly float MinWallAngle;
        public readonly float MaxWallAngle;
        /// <summary>입력 방향과 "벽 쪽"(법선 반대) 사이가 이 각도 이내여야 붙는다 (도).</summary>
        public readonly float AttachInputAngle;
        /// <summary>벽 위 이동 속력 (m/s).</summary>
        public readonly float Speed;
        /// <summary>움직이는 동안 초당 스태미나.</summary>
        public readonly float DrainPerSecond;
        /// <summary>등반 점프(위로 도약) 비용.</summary>
        public readonly float JumpCost;
        /// <summary>도약 높이 (m) · 시간 (초).</summary>
        public readonly float JumpHeight;
        public readonly float JumpDuration;
        /// <summary>떨어진 뒤(회피 · 바닥남 · 벽 끝) 다시 붙기까지 (초).</summary>
        public readonly float ReattachDelay;
        /// <summary>벽 · 턱 검사 거리 (캡슐 표면에서, m).</summary>
        public readonly float ProbeDistance;
        /// <summary>턱 검사 높이: 캡슐 꼭대기에서 이만큼 아래 (m). 여기서 벽이 사라지면 올라서기.</summary>
        public readonly float LedgeHeight;
        /// <summary>올라서기: 앞으로 가는 거리 (m) · 시간 (초).</summary>
        public readonly float MantleForward;
        public readonly float MantleDuration;

        public ClimbRules(float minWallAngle, float maxWallAngle, float attachInputAngle, float speed, float drainPerSecond,
            float jumpCost, float jumpHeight, float jumpDuration, float reattachDelay,
            float probeDistance, float ledgeHeight, float mantleForward, float mantleDuration)
        {
            MinWallAngle = minWallAngle;
            MaxWallAngle = maxWallAngle;
            AttachInputAngle = attachInputAngle;
            Speed = speed;
            DrainPerSecond = drainPerSecond;
            JumpCost = jumpCost;
            JumpHeight = jumpHeight;
            JumpDuration = jumpDuration;
            ReattachDelay = reattachDelay;
            ProbeDistance = probeDistance;
            LedgeHeight = ledgeHeight;
            MantleForward = mantleForward;
            MantleDuration = mantleDuration;
        }

        /// <summary>도약 속력 = 높이 ÷ 시간 (m/s).</summary>
        public float JumpSpeed => JumpDuration > 0f ? JumpHeight / JumpDuration : 0f;
    }

    /// <summary>등반 단계.</summary>
    public enum ClimbPhase
    {
        None,
        Climbing,
        Leaping,
        Mantling,
    }

    /// <summary>붙지 못한 까닭 (끝에만 추가).</summary>
    public enum ClimbBlock
    {
        None,
        Busy,
        Cooldown,
        WallAngle,
        InputAngle,
        NoStamina,
    }

    /// <summary>등반이 끝난 까닭 (끝에만 추가).</summary>
    public enum ClimbEnd
    {
        None,
        Dodge,
        Exhausted,
        Landed,
        Mantled,
        WallEnded,
        Cancelled,
    }

    /// <summary>
    /// 등반 순수 규칙 (1-3): 붙는 조건(벽 기울기 · 입력 각도 · 스태미나 · 다시 붙기 지연) · 움직일 때만 소모 ·
    /// 위로 도약(비용 · 시간) · 올라서기(시간, 경로) · 끝나는 까닭.
    /// 벽 검사(레이) · 실제 이동은 PlayerMotor가 한다 — 도약은 <see cref="AdvanceLeap"/>이 돌려준 시간 × JumpSpeed만큼 민다.
    /// 스태미나는 <see cref="Stamina"/> 하나를 질주 · 회피와 같이 쓴다. 붙어 있는 동안은 회복하지 않는다(Stamina.Tick(dt, false)).
    /// </summary>
    public sealed class Climb
    {
        const float Epsilon = 1e-5f;
        const float RadToDeg = 57.29578f;
        readonly ClimbRules _rules;

        public Climb(ClimbRules rules) { _rules = rules; }

        public ClimbRules Rules => _rules;
        public ClimbPhase Phase { get; private set; }
        public bool Active => Phase != ClimbPhase.None;
        public bool Leaping => Phase == ClimbPhase.Leaping;
        public bool Mantling => Phase == ClimbPhase.Mantling;
        /// <summary>도약 · 올라서기가 시작된 뒤 지난 시간 (초).</summary>
        public float Elapsed { get; private set; }
        /// <summary>다시 붙을 수 있기까지 남은 시간 (초).</summary>
        public float ReattachLeft { get; private set; }
        public ClimbBlock LastBlock { get; private set; }
        public ClimbEnd LastEnd { get; private set; }
        /// <summary>붙은 수 · 도약 수 · 올라서기 수 (디버그 · 확인용).</summary>
        public int Count { get; private set; }
        public int LeapCount { get; private set; }
        public int MantleCount { get; private set; }

        /// <summary>벽 기울기 (도): 위쪽(0,1,0)과 법선 사이. 수직 벽 = 90.</summary>
        public static float WallAngle(float normalY)
        {
            float y = normalY < -1f ? -1f : (normalY > 1f ? 1f : normalY);
            return MathF.Acos(y) * RadToDeg;
        }

        public bool IsClimbableWall(float normalY)
        {
            float a = WallAngle(normalY);
            return a >= _rules.MinWallAngle - Epsilon && a <= _rules.MaxWallAngle + Epsilon;
        }

        /// <summary>수평 방향(dirX, dirZ)과 벽 쪽(법선 반대의 수평 성분) 사이 각도 (도, 0 ~ 180). 법선이 수평 성분이 없으면 180.</summary>
        public static float InputAngle(float dirX, float dirZ, float normalX, float normalZ)
        {
            float dl = MathF.Sqrt(dirX * dirX + dirZ * dirZ);
            float nl = MathF.Sqrt(normalX * normalX + normalZ * normalZ);
            if (dl < Epsilon || nl < Epsilon) return 180f;
            float dot = (dirX * -normalX + dirZ * -normalZ) / (dl * nl);
            if (dot > 1f) dot = 1f; else if (dot < -1f) dot = -1f;
            return MathF.Acos(dot) * RadToDeg;
        }

        public ClimbBlock Check(Stamina stamina, float wallNormalY, float inputAngle)
        {
            if (Active) return ClimbBlock.Busy;
            if (ReattachLeft > 0f) return ClimbBlock.Cooldown;
            if (!IsClimbableWall(wallNormalY)) return ClimbBlock.WallAngle;
            if (inputAngle > _rules.AttachInputAngle + Epsilon) return ClimbBlock.InputAngle;
            if (stamina != null && !stamina.CanDrain) return ClimbBlock.NoStamina;
            return ClimbBlock.None;
        }

        /// <summary>조건이 되면 붙는다 (스태미나는 쓰지 않음). 안 되면 false (까닭은 LastBlock).</summary>
        public bool TryAttach(Stamina stamina, float wallNormalY, float inputAngle)
        {
            LastBlock = Check(stamina, wallNormalY, inputAngle);
            if (LastBlock != ClimbBlock.None) return false;
            Phase = ClimbPhase.Climbing;
            Elapsed = 0f;
            LastEnd = ClimbEnd.None;
            Count++;
            return true;
        }

        /// <summary>
        /// 붙어 있는 고정 스텝 (Climbing 단계): moving이면 초당 DrainPerSecond를 쓴다.
        /// 스태미나가 바닥나 있거나 이번에 바닥나면 Exhausted로 끝나고 false.
        /// </summary>
        public bool Hold(Stamina stamina, bool moving, float dt)
        {
            if (!Active) return false;
            if (stamina != null)
            {
                if (!stamina.CanDrain) { End(ClimbEnd.Exhausted); return false; }
                if (moving && Phase == ClimbPhase.Climbing && !stamina.Drain(_rules.DrainPerSecond, dt)) { End(ClimbEnd.Exhausted); return false; }
            }
            return true;
        }

        /// <summary>위로 도약: Climbing 단계에서 비용이 되면 쓰고 시작한다. 안 되면 쓰지 않고 false.</summary>
        public bool TryLeap(Stamina stamina)
        {
            if (Phase != ClimbPhase.Climbing) { LastBlock = ClimbBlock.Busy; return false; }
            if (stamina != null && !stamina.TrySpend(_rules.JumpCost)) { LastBlock = ClimbBlock.NoStamina; return false; }
            LastBlock = ClimbBlock.None;
            Phase = ClimbPhase.Leaping;
            Elapsed = 0f;
            LeapCount++;
            return true;
        }

        /// <summary>도약 한 스텝: 이번에 도약한 시간(0 ~ dt). 끝나면 Climbing으로 돌아간다. 거리 = 시간 × Rules.JumpSpeed.</summary>
        public float AdvanceLeap(float dt)
        {
            if (Phase != ClimbPhase.Leaping) return 0f;
            float remaining = _rules.JumpDuration - Elapsed;
            float used = remaining < dt ? remaining : dt;
            if (used < 0f) used = 0f;
            Elapsed += used;
            if (_rules.JumpDuration - Elapsed <= Epsilon) { Phase = ClimbPhase.Climbing; Elapsed = 0f; }
            return used;
        }

        /// <summary>올라서기 시작 (Climbing · Leaping 단계에서).</summary>
        public bool StartMantle()
        {
            if (Phase != ClimbPhase.Climbing && Phase != ClimbPhase.Leaping) return false;
            Phase = ClimbPhase.Mantling;
            Elapsed = 0f;
            MantleCount++;
            return true;
        }

        /// <summary>올라서기 한 스텝: 진행률(0 ~ 1)을 돌려준다. 1이 되면 Mantled로 끝난다.</summary>
        public float AdvanceMantle(float dt)
        {
            if (Phase != ClimbPhase.Mantling) return 1f;
            Elapsed += dt;
            float t = _rules.MantleDuration > 0f ? Elapsed / _rules.MantleDuration : 1f;
            if (t >= 1f - Epsilon) { t = 1f; End(ClimbEnd.Mantled); }
            return t;
        }

        /// <summary>
        /// 올라서기 경로: 앞 절반에 위로(up01 0 → 1), 뒤 절반에 앞으로(forward01 0 → 1) — 벽을 뚫지 않게 먼저 오른다.
        /// </summary>
        public static void MantlePath(float progress, out float up01, out float forward01)
        {
            float t = progress < 0f ? 0f : (progress > 1f ? 1f : progress);
            up01 = t >= 0.5f ? 1f : t * 2f;
            forward01 = t <= 0.5f ? 0f : (t - 0.5f) * 2f;
        }

        /// <summary>등반을 끝낸다. 떨어지는 끝(회피 · 바닥남 · 벽 끝)이면 다시 붙기 지연이 시작된다.</summary>
        public void End(ClimbEnd reason)
        {
            if (!Active) return;
            Phase = ClimbPhase.None;
            Elapsed = 0f;
            LastEnd = reason;
            ReattachLeft = reason == ClimbEnd.Dodge || reason == ClimbEnd.Exhausted || reason == ClimbEnd.WallEnded ? _rules.ReattachDelay : 0f;
        }

        /// <summary>고정 스텝마다: 다시 붙기 지연을 줄인다.</summary>
        public void Tick(float dt)
        {
            if (Active || ReattachLeft <= 0f) return;
            ReattachLeft -= dt;
            if (ReattachLeft <= Epsilon) ReattachLeft = 0f;
        }

        /// <summary>[상태 변경] 등반을 멈춘다 (순간이동 등). 지연 없음.</summary>
        public void Cancel()
        {
            if (Active) LastEnd = ClimbEnd.Cancelled;
            Phase = ClimbPhase.None;
            Elapsed = 0f;
            ReattachLeft = 0f;
        }
    }
}
