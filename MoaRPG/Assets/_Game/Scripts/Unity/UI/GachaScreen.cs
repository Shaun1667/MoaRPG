using System.Collections.Generic;
using System.Text;
using Moa.Core.Gacha;
using Moa.Core.Growth;
using Moa.Unity.Data;
using Moa.Unity.Player;
using UnityEngine;
using UnityEngine.UI;
using Text = Moa.Core.Localization.Text;
using UiText = UnityEngine.UI.Text;

namespace Moa.Unity.UI
{
    /// <summary>
    /// 가챠(소원) 화면 (3-7, 간단판) — 키 Gacha(G)로 열고 닫는다(열려 있으면 ESC도 닫음, 다른 메뉴가 열려 있으면 안 열림). 열려 있는 동안 커서가 풀리고 게임 입력은 0 (IPlayerInput.MenuOpen).
    /// 위: 제목 · 소원석. 배너 탭(GachaTuning 배너 표) · 배너 이름 · 픽업 · 지금 천장 카운트 · 보장, [1회] [10회] [확률 정보], 결과 카드(★ 색 · 이름 · 새로 / 각성 / 재련 / 재화로).
    /// [확률 정보]: 등급별 기본 · 종합 확률, 항목 하나당 확률, 천장 · 보장 규칙, 지금 천장 카운트 · 픽업 보장 상태 (국내 확률 공개). 실제 결제 없음.
    /// 규칙은 Core GachaAccount.TryPull. 글은 텍스트 표(ui.gacha.*), 크기 · 색은 Setup 표. GameRoot가 Bind로 연결한다.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class GachaScreen : MonoBehaviour
    {
        [SerializeField] GameObject panel;
        [SerializeField] UiText title;
        [SerializeField] UiText wallet;
        [SerializeField] UiText hint;
        [SerializeField] Button[] tabs = new Button[0];
        [SerializeField] UiText[] tabLabels = new UiText[0];
        [SerializeField] UiText bannerLine;
        [SerializeField] UiText pityLine;
        [SerializeField] UiText statusLine;
        [SerializeField] Button pullOne;
        [SerializeField] UiText pullOneLabel;
        [SerializeField] Button pullMulti;
        [SerializeField] UiText pullMultiLabel;
        [SerializeField] Button infoButton;
        [SerializeField] UiText infoButtonLabel;
        [SerializeField] GameObject resultsRoot;
        [SerializeField] GameObject[] cards = new GameObject[0];
        [SerializeField] Image[] cardBands = new Image[0];
        [SerializeField] UiText[] cardStars = new UiText[0];
        [SerializeField] UiText[] cardNames = new UiText[0];
        [SerializeField] UiText[] cardNotes = new UiText[0];
        [SerializeField] GameObject infoRoot;
        [SerializeField] UiText infoText;
        [Tooltip("Rarity colors: index 0 = 3 stars, 1 = 4 stars, 2 = 5 stars")]
        [SerializeField] Color[] rarityColors = new Color[0];

        GachaAccount _account;
        GachaTuning _tuning;
        BannerRules[] _banners = new BannerRules[0];
        IPlayerInput _input;
        int _selected;
        bool _dirty = true;
        readonly List<GachaPull> _shown = new List<GachaPull>();

        public bool Open => panel != null && panel.activeSelf;
        public bool InfoOpen => infoRoot != null && infoRoot.activeSelf;
        public int Selected => _selected;
        public int CardCount => cards != null ? cards.Length : 0;
        /// <summary>디버그 · 확인용: 누른 수 · 마지막 막힘 · 보이는 결과 · 보이는 글.</summary>
        public int PressCount { get; private set; }
        public GachaBlock LastBlock { get; private set; }
        public IReadOnlyList<GachaPull> Shown => _shown;
        public string WalletShown => wallet != null ? wallet.text : null;
        public string PityShown => pityLine != null ? pityLine.text : null;
        public string InfoShown => infoText != null ? infoText.text : null;
        public string CardNameShown(int i) => i >= 0 && i < cardNames.Length && cardNames[i] != null ? cardNames[i].text : null;
        public string CardNoteShown(int i) => i >= 0 && i < cardNotes.Length && cardNotes[i] != null ? cardNotes[i].text : null;
        public Color CardColor(int i) => i >= 0 && i < cardBands.Length && cardBands[i] != null ? cardBands[i].color : Color.clear;
        public bool PullEnabled(bool multi) { var b = multi ? pullMulti : pullOne; return b != null && b.interactable; }

        public void Bind(GachaAccount account, GachaTuning tuning, BannerRules[] banners, IPlayerInput input, Font font)
        {
            if (_account != null) _account.Changed -= MarkDirty;
            _account = account;
            _tuning = tuning;
            _banners = banners ?? new BannerRules[0];
            _input = input;
            _selected = 0;
            _shown.Clear();
            if (_account != null) _account.Changed += MarkDirty;
            if (font != null)
                foreach (var t in GetComponentsInChildren<UiText>(true)) t.font = font;
            for (int k = 0; k < tabs.Length; k++)
            {
                if (tabs[k] == null) continue;
                int i = k;
                tabs[k].onClick.RemoveAllListeners();
                tabs[k].onClick.AddListener(() => SelectBanner(i));
            }
            if (pullOne != null) { pullOne.onClick.RemoveAllListeners(); pullOne.onClick.AddListener(() => Pull(false)); }
            if (pullMulti != null) { pullMulti.onClick.RemoveAllListeners(); pullMulti.onClick.AddListener(() => Pull(true)); }
            if (infoButton != null) { infoButton.onClick.RemoveAllListeners(); infoButton.onClick.AddListener(() => SetInfoOpen(!InfoOpen)); }
            Text.LanguageChanged -= MarkDirty;
            Text.LanguageChanged += MarkDirty;
            SetOpen(false);
        }

        void OnDestroy()
        {
            if (_account != null) _account.Changed -= MarkDirty;
            Text.LanguageChanged -= MarkDirty;
        }

        void MarkDirty() => _dirty = true;

        void Update()
        {
            // 다른 메뉴(캐릭터 · 지도)가 열려 있으면 열지 않는다 — 열린 화면만 닫힌다
            if (_input != null && _input.ConsumeGacha() && (Open || !_input.MenuOpen)) SetOpen(!Open);
        }

        void LateUpdate()
        {
            if (Open && _dirty) Refresh();
        }

        /// <summary>[상태 변경] 화면을 열거나 닫는다 (열면 커서를 풀고 게임 입력을 막는다). 가챠가 없으면 안 열린다.</summary>
        public void SetOpen(bool open)
        {
            bool was = Open;
            if (panel != null) panel.SetActive(open && _account != null && _banners.Length > 0);
            if (_input != null && was != Open) _input.MenuOpen = Open;
            if (!Open) SetInfoOpen(false);
            _dirty = true;
            if (Open) Refresh();
        }

        /// <summary>[보기] 확률 정보를 열거나 닫는다 (결과 자리에 겹친다).</summary>
        public void SetInfoOpen(bool open)
        {
            if (infoRoot != null) infoRoot.SetActive(open);
            if (resultsRoot != null) resultsRoot.SetActive(!open);
            _dirty = true;
        }

        /// <summary>[보기] 배너 고르기 (탭 · 디버그 공통).</summary>
        public void SelectBanner(int i)
        {
            if (i < 0 || i >= _banners.Length) return;
            if (_selected != i) _shown.Clear();
            _selected = i;
            _dirty = true;
        }

        /// <summary>[상태 변경] 지금 배너에서 1회 / 여러 번 뽑는다 (버튼 · 디버그 공통). 되면 true (결과 카드), 안 되면 LastBlock.</summary>
        public bool Pull(bool multi)
        {
            PressCount++;
            BannerRules b = CurrentBanner;
            if (_account == null || b == null) { LastBlock = GachaBlock.NoBanner; return false; }
            int count = multi ? _account.Rules.MultiPull : 1;
            IReadOnlyList<GachaPull> r = _account.TryPull(b, count);
            LastBlock = _account.LastBlock;
            if (r != null)
            {
                _shown.Clear();
                _shown.AddRange(r);
                SetInfoOpen(false);
            }
            _dirty = true;
            if (Open) Refresh();
            return r != null;
        }

        public BannerRules CurrentBanner => _selected >= 0 && _selected < _banners.Length ? _banners[_selected] : null;
        BannerData CurrentData => _tuning != null ? _tuning.GetBanner(_selected) : null;

        public void Refresh()
        {
            _dirty = false;
            if (_account == null) return;
            BannerRules b = CurrentBanner;
            BannerData d = CurrentData;
            if (title != null) title.text = Text.Get("ui.gacha.title");
            if (hint != null) hint.text = Text.Get("ui.gacha.close");
            if (wallet != null) wallet.text = Text.Get("ui.gacha.wallet", _account.Wallet.Get(Currency.Wish));
            for (int i = 0; i < tabs.Length; i++)
            {
                if (tabs[i] == null) continue;
                BannerData bd = _tuning != null ? _tuning.GetBanner(i) : null;
                tabs[i].gameObject.SetActive(i < _banners.Length);
                tabs[i].interactable = i != _selected;
                if (i < tabLabels.Length && tabLabels[i] != null) tabLabels[i].text = bd != null ? Text.Get(bd.NameKey) : "";
            }
            if (b == null) return;
            GachaPity p = _account.Pity(b.Kind);
            if (bannerLine != null)
                bannerLine.text = b.HasFeatured ? Text.Get("ui.gacha.featured", Names(b.Featured)) : Text.Get("ui.gacha.standard_desc");
            if (pityLine != null)
                pityLine.text = Text.Get("ui.gacha.pity", p.SinceFive, b.Five.Hard, p.SinceFour, b.Four.Hard)
                    + (b.HasFeatured ? "  " + Text.Get(p.FeaturedGuaranteed ? "ui.gacha.guaranteed" : "ui.gacha.fifty") : "");
            if (statusLine != null)
                statusLine.text = LastBlock == GachaBlock.NotEnoughWishes ? Text.Get("ui.gacha.not_enough") : Text.Get("ui.gacha.no_payment");
            int multi = _account.Rules.MultiPull;
            if (pullOne != null) pullOne.interactable = _account.Check(b, 1, out _) == GachaBlock.None;
            if (pullMulti != null) pullMulti.interactable = _account.Check(b, multi, out _) == GachaBlock.None;
            if (pullOneLabel != null) pullOneLabel.text = Text.Get("ui.gacha.pull_one", b.Cost);
            if (pullMultiLabel != null) pullMultiLabel.text = Text.Get("ui.gacha.pull_multi", multi, b.Cost * multi);
            if (infoButtonLabel != null) infoButtonLabel.text = Text.Get(InfoOpen ? "ui.gacha.info_close" : "ui.gacha.info_button");
            for (int i = 0; i < cards.Length; i++)
            {
                if (cards[i] == null) continue;
                bool on = i < _shown.Count;
                cards[i].SetActive(on);
                if (!on) continue;
                GachaPull r = _shown[i];
                Color c = RarityColor(r.Stars);
                if (i < cardBands.Length && cardBands[i] != null) cardBands[i].color = c;
                if (i < cardStars.Length && cardStars[i] != null) { cardStars[i].text = Text.Get("ui.gacha.rarity", r.Stars); cardStars[i].color = c; }
                if (i < cardNames.Length && cardNames[i] != null) cardNames[i].text = ItemName(r.Item.Id);
                if (i < cardNotes.Length && cardNotes[i] != null) cardNotes[i].text = Note(r);
            }
            if (InfoOpen && infoText != null) infoText.text = InfoText(b, d, p);
        }

        public Color RarityColor(int stars)
        {
            int i = stars - 3;
            return rarityColors != null && i >= 0 && i < rarityColors.Length ? rarityColors[i] : Color.white;
        }

        string ItemName(string id)
        {
            GachaItemData it = _tuning != null ? _tuning.FindItem(id) : null;
            return it != null && !string.IsNullOrEmpty(it.NameKey) ? Text.Get(it.NameKey) : id;
        }

        string Names(IReadOnlyList<GachaItem> items)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < items.Count; i++)
            {
                if (i > 0) sb.Append(Text.Get("ui.gacha.separator"));
                sb.Append(ItemName(items[i].Id));
            }
            return sb.ToString();
        }

        static string CurrencyName(Currency c)
        {
            switch (c)
            {
                case Currency.Wish: return Text.Get("currency.wish");
                case Currency.AdventureExp: return Text.Get("currency.adventure_exp");
                default: return Text.Get("currency.gold");
            }
        }

        static string Note(GachaPull r)
        {
            string s;
            switch (r.Outcome)
            {
                case GachaOutcome.New: s = Text.Get("ui.gacha.new"); break;
                case GachaOutcome.Awakened: s = Text.Get("ui.gacha.awakened", r.Rank); break;
                case GachaOutcome.Refined: s = Text.Get("ui.gacha.refined", r.Rank); break;
                default: s = r.ConvertedAmount > 0 ? Text.Get("ui.gacha.converted", CurrencyName(r.ConvertedTo), r.ConvertedAmount) : Text.Get("ui.gacha.converted_none"); break;
            }
            if (r.Featured) s = Text.Get("ui.gacha.featured_tag") + "  " + s;
            return s;
        }

        /// <summary>확률 정보 글 (확률 공개) — 줄마다 텍스트 키 하나.</summary>
        public string InfoText(BannerRules b, BannerData d, GachaPity p)
        {
            var sb = new StringBuilder();
            double[] c = b.ConsolidatedRates();
            double base5 = b.Five.BaseRate, base4 = b.Four.BaseRate, base3 = 1.0 - base5 - base4;
            sb.AppendLine(Text.Get("ui.gacha.info.title", d != null ? Text.Get(d.NameKey) : b.Id));
            sb.AppendLine(Text.Get("ui.gacha.info.no_payment"));
            sb.AppendLine(Text.Get("ui.gacha.info.rate", 5, base5 * 100.0, c[2] * 100.0));
            sb.AppendLine(Text.Get("ui.gacha.info.rate", 4, base4 * 100.0, c[1] * 100.0));
            sb.AppendLine(Text.Get("ui.gacha.info.rate", 3, base3 * 100.0, c[0] * 100.0));
            sb.AppendLine(Text.Get("ui.gacha.info.pity5", b.Five.SoftStart, b.Five.SoftStep * 100.0, b.Five.Hard));
            sb.AppendLine(Text.Get("ui.gacha.info.pity4", b.Four.SoftStart, b.Four.SoftStep * 100.0, b.Four.Hard));
            if (b.HasFeatured)
                sb.AppendLine(Text.Get("ui.gacha.info.featured", b.FeaturedChance * 100.0, Names(b.Featured)));
            else
                sb.AppendLine(Text.Get("ui.gacha.info.no_featured"));
            sb.AppendLine(Text.Get("ui.gacha.info.shared"));
            sb.AppendLine(Text.Get("ui.gacha.info.count", p.SinceFive, p.SinceFour));
            if (b.HasFeatured) sb.AppendLine(Text.Get(p.FeaturedGuaranteed ? "ui.gacha.info.guaranteed" : "ui.gacha.info.not_guaranteed"));
            if (b.HasFeatured) AppendPool(sb, b, 5, b.Featured, true);
            AppendPool(sb, b, 5, b.OffFeatured, false);
            AppendPool(sb, b, 4, b.Pool(4), false);
            AppendPool(sb, b, 3, b.Pool(3), false);
            return sb.ToString().TrimEnd();
        }

        void AppendPool(StringBuilder sb, BannerRules b, int stars, IReadOnlyList<GachaItem> items, bool featured)
        {
            if (items.Count == 0) return;
            double each = b.ConsolidatedItemRate(items[0].Id) * 100.0;
            sb.AppendLine(Text.Get(featured ? "ui.gacha.info.pool_featured" : "ui.gacha.info.pool", stars, Names(items), each));
        }

        // ---- 만들기 (Setup 메뉴가 부른다) ----

        /// <summary>
        /// HUD 캔버스 아래에 가챠 화면을 만든다 (자기 Canvas(정렬 덮어쓰기) + GraphicRaycaster). 처음엔 닫혀 있다.
        /// tabCount = 배너 탭 수, cardCount = 결과 카드 수 (cardColumns 열), row = (줄 높이, 줄 사이) px.
        /// </summary>
        public static GachaScreen CreateIn(Transform canvas, int sortingOrder, int tabCount, int cardCount, int cardColumns, Vector2 windowSize, float pad, Vector2 row,
            Vector2 tabSize, Vector2 buttonSize, Vector2 cardSize, float cardGap, float bandHeight, int titleFontSize, int bodyFontSize, int infoFontSize,
            Color dim, Color window, Color card, Color button, Color buttonDisabled, Color text, Color outline, Color[] rarityColors)
        {
            var go = new GameObject("GachaScreen", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(canvas, false);
            GrowthScreen.Stretch(rt);
            var c = go.AddComponent<Canvas>();
            c.overrideSorting = true;
            c.sortingOrder = sortingOrder;
            go.AddComponent<GraphicRaycaster>();
            var screen = go.AddComponent<GachaScreen>();
            screen.rarityColors = rarityColors != null ? (Color[])rarityColors.Clone() : new Color[0];

            var panelGo = new GameObject("Panel", typeof(RectTransform));
            var prt = (RectTransform)panelGo.transform;
            prt.SetParent(rt, false);
            GrowthScreen.Stretch(prt);
            panelGo.AddComponent<Image>().color = dim;
            screen.panel = panelGo;

            var win = GrowthScreen.Rect(prt, "Window", new Vector2(0.5f, 0.5f), Vector2.zero, windowSize);
            win.gameObject.AddComponent<Image>().color = window;
            var tl = new Vector2(0f, 1f);
            float inner = windowSize.x - pad * 2f;
            float y = -pad;
            screen.title = GrowthScreen.Label(win, "Title", titleFontSize, text, outline, TextAnchor.MiddleLeft, tl, new Vector2(pad, y), new Vector2(inner * 0.5f, row.x));
            screen.wallet = GrowthScreen.Label(win, "Wallet", bodyFontSize, text, outline, TextAnchor.MiddleRight, new Vector2(1f, 1f), new Vector2(-pad, y), new Vector2(inner * 0.45f, row.x));
            y -= row.x + row.y;

            var tabs = new Button[tabCount];
            var tabLabels = new UiText[tabCount];
            for (int i = 0; i < tabCount; i++)
                tabs[i] = GrowthScreen.MakeButton(win, "Banner" + (i + 1), new Vector2(pad + i * (tabSize.x + row.y), y), tabSize, button, buttonDisabled, bodyFontSize, text, outline, out tabLabels[i]);
            screen.tabs = tabs;
            screen.tabLabels = tabLabels;
            y -= tabSize.y + row.y;
            screen.bannerLine = GrowthScreen.Label(win, "BannerLine", bodyFontSize, text, outline, TextAnchor.MiddleLeft, tl, new Vector2(pad, y), new Vector2(inner, row.x));
            y -= row.x;
            screen.pityLine = GrowthScreen.Label(win, "PityLine", bodyFontSize, text, outline, TextAnchor.MiddleLeft, tl, new Vector2(pad, y), new Vector2(inner, row.x));
            y -= row.x + row.y;
            float bx = pad;
            screen.pullOne = GrowthScreen.MakeButton(win, "PullOne", new Vector2(bx, y), buttonSize, button, buttonDisabled, bodyFontSize, text, outline, out screen.pullOneLabel);
            bx += buttonSize.x + row.y;
            screen.pullMulti = GrowthScreen.MakeButton(win, "PullMulti", new Vector2(bx, y), buttonSize, button, buttonDisabled, bodyFontSize, text, outline, out screen.pullMultiLabel);
            bx += buttonSize.x + row.y;
            screen.infoButton = GrowthScreen.MakeButton(win, "Info", new Vector2(bx, y), buttonSize, button, buttonDisabled, bodyFontSize, text, outline, out screen.infoButtonLabel);
            y -= buttonSize.y + row.y;

            float bodyHeight = windowSize.y + y - pad - row.x - row.y;
            var results = GrowthScreen.Rect(win, "Results", tl, new Vector2(pad, y), new Vector2(inner, bodyHeight));
            screen.resultsRoot = results.gameObject;
            var cards = new GameObject[cardCount];
            var bands = new Image[cardCount];
            UiText[] stars = new UiText[cardCount], names = new UiText[cardCount], notes = new UiText[cardCount];
            int cols = Mathf.Max(1, cardColumns);
            for (int i = 0; i < cardCount; i++)
            {
                int cx = i % cols, cy = i / cols;
                var cr = GrowthScreen.Rect(results, "Card" + (i + 1), tl, new Vector2(cx * (cardSize.x + cardGap), -cy * (cardSize.y + cardGap)), cardSize);
                cr.gameObject.AddComponent<Image>().color = card;
                cards[i] = cr.gameObject;
                var band = GrowthScreen.Rect(cr, "Band", tl, Vector2.zero, new Vector2(cardSize.x, bandHeight));
                bands[i] = band.gameObject.AddComponent<Image>();
                bands[i].raycastTarget = false;
                float lineH = (cardSize.y - bandHeight) / 3f;
                stars[i] = GrowthScreen.Label(cr, "Stars", bodyFontSize, text, outline, TextAnchor.MiddleCenter, tl, new Vector2(0f, -bandHeight), new Vector2(cardSize.x, lineH));
                names[i] = GrowthScreen.Label(cr, "Name", bodyFontSize, text, outline, TextAnchor.MiddleCenter, tl, new Vector2(0f, -bandHeight - lineH), new Vector2(cardSize.x, lineH));
                notes[i] = GrowthScreen.Label(cr, "Note", infoFontSize, text, outline, TextAnchor.MiddleCenter, tl, new Vector2(0f, -bandHeight - lineH * 2f), new Vector2(cardSize.x, lineH));
                cr.gameObject.SetActive(false);
            }
            screen.cards = cards;
            screen.cardBands = bands;
            screen.cardStars = stars;
            screen.cardNames = names;
            screen.cardNotes = notes;

            var info = GrowthScreen.Rect(win, "Info", tl, new Vector2(pad, y), new Vector2(inner, bodyHeight));
            info.gameObject.AddComponent<Image>().color = card;
            screen.infoRoot = info.gameObject;
            screen.infoText = GrowthScreen.Label(info, "Text", infoFontSize, text, outline, TextAnchor.UpperLeft, tl, new Vector2(row.y, -row.y), new Vector2(inner - row.y * 2f, bodyHeight - row.y * 2f));
            screen.infoText.verticalOverflow = VerticalWrapMode.Truncate;
            info.gameObject.SetActive(false);

            screen.statusLine = GrowthScreen.Label(win, "Status", infoFontSize, text, outline, TextAnchor.MiddleLeft, new Vector2(0f, 0f), new Vector2(pad, pad), new Vector2(inner * 0.7f, row.x));
            screen.hint = GrowthScreen.Label(win, "Hint", bodyFontSize, text, outline, TextAnchor.MiddleRight, new Vector2(1f, 0f), new Vector2(-pad, pad), new Vector2(inner * 0.28f, row.x));
            panelGo.SetActive(false);
            return screen;
        }
    }
}
