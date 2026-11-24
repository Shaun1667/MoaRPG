using System;

namespace Moa.Core.Motion
{
    /// <summary>수평면(X · Z) 벡터. Core는 엔진을 모르므로 UnityEngine.Vector2 대신 쓴다.</summary>
    public readonly struct Planar
    {
        public readonly float X;
        public readonly float Z;

        public Planar(float x, float z) { X = x; Z = z; }

        public static readonly Planar Zero = new Planar(0f, 0f);

        public float Length => MathF.Sqrt(X * X + Z * Z);

        public Planar Normalized
        {
            get
            {
                float len = Length;
                return len > 0f ? new Planar(X / len, Z / len) : Zero;
            }
        }

        public static Planar operator +(Planar a, Planar b) => new Planar(a.X + b.X, a.Z + b.Z);
        public static Planar operator -(Planar a, Planar b) => new Planar(a.X - b.X, a.Z - b.Z);
        public static Planar operator *(Planar a, float s) => new Planar(a.X * s, a.Z * s);

        public override string ToString() => "(" + X + ", " + Z + ")";
    }

    /// <summary>
    /// 이동 순수 계산 (1-1): 카메라 기준 방향 변환 · 가속/감속 · 회전 · 점프 속도 · 경사 판정.
    /// 수치는 인자로만 받는다 (MovementTuning 에셋 → PlayerMotor가 넘긴다). 물리 · 충돌은 Unity 쪽.
    /// 각도는 도(degree), 요(yaw) 0 = +Z, 90 = +X (Unity와 같은 왼손 좌표).
    /// </summary>
    public static class MoveMath
    {
        const float DegToRad = MathF.PI / 180f;
        const float RadToDeg = 180f / MathF.PI;

        /// <summary>
        /// 입력(x = 오른쪽, y = 앞)을 카메라 요 기준 월드 수평 방향으로 바꾼다. 길이는 1을 넘지 않는다 (대각선이 빨라지지 않게).
        /// </summary>
        public static Planar CameraRelative(float inputX, float inputY, float cameraYawDegrees)
        {
            float len = MathF.Sqrt(inputX * inputX + inputY * inputY);
            if (len > 1f) { inputX /= len; inputY /= len; }
            float r = cameraYawDegrees * DegToRad;
            float s = MathF.Sin(r), c = MathF.Cos(r);
            // 앞 = (sin, cos), 오른쪽 = (cos, -sin)
            return new Planar(inputX * c + inputY * s, -inputX * s + inputY * c);
        }

        /// <summary>목표 속력: 입력 크기(0~1) × 걷기 또는 뛰기 속력.</summary>
        public static float TargetSpeed(bool walking, float walkSpeed, float runSpeed, float inputMagnitude)
        {
            float m = inputMagnitude < 0f ? 0f : (inputMagnitude > 1f ? 1f : inputMagnitude);
            return (walking ? walkSpeed : runSpeed) * m;
        }

        /// <summary>
        /// 현재 수평 속도를 목표 속도로 일정 비율(m/s²)만큼 옮긴다. 목표가 현재보다 빠르거나 같으면 가속, 느리면 감속 비율.
        /// </summary>
        public static Planar Approach(Planar current, Planar target, float acceleration, float deceleration, float dt)
        {
            float rate = target.Length >= current.Length ? acceleration : deceleration;
            float maxDelta = rate * dt;
            Planar d = target - current;
            float dist = d.Length;
            if (dist <= maxDelta || dist <= 0f) return target;
            return current + d * (maxDelta / dist);
        }

        /// <summary>from에서 to로 가는 가장 짧은 각도 차 (-180 ~ 180].</summary>
        public static float DeltaAngle(float from, float to)
        {
            float d = (to - from) % 360f;
            if (d > 180f) d -= 360f;
            if (d <= -180f) d += 360f;
            return d;
        }

        /// <summary>각도를 [0, 360)으로.</summary>
        public static float Wrap360(float degrees)
        {
            float d = degrees % 360f;
            return d < 0f ? d + 360f : d;
        }

        /// <summary>현재 요를 목표 요 쪽으로 초당 maxDegreesPerSecond만큼 짧은 쪽으로 돌린다.</summary>
        public static float TurnTowards(float currentYaw, float targetYaw, float maxDegreesPerSecond, float dt)
        {
            float d = DeltaAngle(currentYaw, targetYaw);
            float step = maxDegreesPerSecond * dt;
            if (MathF.Abs(d) <= step) return Wrap360(targetYaw);
            return Wrap360(currentYaw + MathF.Sign(d) * step);
        }

        /// <summary>수평 방향의 요 (0 = +Z, 90 = +X).</summary>
        public static float YawOf(Planar direction) => Wrap360(MathF.Atan2(direction.X, direction.Z) * RadToDeg);

        /// <summary>높이 h까지 오르는 처음 위쪽 속도 √(2gh). 값이 0 이하이면 0.</summary>
        public static float JumpVelocity(float height, float gravity)
        {
            if (height <= 0f || gravity <= 0f) return 0f;
            return MathF.Sqrt(2f * gravity * height);
        }

        /// <summary>한 스텝의 중력 적용 (낙하 속도 한계 maxFallSpeed, 양수).</summary>
        public static float ApplyGravity(float verticalVelocity, float gravity, float maxFallSpeed, float dt)
        {
            float v = verticalVelocity - gravity * dt;
            return v < -maxFallSpeed ? -maxFallSpeed : v;
        }

        /// <summary>바닥 법선의 위쪽 성분(normal.y)으로 본 경사각(도).</summary>
        public static float SlopeAngle(float normalY)
        {
            float y = normalY < -1f ? -1f : (normalY > 1f ? 1f : normalY);
            return MathF.Acos(y) * RadToDeg;
        }

        /// <summary>경사각이 한계 이하이면 걸을 수 있는 바닥.</summary>
        public static bool IsWalkable(float normalY, float slopeLimitDegrees) => normalY > 0f && SlopeAngle(normalY) <= slopeLimitDegrees;
    }
}
