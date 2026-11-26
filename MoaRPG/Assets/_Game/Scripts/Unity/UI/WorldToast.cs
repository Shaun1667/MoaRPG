using UnityEngine;
using UnityEngine.UI;
using UiText = UnityEngine.UI.Text;

namespace Moa.Unity.UI
{
    /// <summary>
    /// 탐험 알림 한 줄 (3-6): 위 가운데에 마지막 알림(지점 활성화 · 상자 보상 · 압력판 · 익사)을 showTime 보인 뒤 fadeTime에 사라진다.
    /// 글은 부르는 쪽(GameRoot)이 텍스트 표에서 만든다. 크기 · 색 · 시간은 Setup 표(표현 값).
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class WorldToast : MonoBehaviour
    {
        [SerializeField] UiText label;
        [SerializeField] float showTime;
        [SerializeField] float fadeTime;

        float _age = float.PositiveInfinity;
        Color _color = Color.white;

        public string LastText { get; private set; }
        public int ShowCount { get; private set; }
        public bool Visible => label != null && label.enabled && label.color.a > 0f;

        public void Bind(Font font)
        {
            if (label == null) return;
            if (font != null) label.font = font;
            _color = label.color;
            label.enabled = false;
        }

        /// <summary>알림을 띄운다 (앞의 것을 바꾼다).</summary>
        public void Show(string text)
        {
            LastText = text;
            ShowCount++;
            _age = 0f;
            if (label == null) return;
            label.text = text;
            label.enabled = true;
            label.color = _color;
        }

        void Update()
        {
            if (label == null || !label.enabled) return;
            _age += Time.unscaledDeltaTime;
            if (_age <= showTime) return;
            float t = fadeTime > 0f ? (_age - showTime) / fadeTime : 1f;
            if (t >= 1f) { label.enabled = false; return; }
            var c = _color;
            c.a *= 1f - t;
            label.color = c;
        }

        /// <summary>HUD 캔버스 아래에 만든다 (위 가운데, top = 화면 위에서 px).</summary>
        public static WorldToast CreateIn(Transform canvas, float top, Vector2 size, int fontSize, float showTime, float fadeTime, Color text, Color outline)
        {
            var go = new GameObject("WorldToast", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(canvas, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -top);
            rt.sizeDelta = size;
            var toast = go.AddComponent<WorldToast>();
            toast.label = GrowthScreen.Label(rt, "Label", fontSize, text, outline, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, size);
            toast.label.enabled = false;
            toast.showTime = showTime;
            toast.fadeTime = fadeTime;
            return toast;
        }
    }
}
