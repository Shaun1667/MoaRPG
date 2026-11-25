using Moa.Core.Motion;
using UnityEngine;

namespace Moa.Unity.Data
{
    /// <summary>
    /// 등반 수치 (1-3). 에셋 Assets/_Game/Data/Player/ClimbTuning.asset, 값은 BALANCE.md 4번.
    /// 코드에는 기본값을 두지 않는다 (CLAUDE.md 코딩 규칙). 스태미나 자체는 StaminaTuning(BALANCE 3번).
    /// </summary>
    [CreateAssetMenu(menuName = "MoaRPG/Data/Climb Tuning", fileName = "ClimbTuning")]
    public sealed class ClimbTuning : ScriptableObject
    {
        [Header("Attach")]
        [Tooltip("Degrees between world up and the wall normal (90 = vertical)")]
        [SerializeField] float minWallAngle;
        [SerializeField] float maxWallAngle;
        [Tooltip("Max degrees between the move input and the direction into the wall")]
        [SerializeField] float attachInputAngle;
        [Tooltip("s after dropping off (dodge, out of stamina, wall end) before attaching again")]
        [SerializeField] float reattachDelay;
        [Tooltip("Wall / ledge probe distance from the capsule surface (m)")]
        [SerializeField] float probeDistance;

        [Header("Move")]
        [Tooltip("m/s on the wall")]
        [SerializeField] float climbSpeed;
        [Tooltip("Stamina per second while moving on the wall")]
        [SerializeField] float drainPerSecond;

        [Header("Climb jump (leap up)")]
        [SerializeField] float jumpCost;
        [Tooltip("m")]
        [SerializeField] float jumpHeight;
        [Tooltip("s")]
        [SerializeField] float jumpDuration;

        [Header("Mantle (step up onto the top)")]
        [Tooltip("Ledge probe height below the capsule top (m). When the wall is gone there, mantle")]
        [SerializeField] float ledgeHeight;
        [Tooltip("m forward onto the top")]
        [SerializeField] float mantleForward;
        [Tooltip("s")]
        [SerializeField] float mantleDuration;

        public float MinWallAngle => minWallAngle;
        public float MaxWallAngle => maxWallAngle;
        public float AttachInputAngle => attachInputAngle;
        public float ReattachDelay => reattachDelay;
        public float ProbeDistance => probeDistance;
        public float ClimbSpeed => climbSpeed;
        public float DrainPerSecond => drainPerSecond;
        public float JumpCost => jumpCost;
        public float JumpHeight => jumpHeight;
        public float JumpDuration => jumpDuration;
        public float LedgeHeight => ledgeHeight;
        public float MantleForward => mantleForward;
        public float MantleDuration => mantleDuration;

        public ClimbRules ToRules() => new ClimbRules(minWallAngle, maxWallAngle, attachInputAngle, climbSpeed, drainPerSecond,
            jumpCost, jumpHeight, jumpDuration, reattachDelay, probeDistance, ledgeHeight, mantleForward, mantleDuration);
    }
}
