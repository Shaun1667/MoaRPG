namespace Moa.Unity.Combat
{
    /// <summary>
    /// 처치하면 궁극기 에너지를 주는 적 (2-4). IHitTarget과 같은 컴포넌트가 함께 구현한다 — PlayerCombat이 맞힌 대상이 그 한 방으로 쓰러지면 묻는다.
    /// 값은 적 표(DummyTuning energyOnDefeat, BALANCE 10번).
    /// </summary>
    public interface IEnergyDrop
    {
        float EnergyOnDefeat { get; }
    }
}
