using System;
using System.Collections.Generic;

namespace Moa.Core.Growth
{
    /// <summary>성장 동작 (3-5) — 화면 버튼 하나 = 하나. enum은 끝에만 추가.</summary>
    public enum GrowthAction
    {
        LevelUp = 0,
        Ascend = 1,
        SkillUp = 2,
        BurstUp = 3,
        WeaponUp = 4,
    }

    /// <summary>성장 동작이 막힌 까닭.</summary>
    public enum GrowthBlock
    {
        None,
        NoMember,
        /// <summary>더 오를 레벨 · 돌파가 없다.</summary>
        MaxLevel,
        /// <summary>레벨 상한 — 먼저 돌파.</summary>
        NeedAscension,
        /// <summary>돌파는 상한 레벨에서만.</summary>
        NotAtCap,
        NotEnoughExp,
        NotEnoughGold,
    }

    /// <summary>캐릭터 한 명의 성장 상태 (3-5).</summary>
    public sealed class MemberGrowth
    {
        internal MemberGrowth(string id) { Id = id; }

        public string Id { get; }
        public int Level { get; internal set; }
        /// <summary>지금 레벨에서 모은 경험치 (상한이면 0).</summary>
        public int Exp { get; internal set; }
        /// <summary>넘은 돌파 수 (0 = 아직).</summary>
        public int Ascension { get; internal set; }
        public int SkillLevel { get; internal set; }
        public int BurstLevel { get; internal set; }
        public int WeaponLevel { get; internal set; }
        public int WeaponExp { get; internal set; }
        /// <summary>무기 재련 (아직 없음, 늘 1).</summary>
        public int Refinement { get; internal set; }
    }

    /// <summary>
    /// 계정 성장 규칙 (3-5) 한 곳 — 재화 지갑 + 캐릭터마다 레벨 · 경험치 · 돌파 · 스킬(E) · 궁극기(Q) 레벨 · 무기 레벨.
    /// - 레벨업: 다음 레벨까지 모자란 경험치만큼 경험치 재료를 쓴다(올림, 남은 경험치는 이어짐). 돌파 상한 레벨에서는 막힘(NeedAscension).
    /// - 돌파: 상한 레벨에서 골드로, 상한이 다음 돌파(마지막이면 최대 레벨)로 오르고 능력치 배율에 보너스.
    /// - 스킬 · 궁극기 레벨: 골드로 한 단계씩, 배율은 SkillCurve.
    /// - 무기 레벨: 경험치 재료로, 다음 레벨까지 모자란 만큼(한 번에 여러 레벨이 오를 수 있음).
    /// 모자라면 아무것도 바꾸지 않고 막힌 까닭(<see cref="LastBlock"/>). 바뀌면 <see cref="Changed"/>(칸, 재화만이면 −1).
    /// 새 계정은 곡선의 StartLevel (지금 데이터 값이 그 레벨 = 배율 1, 2-1 ~ 3-4 숫자 그대로). 저장은 <see cref="ToSnapshot"/> · <see cref="Load"/>.
    /// </summary>
    public sealed class Progression
    {
        readonly MemberGrowth[] _members;
        readonly List<MemberSave> _others = new List<MemberSave>(); // 지금 파티에 없는 캐릭터의 저장 (지우지 않고 다시 쓴다)

        public Progression(GrowthRules rules, string[] memberIds)
        {
            Rules = rules ?? throw new ArgumentNullException(nameof(rules));
            if (memberIds == null) throw new ArgumentNullException(nameof(memberIds));
            _members = new MemberGrowth[memberIds.Length];
            var seen = new HashSet<string>();
            for (int i = 0; i < memberIds.Length; i++)
            {
                if (string.IsNullOrEmpty(memberIds[i])) throw new ArgumentException("member " + i + " has no id", nameof(memberIds));
                if (!seen.Add(memberIds[i])) throw new ArgumentException("member id " + memberIds[i] + " twice", nameof(memberIds));
                _members[i] = new MemberGrowth(memberIds[i]);
                ResetMember(_members[i]);
            }
            Wallet = new Wallet();
            Wallet.Changed += () => Changed?.Invoke(-1);
        }

        public GrowthRules Rules { get; }
        public Wallet Wallet { get; }
        public int Count => _members.Length;
        public MemberGrowth Member(int index) => _members[index];
        public GrowthBlock LastBlock { get; private set; }
        /// <summary>마지막으로 처치 보상을 받은 양 (디버그 · 확인용).</summary>
        public int LastRewardGold { get; private set; }
        public int LastRewardExp { get; private set; }

        /// <summary>바뀐 칸 (0부터, 재화만 바뀌었으면 −1).</summary>
        public event Action<int> Changed;

        public int IndexOf(string id)
        {
            for (int i = 0; i < _members.Length; i++) if (_members[i].Id == id) return i;
            return -1;
        }

        bool Has(int index) => index >= 0 && index < _members.Length;

        void ResetMember(MemberGrowth m)
        {
            m.Level = Rules.Character.StartLevel;
            m.Ascension = Rules.Character.StartPhase;
            m.Exp = 0;
            m.SkillLevel = 1;
            m.BurstLevel = 1;
            m.WeaponLevel = Rules.Weapon.StartLevel;
            m.WeaponExp = 0;
            m.Refinement = 1;
        }

        // ---- 능력치 ----

        /// <summary>칸의 성장 배율 (없는 칸이면 Identity).</summary>
        public MemberStats Stats(int index)
        {
            if (!Has(index)) return MemberStats.Identity;
            MemberGrowth m = _members[index];
            return new MemberStats(Rules.Character.Multiplier(m.Level, m.Ascension), Rules.Weapon.Multiplier(m.WeaponLevel, 0),
                Rules.Skill.Multiplier(m.SkillLevel), Rules.Skill.Multiplier(m.BurstLevel));
        }

        /// <summary>지금 레벨 상한 (돌파 단계의).</summary>
        public int LevelCap(int index) => Has(index) ? Rules.Character.Cap(_members[index].Ascension) : 0;
        public int WeaponCap => Rules.Weapon.Cap(0);

        // ---- 동작 ----

        /// <summary>동작을 할 수 있는지와 값 (막혀도 값은 알 수 있으면 채운다: 재화 · 개수).</summary>
        public GrowthBlock Check(int index, GrowthAction action, out Currency currency, out int cost)
        {
            currency = Currency.Gold;
            cost = 0;
            if (!Has(index)) return GrowthBlock.NoMember;
            MemberGrowth m = _members[index];
            switch (action)
            {
                case GrowthAction.LevelUp:
                {
                    currency = Currency.AdventureExp;
                    LevelCurve c = Rules.Character;
                    if (m.Level >= c.MaxLevel) return GrowthBlock.MaxLevel;
                    if (m.Level >= c.Cap(m.Ascension)) return GrowthBlock.NeedAscension;
                    cost = ItemsFor(c.ExpToNext(m.Level) - m.Exp);
                    return Wallet.Has(currency, cost) ? GrowthBlock.None : GrowthBlock.NotEnoughExp;
                }
                case GrowthAction.Ascend:
                {
                    LevelCurve c = Rules.Character;
                    if (m.Ascension >= c.AscensionCount) return GrowthBlock.MaxLevel;
                    cost = c.Ascension(m.Ascension).Cost;
                    if (m.Level < c.Cap(m.Ascension)) return GrowthBlock.NotAtCap;
                    return Wallet.Has(currency, cost) ? GrowthBlock.None : GrowthBlock.NotEnoughGold;
                }
                case GrowthAction.SkillUp:
                case GrowthAction.BurstUp:
                {
                    int level = action == GrowthAction.SkillUp ? m.SkillLevel : m.BurstLevel;
                    if (level >= Rules.Skill.MaxLevel) return GrowthBlock.MaxLevel;
                    cost = Rules.Skill.CostToNext(level);
                    return Wallet.Has(currency, cost) ? GrowthBlock.None : GrowthBlock.NotEnoughGold;
                }
                case GrowthAction.WeaponUp:
                {
                    currency = Currency.AdventureExp;
                    LevelCurve w = Rules.Weapon;
                    if (m.WeaponLevel >= w.Cap(0)) return GrowthBlock.MaxLevel;
                    cost = ItemsFor(w.ExpToNext(m.WeaponLevel) - m.WeaponExp);
                    return Wallet.Has(currency, cost) ? GrowthBlock.None : GrowthBlock.NotEnoughExp;
                }
            }
            return GrowthBlock.NoMember;
        }

        public GrowthBlock Check(int index, GrowthAction action) => Check(index, action, out _, out _);

        /// <summary>동작을 한다. 되면 true (재화를 쓰고 Changed), 안 되면 false · LastBlock.</summary>
        public bool Try(int index, GrowthAction action)
        {
            GrowthBlock b = Check(index, action, out Currency currency, out int cost);
            LastBlock = b;
            if (b != GrowthBlock.None) return false;
            MemberGrowth m = _members[index];
            if (!Wallet.TrySpend(currency, cost)) { LastBlock = currency == Currency.Gold ? GrowthBlock.NotEnoughGold : GrowthBlock.NotEnoughExp; return false; }
            switch (action)
            {
                case GrowthAction.LevelUp:
                {
                    int level = m.Level, exp = m.Exp;
                    Gain(Rules.Character, m.Ascension, ref level, ref exp, cost * Rules.ExpPerItem);
                    m.Level = level; m.Exp = exp;
                    break;
                }
                case GrowthAction.Ascend:
                    m.Ascension++;
                    break;
                case GrowthAction.SkillUp:
                    m.SkillLevel++;
                    break;
                case GrowthAction.BurstUp:
                    m.BurstLevel++;
                    break;
                case GrowthAction.WeaponUp:
                {
                    int level = m.WeaponLevel, exp = m.WeaponExp;
                    Gain(Rules.Weapon, 0, ref level, ref exp, cost * Rules.ExpPerItem);
                    m.WeaponLevel = level; m.WeaponExp = exp;
                    break;
                }
            }
            Changed?.Invoke(index);
            return true;
        }

        int ItemsFor(int exp) => exp <= 0 ? 0 : (exp + Rules.ExpPerItem - 1) / Rules.ExpPerItem;

        /// <summary>경험치를 더하고 상한까지 레벨을 올린다. 상한에 닿으면 남은 경험치는 버린다.</summary>
        static void Gain(LevelCurve c, int phase, ref int level, ref int exp, int amount)
        {
            int cap = c.Cap(phase);
            exp += amount;
            while (level < cap && exp >= c.ExpToNext(level))
            {
                exp -= c.ExpToNext(level);
                level++;
            }
            if (level >= cap) exp = 0;
        }

        /// <summary>적 처치 보상 (3-5) — 골드 · 경험치 재료를 지갑에.</summary>
        public void AddReward(int gold, int expItems)
        {
            LastRewardGold = gold > 0 ? gold : 0;
            LastRewardExp = expItems > 0 ? expItems : 0;
            if (gold > 0) Wallet.Add(Currency.Gold, gold);
            if (expItems > 0) Wallet.Add(Currency.AdventureExp, expItems);
        }

        // ---- 디버그 ----

        /// <summary>[상태 변경] 캐릭터 레벨을 정한다 (경험치 0, 돌파는 그 레벨을 가질 수 있는 가장 낮은 단계 — 상한 레벨이면 아직 돌파 전).</summary>
        public void SetLevel(int index, int level)
        {
            if (!Has(index)) return;
            MemberGrowth m = _members[index];
            m.Level = Rules.Character.ClampLevel(level);
            m.Ascension = Rules.Character.PhaseFor(m.Level);
            m.Exp = 0;
            Changed?.Invoke(index);
        }

        /// <summary>[상태 변경] 스킬(E) 또는 궁극기(Q) 레벨을 정한다.</summary>
        public void SetSkillLevel(int index, GrowthAction which, int level)
        {
            if (!Has(index)) return;
            int v = Rules.Skill.ClampLevel(level);
            if (which == GrowthAction.BurstUp) _members[index].BurstLevel = v;
            else _members[index].SkillLevel = v;
            Changed?.Invoke(index);
        }

        /// <summary>[상태 변경] 무기 레벨을 정한다 (경험치 0).</summary>
        public void SetWeaponLevel(int index, int level)
        {
            if (!Has(index)) return;
            int cap = Rules.Weapon.Cap(0);
            _members[index].WeaponLevel = level < 1 ? 1 : level > cap ? cap : level;
            _members[index].WeaponExp = 0;
            Changed?.Invoke(index);
        }

        /// <summary>[상태 변경] 모두 처음으로 (새 계정: 시작 레벨 · 재화 0, 다른 캐릭터 저장도 지움).</summary>
        public void ResetAll()
        {
            foreach (var m in _members) ResetMember(m);
            _others.Clear();
            Wallet.Clear(); // Changed(-1)
            for (int i = 0; i < _members.Length; i++) Changed?.Invoke(i);
        }

        // ---- 저장 ----

        public SaveSnapshot ToSnapshot()
        {
            var s = new SaveSnapshot { version = SaveFormat.Version };
            var wallet = new CurrencySave[Wallet.CurrencyCount];
            for (int i = 0; i < wallet.Length; i++) wallet[i] = new CurrencySave { id = ((Currency)i).ToString(), amount = Wallet.Get((Currency)i) };
            s.wallet = wallet;
            var list = new List<MemberSave>();
            foreach (var m in _members)
                list.Add(new MemberSave
                {
                    id = m.Id, level = m.Level, exp = m.Exp, ascension = m.Ascension, skillLevel = m.SkillLevel, burstLevel = m.BurstLevel,
                    weaponLevel = m.WeaponLevel, weaponExp = m.WeaponExp, refinement = m.Refinement,
                });
            foreach (var o in _others) list.Add(o.Clone());
            s.characters = list.ToArray();
            return s;
        }

        /// <summary>
        /// 스냅샷을 읽는다 (SaveFormat.Validate를 통과한 것). 범위를 벗어난 값은 고친다(고친 칸 수를 돌려준다) — 레벨 1 ~ 최대, 돌파는 레벨에 맞게,
        /// 경험치 0 ~ 다음 레벨 미만, 스킬 1 ~ 최대, 무기 1 ~ 상한, 재화 0 이상. 모르는 재화는 버리고, 지금 파티에 없는 캐릭터는 그대로 들고 있다가 다시 쓴다.
        /// 스냅샷에 없는 칸은 새 계정 값.
        /// </summary>
        public int Load(SaveSnapshot s)
        {
            if (!SaveFormat.Validate(s, out string reason)) throw new ArgumentException("invalid snapshot: " + reason, nameof(s));
            int fixedCount = 0;
            foreach (var m in _members) ResetMember(m);
            _others.Clear();
            var amounts = new int[Wallet.CurrencyCount];
            foreach (var c in s.wallet)
            {
                if (c == null || string.IsNullOrEmpty(c.id) || !Enum.TryParse(c.id, out Currency cur) || !Enum.IsDefined(typeof(Currency), cur)) { fixedCount++; continue; }
                int a = c.amount;
                if (a < 0) { a = 0; fixedCount++; }
                amounts[(int)cur] = a;
            }
            foreach (var save in s.characters)
            {
                int i = IndexOf(save.id);
                if (i < 0) { _others.Add(save.Clone()); continue; }
                fixedCount += Apply(_members[i], save);
            }
            for (int i = 0; i < amounts.Length; i++) Wallet.Set((Currency)i, amounts[i]); // Changed(-1)
            for (int i = 0; i < _members.Length; i++) Changed?.Invoke(i);
            return fixedCount;
        }

        int Apply(MemberGrowth m, MemberSave s)
        {
            int fixes = 0;
            LevelCurve c = Rules.Character;
            m.Level = Fix(s.level, 1, c.MaxLevel, ref fixes);
            m.Ascension = s.ascension;
            if (!c.IsValid(m.Level, m.Ascension))
            {
                // 그 레벨에 맞는 가장 가까운 단계로
                int lo = c.PhaseFor(m.Level), hi = lo;
                while (hi + 1 <= c.AscensionCount && c.IsValid(m.Level, hi + 1)) hi++;
                m.Ascension = m.Ascension < lo ? lo : m.Ascension > hi ? hi : m.Ascension;
                fixes++;
            }
            m.Exp = m.Level >= c.Cap(m.Ascension) ? FixZero(s.exp, ref fixes) : Fix(s.exp, 0, c.ExpToNext(m.Level) - 1, ref fixes);
            m.SkillLevel = Fix(s.skillLevel, 1, Rules.Skill.MaxLevel, ref fixes);
            m.BurstLevel = Fix(s.burstLevel, 1, Rules.Skill.MaxLevel, ref fixes);
            LevelCurve w = Rules.Weapon;
            m.WeaponLevel = Fix(s.weaponLevel, 1, w.Cap(0), ref fixes);
            m.WeaponExp = m.WeaponLevel >= w.Cap(0) ? FixZero(s.weaponExp, ref fixes) : Fix(s.weaponExp, 0, w.ExpToNext(m.WeaponLevel) - 1, ref fixes);
            m.Refinement = s.refinement < 1 ? 1 : s.refinement;
            if (s.refinement < 1) fixes++;
            return fixes;
        }

        static int Fix(int v, int min, int max, ref int fixes)
        {
            if (max < min) max = min;
            if (v < min) { fixes++; return min; }
            if (v > max) { fixes++; return max; }
            return v;
        }

        static int FixZero(int v, ref int fixes)
        {
            if (v != 0) fixes++;
            return 0;
        }
    }
}
