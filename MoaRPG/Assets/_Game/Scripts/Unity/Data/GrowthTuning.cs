using System;
using Moa.Core.Growth;
using UnityEngine;

namespace Moa.Unity.Data
{
    /// <summary>
    /// 성장 곡선 (3-5). 에셋 Assets/_Game/Data/Growth/Growth.asset, 값은 BALANCE 17번.
    /// 캐릭터 레벨: 줄 하나 = 레벨 하나(다음 레벨까지 경험치 · 능력치 배율 — 기본 체력 · 공격 · 방어에 곱함), 최대 레벨 = 줄 수, 돌파 표(상한 레벨 · 골드 · 배율 보너스).
    /// 무기 레벨: 같은 꼴(배율은 무기 기본 공격력에), 1차는 돌파 없음. 스킬 레벨: 줄 하나 = 레벨 하나(E · Q 베기 배율 · 다음 레벨까지 골드).
    /// 새 계정은 characterStartLevel · weaponStartLevel — 그 레벨의 배율이 1이어야 캐릭터 · 무기 에셋의 값(체력 1000 · 공격 60 · 40 …)이 그대로 나온다.
    /// 재련 · 장신구는 아직 없음. 코드에는 기본값을 두지 않는다.
    /// </summary>
    [CreateAssetMenu(menuName = "MoaRPG/Data/Growth Tuning", fileName = "Growth")]
    public sealed class GrowthTuning : ScriptableObject
    {
        [Serializable]
        public struct LevelRow
        {
            [Tooltip("Exp from this level to the next (unused on the last row)")]
            public int expToNext;
            [Tooltip("Stat multiplier at this level (1 = the values in the character / weapon asset)")]
            public float multiplier;
        }

        [Serializable]
        public struct AscensionRow
        {
            [Tooltip("Level where leveling stops until this ascension")]
            public int levelCap;
            [Tooltip("Gold to ascend")]
            public int goldCost;
            [Tooltip("Added to the stat multiplier after this ascension")]
            public float statBonus;
        }

        [Serializable]
        public struct SkillRow
        {
            [Tooltip("Skill (E) / burst (Q) slash multiplier x this at this level")]
            public float multiplier;
            [Tooltip("Gold from this level to the next (unused on the last row)")]
            public int goldToNext;
        }

        [Header("Character level (one row per level, row 1 = level 1)")]
        [SerializeField] LevelRow[] characterLevels;
        [Tooltip("Ascensions, in order")]
        [SerializeField] AscensionRow[] ascensions;
        [Tooltip("Level of a new account (its multiplier should be 1)")]
        [SerializeField] int characterStartLevel;

        [Header("Weapon level (one row per level)")]
        [SerializeField] LevelRow[] weaponLevels;
        [Tooltip("Weapon level of a new account (its multiplier should be 1)")]
        [SerializeField] int weaponStartLevel;

        [Header("Skill / burst level (one row per level)")]
        [SerializeField] SkillRow[] skillLevels;

        [Header("Exp material")]
        [Tooltip("Exp one exp material (Adventure EXP) gives, for character and weapon levels")]
        [SerializeField] int expPerItem;

        public int CharacterLevelCount => characterLevels != null ? characterLevels.Length : 0;
        public LevelRow GetCharacterLevel(int level) => characterLevels[level - 1];
        public int AscensionCount => ascensions != null ? ascensions.Length : 0;
        public AscensionRow GetAscension(int index) => ascensions[index];
        public int CharacterStartLevel => characterStartLevel;
        public int WeaponLevelCount => weaponLevels != null ? weaponLevels.Length : 0;
        public LevelRow GetWeaponLevel(int level) => weaponLevels[level - 1];
        public int WeaponStartLevel => weaponStartLevel;
        public int SkillLevelCount => skillLevels != null ? skillLevels.Length : 0;
        public SkillRow GetSkillLevel(int level) => skillLevels[level - 1];
        public int ExpPerItem => expPerItem;

        public GrowthRules ToRules()
        {
            var asc = new AscensionStep[AscensionCount];
            for (int i = 0; i < asc.Length; i++) asc[i] = new AscensionStep(ascensions[i].levelCap, ascensions[i].goldCost, ascensions[i].statBonus);
            var skillMul = new float[SkillLevelCount];
            var skillCost = new int[SkillLevelCount];
            for (int i = 0; i < skillMul.Length; i++) { skillMul[i] = skillLevels[i].multiplier; skillCost[i] = skillLevels[i].goldToNext; }
            return new GrowthRules(Curve(characterLevels, asc, characterStartLevel), Curve(weaponLevels, new AscensionStep[0], weaponStartLevel),
                new SkillCurve(skillMul, skillCost), expPerItem);
        }

        static LevelCurve Curve(LevelRow[] rows, AscensionStep[] asc, int start)
        {
            int n = rows != null ? rows.Length : 0;
            var exp = new int[n];
            var mul = new float[n];
            for (int i = 0; i < n; i++) { exp[i] = rows[i].expToNext; mul[i] = rows[i].multiplier; }
            return new LevelCurve(exp, mul, asc, start);
        }
    }
}
