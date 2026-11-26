using System;
using System.Collections.Generic;
using Moa.Core.Growth;
using Moa.Core.Rng;

namespace Moa.Core.Gacha
{
    /// <summary>뽑는 것의 종류 (3-7). 에셋 · 저장에 정수로 남을 수 있으니 끝에만 추가.</summary>
    public enum GachaItemKind
    {
        Character = 0,
        Weapon = 1,
    }

    /// <summary>
    /// 배너 종류 (3-7) — 천장 카운트 · 픽업 보장은 종류마다 따로 (같은 종류 배너끼리는 이어진다). 저장은 이름으로. 끝에만 추가.
    /// </summary>
    public enum BannerKind
    {
        Standard = 0,
        LimitedCharacter = 1,
    }

    /// <summary>뽑기가 막힌 까닭 (끝에만 추가).</summary>
    public enum GachaBlock
    {
        None,
        NoBanner,
        BadCount,
        NotEnoughWishes,
    }

    /// <summary>뽑은 것이 보관함에 들어간 결과.</summary>
    public enum GachaOutcome
    {
        /// <summary>처음 얻음.</summary>
        New,
        /// <summary>캐릭터 중복 → 각성 +1.</summary>
        Awakened,
        /// <summary>무기 중복 → 재련 +1.</summary>
        Refined,
        /// <summary>각성 · 재련이 최대 → 재화로 바뀜.</summary>
        Converted,
    }

    /// <summary>뽑을 수 있는 것 하나 (캐릭터 · 무기). 이름 · 모습은 Unity 쪽 데이터가 id로 찾는다.</summary>
    public sealed class GachaItem
    {
        public GachaItem(string id, GachaItemKind kind, int stars)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("gacha item without id", nameof(id));
            if (stars < 3 || stars > 5) throw new ArgumentOutOfRangeException(nameof(stars), "stars must be 3, 4 or 5");
            Id = id;
            Kind = kind;
            Stars = stars;
        }

        public string Id { get; }
        public GachaItemKind Kind { get; }
        public int Stars { get; }
    }

    /// <summary>한 등급의 확률 곡선: 기본 확률 · 상승 시작(이 회차부터) · 회차당 상승 · 확정 회차. 회차 = 마지막으로 이 등급이 나온 뒤 몇 번째 뽑기인지 (1부터).</summary>
    public readonly struct RateCurve
    {
        public readonly double BaseRate;
        public readonly int SoftStart;
        public readonly double SoftStep;
        public readonly int Hard;

        public RateCurve(double baseRate, int softStart, double softStep, int hard)
        {
            if (baseRate < 0 || baseRate > 1) throw new ArgumentOutOfRangeException(nameof(baseRate));
            if (hard < 1) throw new ArgumentOutOfRangeException(nameof(hard), "hard pity must be at least 1");
            if (softStep < 0) throw new ArgumentOutOfRangeException(nameof(softStep));
            BaseRate = baseRate;
            SoftStart = softStart;
            SoftStep = softStep;
            Hard = hard;
        }

        /// <summary>n번째 뽑기(1부터)의 확률. 확정 회차부터 1, 상승 시작부터 기본 + 상승 × (n − 시작 + 1), 1에서 멈춤.</summary>
        public double At(int n)
        {
            if (n >= Hard) return 1.0;
            if (SoftStart > 0 && n >= SoftStart)
            {
                double p = BaseRate + SoftStep * (n - SoftStart + 1);
                return p > 1.0 ? 1.0 : p;
            }
            return BaseRate;
        }
    }

    /// <summary>
    /// 배너 하나의 규칙 (3-7, BannerData 에셋 → GameRoot가 넘긴다, BALANCE 19번): 종류 · 1회 비용 · ★5 · ★4 확률 곡선 · 풀(등급별) · 픽업 ★5와 그 확률(50/50).
    /// 픽업이 있으면 ★5가 나올 때 픽업 확률로 픽업, 아니면 픽업이 아닌 ★5 — 그다음 ★5는 반드시 픽업(보장). 픽업이 없으면 ★5 풀에서 고르게.
    /// ★4 · ★3은 등급 풀에서 고르게. 종합 확률(천장 포함 실제 비율)은 <see cref="ConsolidatedRates"/>.
    /// </summary>
    public sealed class BannerRules
    {
        readonly GachaItem[] _five, _offFive, _featured, _four, _three;
        double[] _consolidated;

        public BannerRules(string id, BannerKind kind, int cost, RateCurve five, RateCurve four, double featuredChance, GachaItem[] pool, GachaItem[] featured)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("banner without id", nameof(id));
            if (cost < 0) throw new ArgumentOutOfRangeException(nameof(cost));
            if (featuredChance < 0 || featuredChance > 1) throw new ArgumentOutOfRangeException(nameof(featuredChance));
            if (pool == null) throw new ArgumentNullException(nameof(pool));
            Id = id;
            Kind = kind;
            Cost = cost;
            Five = five;
            Four = four;
            FeaturedChance = featuredChance;
            var feat = new List<GachaItem>();
            if (featured != null)
                foreach (var f in featured)
                {
                    if (f == null) continue;
                    if (f.Stars != 5) throw new ArgumentException("banner " + id + ": featured item " + f.Id + " is not 5 stars", nameof(featured));
                    feat.Add(f);
                }
            var five5 = new List<GachaItem>();
            var off = new List<GachaItem>();
            var four4 = new List<GachaItem>();
            var three3 = new List<GachaItem>();
            var seen = new HashSet<string>();
            foreach (var f in feat) { seen.Add(f.Id); five5.Add(f); }
            foreach (var it in pool)
            {
                if (it == null) continue;
                if (!seen.Add(it.Id)) continue; // 픽업이 풀에도 있으면 한 번만
                if (it.Stars == 5) { five5.Add(it); off.Add(it); }
                else if (it.Stars == 4) four4.Add(it);
                else three3.Add(it);
            }
            if (five5.Count == 0 || four4.Count == 0 || three3.Count == 0)
                throw new ArgumentException("banner " + id + " needs at least one 5, 4 and 3 star item", nameof(pool));
            if (feat.Count > 0 && off.Count == 0)
                throw new ArgumentException("banner " + id + " has featured items but no other 5 star item to lose the 50/50 to", nameof(pool));
            _five = five5.ToArray();
            _offFive = off.ToArray();
            _featured = feat.ToArray();
            _four = four4.ToArray();
            _three = three3.ToArray();
        }

        public string Id { get; }
        public BannerKind Kind { get; }
        /// <summary>1회 비용 (소원석).</summary>
        public int Cost { get; }
        public RateCurve Five { get; }
        public RateCurve Four { get; }
        /// <summary>★5가 나올 때 픽업일 확률 (보장이 없을 때).</summary>
        public double FeaturedChance { get; }
        public bool HasFeatured => _featured.Length > 0;
        public IReadOnlyList<GachaItem> Featured => _featured;
        /// <summary>등급(3 · 4 · 5)의 풀 (★5는 픽업 포함).</summary>
        public IReadOnlyList<GachaItem> Pool(int stars) => stars == 5 ? _five : stars == 4 ? _four : _three;
        /// <summary>픽업이 아닌 ★5.</summary>
        public IReadOnlyList<GachaItem> OffFeatured => _offFive;

        /// <summary>
        /// 이 배너만 계속 뽑을 때 등급별 실제 비율 [★3, ★4, ★5] — 천장 · 보장을 넣은 "종합 확률" (확률 공개용).
        /// (★5 회차, ★4 회차) 상태 사슬의 분포를 burnIn 회 굴린 뒤 samples 회의 평균 (처음 한 번 계산해 둔다).
        /// </summary>
        public double[] ConsolidatedRates()
        {
            if (_consolidated != null) return (double[])_consolidated.Clone();
            const int burnIn = 2000, samples = 20000; // 계산 반복 수 (게임 수치 아님)
            int na = Five.Hard, nb = Four.Hard + 1;
            var cur = new double[na, nb];
            var next = new double[na, nb];
            cur[0, 0] = 1.0;
            double s3 = 0, s4 = 0, s5 = 0;
            for (int t = 0; t < burnIn + samples; t++)
            {
                Array.Clear(next, 0, next.Length);
                double p3 = 0, p4 = 0, p5 = 0;
                for (int a = 0; a < na; a++)
                    for (int b = 0; b < nb; b++)
                    {
                        double w = cur[a, b];
                        if (w == 0) continue;
                        GachaRoll.Rates(this, a, b, out double r5, out double r4);
                        double r3 = 1.0 - r5 - r4;
                        int b1 = b + 1 >= nb ? nb - 1 : b + 1;
                        next[0, b1] += w * r5;
                        if (a + 1 < na)
                        {
                            next[a + 1, 0] += w * r4;
                            next[a + 1, b1] += w * r3;
                        }
                        p5 += w * r5; p4 += w * r4; p3 += w * r3;
                    }
                var tmp = cur; cur = next; next = tmp;
                if (t >= burnIn) { s3 += p3; s4 += p4; s5 += p5; }
            }
            _consolidated = new[] { s3 / samples, s4 / samples, s5 / samples };
            return (double[])_consolidated.Clone();
        }

        /// <summary>
        /// 항목 하나의 종합 확률 (확률 공개용, 0 ~ 1): 등급 종합 확률 × 그 등급 안 몸.
        /// 픽업 ★5 = ★5 × 1 ÷ (2 − 픽업 확률) ÷ 픽업 수 (보장까지 넣은 픽업 비율 — 50%면 2/3), 그 밖의 ★ = 남은 몸을 고르게. 풀에 없으면 0.
        /// </summary>
        public double ConsolidatedItemRate(string id)
        {
            double[] r = ConsolidatedRates();
            foreach (var f in _featured)
                if (f.Id == id) return r[2] * FeaturedShare / _featured.Length;
            foreach (var o in _offFive)
                if (o.Id == id) return r[2] * (HasFeatured ? 1.0 - FeaturedShare : 1.0) / _offFive.Length;
            foreach (var o in _four) if (o.Id == id) return r[1] / _four.Length;
            foreach (var o in _three) if (o.Id == id) return r[0] / _three.Length;
            return 0;
        }

        /// <summary>오래 뽑을 때 ★5 가운데 픽업의 비율 (50/50 + 보장 — 1 ÷ (2 − 픽업 확률), 픽업이 없으면 0).</summary>
        public double FeaturedShare => HasFeatured ? 1.0 / (2.0 - FeaturedChance) : 0.0;
    }

    /// <summary>배너 종류 하나의 천장 상태 (3-7): 마지막 ★5 · ★4 이후 뽑은 수 · 픽업 보장 · 합계 (저장됨).</summary>
    public sealed class GachaPity
    {
        internal GachaPity(BannerKind kind) { Kind = kind; }

        public BannerKind Kind { get; }
        /// <summary>마지막 ★5 이후 뽑은 수 (다음 뽑기는 이것 + 1번째).</summary>
        public int SinceFive { get; internal set; }
        /// <summary>마지막 ★4 이상 이후 뽑은 수 (★5가 나와도 늘어난다 — ★4 보장은 다음으로 밀림).</summary>
        public int SinceFour { get; internal set; }
        /// <summary>다음 ★5는 픽업 확정 (50/50에서 졌음).</summary>
        public bool FeaturedGuaranteed { get; internal set; }
        public int Pulls { get; internal set; }
        public int FiveCount { get; internal set; }
        public int FourCount { get; internal set; }
    }

    /// <summary>뽑기 한 번의 결과.</summary>
    public readonly struct GachaPull
    {
        public readonly GachaItem Item;
        public readonly BannerKind Kind;
        /// <summary>이 종류의 몇 번째 뽑기에서 나왔는지 (★5면 천장 회차, 1부터).</summary>
        public readonly int PityCount;
        /// <summary>★5 픽업인지 · 50/50을 이겨서인지(보장이 아니라) · 50/50에서 졌는지.</summary>
        public readonly bool Featured;
        public readonly bool WonFiftyFifty;
        public readonly bool LostFiftyFifty;
        public readonly GachaOutcome Outcome;
        /// <summary>각성 (캐릭터, 0부터) · 재련 (무기, 1부터) — 이 뽑기 뒤.</summary>
        public readonly int Rank;
        public readonly Currency ConvertedTo;
        public readonly int ConvertedAmount;

        public GachaPull(GachaItem item, BannerKind kind, int pityCount, bool featured, bool won, bool lost, GachaOutcome outcome, int rank, Currency convertedTo, int convertedAmount)
        {
            Item = item;
            Kind = kind;
            PityCount = pityCount;
            Featured = featured;
            WonFiftyFifty = won;
            LostFiftyFifty = lost;
            Outcome = outcome;
            Rank = rank;
            ConvertedTo = convertedTo;
            ConvertedAmount = convertedAmount;
        }

        public int Stars => Item != null ? Item.Stars : 0;
    }

    /// <summary>
    /// 확률 굴리기 한 곳 (3-7) — 배너 규칙 + 천장 상태 + 시드 난수(RandomStream.Gacha). 한 번에 난수 하나로 등급을 정하고(★5 → ★4 → ★3 순),
    /// 픽업 배너의 ★5는 난수 하나로 50/50, 등급 안에서는 난수 하나로 고르게. 상태만 바꾸고 보관함 · 지갑은 건드리지 않는다.
    /// </summary>
    public static class GachaRoll
    {
        /// <summary>상태(마지막 ★5 이후 a번, ★4 이후 b번)에서 다음 뽑기의 ★5 · ★4 확률 (합은 1을 넘지 않는다 — ★5가 먼저).</summary>
        public static void Rates(BannerRules banner, int sinceFive, int sinceFour, out double five, out double four)
        {
            five = banner.Five.At(sinceFive + 1);
            four = banner.Four.At(sinceFour + 1);
            if (five + four > 1.0) four = 1.0 - five;
        }

        /// <summary>한 번 굴린다: (아이템, 픽업, 50/50 이김, 짐). 천장 상태를 고친다.</summary>
        public static GachaItem Roll(BannerRules banner, GachaPity pity, SeededRandom random, out bool featured, out bool won, out bool lost, out int pityCount)
        {
            if (banner == null) throw new ArgumentNullException(nameof(banner));
            if (pity == null) throw new ArgumentNullException(nameof(pity));
            if (random == null) throw new ArgumentNullException(nameof(random));
            featured = won = lost = false;
            Rates(banner, pity.SinceFive, pity.SinceFour, out double p5, out double p4);
            double r = random.NextDouble();
            pity.Pulls++;
            pity.SinceFive++;
            pity.SinceFour++;
            if (pity.SinceFour > banner.Four.Hard) pity.SinceFour = banner.Four.Hard;
            if (r < p5)
            {
                pityCount = pity.SinceFive;
                pity.SinceFive = 0;
                pity.FiveCount++;
                if (banner.HasFeatured)
                {
                    if (pity.FeaturedGuaranteed || random.NextDouble() < banner.FeaturedChance)
                    {
                        featured = true;
                        won = !pity.FeaturedGuaranteed;
                        pity.FeaturedGuaranteed = false;
                        return Pick(banner.Featured, random);
                    }
                    lost = true;
                    pity.FeaturedGuaranteed = true;
                    return Pick(banner.OffFeatured, random);
                }
                return Pick(banner.Pool(5), random);
            }
            if (r < p5 + p4)
            {
                pityCount = pity.SinceFour;
                pity.SinceFour = 0;
                pity.FourCount++;
                return Pick(banner.Pool(4), random);
            }
            pityCount = pity.SinceFour;
            return Pick(banner.Pool(3), random);
        }

        static GachaItem Pick(IReadOnlyList<GachaItem> list, SeededRandom random) => list.Count == 1 ? list[0] : list[random.Range(0, list.Count)];
    }

    /// <summary>보관함 규칙 수치 (3-7, GachaTuning 에셋, BALANCE 19번): 각성 · 재련 최대, 넘치면 등급마다 재화, 처음부터 가진 것, 여러 번 뽑기 수.</summary>
    public sealed class CollectionRules
    {
        readonly Currency[] _overflowCurrency = new Currency[6];
        readonly int[] _overflowAmount = new int[6];

        /// <param name="overflow">(등급, 재화, 양) — 없는 등급은 0.</param>
        public CollectionRules(int maxAwakening, int maxRefinement, int multiPull, (int stars, Currency currency, int amount)[] overflow, GachaItem[] starting)
        {
            if (maxAwakening < 0) throw new ArgumentOutOfRangeException(nameof(maxAwakening));
            if (maxRefinement < 1) throw new ArgumentOutOfRangeException(nameof(maxRefinement));
            if (multiPull < 1) throw new ArgumentOutOfRangeException(nameof(multiPull));
            MaxAwakening = maxAwakening;
            MaxRefinement = maxRefinement;
            MultiPull = multiPull;
            if (overflow != null)
                foreach (var o in overflow)
                    if (o.stars >= 3 && o.stars <= 5) { _overflowCurrency[o.stars] = o.currency; _overflowAmount[o.stars] = o.amount; }
            var list = new List<GachaItem>();
            if (starting != null) foreach (var s in starting) if (s != null) list.Add(s);
            Starting = list.ToArray();
        }

        public int MaxAwakening { get; }
        public int MaxRefinement { get; }
        /// <summary>"여러 번" 버튼의 뽑기 수 (10).</summary>
        public int MultiPull { get; }
        public IReadOnlyList<GachaItem> Starting { get; }

        public Currency OverflowCurrency(int stars) => stars >= 3 && stars <= 5 ? _overflowCurrency[stars] : Currency.Gold;
        public int OverflowAmount(int stars) => stars >= 3 && stars <= 5 ? _overflowAmount[stars] : 0;

        /// <summary>종류의 최대 장 수: 캐릭터 = 1 + 최대 각성, 무기 = 최대 재련.</summary>
        public int MaxCopies(GachaItemKind kind) => kind == GachaItemKind.Character ? 1 + MaxAwakening : MaxRefinement;
    }

    /// <summary>
    /// 가챠 계정 한 곳 (3-7) — 소원석(지갑의 Currency.Wish)을 쓰고 배너에서 뽑아 보관함에 넣는다. 천장 · 픽업 보장은 배너 종류마다 따로(<see cref="Pity"/>).
    /// 보관함: 처음 얻으면 New, 캐릭터 중복은 각성 +1 (최대에서 넘치면 재화), 무기 중복은 재련 +1 (최대에서 넘치면 재화). 각성 · 재련의 효과는 아직 없음 — 수만.
    /// 난수는 시드 난수 하나(RandomStream.Gacha) — 상태도 저장한다(다시 켜도 같은 결과가 되풀이되지 않게). 실제 결제 없음: 소원석은 게임 안에서만 얻는다.
    /// 저장은 <see cref="ToSave"/> · <see cref="Load"/> (SaveSnapshot.gacha). 바뀌면 <see cref="Changed"/>.
    /// </summary>
    public sealed class GachaAccount
    {
        readonly Wallet _wallet;
        readonly GachaPity[] _pity;
        readonly Dictionary<string, int> _copies = new Dictionary<string, int>();
        readonly Dictionary<string, GachaItemKind> _kinds = new Dictionary<string, GachaItemKind>();
        readonly List<string> _order = new List<string>();

        public GachaAccount(CollectionRules rules, Wallet wallet, SeededRandom random)
        {
            Rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            Random = random ?? throw new ArgumentNullException(nameof(random));
            var kinds = (BannerKind[])Enum.GetValues(typeof(BannerKind));
            int max = 0;
            foreach (var k in kinds) if ((int)k > max) max = (int)k;
            _pity = new GachaPity[max + 1];
            for (int i = 0; i < _pity.Length; i++) _pity[i] = new GachaPity((BannerKind)i);
            GrantStarting();
        }

        public CollectionRules Rules { get; }
        public Wallet Wallet => _wallet;
        /// <summary>가챠 난수 (상태는 저장된다).</summary>
        public SeededRandom Random { get; private set; }
        public GachaBlock LastBlock { get; private set; }
        public int PullCount { get; private set; }

        /// <summary>무엇이든 바뀌면 (뽑기 · 읽기 · 디버그).</summary>
        public event Action Changed;
        /// <summary>뽑은 배너와 결과들 (한 번 누름에 한 번).</summary>
        public event Action<BannerRules, IReadOnlyList<GachaPull>> Pulled;

        public GachaPity Pity(BannerKind kind) => _pity[(int)kind];

        // ---- 보관함 ----

        public IReadOnlyList<string> Owned => _order;
        public bool Owns(string id) => id != null && _copies.ContainsKey(id);
        public int Copies(string id) => id != null && _copies.TryGetValue(id, out int n) ? n : 0;
        /// <summary>캐릭터 각성 (0 ~ 최대, 없으면 −1).</summary>
        public int Awakening(string id) => Owns(id) ? Copies(id) - 1 : -1;
        /// <summary>무기 재련 (1 ~ 최대, 없으면 0).</summary>
        public int Refinement(string id) => Copies(id);
        public GachaItemKind KindOf(string id) => id != null && _kinds.TryGetValue(id, out var k) ? k : GachaItemKind.Character;

        void GrantStarting()
        {
            foreach (var s in Rules.Starting)
                if (!_copies.ContainsKey(s.Id)) { _copies[s.Id] = 1; _kinds[s.Id] = s.Kind; _order.Add(s.Id); }
        }

        GachaOutcome Add(GachaItem item, out int rank, out Currency currency, out int amount)
        {
            currency = Rules.OverflowCurrency(item.Stars);
            amount = 0;
            int max = Rules.MaxCopies(item.Kind);
            GachaOutcome outcome;
            if (!_copies.TryGetValue(item.Id, out int n))
            {
                n = 1;
                _kinds[item.Id] = item.Kind;
                _order.Add(item.Id);
                outcome = GachaOutcome.New;
            }
            else if (n < max)
            {
                n++;
                outcome = item.Kind == GachaItemKind.Character ? GachaOutcome.Awakened : GachaOutcome.Refined;
            }
            else
            {
                amount = Rules.OverflowAmount(item.Stars);
                if (amount > 0) _wallet.Add(currency, amount);
                outcome = GachaOutcome.Converted;
            }
            _copies[item.Id] = n;
            rank = item.Kind == GachaItemKind.Character ? n - 1 : n;
            return outcome;
        }

        // ---- 뽑기 ----

        public GachaBlock Check(BannerRules banner, int count, out int cost)
        {
            cost = 0;
            if (banner == null) return GachaBlock.NoBanner;
            if (count < 1) return GachaBlock.BadCount;
            long c = (long)banner.Cost * count;
            cost = c > int.MaxValue ? int.MaxValue : (int)c;
            return _wallet.Has(Currency.Wish, cost) ? GachaBlock.None : GachaBlock.NotEnoughWishes;
        }

        /// <summary>count번 뽑는다 (소원석 비용 × count). 되면 결과 목록, 안 되면 null · LastBlock (아무것도 안 바뀜).</summary>
        public IReadOnlyList<GachaPull> TryPull(BannerRules banner, int count)
        {
            LastBlock = Check(banner, count, out int cost);
            if (LastBlock != GachaBlock.None) return null;
            if (!_wallet.TrySpend(Currency.Wish, cost)) { LastBlock = GachaBlock.NotEnoughWishes; return null; }
            var results = new List<GachaPull>(count);
            GachaPity pity = Pity(banner.Kind);
            for (int i = 0; i < count; i++)
            {
                GachaItem item = GachaRoll.Roll(banner, pity, Random, out bool featured, out bool won, out bool lost, out int pityCount);
                GachaOutcome outcome = Add(item, out int rank, out Currency cur, out int amount);
                results.Add(new GachaPull(item, banner.Kind, pityCount, featured, won, lost, outcome, rank, cur, amount));
            }
            PullCount += count;
            Pulled?.Invoke(banner, results);
            Changed?.Invoke();
            return results;
        }

        // ---- 디버그 ----

        /// <summary>[상태 변경] 천장 카운트 · 보장을 정한다 (★5는 0 ~ 확정 − 1, ★4는 0 ~ 확정 이하로 자름 — 잘라 낸 값은 hardFive · hardFour).</summary>
        public void SetPity(BannerKind kind, int sinceFive, int sinceFour, bool guaranteed, int hardFive, int hardFour)
        {
            GachaPity p = Pity(kind);
            p.SinceFive = Clamp(sinceFive, 0, Math.Max(0, hardFive - 1));
            p.SinceFour = Clamp(sinceFour, 0, Math.Max(0, hardFour));
            p.FeaturedGuaranteed = guaranteed;
            Changed?.Invoke();
        }

        /// <summary>[상태 변경] 천장 · 보장 · 보관함을 처음으로 (처음부터 가진 것만, 난수 상태는 그대로 · 소원석은 그대로).</summary>
        public void ResetAll()
        {
            for (int i = 0; i < _pity.Length; i++) _pity[i] = new GachaPity((BannerKind)i);
            _copies.Clear();
            _kinds.Clear();
            _order.Clear();
            GrantStarting();
            Changed?.Invoke();
        }

        static int Clamp(int v, int lo, int hi) => v < lo ? lo : v > hi ? hi : v;

        // ---- 저장 ----

        public GachaSave ToSave()
        {
            var pity = new List<PitySave>();
            foreach (var p in _pity)
                pity.Add(new PitySave
                {
                    kind = p.Kind.ToString(), sinceFive = p.SinceFive, sinceFour = p.SinceFour, guaranteed = p.FeaturedGuaranteed,
                    pulls = p.Pulls, fives = p.FiveCount, fours = p.FourCount,
                });
            var owned = new List<OwnedSave>();
            foreach (var id in _order) owned.Add(new OwnedSave { id = id, kind = _kinds[id].ToString(), copies = _copies[id] });
            return new GachaSave { rng = Random.State.ToString(System.Globalization.CultureInfo.InvariantCulture), pity = pity.ToArray(), owned = owned.ToArray() };
        }

        /// <summary>
        /// 저장을 읽는다 (null = 처음 상태, 난수는 그대로). 고친 값 수를 돌려준다: 모르는 배너 종류 · 아이템 종류 · 빈 id · 같은 id 두 번 · 음수 · 최대를 넘는 장 수 · 읽을 수 없는 난수.
        /// 천장 회차는 ★5 0 ~ hardFive − 1, ★4 0 ~ hardFour로 자른다 (배너 규칙이 바뀌어도 바로 확정이 되도록). 처음부터 가진 것은 늘 있다.
        /// </summary>
        public int Load(GachaSave save, int hardFive, int hardFour)
        {
            int fixes = 0;
            for (int i = 0; i < _pity.Length; i++) _pity[i] = new GachaPity((BannerKind)i);
            _copies.Clear();
            _kinds.Clear();
            _order.Clear();
            if (save != null)
            {
                if (!string.IsNullOrEmpty(save.rng))
                {
                    if (ulong.TryParse(save.rng, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out ulong state)) Random = SeededRandom.FromState(state);
                    else fixes++;
                }
                if (save.pity != null)
                    foreach (var ps in save.pity)
                    {
                        if (ps == null || !Enum.TryParse(ps.kind, out BannerKind kind) || !Enum.IsDefined(typeof(BannerKind), kind)) { fixes++; continue; }
                        GachaPity p = _pity[(int)kind];
                        p.SinceFive = Fix(ps.sinceFive, 0, Math.Max(0, hardFive - 1), ref fixes);
                        p.SinceFour = Fix(ps.sinceFour, 0, Math.Max(0, hardFour), ref fixes);
                        p.FeaturedGuaranteed = ps.guaranteed;
                        p.Pulls = Fix(ps.pulls, 0, int.MaxValue, ref fixes);
                        p.FiveCount = Fix(ps.fives, 0, int.MaxValue, ref fixes);
                        p.FourCount = Fix(ps.fours, 0, int.MaxValue, ref fixes);
                    }
                if (save.owned != null)
                    foreach (var o in save.owned)
                    {
                        if (o == null || string.IsNullOrEmpty(o.id) || _copies.ContainsKey(o.id) || !Enum.TryParse(o.kind, out GachaItemKind k) || !Enum.IsDefined(typeof(GachaItemKind), k)) { fixes++; continue; }
                        _copies[o.id] = Fix(o.copies, 1, Rules.MaxCopies(k), ref fixes);
                        _kinds[o.id] = k;
                        _order.Add(o.id);
                    }
            }
            GrantStarting();
            Changed?.Invoke();
            return fixes;
        }

        static int Fix(int v, int lo, int hi, ref int fixes)
        {
            if (v < lo) { fixes++; return lo; }
            if (v > hi) { fixes++; return hi; }
            return v;
        }
    }
}
