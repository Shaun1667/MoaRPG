using System;
using Moa.Core.Growth;
using Moa.Unity.Combat;
using Moa.Unity.Data;
using Moa.Unity.Player;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Text = Moa.Core.Localization.Text;
using UiText = UnityEngine.UI.Text;

namespace Moa.Unity.UI
{
    /// <summary>
    /// 캐릭터(성장) 화면 (3-5, 간단판) — 키 CharacterMenu(C)로 열고 닫는다(열려 있으면 ESC도 닫음). 열려 있는 동안 커서가 풀리고 게임 입력은 0 (IPlayerInput.MenuOpen),
    /// 시간은 그대로 흐른다(Time.timeScale은 PlayerDefense만 바꾼다).
    /// 위: 제목 · 재화(골드 · 모험 경험). 파티 칸마다 한 줄 기둥: 이름 · 레벨/상한 · 경험치 · 체력 · 공격 · 방어 · [레벨업] · [돌파] · 스킬 E 레벨 [올리기] · 궁극기 Q 레벨 [올리기] · 무기 레벨 [올리기].
    /// 버튼은 규칙(Core Progression.Check)이 막으면 꺼진다(재화 모자람 · 상한 · 최대). 누르면 Progression.Try → GameRoot가 능력치를 다시 넣고 저장한다.
    /// 글은 모두 텍스트 표(ui.growth.*), 크기 · 색은 Setup 표(표현 값). GameRoot가 Bind로 연결한다.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class GrowthScreen : MonoBehaviour
    {
        public const int Slots = 4;
        /// <summary>칸마다 버튼 수 = GrowthAction 수 (버튼 순서 = enum 순서).</summary>
        public static readonly int ActionCount = Enum.GetValues(typeof(GrowthAction)).Length;

        [SerializeField] GameObject panel;
        [SerializeField] UiText title;
        [SerializeField] UiText wallet;
        [SerializeField] UiText hint;
        [SerializeField] GameObject[] columns = new GameObject[0];
        [SerializeField] Image[] swatches = new Image[0];
        [SerializeField] UiText[] names = new UiText[0];
        [SerializeField] UiText[] levels = new UiText[0];
        [SerializeField] UiText[] exps = new UiText[0];
        [SerializeField] UiText[] stats = new UiText[0];
        [SerializeField] UiText[] skillLines = new UiText[0];
        [SerializeField] UiText[] burstLines = new UiText[0];
        [SerializeField] UiText[] weaponLines = new UiText[0];
        [Tooltip("slot * ActionCount + (int)GrowthAction")]
        [SerializeField] Button[] buttons = new Button[0];
        [SerializeField] UiText[] buttonLabels = new UiText[0];

        Progression _growth;
        PlayerCombat _combat;
        PlayerVitals _vitals;
        IPlayerInput _input;
        bool _dirty = true;

        public bool Open => panel != null && panel.activeSelf;
        /// <summary>디버그 · 확인용: 누른 수 · 마지막으로 막힌 까닭 · 보이는 글.</summary>
        public int PressCount { get; private set; }
        public GrowthBlock LastBlock { get; private set; }
        public int RefreshCount { get; private set; }
        public string WalletShown => wallet != null ? wallet.text : null;
        public string LevelShown(int slot) => At(levels, slot)?.text;
        public string ExpShown(int slot) => At(exps, slot)?.text;
        public string StatsShown(int slot) => At(stats, slot)?.text;
        public string WeaponShown(int slot) => At(weaponLines, slot)?.text;
        public string ButtonShown(int slot, GrowthAction a) => At(buttonLabels, slot * ActionCount + (int)a)?.text;
        public bool ButtonEnabled(int slot, GrowthAction a) { var b = At(buttons, slot * ActionCount + (int)a); return b != null && b.interactable; }

        static T At<T>(T[] arr, int i) where T : class => arr != null && i >= 0 && i < arr.Length ? arr[i] : null;

        public void Bind(Progression growth, PlayerCombat combat, PlayerVitals vitals, IPlayerInput input, Font font)
        {
            if (_growth != null) _growth.Changed -= OnChanged;
            _growth = growth;
            _combat = combat;
            _vitals = vitals;
            _input = input;
            if (_growth != null) _growth.Changed += OnChanged;
            if (font != null)
                foreach (var t in GetComponentsInChildren<UiText>(true)) t.font = font;
            for (int k = 0; k < buttons.Length; k++)
            {
                if (buttons[k] == null) continue;
                int slot = k / ActionCount, action = k % ActionCount;
                buttons[k].onClick.RemoveAllListeners();
                buttons[k].onClick.AddListener(() => Press(slot, (GrowthAction)action));
            }
            Text.LanguageChanged -= MarkDirty;
            Text.LanguageChanged += MarkDirty;
            SetOpen(false);
        }

        void OnDestroy()
        {
            if (_growth != null) _growth.Changed -= OnChanged;
            Text.LanguageChanged -= MarkDirty;
        }

        void OnChanged(int slot) => _dirty = true;
        void MarkDirty() => _dirty = true;

        void Update()
        {
            if (_input != null && _input.ConsumeCharacterMenu()) SetOpen(!Open);
        }

        void LateUpdate()
        {
            if (Open && _dirty) Refresh();
        }

        /// <summary>[상태 변경] 화면을 열거나 닫는다 (열면 커서를 풀고 게임 입력을 막는다).</summary>
        public void SetOpen(bool open)
        {
            if (panel != null) panel.SetActive(open && _growth != null);
            if (_input != null) _input.MenuOpen = Open;
            _dirty = true;
            if (Open) Refresh();
        }

        /// <summary>[상태 변경] 칸의 버튼을 누른다 (버튼 · 디버그 공통). 되면 true, 안 되면 LastBlock.</summary>
        public bool Press(int slot, GrowthAction action)
        {
            if (_growth == null) return false;
            bool ok = _growth.Try(slot, action);
            LastBlock = _growth.LastBlock;
            PressCount++;
            _dirty = true;
            return ok;
        }

        /// <summary>글 · 버튼을 지금 상태로 (열려 있을 때 바뀌면 LateUpdate가, 열 때 한 번).</summary>
        public void Refresh()
        {
            _dirty = false;
            RefreshCount++;
            if (_growth == null) return;
            if (title != null) title.text = Text.Get("ui.growth.title");
            if (hint != null) hint.text = Text.Get("ui.growth.close");
            if (wallet != null) wallet.text = Text.Get("ui.growth.wallet", _growth.Wallet.Get(Currency.Gold), _growth.Wallet.Get(Currency.AdventureExp));
            int count = Mathf.Min(_growth.Count, Slots);
            for (int i = 0; i < Slots && i < columns.Length; i++)
            {
                if (columns[i] != null) columns[i].SetActive(i < count);
                if (i >= count) continue;
                MemberGrowth m = _growth.Member(i);
                CharacterData c = _combat != null ? (_combat.Party != null ? _combat.GetCharacter(i) : _combat.SoloCharacter) : null;
                MemberStats g = _growth.Stats(i);
                Set(names, i, c != null && !string.IsNullOrEmpty(c.NameKey) ? Text.Get(c.NameKey) : (c != null ? c.name : ""));
                var sw = At(swatches, i);
                if (sw != null && c != null) sw.color = c.Color;
                int cap = _growth.LevelCap(i);
                Set(levels, i, Text.Get("ui.growth.level", m.Level, cap));
                Set(exps, i, m.Level >= cap ? Text.Get("ui.growth.exp_max") : Text.Get("ui.growth.exp", m.Exp, _growth.Rules.Character.ExpToNext(m.Level)));
                var pv = _vitals != null ? _vitals.Vitals : null;
                int hp = pv != null && i < pv.Count ? pv.Member(i).Max : (c != null ? g.ScaleHealth(c.MaxHealth) : 0);
                float def = pv != null && i < pv.Count ? pv.Defense(i) : (c != null ? g.ScaleDefense(c.Defense) : 0f);
                float atk = _combat != null ? _combat.MemberAttack(i) : 0f;
                Set(stats, i, Text.Get("ui.growth.stats", hp, Mathf.RoundToInt(atk), Mathf.RoundToInt(def)));
                Set(skillLines, i, Text.Get("ui.growth.skill", m.SkillLevel));
                Set(burstLines, i, Text.Get("ui.growth.burst", m.BurstLevel));
                WeaponTuning w = c != null ? c.Weapon : null;
                Set(weaponLines, i, Text.Get("ui.growth.weapon", m.WeaponLevel, _growth.WeaponCap, Mathf.RoundToInt(w != null ? w.BaseAttack * g.Weapon : 0f)));
                for (int a = 0; a < ActionCount; a++)
                {
                    var action = (GrowthAction)a;
                    GrowthBlock block = _growth.Check(i, action, out _, out int cost);
                    int k = i * ActionCount + a;
                    var b = At(buttons, k);
                    if (b != null) b.interactable = block == GrowthBlock.None;
                    Set(buttonLabels, k, Label(action, block, cost));
                }
            }
        }

        static string Label(GrowthAction action, GrowthBlock block, int cost)
        {
            if (block == GrowthBlock.MaxLevel) return Text.Get("ui.growth.max");
            if (block == GrowthBlock.NeedAscension) return Text.Get("ui.growth.need_ascend");
            switch (action)
            {
                case GrowthAction.LevelUp: return Text.Get("ui.growth.level_up", cost);
                case GrowthAction.Ascend: return Text.Get("ui.growth.ascend", cost);
                case GrowthAction.WeaponUp: return Text.Get("ui.growth.upgrade_exp", cost);
                default: return Text.Get("ui.growth.upgrade_gold", cost);
            }
        }

        static void Set(UiText[] arr, int i, string s)
        {
            var t = At(arr, i);
            if (t != null && t.text != s) t.text = s;
        }

        // ---- 만들기 (Setup 메뉴가 부른다 — uGUI 참조가 이 어셈블리에 있어서 여기 둔다) ----

        /// <summary>
        /// HUD 캔버스 아래에 화면을 만든다 (자기 Canvas(정렬 덮어쓰기, sortingOrder) + GraphicRaycaster — 버튼을 누를 수 있게). 처음엔 닫혀 있다.
        /// windowSize = 가운데 창 크기, pad = 창 안 여백, gap = 칸 사이, row = 줄 높이 · 줄 사이 (기준 해상도 px).
        /// </summary>
        public static GrowthScreen CreateIn(Transform canvas, int sortingOrder, Vector2 windowSize, float pad, float gap, Vector2 row, float headerHeight,
            int titleFontSize, int bodyFontSize, int buttonFontSize, Color dim, Color window, Color column, Color button, Color buttonDisabled, Color text, Color outline)
        {
            var go = new GameObject("GrowthScreen", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(canvas, false);
            Stretch(rt);
            var c = go.AddComponent<Canvas>();
            c.overrideSorting = true;
            c.sortingOrder = sortingOrder;
            go.AddComponent<GraphicRaycaster>();
            var screen = go.AddComponent<GrowthScreen>();

            var panelGo = new GameObject("Panel", typeof(RectTransform));
            var prt = (RectTransform)panelGo.transform;
            prt.SetParent(rt, false);
            Stretch(prt);
            var dimImg = panelGo.AddComponent<Image>();
            dimImg.color = dim; // 뒤를 가리고 클릭을 막는다
            screen.panel = panelGo;

            var win = Rect(prt, "Window", new Vector2(0.5f, 0.5f), Vector2.zero, windowSize);
            win.gameObject.AddComponent<Image>().color = window;
            Vector2 topLeft = new Vector2(0f, 1f);
            screen.title = Label(win, "Title", titleFontSize, text, outline, TextAnchor.MiddleLeft, topLeft, new Vector2(pad, -pad), new Vector2(windowSize.x * 0.4f, headerHeight * 0.5f));
            screen.wallet = Label(win, "Wallet", bodyFontSize, text, outline, TextAnchor.MiddleRight, new Vector2(1f, 1f), new Vector2(-pad, -pad), new Vector2(windowSize.x * 0.55f, headerHeight * 0.5f));
            screen.hint = Label(win, "Hint", bodyFontSize, text, outline, TextAnchor.MiddleRight, new Vector2(1f, 0f), new Vector2(-pad, pad), new Vector2(windowSize.x * 0.5f, row.x));

            float colWidth = (windowSize.x - pad * 2f - gap * (Slots - 1)) / Slots;
            float colHeight = windowSize.y - pad * 2f - headerHeight - row.x;
            var cols = new GameObject[Slots];
            var sws = new Image[Slots];
            UiText[] ns = new UiText[Slots], lv = new UiText[Slots], ex = new UiText[Slots], st = new UiText[Slots], sk = new UiText[Slots], bu = new UiText[Slots], we = new UiText[Slots];
            var btns = new Button[Slots * ActionCount];
            var btnLabels = new UiText[Slots * ActionCount];
            float step = row.x + row.y;
            for (int i = 0; i < Slots; i++)
            {
                var col = Rect(win, "Slot" + (i + 1), topLeft, new Vector2(pad + i * (colWidth + gap), -(pad + headerHeight)), new Vector2(colWidth, colHeight));
                col.pivot = topLeft;
                col.anchoredPosition = new Vector2(pad + i * (colWidth + gap), -(pad + headerHeight));
                col.gameObject.AddComponent<Image>().color = column;
                cols[i] = col.gameObject;
                var sw = Rect(col, "Color", topLeft, Vector2.zero, new Vector2(colWidth, row.y * 0.75f));
                sw.pivot = topLeft;
                sw.anchoredPosition = Vector2.zero;
                sws[i] = sw.gameObject.AddComponent<Image>();
                float inner = colWidth - row.y * 2f;
                int line = 0;
                UiText Line(string n, int size) => Label(col, n, size, text, outline, TextAnchor.MiddleLeft, topLeft, new Vector2(row.y, -(row.y + line++ * step)), new Vector2(inner, row.x));
                Button Btn(GrowthAction a)
                {
                    int k = i * ActionCount + (int)a;
                    var b = MakeButton(col, a.ToString(), new Vector2(row.y, -(row.y + line++ * step)), new Vector2(inner, row.x), button, buttonDisabled, buttonFontSize, text, outline, out btnLabels[k]);
                    btns[k] = b;
                    return b;
                }
                ns[i] = Line("Name", titleFontSize - 6);
                lv[i] = Line("Level", bodyFontSize);
                ex[i] = Line("Exp", bodyFontSize);
                st[i] = Line("Stats", bodyFontSize);
                Btn(GrowthAction.LevelUp);
                Btn(GrowthAction.Ascend);
                sk[i] = Line("Skill", bodyFontSize);
                Btn(GrowthAction.SkillUp);
                bu[i] = Line("Burst", bodyFontSize);
                Btn(GrowthAction.BurstUp);
                we[i] = Line("Weapon", bodyFontSize);
                Btn(GrowthAction.WeaponUp);
            }
            screen.columns = cols;
            screen.swatches = sws;
            screen.names = ns;
            screen.levels = lv;
            screen.exps = ex;
            screen.stats = st;
            screen.skillLines = sk;
            screen.burstLines = bu;
            screen.weaponLines = we;
            screen.buttons = btns;
            screen.buttonLabels = btnLabels;
            panelGo.SetActive(false);
            return screen;
        }

        /// <summary>
        /// 버튼을 누를 수 있게 EventSystem을 만든다 (Setup 메뉴가 부른다). 입력 모듈은 Input System의 InputSystemUIInputModule을 이름으로 찾아 붙인다
        /// (이 어셈블리는 Input System을 참조하지 않는다 — 입력은 Moa.Unity.Input 한 곳), 없으면 StandaloneInputModule.
        /// </summary>
        public static GameObject CreateEventSystem(string name)
        {
            var go = new GameObject(name);
            go.AddComponent<EventSystem>();
            Type module = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (module != null) go.AddComponent(module);
            else go.AddComponent<StandaloneInputModule>();
            return go;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        static RectTransform Rect(RectTransform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        static UiText Label(RectTransform parent, string name, int size, Color color, Color outline, TextAnchor align, Vector2 anchor, Vector2 pos, Vector2 box)
        {
            var rt = Rect(parent, name, anchor, pos, box);
            var t = rt.gameObject.AddComponent<UiText>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = size;
            t.fontStyle = FontStyle.Bold;
            t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.resizeTextForBestFit = true; // 번역으로 길어져도 칸 안에
            t.resizeTextMinSize = Mathf.Max(10, size / 2);
            t.resizeTextMaxSize = size;
            t.color = color;
            t.raycastTarget = false;
            var o = rt.gameObject.AddComponent<Outline>();
            o.effectColor = outline;
            o.effectDistance = new Vector2(1.5f, -1.5f);
            return t;
        }

        static Button MakeButton(RectTransform parent, string name, Vector2 pos, Vector2 size, Color normal, Color disabled, int fontSize, Color text, Color outline, out UiText label)
        {
            var rt = Rect(parent, name, new Vector2(0f, 1f), pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = Color.white; // 색은 버튼 ColorBlock이 입힌다
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            var cb = b.colors;
            cb.normalColor = normal;
            cb.highlightedColor = Color.Lerp(normal, Color.white, 0.25f);
            cb.pressedColor = Color.Lerp(normal, Color.black, 0.25f);
            cb.selectedColor = normal;
            cb.disabledColor = disabled;
            cb.colorMultiplier = 1f;
            b.colors = cb;
            b.navigation = new Navigation { mode = Navigation.Mode.None };
            label = Label(rt, "Label", fontSize, text, outline, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, size);
            return b;
        }
    }
}
