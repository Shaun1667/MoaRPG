using Moa.Core.Combat;

namespace Moa.Unity.Combat
{
    /// <summary>
    /// 경직 게이지가 있는 맞는 쪽 (2-3). IHitTarget과 같은 컴포넌트가 함께 구현한다 — 없으면 경직 · 그로기 없이 피해만.
    /// 규칙은 Core Poise. PlayerCombat이 피해를 계산할 때 <see cref="DamageTakenMultiplier"/>를 곱하고, 피해 뒤에 <see cref="ReceivePoise"/>.
    /// </summary>
    public interface IPoiseTarget
    {
        /// <summary>경직 게이지 (Core, 없으면 null).</summary>
        Poise Poise { get; }
        bool Groggy { get; }
        /// <summary>지금 받는 피해 배율 (그로기면 1보다 큼).</summary>
        float DamageTakenMultiplier { get; }

        /// <summary>경직을 쌓는다. 이번에 그로기가 됐으면 true.</summary>
        bool ReceivePoise(float amount);
    }
}
