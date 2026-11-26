using Moa.Core.Gacha;
using UnityEngine;

namespace Moa.Unity.Data
{
    /// <summary>
    /// 가챠에서 나오는 것 하나 (3-7): 캐릭터 또는 무기. 에셋 Assets/_Game/Data/Banners/Items/&lt;id&gt;.asset — 새 항목 = 에셋 하나 + 배너 풀에 한 줄 (BALANCE 19번).
    /// 지금 있는 캐릭터 · 무기는 character · weapon 칸에 에셋을 넣는다(id = 캐릭터 saveId, 이름 = 캐릭터 nameKey). 아직 플레이할 수 없는 그레이박스 항목은 칸을 비우고 itemId · nameKey만 쓴다.
    /// 등급(★)은 여기 한 곳. 코드에 기본값 없음.
    /// </summary>
    [CreateAssetMenu(menuName = "MoaRPG/Data/Gacha Item", fileName = "GachaItem")]
    public sealed class GachaItemData : ScriptableObject
    {
        [Tooltip("Save id when there is no character asset (characters use their saveId). Never change it once players have saves")]
        [SerializeField] string itemId;
        [SerializeField] GachaItemKind kind;
        [Tooltip("Rarity: 3, 4 or 5 stars")]
        [SerializeField] int stars;
        [Tooltip("Text table key of the name when there is no character asset")]
        [SerializeField] string nameKey;
        [Tooltip("Playable character (optional) - id and name come from it")]
        [SerializeField] CharacterData character;
        [Tooltip("Weapon asset (optional) - weapon duplicates raise the refinement slot of party members holding it")]
        [SerializeField] WeaponTuning weapon;

        public GachaItemKind Kind => kind;
        public int Stars => stars;
        public CharacterData Character => character;
        public WeaponTuning Weapon => weapon;
        /// <summary>저장 id (캐릭터 에셋이 있으면 그 saveId).</summary>
        public string Id => character != null && !string.IsNullOrEmpty(character.SaveId) ? character.SaveId : itemId;
        /// <summary>이름 텍스트 키 (캐릭터 에셋이 있으면 그 nameKey).</summary>
        public string NameKey => character != null && !string.IsNullOrEmpty(character.NameKey) ? character.NameKey : nameKey;
        /// <summary>플레이할 수 있는지 (캐릭터 에셋이 있음 — 없으면 보유 목록에만).</summary>
        public bool Playable => kind == GachaItemKind.Character ? character != null : weapon != null;

        public GachaItem ToItem() => new GachaItem(Id, kind, stars);
    }
}
