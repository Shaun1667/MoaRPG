using Moa.Core.Ids;
using UnityEngine;
using EntityId = Moa.Core.Ids.EntityId; // UnityEngine.EntityId와 같은 이름

namespace Moa.Unity.Combat
{
    /// <summary>
    /// 맞을 수 있는 것 (2-1). 콜라이더가 있는 오브젝트(또는 그 부모)의 컴포넌트가 구현한다 — 판정은 GetComponentInParent로 찾는다.
    /// 피해 계산은 Core DamageMath, 체력은 Core Health. 여기는 Unity 쪽 창구만.
    /// </summary>
    public interface IHitTarget
    {
        EntityId Id { get; }
        bool Alive { get; }
        float Defense { get; }
        /// <summary>발 위치 (자동 조준 · 거리 계산).</summary>
        Vector3 Position { get; }
        /// <summary>피해 숫자가 뜨는 자리.</summary>
        Vector3 NumberPosition { get; }

        /// <summary>피해를 받는다 (from = 때린 쪽 위치, 넉백 방향). 실제로 줄어든 체력을 돌려준다.</summary>
        int ReceiveHit(int damage, Vector3 from);
    }

    /// <summary>한 번 맞힌 기록 (PlayerCombat.HitLanded).</summary>
    public readonly struct HitReport
    {
        public readonly EntityId Target;
        public readonly string TargetName;
        /// <summary>계산된 피해 (화면 숫자).</summary>
        public readonly int Damage;
        /// <summary>실제로 줄어든 체력 (무적이면 0).</summary>
        public readonly int Applied;
        /// <summary>콤보 단계 (1부터).</summary>
        public readonly int Step;
        /// <summary>휘두르기 번호 (ComboAttack.SwingCount).</summary>
        public readonly int Swing;
        public readonly Vector3 NumberPosition;

        public HitReport(EntityId target, string targetName, int damage, int applied, int step, int swing, Vector3 numberPosition)
        {
            Target = target;
            TargetName = targetName;
            Damage = damage;
            Applied = applied;
            Step = step;
            Swing = swing;
            NumberPosition = numberPosition;
        }
    }
}
