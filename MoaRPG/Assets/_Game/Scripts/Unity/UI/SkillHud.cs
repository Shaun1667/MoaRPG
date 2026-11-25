using Moa.Core.Combat;
using Moa.Unity.Combat;
using UnityEngine;
using UnityEngine.UI;
using Text = Moa.Core.Localization.Text;
using UiText = UnityEngine.UI.Text;

namespace Moa.Unity.UI
{
    /// <summary>
    /// 스킬(E) · 궁극기(Q) 임시 HUD (2-4) — 화면 오른쪽 아래에 칸 둘.
    /// E 칸: 쿨타임 동안 어두운 덮개가 위에서 줄어들고 남은 초(키 ui.skill.cooldown), 누르는 중이면 칸 색이 바뀐다.
    /// Q 칸: 에너지 원형 게이지(가득 = 준비 색), 아래에 에너지 숫자(키 ui.burst.energy), 쿨타임이면 덮개 + 남은 초.
    /// 칸 글자는 키 ui.skill.key · ui.burst.key. 크기 · 위치 · 색은 Setup 표(표현 값). 칸 이미지는 스프라이트 없는 단색 · 링 스프라이트.
    /// GameRoot가 Bind로 연결한다.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class SkillHud : MonoBehaviour
    {
        [SerializeField] Image skillBack;
        [SerializeField] Image skillCooldown;
        [SerializeField] UiText skillKey;
        [SerializeField] UiText skillNumber;
        [SerializeField] Image burstBack;
        [SerializeField] Image burstEnergy;
        [SerializeField] Image burstCooldown;
        [SerializeField] UiText burstKey;
        [SerializeField] UiText burstNumber;
        [SerializeField] UiText burstEnergyLabel;
        [SerializeField] Color slotColor;
        [SerializeField] Color pressingColor;
        [SerializeField] Color castingColor;
        [SerializeField] Color energyColor;
        [SerializeField] Color energyFullColor;

        PlayerCombat _combat;
        string _skillNumberText, _burstNumberText, _energyText;
        int _skillState = -1, _burstState = -1;

        /// <summary>지금 그리는 글 · 비율 (디버그 · 확인용).</summary>
        public string SkillNumberText => _skillNumberText;
        public string BurstNumberText => _burstNumberText;
        public string EnergyText => _energyText;
        public float SkillCooldownShown => skillCooldown != null ? skillCooldown.fillAmount : 0f;
        public float EnergyShown => burstEnergy != null ? burstEnergy.fillAmount : 0f;

        public void Bind(PlayerCombat combat, Font font)
        {
            _combat = combat;
            foreach (var t in new[] { skillKey, skillNumber, burstKey, burstNumber, burstEnergyLabel })
                if (t != null && font != null) t.font = font;
            if (skillKey != null) skillKey.text = Text.Get("ui.skill.key");
            if (burstKey != null) burstKey.text = Text.Get("ui.burst.key");
            Moa.Core.Localization.Text.LanguageChanged += OnLanguage;
        }

        void OnDestroy() => Moa.Core.Localization.Text.LanguageChanged -= OnLanguage;

        void OnLanguage()
        {
            if (skillKey != null) skillKey.text = Text.Get("ui.skill.key");
            if (burstKey != null) burstKey.text = Text.Get("ui.burst.key");
            _skillNumberText = _burstNumberText = _energyText = null;
        }

        void LateUpdate()
        {
            if (_combat == null || _combat.Skill == null) return;
            SkillAttack s = _combat.Skill;
            BurstAttack b = _combat.Burst;
            Energy e = _combat.Energy;

            // E 칸
            float skillRatio = s.CooldownTotal > 0f ? s.CooldownLeft / s.CooldownTotal : 0f;
            if (skillCooldown != null) skillCooldown.fillAmount = Mathf.Clamp01(skillRatio);
            SetNumber(skillNumber, s.CooldownLeft, ref _skillNumberText);
            int skillState = s.Pressing ? 1 : s.Casting ? 2 : 0;
            if (skillState != _skillState && skillBack != null)
            {
                _skillState = skillState;
                skillBack.color = skillState == 1 ? pressingColor : skillState == 2 ? castingColor : slotColor;
            }

            // Q 칸
            if (burstEnergy != null) burstEnergy.fillAmount = e != null ? e.Ratio : 0f;
            float burstRatio = b.Rules.Cooldown > 0f ? b.CooldownLeft / b.Rules.Cooldown : 0f;
            if (burstCooldown != null) burstCooldown.fillAmount = Mathf.Clamp01(burstRatio);
            SetNumber(burstNumber, b.CooldownLeft, ref _burstNumberText);
            int burstState = b.Active ? 2 : e != null && e.Full && b.Ready ? 1 : 0;
            if (burstState != _burstState)
            {
                _burstState = burstState;
                if (burstEnergy != null) burstEnergy.color = burstState >= 1 ? energyFullColor : energyColor;
                if (burstBack != null) burstBack.color = burstState == 2 ? castingColor : slotColor;
            }
            if (burstEnergyLabel != null && e != null)
            {
                string txt = Text.Get("ui.burst.energy", Mathf.Floor(e.Current + 1e-3f), e.Max);
                if (txt != _energyText) { _energyText = txt; burstEnergyLabel.text = txt; }
            }
        }

        static void SetNumber(UiText label, float left, ref string shown)
        {
            if (label == null) return;
            string txt = left > 0f ? Text.Get("ui.skill.cooldown", left) : "";
            if (txt == shown) return;
            shown = txt;
            label.text = txt;
            label.enabled = txt.Length > 0;
        }

        /// <summary>
        /// HUD 캔버스 아래 오른쪽 아래에 칸 둘을 만든다 (Setup 메뉴가 부른다 — uGUI 참조가 이 어셈블리에 있어서 여기 둔다).
        /// margin = 화면 오른쪽 · 아래에서 Q 칸까지, gap = 칸 사이 (기준 해상도 px).
        /// </summary>
        public static SkillHud CreateIn(Transform canvas, Sprite ring, float slotSize, float gap, Vector2 margin, int keyFontSize, int numberFontSize,
            Color slot, Color pressing, Color casting, Color cooldownCover, Color energy, Color energyFull, Color textColor, Color outline)
        {
            var go = new GameObject("SkillHud", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(canvas, false);
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-margin.x, margin.y);
            rt.sizeDelta = new Vector2(slotSize * 2f + gap, slotSize);
            var hud = go.AddComponent<SkillHud>();

            // E 칸 (왼쪽)
            var e = Slot(rt, "Skill", new Vector2(0f, 0f), slotSize, slot);
            hud.skillBack = e.GetComponent<Image>();
            hud.skillCooldown = Fill(e, "Cooldown", null, cooldownCover, Image.FillMethod.Vertical, (int)Image.OriginVertical.Bottom);
            hud.skillKey = Label(e, "Key", keyFontSize, textColor, outline, Vector2.zero);
            hud.skillNumber = Label(e, "Number", numberFontSize, textColor, outline, new Vector2(0f, -slotSize * 0.32f));

            // Q 칸 (오른쪽)
            var q = Slot(rt, "Burst", new Vector2(slotSize + gap, 0f), slotSize, slot);
            hud.burstBack = q.GetComponent<Image>();
            hud.burstEnergy = Fill(q, "Energy", ring, energy, Image.FillMethod.Radial360, (int)Image.Origin360.Top);
            hud.burstEnergy.fillClockwise = true;
            hud.burstEnergy.fillAmount = 0f;
            hud.burstCooldown = Fill(q, "Cooldown", null, cooldownCover, Image.FillMethod.Vertical, (int)Image.OriginVertical.Bottom);
            hud.burstKey = Label(q, "Key", keyFontSize, textColor, outline, Vector2.zero);
            hud.burstNumber = Label(q, "Number", numberFontSize, textColor, outline, new Vector2(0f, -slotSize * 0.32f));
            hud.burstEnergyLabel = Label(q, "EnergyText", numberFontSize, textColor, outline, new Vector2(0f, -slotSize * 0.5f - numberFontSize * 0.8f));

            hud.slotColor = slot;
            hud.pressingColor = pressing;
            hud.castingColor = casting;
            hud.energyColor = energy;
            hud.energyFullColor = energyFull;
            return hud;
        }

        static RectTransform Slot(RectTransform parent, string name, Vector2 pos, float size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = Vector2.zero;
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(size, size);
            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return rt;
        }

        static Image Fill(RectTransform parent, string name, Sprite sprite, Color color, Image.FillMethod method, int origin)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            img.type = Image.Type.Filled;
            img.fillMethod = method;
            img.fillOrigin = origin;
            img.fillAmount = 0f;
            return img;
        }

        static UiText Label(RectTransform parent, string name, int size, Color color, Color outline, Vector2 offset)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = offset;
            rt.sizeDelta = new Vector2(200f, size * 1.5f);
            var t = go.AddComponent<UiText>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = size;
            t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter;
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
