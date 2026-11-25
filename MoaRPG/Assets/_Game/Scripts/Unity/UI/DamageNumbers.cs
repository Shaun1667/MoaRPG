using System.Collections.Generic;
using Moa.Unity.Data;
using UnityEngine;
using UnityEngine.UI;
using Text = Moa.Core.Localization.Text;
using UiText = UnityEngine.UI.Text;

namespace Moa.Unity.UI
{
    /// <summary>
    /// 피해 숫자 (2-1) — 맞은 자리(월드)에서 떠오르며 사라지는 숫자를 HUD 캔버스에 그린다.
    /// 글은 텍스트 표 키 ui.combat.damage의 {0} 자리에 숫자를 넣는다. 크기 · 시간 · 떠오르는 높이는 CombatTuning(BALANCE 7번), 색은 Setup 표.
    /// 무적 대상(체력이 줄지 않음)은 blockedColor. 숫자 칸은 처음 쓸 때 만들어 다시 쓴다.
    /// RectTransform은 캔버스 왼쪽 아래 기준. GameRoot가 Bind로 연결한다.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class DamageNumbers : MonoBehaviour
    {
        [SerializeField] Color normalColor;
        [SerializeField] Color blockedColor;
        [SerializeField] Color outlineColor;

        sealed class Entry
        {
            public UiText Label;
            public Vector3 World;
            public float Age;
            public float OffsetX;
            public bool Active;
        }

        readonly List<Entry> _entries = new List<Entry>();
        CombatTuning _tuning;
        Camera _camera;
        Canvas _canvas;
        Font _font;
        int _shownCount;

        /// <summary>지금 떠 있는 숫자 수 · 마지막 글 · 지금까지 띄운 수 (디버그 · 확인용).</summary>
        public int ActiveCount { get { int n = 0; foreach (var e in _entries) if (e.Active) n++; return n; } }
        public string LastText { get; private set; }
        public int ShownCount => _shownCount;

        public void Bind(CombatTuning tuning, Camera viewCamera, Font font)
        {
            _tuning = tuning;
            _camera = viewCamera;
            _font = font;
            _canvas = GetComponentInParent<Canvas>();
        }

        public void Show(Vector3 world, int amount, bool blocked)
        {
            if (_tuning == null) return;
            Entry e = Take();
            string s = Text.Get("ui.combat.damage", amount);
            e.Label.text = s;
            e.Label.color = blocked ? blockedColor : normalColor;
            e.Label.fontSize = _tuning.DamageFontSize;
            e.World = world;
            e.Age = 0f;
            e.OffsetX = ((_shownCount % 3) - 1) * _tuning.DamageSpread;
            e.Active = true;
            e.Label.gameObject.SetActive(true);
            LastText = s;
            _shownCount++;
            Place(e);
        }

        Entry Take()
        {
            foreach (var e in _entries) if (!e.Active) return e;
            var go = new GameObject("Number" + _entries.Count, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(transform, false);
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(300f, 80f);
            var label = go.AddComponent<UiText>();
            label.font = _font != null ? _font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.fontStyle = FontStyle.Bold;
            label.raycastTarget = false;
            var outline = go.AddComponent<Outline>();
            outline.effectColor = outlineColor;
            outline.effectDistance = new Vector2(2f, -2f);
            var entry = new Entry { Label = label };
            _entries.Add(entry);
            return entry;
        }

        void LateUpdate()
        {
            if (_tuning == null) return;
            float dt = Time.deltaTime;
            foreach (var e in _entries)
            {
                if (!e.Active) continue;
                e.Age += dt;
                if (e.Age >= _tuning.DamageLifetime)
                {
                    e.Active = false;
                    e.Label.gameObject.SetActive(false);
                    continue;
                }
                Place(e);
            }
        }

        void Place(Entry e)
        {
            float life = _tuning.DamageLifetime > 0f ? _tuning.DamageLifetime : 1f;
            float k = Mathf.Clamp01(e.Age / life);
            Vector3 world = e.World + Vector3.up * (_tuning.DamageRise * k);
            float fadeStart = life - _tuning.DamageFadeTime;
            float alpha = e.Age <= fadeStart || _tuning.DamageFadeTime <= 0f ? 1f : Mathf.Clamp01((life - e.Age) / _tuning.DamageFadeTime);
            Color c = e.Label.color;
            c.a = alpha;
            e.Label.color = c;
            if (_camera == null) return;
            Vector3 screen = _camera.WorldToScreenPoint(world);
            float scale = _canvas != null && _canvas.scaleFactor > 0f ? _canvas.scaleFactor : 1f;
            ((RectTransform)e.Label.transform).anchoredPosition = new Vector2(screen.x / scale + e.OffsetX, screen.y / scale);
            e.Label.enabled = screen.z > 0f;
        }

        /// <summary>HUD 캔버스 아래에 숫자 칸 루트를 만든다 (Setup 메뉴가 부른다 — uGUI 참조가 이 어셈블리에 있어서 여기 둔다).</summary>
        public static DamageNumbers CreateIn(Transform canvas, Color normal, Color blocked, Color outline)
        {
            var go = new GameObject("DamageNumbers", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(canvas, false);
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
            var numbers = go.AddComponent<DamageNumbers>();
            numbers.normalColor = normal;
            numbers.blockedColor = blocked;
            numbers.outlineColor = outline;
            return numbers;
        }
    }
}
