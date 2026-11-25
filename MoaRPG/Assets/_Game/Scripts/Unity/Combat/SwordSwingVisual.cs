using Moa.Core.Combat;
using UnityEngine;

namespace Moa.Unity.Combat
{
    /// <summary>
    /// 그레이박스 칼 휘두르기 (2-1): 칼 상자의 회전 중심(pivot)을 콤보 단계에 맞춰 돌리고, 판정 구간 동안만 궤적(TrailRenderer)을 남긴다.
    /// 선딜 = 쉬는 자세 → 시작 자세, 판정 = 시작 → 끝, 후딜 = 끝 → 쉬는 자세. 홀수 타는 오른쪽 → 왼쪽, 짝수 타는 반대, 마지막 타는 위 → 아래.
    /// 자세 각도는 Setup 표(표현 값, 게임 수치 아님). 캐릭터 모델 · 애니메이션이 오면 이것을 뺀다 — 규칙(PlayerCombat)은 이것을 모른다.
    /// </summary>
    public sealed class SwordSwingVisual : MonoBehaviour
    {
        [SerializeField] PlayerCombat combat;
        [Tooltip("Rotated transform (blade is its child, pointing along local +Z)")]
        [SerializeField] Transform pivot;
        [SerializeField] TrailRenderer trail;
        [Tooltip("Local euler of the pivot at rest")]
        [SerializeField] Vector3 restEuler;
        [Tooltip("Side swing: start and end local euler (odd hits; even hits go the other way)")]
        [SerializeField] Vector3 sweepStartEuler;
        [SerializeField] Vector3 sweepEndEuler;
        [Tooltip("Last hit: start and end local euler")]
        [SerializeField] Vector3 finisherStartEuler;
        [SerializeField] Vector3 finisherEndEuler;

        /// <summary>궤적을 남기는 중인지 (디버그 · 확인용).</summary>
        public bool TrailOn => trail != null && trail.emitting;

        void Awake()
        {
            if (pivot != null) pivot.localRotation = Quaternion.Euler(restEuler);
            if (trail != null) trail.emitting = false;
        }

        void LateUpdate()
        {
            if (pivot == null || combat == null) return;
            ComboAttack combo = combat.Combo;
            Quaternion rest = Quaternion.Euler(restEuler);
            if (combo == null || !combo.Attacking)
            {
                pivot.localRotation = rest;
                SetTrail(false);
                return;
            }

            ComboStepRules s = combo.CurrentStep;
            bool last = combo.IsLastStep;
            bool reverse = !last && combo.StepIndex % 2 == 1;
            Quaternion start = Quaternion.Euler(last ? finisherStartEuler : reverse ? sweepEndEuler : sweepStartEuler);
            Quaternion end = Quaternion.Euler(last ? finisherEndEuler : reverse ? sweepStartEuler : sweepEndEuler);
            float t = combo.Elapsed;

            Quaternion q;
            bool active = false;
            if (t < s.Windup) q = Quaternion.Slerp(rest, start, Smooth(s.Windup > 0f ? t / s.Windup : 1f));
            else if (t < s.ActiveEnd) { q = Quaternion.Slerp(start, end, Smooth(s.Active > 0f ? (t - s.Windup) / s.Active : 1f)); active = true; }
            else q = Quaternion.Slerp(end, rest, Smooth(s.Recovery > 0f ? (t - s.ActiveEnd) / s.Recovery : 1f));
            pivot.localRotation = q;
            SetTrail(active);
        }

        void SetTrail(bool on)
        {
            if (trail == null || trail.emitting == on) return;
            trail.emitting = on;
            if (on) trail.Clear();
        }

        static float Smooth(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }
    }
}
