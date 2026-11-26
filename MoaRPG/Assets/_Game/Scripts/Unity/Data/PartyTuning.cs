using Moa.Core.Combat;
using UnityEngine;

namespace Moa.Unity.Data
{
    /// <summary>
    /// 파티 교체 수치 + 시작 파티 (2-6). 에셋 Assets/_Game/Data/Characters/Party.asset, 값은 BALANCE 12번.
    /// 칸 = members 줄 순서 (1 ~ 4번 키). 칸 수는 표의 줄 수 (최대 4 — 입력이 넷).
    /// 계정의 캐릭터 · 파티 구성(저장 스냅샷)은 가챠 · 성장 때 — 지금은 이 표가 시작 파티. 코드에는 기본값을 두지 않는다.
    /// </summary>
    [CreateAssetMenu(menuName = "MoaRPG/Data/Party Tuning", fileName = "Party")]
    public sealed class PartyTuning : ScriptableObject
    {
        public const int MaxMembers = 4;

        [Tooltip("s from a switch until the next switch (Genshin: 1 s)")]
        [SerializeField] float switchCooldown;
        [Tooltip("Share of defeat energy the off-field members get (0..1, the on-field member gets all of it)")]
        [SerializeField] float benchEnergyRatio;
        [Tooltip("Starting party, slot 1 first (up to 4)")]
        [SerializeField] CharacterData[] members;

        public float SwitchCooldown => switchCooldown;
        public float BenchEnergyRatio => benchEnergyRatio;
        public int MemberCount => members != null ? Mathf.Min(members.Length, MaxMembers) : 0;
        public CharacterData GetMember(int index) => members != null && index >= 0 && index < MemberCount ? members[index] : null;

        public PartyRules ToRules() => new PartyRules(switchCooldown, benchEnergyRatio);
    }
}
