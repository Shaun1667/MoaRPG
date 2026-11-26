using System;

namespace Moa.Core.Growth
{
    /// <summary>돌파 한 단계 (3-5): 이 레벨(LevelCap)에서 막히고, 골드 Cost로 넘으면 능력치 배율에 StatBonus가 더해진다.</summary>
    public readonly struct AscensionStep
    {
        public readonly int LevelCap;
        public readonly int Cost;
        public readonly float StatBonus;

        public AscensionStep(int levelCap, int cost, float statBonus)
        {
            LevelCap = levelCap;
            Cost = cost < 0 ? 0 : cost;
            StatBonus = statBonus;
        }
    }

    /// <summary>
    /// 레벨 곡선 (3-5, 캐릭터 · 무기가 같은 꼴). 줄 하나 = 레벨 하나: 다음 레벨까지 경험치 · 능력치 배율. 최대 레벨 = 줄 수.
    /// 돌파 단계 p의 레벨 상한 = <see cref="Cap"/>(p) (마지막 돌파 뒤에는 최대 레벨). 배율 = 그 레벨의 배율 + 넘은 돌파들의 StatBonus.
    /// 기준(지금 데이터 값 = 배율 1)은 데이터의 몫 — 새 계정은 <see cref="StartLevel"/>에서 시작한다.
    /// </summary>
    public sealed class LevelCurve
    {
        readonly int[] _expToNext;
        readonly float[] _multiplier;
        readonly AscensionStep[] _ascensions;

        public LevelCurve(int[] expToNext, float[] multiplier, AscensionStep[] ascensions, int startLevel)
        {
            if (multiplier == null || multiplier.Length == 0) throw new ArgumentException("needs at least one level", nameof(multiplier));
            if (expToNext == null || expToNext.Length < multiplier.Length - 1) throw new ArgumentException("one exp row per level below the max", nameof(expToNext));
            _multiplier = (float[])multiplier.Clone();
            _expToNext = new int[multiplier.Length];
            for (int i = 0; i < multiplier.Length - 1; i++) _expToNext[i] = expToNext[i] > 0 ? expToNext[i] : 1;
            _ascensions = ascensions != null ? (AscensionStep[])ascensions.Clone() : new AscensionStep[0];
            int last = 0;
            foreach (var a in _ascensions)
            {
                if (a.LevelCap <= last || a.LevelCap >= MaxLevel) throw new ArgumentException("ascension caps must rise and stay below the max level", nameof(ascensions));
                last = a.LevelCap;
            }
            StartLevel = ClampLevel(startLevel);
        }

        public int MaxLevel => _multiplier.Length;
        public int AscensionCount => _ascensions.Length;
        public AscensionStep Ascension(int phase) => _ascensions[phase];
        /// <summary>새 계정의 레벨 (그 레벨을 가질 수 있는 가장 낮은 돌파 단계).</summary>
        public int StartLevel { get; }
        public int StartPhase => PhaseFor(StartLevel);

        public int ClampLevel(int level) => level < 1 ? 1 : level > MaxLevel ? MaxLevel : level;

        /// <summary>돌파 단계 phase의 레벨 상한.</summary>
        public int Cap(int phase) => phase >= 0 && phase < _ascensions.Length ? _ascensions[phase].LevelCap : MaxLevel;

        /// <summary>level을 가질 수 있는 가장 낮은 돌파 단계.</summary>
        public int PhaseFor(int level)
        {
            for (int p = 0; p < _ascensions.Length; p++) if (level <= _ascensions[p].LevelCap) return p;
            return _ascensions.Length;
        }

        /// <summary>level에서 phase가 될 수 있는지 (p는 앞 돌파의 상한 이상 · 자기 상한 이하).</summary>
        public bool IsValid(int level, int phase) =>
            phase >= 0 && phase <= _ascensions.Length && level >= 1 && level <= Cap(phase) && (phase == 0 || level >= Cap(phase - 1));

        /// <summary>level에서 다음 레벨까지 경험치 (최대 레벨이면 0).</summary>
        public int ExpToNext(int level) => level >= 1 && level < MaxLevel ? _expToNext[level - 1] : 0;

        /// <summary>레벨 배율 (돌파 보너스 없이).</summary>
        public float LevelMultiplier(int level) => _multiplier[ClampLevel(level) - 1];

        /// <summary>능력치 배율 = 레벨 배율 + 넘은 돌파(phase개)의 보너스.</summary>
        public float Multiplier(int level, int phase)
        {
            float m = LevelMultiplier(level);
            for (int p = 0; p < phase && p < _ascensions.Length; p++) m += _ascensions[p].StatBonus;
            return m;
        }
    }

    /// <summary>스킬 레벨 곡선 (3-5): 줄 하나 = 레벨 하나 — 피해 배율 · 다음 레벨까지 골드. 최대 레벨 = 줄 수. E · Q가 같은 표를 쓴다.</summary>
    public sealed class SkillCurve
    {
        readonly float[] _multiplier;
        readonly int[] _cost;

        public SkillCurve(float[] multiplier, int[] costToNext)
        {
            if (multiplier == null || multiplier.Length == 0) throw new ArgumentException("needs at least one level", nameof(multiplier));
            if (costToNext == null || costToNext.Length < multiplier.Length - 1) throw new ArgumentException("one cost per level below the max", nameof(costToNext));
            _multiplier = (float[])multiplier.Clone();
            _cost = new int[multiplier.Length];
            for (int i = 0; i < multiplier.Length - 1; i++) _cost[i] = costToNext[i] > 0 ? costToNext[i] : 0;
        }

        public int MaxLevel => _multiplier.Length;
        public int ClampLevel(int level) => level < 1 ? 1 : level > MaxLevel ? MaxLevel : level;
        public float Multiplier(int level) => _multiplier[ClampLevel(level) - 1];
        /// <summary>level에서 다음 레벨까지 골드 (최대면 0).</summary>
        public int CostToNext(int level) => level >= 1 && level < MaxLevel ? _cost[level - 1] : 0;
    }

    /// <summary>
    /// 성장 규칙 전체 (3-5, GrowthTuning 에셋 → GameRoot가 넘긴다, BALANCE 17번): 캐릭터 레벨 · 돌파 · 무기 레벨 · 스킬 레벨, 경험치 재료 한 개의 경험치.
    /// 재련 · 장신구는 아직 없음 (저장 칸 refinement만 자리를 둔다).
    /// </summary>
    public sealed class GrowthRules
    {
        public GrowthRules(LevelCurve character, LevelCurve weapon, SkillCurve skill, int expPerItem)
        {
            Character = character ?? throw new ArgumentNullException(nameof(character));
            Weapon = weapon ?? throw new ArgumentNullException(nameof(weapon));
            Skill = skill ?? throw new ArgumentNullException(nameof(skill));
            ExpPerItem = expPerItem > 0 ? expPerItem : 1;
        }

        public LevelCurve Character { get; }
        public LevelCurve Weapon { get; }
        public SkillCurve Skill { get; }
        /// <summary>경험치 재료(모험 경험) 한 개 = 경험치 몇 (캐릭터 · 무기 같음).</summary>
        public int ExpPerItem { get; }
    }

    /// <summary>
    /// 한 캐릭터의 성장 배율 (3-5): 캐릭터 = 기본 체력 · 공격 · 방어, 무기 = 무기 기본 공격력, 스킬 · 궁극기 = 그 베기 배율.
    /// 장신구 · 재련이 생기면 여기에 더한다 (효과 조회 한 곳).
    /// </summary>
    public readonly struct MemberStats
    {
        public readonly float Character;
        public readonly float Weapon;
        public readonly float Skill;
        public readonly float Burst;

        public MemberStats(float character, float weapon, float skill, float burst)
        {
            Character = character;
            Weapon = weapon;
            Skill = skill;
            Burst = burst;
        }

        /// <summary>성장 없음 — 데이터 값 그대로 (2-1 ~ 3-4와 같은 숫자).</summary>
        public static readonly MemberStats Identity = new MemberStats(1f, 1f, 1f, 1f);

        /// <summary>최대 체력 = 기본 × 캐릭터 배율 (0.5 올림, 최소 1).</summary>
        public int ScaleHealth(int baseMax)
        {
            int v = (int)Math.Round(baseMax * Character, MidpointRounding.AwayFromZero);
            return v < 1 ? 1 : v;
        }

        public float ScaleDefense(float baseDefense) => baseDefense * Character;
    }
}
