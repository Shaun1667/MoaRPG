using Moa.Core.Motion;
using Moa.Unity.Data;
using Moa.Unity.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Moa.Unity.UI
{
    /// <summary>
    /// 스태미나 원형 게이지 (1-2) — 화면(Overlay 캔버스)에서 캐릭터 옆을 따라간다.
    /// 원신처럼 가득 차 있으면 숨기고, 줄면 바로 보이고, 다시 가득 차면 잠시 뒤 흐려지며 사라진다.
    /// 지침 중에는 지침 색. 글은 없다. 위치 · 크기 · 숨김 시간은 StaminaTuning(BALANCE 3번), 색은 Setup 메뉴.
    /// RectTransform은 캔버스 왼쪽 아래 기준(anchor 0,0)이어야 한다. GameRoot가 Bind로 연결한다.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class StaminaGauge : MonoBehaviour
    {
        [SerializeField] Image fill;
        [SerializeField] Color normalColor;
        [SerializeField] Color exhaustedColor;

        PlayerMotor _motor;
        Camera _camera;
        RectTransform _rect;
        CanvasGroup _group;
        Canvas _canvas;
        float _fullTime;

        /// <summary>지금 보이는 정도 (0 = 숨김, 1 = 다 보임) — 디버그 · 확인용.</summary>
        public float Alpha => _group != null ? _group.alpha : 0f;

        public void Bind(PlayerMotor motor, Camera viewCamera)
        {
            _motor = motor;
            _camera = viewCamera;
            _rect = (RectTransform)transform;
            _group = GetComponent<CanvasGroup>();
            _canvas = GetComponentInParent<Canvas>();
            _group.alpha = 0f;
            _group.interactable = false;
            _group.blocksRaycasts = false;
            var t = motor != null ? motor.StaminaTuning : null;
            if (t != null) _rect.sizeDelta = new Vector2(t.GaugeSize, t.GaugeSize);
            _fullTime = float.PositiveInfinity; // 가득 찬 채 시작 = 숨김
        }

        void LateUpdate()
        {
            if (_motor == null || _group == null) return;
            Stamina s = _motor.Stamina;
            StaminaTuning t = _motor.StaminaTuning;
            if (s == null || t == null) { _group.alpha = 0f; return; }

            // 보이기 · 숨기기
            if (!s.IsFull) { _fullTime = 0f; _group.alpha = 1f; }
            else
            {
                _fullTime += Time.deltaTime;
                float fade = t.GaugeFadeTime > 0f ? (_fullTime - t.GaugeHideDelay) / t.GaugeFadeTime : (_fullTime >= t.GaugeHideDelay ? 1f : 0f);
                _group.alpha = Mathf.Clamp01(1f - fade);
            }
            if (_group.alpha <= 0f) return;

            // 채움 · 색
            if (fill != null)
            {
                fill.fillAmount = s.Ratio;
                fill.color = s.Exhausted ? exhaustedColor : normalColor;
            }

            // 위치: 캐릭터 위 한 점의 화면 좌표 + 오프셋 (캔버스 크기 비율로)
            if (_camera != null)
            {
                Vector3 world = _motor.SmoothedPosition + Vector3.up * t.GaugeAnchorHeight;
                Vector3 screen = _camera.WorldToScreenPoint(world);
                bool behind = screen.z < 0f;
                float scale = _canvas != null && _canvas.scaleFactor > 0f ? _canvas.scaleFactor : 1f;
                _rect.anchoredPosition = new Vector2(screen.x / scale, screen.y / scale) + t.GaugeScreenOffset;
                if (behind) _group.alpha = 0f;
            }
        }
    

        /// <summary>
        /// 그레이박스 HUD를 만든다 (Setup 메뉴가 부른다 — uGUI 참조가 이 어셈블리에 있어서 여기 둔다):
        /// Overlay 캔버스(화면 크기에 맞춰 비율) + 게이지(배경 링 + 채움 링, 위에서 시작). 루트 이름은 name.
        /// </summary>
        public static StaminaGauge CreateHud(string name, Sprite ring, Vector2 referenceResolution, Color backColor, Color fillColor, Color exhaustedFillColor)
        {
            var root = new GameObject(name, typeof(RectTransform));
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = referenceResolution;
            scaler.matchWidthOrHeight = 0.5f;

            var g = new GameObject("StaminaGauge", typeof(RectTransform), typeof(CanvasGroup));
            var rt = (RectTransform)g.transform;
            rt.SetParent(root.transform, false);
            rt.anchorMin = rt.anchorMax = Vector2.zero; // 캔버스 왼쪽 아래 기준 — LateUpdate가 화면 좌표로 옮긴다
            rt.pivot = new Vector2(0.5f, 0.5f);
            g.GetComponent<CanvasGroup>().alpha = 0f;

            RingImage(rt, "Back", ring, backColor);
            var fillImage = RingImage(rt, "Fill", ring, fillColor);
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Radial360;
            fillImage.fillOrigin = (int)Image.Origin360.Top;
            fillImage.fillClockwise = false;
            fillImage.fillAmount = 1f;

            var gauge = g.AddComponent<StaminaGauge>();
            gauge.fill = fillImage;
            gauge.normalColor = fillColor;
            gauge.exhaustedColor = exhaustedFillColor;
            return gauge;
        }

        static Image RingImage(RectTransform parent, string name, Sprite sprite, Color color)
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
            return img;
        }
}
}
