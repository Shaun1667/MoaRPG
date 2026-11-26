using Moa.Core.Combat;
using Moa.Unity.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace Moa.Unity.UI
{
    /// <summary>
    /// 플레이어 체력 HUD (3-1): 화면 아래 가운데에 필드 캐릭터 체력 막대(낮으면 색이 바뀜, 글 없음) + 전원 쓰러짐 때 화면을 덮는 검은 판.
    /// 크기 · 위치 · 색 · "낮음" 기준은 Setup 표(표현 값). 어둡기는 Core PartyVitals.Darkness. GameRoot가 Bind로 연결한다.
    /// 검은 판이 HUD 맨 위에 오도록 Setup이 HUD 캔버스의 마지막 자식으로 만든다.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class HealthHud : MonoBehaviour
    {
        [SerializeField] GameObject bar;
        [SerializeField] Image fill;
        [SerializeField] Image fade;
        [SerializeField] Color fillColor;
        [SerializeField] Color lowColor;
        [Tooltip("Health ratio at or below which the bar uses lowColor")]
        [SerializeField] float lowRatio;

        PlayerVitals _vitals;

        /// <summary>지금 그리는 것 (디버그 · 확인용): 막대 보임 · 채움 비율 · 낮음 색 · 검은 판 알파.</summary>
        public bool BarShown => bar != null && bar.activeSelf;
        public float FillShown { get; private set; }
        public bool LowShown { get; private set; }
        public float FadeShown => fade != null && fade.enabled ? fade.color.a : 0f;

        public void Bind(PlayerVitals vitals) => _vitals = vitals;

        void LateUpdate()
        {
            PartyVitals v = _vitals != null ? _vitals.Vitals : null;
            bool show = v != null;
            if (bar != null && bar.activeSelf != show) bar.SetActive(show);
            float dark = v != null ? v.Darkness : 0f;
            if (fade != null)
            {
                Color c = fade.color;
                c.a = dark;
                fade.color = c;
                fade.enabled = dark > 0f;
            }
            if (!show || fill == null) return;
            float ratio = v.Active.Ratio;
            FillShown = ratio;
            SetFill(fill, ratio);
            LowShown = ratio <= lowRatio;
            fill.color = LowShown ? lowColor : fillColor;
        }

        /// <summary>
        /// HUD 캔버스 아래에 체력 막대 · 검은 판을 만든다 (Setup 메뉴가 부른다 — uGUI 참조가 이 어셈블리에 있어서 여기 둔다).
        /// barSize = 막대 너비 · 높이, bottom = 화면 아래에서 막대까지, border = 바탕이 막대보다 넓은 폭 (기준 해상도 px).
        /// </summary>
        public static HealthHud CreateIn(Transform canvas, Vector2 barSize, float bottom, float border, Color back, Color fillColor, Color lowColor, float lowRatio, Color fadeColor)
        {
            var go = new GameObject("HealthHud", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(canvas, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var hud = go.AddComponent<HealthHud>();

            var barGo = new GameObject("Bar", typeof(RectTransform));
            var br = (RectTransform)barGo.transform;
            br.SetParent(rt, false);
            br.anchorMin = br.anchorMax = new Vector2(0.5f, 0f);
            br.pivot = new Vector2(0.5f, 0f);
            br.anchoredPosition = new Vector2(0f, bottom);
            br.sizeDelta = barSize + Vector2.one * (border * 2f);
            var backImg = barGo.AddComponent<Image>();
            backImg.color = back;
            backImg.raycastTarget = false;

            var fillGo = new GameObject("Fill", typeof(RectTransform));
            var fr = (RectTransform)fillGo.transform;
            fr.SetParent(br, false);
            fr.anchorMin = fr.anchorMax = new Vector2(0f, 0.5f);
            fr.pivot = new Vector2(0f, 0.5f);
            fr.anchoredPosition = new Vector2(border, 0f);
            fr.sizeDelta = barSize;
            var fillImg = fillGo.AddComponent<Image>();
            fillImg.color = fillColor;
            fillImg.raycastTarget = false;
            fillGo.AddComponent<LayoutElement>().preferredWidth = barSize.x; // 가득 찬 너비를 기억해 둔다 (SetFill이 이 비율로 줄인다)

            var fadeGo = new GameObject("Fade", typeof(RectTransform));
            var dr = (RectTransform)fadeGo.transform;
            dr.SetParent(rt, false);
            dr.anchorMin = Vector2.zero;
            dr.anchorMax = Vector2.one;
            dr.offsetMin = dr.offsetMax = Vector2.zero;
            var fadeImg = fadeGo.AddComponent<Image>();
            Color fc = fadeColor;
            fc.a = 0f;
            fadeImg.color = fc;
            fadeImg.raycastTarget = false;
            fadeImg.enabled = false;

            hud.bar = barGo;
            hud.fill = fillImg;
            hud.fade = fadeImg;
            hud.fillColor = fillColor;
            hud.lowColor = lowColor;
            hud.lowRatio = lowRatio;
            return hud;
        }

        /// <summary>
        /// 막대 채움: 왼쪽 기준으로 너비를 줄인다 (스프라이트 없는 Image는 Filled가 먹지 않아서). 가득 찬 너비 = 같은 오브젝트의 LayoutElement.preferredWidth.
        /// </summary>
        internal static void SetFill(Image img, float ratio)
        {
            if (img == null) return;
            var le = img.GetComponent<LayoutElement>();
            if (le == null) return;
            var rt = (RectTransform)img.transform;
            float w = le.preferredWidth * Mathf.Clamp01(ratio);
            if (!Mathf.Approximately(rt.sizeDelta.x, w)) rt.sizeDelta = new Vector2(w, rt.sizeDelta.y);
            img.enabled = ratio > 0f;
        }
    }
}
