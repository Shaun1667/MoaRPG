using System.Collections.Generic;
using Moa.Unity.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace Moa.Unity.UI
{
    /// <summary>
    /// 경직 게이지 임시 막대 (2-3) — 허수아비마다 머리 위(월드 → 화면)에 가로 막대 하나를 HUD 캔버스에 그린다. 글 없음.
    /// 평소 = 쌓인 경직 비율(fillColor), 그로기 = 남은 그로기 시간 비율(groggyColor), 쓰러지면 숨김.
    /// 크기 · 높이 · 색은 Setup 표(표현 값). 막대는 스프라이트 없는 Image(단색). GameRoot가 Bind로 연결한다.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class PoiseBars : MonoBehaviour
    {
        [SerializeField] Color backColor;
        [SerializeField] Color fillColor;
        [SerializeField] Color groggyColor;
        [Tooltip("Bar size, 1920x1080 reference pixels")]
        [SerializeField] Vector2 barSize;
        [Tooltip("m above the dummy feet")]
        [SerializeField] float barHeight;

        sealed class Bar
        {
            public IHitTarget Target;
            public IPoiseTarget Gauge;
            public RectTransform Root;
            public RectTransform Fill;
            public Image FillImage;
        }

        readonly List<Bar> _bars = new List<Bar>();
        Camera _camera;
        Canvas _canvas;

        /// <summary>보이는 막대 수 (디버그 · 확인용).</summary>
        public int VisibleCount { get { int n = 0; foreach (var b in _bars) if (b.Root.gameObject.activeSelf) n++; return n; } }
        /// <summary>i번째 막대가 지금 그리는 비율 (확인용, 없으면 −1).</summary>
        public float ShownRatio(int i) => i >= 0 && i < _bars.Count ? _bars[i].Fill.anchorMax.x : -1f;

        public void Bind(Camera viewCamera, TrainingDummy[] targets) => Bind(viewCamera, targets, null);

        /// <summary>허수아비 막대 뒤에 적 막대 (2-5). i번째 막대 = 허수아비 순서, 그 뒤 적 순서.</summary>
        public void Bind(Camera viewCamera, TrainingDummy[] targets, EnemyAgent[] enemies)
        {
            _camera = viewCamera;
            _canvas = GetComponentInParent<Canvas>();
            foreach (var b in _bars) if (b.Root != null) Destroy(b.Root.gameObject);
            _bars.Clear();
            if (targets != null)
                foreach (var t in targets) if (t != null) _bars.Add(Make(t, t, t.name));
            if (enemies != null)
                foreach (var e in enemies) if (e != null) _bars.Add(Make(e, e, e.name));
        }

        Bar Make(IHitTarget target, IPoiseTarget gauge, string targetName)
        {
            var go = new GameObject("Poise_" + targetName, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(transform, false);
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = barSize;
            var back = go.AddComponent<Image>();
            back.color = backColor;
            back.raycastTarget = false;

            var fillGo = new GameObject("Fill", typeof(RectTransform));
            var fill = (RectTransform)fillGo.transform;
            fill.SetParent(rt, false);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0f, 1f);
            fill.pivot = new Vector2(0f, 0.5f);
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            var img = fillGo.AddComponent<Image>();
            img.color = fillColor;
            img.raycastTarget = false;
            return new Bar { Target = target, Gauge = gauge, Root = rt, Fill = fill, FillImage = img };
        }

        void LateUpdate()
        {
            float scale = _canvas != null && _canvas.scaleFactor > 0f ? _canvas.scaleFactor : 1f;
            foreach (var b in _bars)
            {
                IHitTarget t = b.Target;
                bool show = t != null && (t as Object) != null && t.Alive && b.Gauge.Poise != null && _camera != null;
                Vector3 screen = Vector3.zero;
                if (show)
                {
                    screen = _camera.WorldToScreenPoint(t.Position + Vector3.up * barHeight);
                    show = screen.z > 0f;
                }
                if (b.Root.gameObject.activeSelf != show) b.Root.gameObject.SetActive(show);
                if (!show) continue;
                b.Root.anchoredPosition = new Vector2(screen.x / scale, screen.y / scale);
                var p = b.Gauge.Poise;
                bool groggy = p.Groggy;
                float ratio = groggy
                    ? (p.Rules.GroggyDuration > 0f ? p.GroggyLeft / p.Rules.GroggyDuration : 0f)
                    : p.Ratio;
                b.Fill.anchorMax = new Vector2(Mathf.Clamp01(ratio), 1f);
                b.FillImage.color = groggy ? groggyColor : fillColor;
            }
        }

        /// <summary>HUD 캔버스 아래에 막대 루트를 만든다 (Setup 메뉴가 부른다 — uGUI 참조가 이 어셈블리에 있어서 여기 둔다).</summary>
        public static PoiseBars CreateIn(Transform canvas, Color back, Color fill, Color groggy, Vector2 size, float height)
        {
            var go = new GameObject("PoiseBars", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(canvas, false);
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
            var bars = go.AddComponent<PoiseBars>();
            bars.backColor = back;
            bars.fillColor = fill;
            bars.groggyColor = groggy;
            bars.barSize = size;
            bars.barHeight = height;
            return bars;
        }
    }
}
