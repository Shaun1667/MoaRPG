using UnityEngine;

namespace Moa.Unity.Data
{
    /// <summary>
    /// 플레이어블 캐릭터 한 명 (2-6). 에셋 Assets/_Game/Data/Characters/&lt;캐릭터&gt;.asset — 새 캐릭터 = 이 폴더에 에셋 하나 (BALANCE 12번).
    /// 이름은 텍스트 표 키(nameKey), 무기 = WeaponTuning(콤보 · 강공격 · 낙하), 스킬 · 궁극기 = SkillTuning.
    /// 공격력 = 캐릭터 기본 공격력 + 무기 공격력 (원신 방식, 성장은 나중). 색은 그레이박스 몸 색 · 파티 HUD 색 (모델이 오면 비주얼 ID로 바꾼다).
    /// 캐릭터별 분기 코드 없음. 코드에는 기본값을 두지 않는다.
    /// </summary>
    [CreateAssetMenu(menuName = "MoaRPG/Data/Character", fileName = "Character")]
    public sealed class CharacterData : ScriptableObject
    {
        [Tooltip("Text table key of the character name")]
        [SerializeField] string nameKey;
        [Tooltip("Graybox body color and party HUD color")]
        [SerializeField] Color color;
        [Tooltip("Weapon: combo, charged attack, plunge")]
        [SerializeField] WeaponTuning weapon;
        [Tooltip("Skill (E) and burst (Q)")]
        [SerializeField] SkillTuning skills;
        [Tooltip("Character base attack, added to the weapon base attack")]
        [SerializeField] float baseAttack;

        public string NameKey => nameKey;
        public Color Color => color;
        public WeaponTuning Weapon => weapon;
        public SkillTuning Skills => skills;
        public float BaseAttack => baseAttack;
        /// <summary>캐릭터 + 무기 공격력.</summary>
        public float TotalAttack => baseAttack + (weapon != null ? weapon.BaseAttack : 0f);
    }
}
