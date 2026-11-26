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
    /// 교체 강화 버프 HUD (3-2) — 체력 막대(HealthHud) 바로 위, 필드 캐릭터에게 버프가 있는 동안만:
    /// 아이콘(준 캐릭터 색 네모 + 남은 시간만큼 줄어드는 띠) · 남은 초(키 ui.skill.cooldown) · 효과(키 ui.buff.damage · ui.buff.poise · ui.buff.shield).
    /// 규칙은 Core Party.Buff(HandoffBuff). 크기 · 위치 · 색은 Setup 표(표현 값). GameRoot가 Bind로 연결한다.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class BuffHud : MonoBehaviour
    {
        const string Separator = "   "; // 효과 사이 띄움 (글이 아니라 빈칸)

        [SerializeField] GameObject panel;
        [SerializeField] Image icon;
        [SerializeField] Image timeFill;
        [SerializeField] UiText timeText;
        [SerializeField] UiText effectText;

        PlayerCombat _combat;
        string _timeShown, _effectShown;
        int _effectKey = int.MinValue;

        /// <summary>지금 그리는 것 (디버그 · 확인용).</summary>
        public bool Shown => panel != null && panel.activeSelf;
        public string TimeShown => _timeShown;
        public string EffectShown => _effectShown;
        public Color IconColorShown => icon != null ? icon.color : Color.clear;
        public float TimeFillShown { get; private set; }

        public void Bind(PlayerCombat combat, Font font)
        {
            _combat = combat;
            if (font != null)
            {
                if (timeText != null) timeText.font = font;
                if (effectText != null) effectText.font = font;
            }
            Text.LanguageChanged += Refresh;
            Refresh();
        }

        void OnDestroy() => Text.LanguageChanged -= Refresh;

        void Refresh()
        {
            _effectKey = int.MinValue;
            _timeShown = null;
        }

        void LateUpdate()
        {
            Party party = _combat != null ? _combat.Party : null;
            HandoffBuff buff = party != null ? party.Buff : null;
            bool show = buff != null && buff.Active && buff.To == party.ActiveIndex;
            if (panel != null && panel.activeSelf != show) panel.SetActive(show);
            if (!show) { TimeFillShown = 0f; return; }

            CharacterData from = _combat.GetCharacter(buff.From);
            if (icon != null && from != null && icon.color != from.Color) icon.color = from.Color;
            float ratio = buff.Duration > 0f ? Mathf.Clamp01(buff.TimeLeft / buff.Duration) : 0f;
            TimeFillShown = ratio;
            if (timeFill != null) HealthHud.SetFill(timeFill, ratio);

            string time = Text.Get("ui.skill.cooldown", buff.TimeLeft);
            if (time != _timeShown) { _timeShown = time; if (timeText != null) timeText.text = time; }

            HandoffRules r = buff.Rules;
            int key = Mathf.RoundToInt(r.DamageBonus * 1000f) * 7919 ^ Mathf.RoundToInt(r.PoiseBonus * 1000f) * 31 ^ buff.ShieldLeft;
            if (key == _effectKey) return;
            _effectKey = key;
            var sb = new System.Text.StringBuilder();
            if (r.DamageBonus > 0f) Append(sb, Text.Get("ui.buff.damage", r.DamageBonus * 100f));
            if (r.PoiseBonus > 0f) Append(sb, Text.Get("ui.buff.poise", r.PoiseBonus * 100f));
            if (buff.ShieldLeft > 0) Append(sb, Text.Get("ui.buff.shield", buff.ShieldLeft));
            _effectShown = sb.ToString();
            if (effectText != null) effectText.text = _effectShown;
        }

        static void Append(System.Text.StringBuilder sb, string s)
        {
            if (sb.Length > 0) sb.Append(Separator);
            sb.Append(s);
        }

        /// <summary>
        /// HUD 캔버스 아래 가운데에 버프 칸을 만든다 (Setup 메뉴가 부른다 — uGUI 참조가 이 어셈블리에 있어서 여기 둔다).
        /// bottom = 화면 아래에서 칸까지, iconSize = 아이콘 한 변, barHeight = 아이콘 아래 남은 시간 띠 (기준 해상도 px). 체력 HUD보다 먼저 만든다(검은 판이 위).
        /// </summary>
        public static BuffHud CreateIn(Transform canvas, float bottom, float iconSize, float barHeight, float gap, int fontSize,
            Color back, Color barColor, Color textColor, Color outline)
        {
            var go = new GameObject("BuffHud", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(canvas, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, bottom);
            rt.sizeDelta = new Vector2(iconSize, iconSize + barHeight + gap);
            var hud = go.AddComponent<BuffHud>();

            var panelGo = new GameObject("Panel", typeof(RectTransform));
            var pr = (RectTransform)panelGo.transform;
            pr.SetParent(rt, false);
            pr.anchorMin = Vector2.zero;
            pr.anchorMax = Vector2.one;
            pr.offsetMin = pr.offsetMax = Vector2.zero;

            var backImg = Box(pr, "IconBack", new Vector2(0f, barHeight + gap), new Vector2(iconSize, iconSize), back);
            backImg.rectTransform.SetAsFirstSibling();
            float inset = iconSize * 0.15f;
            hud.icon = Box(pr, "Icon", new Vector2(inset, barHeight + gap + inset), new Vector2(iconSize - inset * 2f, iconSize - inset * 2f), Color.white);
            Box(pr, "TimeBack", Vector2.zero, new Vector2(iconSize, barHeight), back);
            hud.timeFill = Box(pr, "TimeFill", Vector2.zero, new Vector2(iconSize, barHeight), barColor);
            hud.timeFill.gameObject.AddComponent<LayoutElement>().preferredWidth = iconSize; // 가득 찬 너비 (HealthHud.SetFill)
            hud.timeText = Label(pr, "Time", fontSize, textColor, outline, TextAnchor.MiddleRight, new Vector2(-gap - 400f, barHeight + gap), new Vector2(400f, iconSize));
            hud.effectText = Label(pr, "Effect", fontSize, textColor, outline, TextAnchor.MiddleLeft, new Vector2(iconSize + gap, barHeight + gap), new Vector2(600f, iconSize));
            hud.panel = panelGo;
            panelGo.SetActive(false);
            return hud;
        }

        static Image Box(RectTransform parent, string name, Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = Vector2.zero;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = go.AddComponent<Image>();
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
