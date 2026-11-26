namespace Moa.Unity.Combat
{
    /// <summary>
    /// 처치하면 재화를 주는 적 (3-5). IHitTarget과 같은 컴포넌트가 함께 구현한다 — PlayerCombat이 맞힌 대상이 그 한 방으로 쓰러지면
    /// Defeated 이벤트로 알리고 GameRoot가 성장(Progression) 지갑에 넣는다. 값은 적 표(EnemyTuning goldOnDefeat · expItemsOnDefeat, BALANCE 17번).
    /// 다시 나면(RespawnDelay) 다시 벌 수 있다.
    /// </summary>
    public interface IRewardDrop
    {
        int GoldOnDefeat { get; }
        int ExpItemsOnDefeat { get; }
        /// <summary>3-7: 소원석 (EnemyTuning wishesOnDefeat).</summary>
        int WishesOnDefeat { get; }
    }
}
