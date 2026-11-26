using Moa.Core.Combat;
using UnityEngine;

namespace Moa.Unity.Data
{
    /// <summary>
    /// 플레이어 체력 공통 수치 (3-1). 에셋 Assets/_Game/Data/Player/PlayerHealthTuning.asset, 값은 BALANCE.md 13번.
    /// 피격 경직 · 밀려남 · 전원 쓰러짐(어두워짐 · 되살아나는 체력) · 회복 지점. 캐릭터마다 최대 체력 · 방어는 CharacterData, 적 공격력은 EnemyTuning.
    /// 코드에는 기본값을 두지 않는다. 색 · 막대 크기는 Setup 표.
    /// </summary>
    [CreateAssetMenu(menuName = "MoaRPG/Data/Player Health Tuning", fileName = "PlayerHealthTuning")]
    public sealed class PlayerHealthTuning : ScriptableObject
    {
        [Header("Hit reaction")]
        [Tooltip("s the field character stops after an enemy hit (on the ground only, a dodge ends it)")]
        [SerializeField] float hitStunTime;
        [Tooltip("m pushed away from the attacker during the stun")]
        [SerializeField] float knockbackDistance;

        [Header("Everyone down")]
        [Tooltip("s the screen takes to go dark")]
        [SerializeField] float wipeFadeOutTime;
        [Tooltip("s it stays dark (the party is moved to the respawn point at the start)")]
        [SerializeField] float wipeDarkTime;
        [Tooltip("s the screen takes to come back")]
        [SerializeField] float wipeFadeInTime;
        [Tooltip("Health everyone comes back with (x max health, 0 - 1)")]
        [SerializeField] float reviveHealthRatio;

        [Header("Healing point")]
        [Tooltip("m (flat, and height difference) the player must be within")]
        [SerializeField] float healPointRadius;
        [Tooltip("Health per second for every member while standing there (x max health). Downed members come back")]
        [SerializeField] float healPerSecond;

        public float HitStunTime => hitStunTime;
        public float KnockbackDistance => knockbackDistance;
        public float WipeFadeOutTime => wipeFadeOutTime;
        public float WipeDarkTime => wipeDarkTime;
        public float WipeFadeInTime => wipeFadeInTime;
        public float ReviveHealthRatio => reviveHealthRatio;
        public float HealPointRadius => healPointRadius;
        public float HealPerSecond => healPerSecond;

        public VitalsRules ToRules() => new VitalsRules(hitStunTime, knockbackDistance, wipeFadeOutTime, wipeDarkTime, wipeFadeInTime,
            reviveHealthRatio, healPerSecond);
    }
}
