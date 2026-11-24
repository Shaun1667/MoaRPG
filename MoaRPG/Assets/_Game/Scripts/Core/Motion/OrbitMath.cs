using System;

namespace Moa.Core.Motion
{
    /// <summary>궤도 카메라 각도 (도). 피치 양수 = 위에서 내려다봄 (Unity 오일러 X와 같은 부호).</summary>
    public readonly struct OrbitAngles
    {
        public readonly float Yaw;
        public readonly float Pitch;

        public OrbitAngles(float yaw, float pitch) { Yaw = yaw; Pitch = pitch; }
    }

    /// <summary>
    /// 3인칭 궤도 카메라 순수 계산 (1-1): 각도 제한 · 줌 · 벽 당김 거리 · 부드럽게 따라가기 비율.
    /// 수치는 인자로만 받는다 (CameraTuning 에셋 → OrbitCamera가 넘긴다). SphereCast는 Unity 쪽.
    /// </summary>
    public static class OrbitMath
    {
        const float DegToRad = MathF.PI / 180f;

        public static float Clamp(float v, float min, float max) => v < min ? min : (v > max ? max : v);

        public static float ClampPitch(float pitch, float minPitch, float maxPitch) => Clamp(pitch, minPitch, maxPitch);

        /// <summary>
        /// 시점 입력(도 단위로 이미 바꾼 값)을 더한다. lookX 양수 = 오른쪽으로 돈다, lookY 양수 = 위를 본다(피치 감소).
        /// invertY면 위아래를 뒤집는다. 피치는 제한, 요는 [0, 360).
        /// </summary>
        public static OrbitAngles ApplyLook(OrbitAngles current, float lookXDegrees, float lookYDegrees, float minPitch, float maxPitch, bool invertY)
        {
            float yaw = MoveMath.Wrap360(current.Yaw + lookXDegrees);
            float dy = invertY ? -lookYDegrees : lookYDegrees;
            float pitch = ClampPitch(current.Pitch - dy, minPitch, maxPitch);
            return new OrbitAngles(yaw, pitch);
        }

        /// <summary>휠 한 번(부호만 본다): 위로 굴리면(양수) 가까이, 아래로(음수) 멀리. 거리 제한.</summary>
        public static float Zoom(float distance, float scroll, float step, float minDistance, float maxDistance)
        {
            float sign = scroll > 0f ? 1f : (scroll < 0f ? -1f : 0f);
            return Clamp(distance - sign * step, minDistance, maxDistance);
        }

        /// <summary>
        /// 벽에 가렸을 때 허용 거리: 맞은 거리 - 여유, 단 minimum 이상 desired 이하. 안 맞았으면 desired.
        /// </summary>
        public static float CollisionDistance(float desired, bool hit, float hitDistance, float padding, float minimum)
        {
            if (!hit) return desired;
            float d = hitDistance - padding;
            if (d > desired) d = desired;
            if (d < minimum) d = minimum;
            return d;
        }

        /// <summary>
        /// 실제 카메라 거리: 허용 거리가 더 짧으면 바로 당기고(벽 뒤로 넘어가지 않게), 길어지면 releaseSharpness로 천천히 돌아간다.
        /// </summary>
        public static float SmoothDistance(float current, float allowed, float releaseSharpness, float dt)
        {
            if (allowed <= current) return allowed;
            return current + (allowed - current) * SmoothFactor(releaseSharpness, dt);
        }

        /// <summary>프레임 수와 상관없는 지수 감쇠 보간 비율 1 - e^(-sharpness·dt), 0 ~ 1. sharpness 0 이하 = 0(움직이지 않음).</summary>
        public static float SmoothFactor(float sharpness, float dt)
        {
            if (sharpness <= 0f || dt <= 0f) return 0f;
            return 1f - MathF.Exp(-sharpness * dt);
        }

        /// <summary>피벗에서 카메라까지의 오프셋 (x, y, z): 바라보는 방향의 반대쪽으로 distance.</summary>
        public static void Offset(OrbitAngles angles, float distance, out float x, out float y, out float z)
        {
            float yaw = angles.Yaw * DegToRad, pitch = angles.Pitch * DegToRad;
            float cp = MathF.Cos(pitch);
            // 앞 = (cp·sin yaw, -sin pitch, cp·cos yaw) → 오프셋 = -앞 × 거리
            x = -cp * MathF.Sin(yaw) * distance;
            y = MathF.Sin(pitch) * distance;
            z = -cp * MathF.Cos(yaw) * distance;
        }
    }
}
