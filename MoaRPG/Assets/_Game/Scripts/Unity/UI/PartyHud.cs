using Moa.Core.Combat;
using Moa.Unity.Combat;
using Moa.Unity.Data;
using UnityEngine;
using UnityEngine.UI;
using Text = Moa.Core.Localization.Text;
using UiText = UnityEngine.UI.Text;

namespace Moa.Unity.UI
{
    /// <summary>
    /// 파티 임시 HUD (2-6) — 화면 오른쪽에 칸 넷 (위에서 1 · 2 · 3 · 4번).
    /// 칸마다: 번호(키 ui.party.slot) · 이름(캐릭터 nameKey) · 캐릭터 색 띠 · 현재 캐릭터면 바탕색이 밝아진다 ·
    /// 교체 쿨 동안 대기 칸에 덮개가 줄어들고 남은 초(키 ui.skill.cooldown) · 그 캐릭터의 궁극기(Q)가 준비되면(에너지 가득 + 쿨 끝) 금색 점.
    /// 크기 · 위치 · 색은 Setup 표(표현 값). 캐릭터가 없는 칸은 숨긴다. GameRoot가 Bind로 연결한다.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class PartyHud : MonoBehaviour
    {
        public const int Slots = 4;

        [SerializeField] GameObject[] rows = new GameObject[0];
        [SerializeField] Image[] backs = new Image[0];
        [SerializeField] Image[] swatches = new Image[0];
        [SerializeField] Image[] cooldowns = new Image[0];
        [SerializeField] Image[] burstReady = new Image[0];
        [SerializeField] UiText[] numbers = new UiText[0];
        [SerializeField] UiText[] names = new UiText[0];
        [SerializeField] UiText[] cooldownTexts = new UiText[0];
        [SerializeField] Color slotColor;
        [SerializeField] Color activeColor;

        PlayerCombat _combat;
        readonly string[] _cooldownShown = new string[Slots];
        readonly int[] _state = { -1, -1, -1, -1 };
        int _shownCount = -1;

        /// <summary>지금 그리는 것 (디버그 · 확인용): 보이는 칸 수 · 현재 칸 · 칸의 이름 글 · 쿨 덮개 비율 · Q 준비 점.</summary>
        public int VisibleRows { get; private set; }
        public int HighlightedRow { get; private set; } = -1;
        public string NameShown(int i) => i >= 0 && i < names.Length && names[i] != null ? names[i].text : null;
        public float CooldownShown(int i) => i >= 0 && i < cooldowns.Length && cooldowns[i] != null ? cooldowns[i].fillAmount : 0f;
        public bool BurstReadyShown(int i) => i >= 0 && i < burstReady.Length && burstReady[i] != null && burstReady[i].enabled;

        public void Bind(PlayerCombat combat, Font font)
        {
            _combat = combat;
            foreach (var arr in new[] { numbers, names, cooldownTexts })
                foreach (var t in arr) if (t != null && font != null) t.font = font;
            RefreshLabels();
            Moa.Core.Localization.Text.LanguageChanged += RefreshLabels;
        }

        void OnDestroy() => Moa.Core.Localization.Text.LanguageChanged -= RefreshLabels;

        void RefreshLabels()
        {
            for (int i = 0; i < Slots; i++)
            {
                if (i < numbers.Length && numbers[i] != null) numbers[i].text = Text.Get("ui.party.slot", i + 1);
                CharacterData c = _combat != null ? _combat.GetCharacter(i) : null;
                if (i < names.Length && names[i] != null) names[i].text = c != null && !string.IsNullOrEmpty(c.NameKey) ? Text.Get(c.NameKey) : "";
                if (i < swatches.Length && swatches[i] != null && c != null) swatches[i].color = c.Color;
                _cooldownShown[i] = null;
            }
        }

        void LateUpdate()
        {
            int count = _combat != null ? _combat.CharacterCount : 0;
            if (count != _shownCount)
            {
                _shownCount = count;
                for (int i = 0; i < rows.Length; i++) if (rows[i] != null) rows[i].SetActive(i < count);
                VisibleRows = Mathf.Min(count, rows.Length);
                RefreshLabels();
            }
            if (count == 0) { HighlightedRow = -1; return; }

            Party party = _combat.Party;
            int active = party.ActiveIndex;
            HighlightedRow = active;
            float total = party.Rules.SwitchCooldown;
            float ratio = total > 0f ? Mathf.Clamp01(party.CooldownLeft / total) : 0f;
            for (int i = 0; i < count && i < Slots; i++)
            {
                bool isActive = i == active;
                PlayerAttack a = party.Member(i);
                bool ready = a.Energy.Full && a.Burst.Ready && a.Burst.Rules.SlashCount > 0;
                int state = (isActive ? 1 : 0) | (ready ? 2 : 0);
                if (state != _state[i])
                {
                    _state[i] = state;
                    if (i < backs.Length && backs[i] != null) backs[i].color = isActive ? activeColor : slotColor;
                    if (i < burstReady.Length && burstReady[i] != null) burstReady[i].enabled = ready;
                }
                float cover = isActive ? 0f : ratio;
                if (i < cooldowns.Length && cooldowns[i] != null) cooldowns[i].fillAmount = cover;
                if (i < cooldownTexts.Length && cooldownTexts[i] != null)
                {
                    string txt = cover > 0f ? Text.Get("ui.skill.cooldown", party.CooldownLeft) : "";
                    if (txt != _cooldownShown[i])
                    {
                        _cooldownShown[i] = txt;
                        cooldownTexts[i].text = txt;
                        cooldownTexts[i].enabled = txt.Length > 0;
                    }
                }
            }
        }

        /// <summary>
        /// HUD 캔버스 아래 오른쪽 가운데에 칸 넷을 만든다 (Setup 메뉴가 부른다 — uGUI 참조가 이 어셈블리에 있어서 여기 둔다).
        /// margin = 화면 오른쪽에서 칸까지 · 화면 가운데에서 위로 (기준 해상도 px), rowSize = 칸 너비 · 높이, gap = 칸 사이.
        /// </summary>
        public static PartyHud CreateIn(Transform canvas, Sprite dot, Vector2 rowSize, float gap, Vector2 margin, float swatchWidth, float dotSize,
            int numberFontSize, int nameFontSize, Color slot, Color active, Color cooldownCover, Color ready, Color textColor, Color outline)
        {
            var go = new GameObject("PartyHud", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(canvas, false);
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(1f, 0.5f);
            float height = rowSize.y * Slots + gap * (Slots - 1);
            rt.anchoredPosition = new Vector2(-margin.x, margin.y);
            rt.sizeDelta = new Vector2(rowSize.x, height);
            var hud = go.AddComponent<PartyHud>();
            hud.rows = new GameObject[Slots];
            hud.backs = new Image[Slots];
            hud.swatches = new Image[Slots];
            hud.cooldowns = new Image[Slots];
            hud.burstReady = new Image[Slots];
            hud.numbers = new UiText[Slots];
            hud.names = new UiText[Slots];
            hud.cooldownTexts = new UiText[Slots];

            for (int i = 0; i < Slots; i++)
            {
                var row = new GameObject("Slot" + (i + 1), typeof(RectTransform));
                var r = (RectTransform)row.transform;
                r.SetParent(rt, false);
                r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
                r.pivot = new Vector2(0f, 1f);
                r.anchoredPosition = new Vector2(0f, -i * (rowSize.y + gap));
                r.sizeDelta = rowSize;
                var back = row.AddComponent<Image>();
                back.color = slot;
                back.raycastTarget = false;
                hud.rows[i] = row;
                hud.backs[i] = back;

                hud.swatches[i] = Box(r, "Color", new Vector2(0f, 0f), new Vector2(swatchWidth, rowSize.y), Color.white, null);
                hud.numbers[i] = Label(r, "Number", numberFontSize, textColor, outline, TextAnchor.MiddleCenter,
                    new Vector2(swatchWidth, 0f), new Vector2(rowSize.y, rowSize.y));
                hud.names[i] = Label(r, "Name", nameFontSize, textColor, outline, TextAnchor.MiddleLeft,
                    new Vector2(swatchWidth + rowSize.y, 0f), new Vector2(rowSize.x - swatchWidth - rowSize.y - dotSize * 2f, rowSize.y));
                var dotImg = Box(r, "BurstReady", new Vector2(rowSize.x - dotSize * 1.5f, (rowSize.y - dotSize) * 0.5f), new Vector2(dotSize, dotSize), ready, dot);
                dotImg.enabled = false;
                hud.burstReady[i] = dotImg;

                var cover = Box(r, "Cooldown", Vector2.zero, rowSize, cooldownCover, null);
                cover.type = Image.Type.Filled;
                cover.fillMethod = Image.FillMethod.Horizontal;
                cover.fillOrigin = (int)Image.OriginHorizontal.Left;
                cover.fillAmount = 0f;
                hud.cooldowns[i] = cover;
                var cd = Label(r, "CooldownText", nameFontSize, textColor, outline, TextAnchor.MiddleRight,
                    new Vector2(0f, 0f), new Vector2(rowSize.x - dotSize * 2.5f, rowSize.y));
                cd.enabled = false;
                hud.cooldownTexts[i] = cd;
            }
            hud.slotColor = slot;
            hud.activeColor = active;
            return hud;
        }

        static Image Box(RectTransform parent, string name, Vector2 pos, Vector2 size, Color color, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = Vector2.zero;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        static UiText Label(RectTransform parent, string name, int size, Color color, Color outline, TextAnchor align, Vector2 pos, Vector2 box)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = Vector2.zero;
            rt.anchoredPosition = pos;
            rt.sizeDelta = box;
            var t = go.AddComponent<UiText>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = size;
            t.fontStyle = FontStyle.Bold;
            t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.color = color;
            t.raycastTarget = false;
            var o = go.AddComponent<Outline>();
            o.effectColor = outline;
            o.effectDistance = new Vector2(2f, -2f);
            return t;
        }
    }
}
