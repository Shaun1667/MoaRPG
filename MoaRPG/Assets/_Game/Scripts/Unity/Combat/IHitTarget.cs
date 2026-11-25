using Moa.Core.Combat;
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
        /// <summary>콤보 단계 · 강공격 베기 (1부터), 낙하면 1 = 낮은 · 2 = 높은.</summary>
        public readonly int Step;
        /// <summary>휘두르기 번호 (콤보 SwingCount · 강공격 Count · 낙하 Count).</summary>
        public readonly int Swing;
        public readonly Vector3 NumberPosition;
        /// <summary>어느 공격의 피해인지 (2-2).</summary>
        public readonly AttackMode Kind;
        /// <summary>쌓은 경직 (2-3, 반격 보너스 포함 — 그로기 중이거나 경직이 없는 대상이면 0).</summary>
        public readonly float Poise;
        /// <summary>이 한 방으로 그로기가 됐는지 (2-3).</summary>
        public readonly bool Broke;
        /// <summary>맞을 때 대상이 그로기였는지 — 그러면 피해에 그로기 배율이 들어 있다 (2-3).</summary>
        public readonly bool OnGroggy;

        public HitReport(EntityId target, string targetName, int damage, int applied, int step, int swing, Vector3 numberPosition)
            : this(target, targetName, damage, applied, step, swing, numberPosition, AttackMode.Combo) { }

        public HitReport(EntityId target, string targetName, int damage, int applied, int step, int swing, Vector3 numberPosition, AttackMode kind)
            : this(target, targetName, damage, applied, step, swing, numberPosition, kind, 0f, false, false) { }

        public HitReport(EntityId target, string targetName, int damage, int applied, int step, int swing, Vector3 numberPosition, AttackMode kind,
            float poise, bool broke, bool onGroggy)
        {
            Poise = poise;
            Broke = broke;
            OnGroggy = onGroggy;
            Target = target;
            TargetName = targetName;
            Damage = damage;
            Applied = applied;
            Step = step;
            Swing = swing;
            NumberPosition = numberPosition;
            Kind = kind;
        }
    }
}
