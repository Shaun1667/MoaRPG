using System;
using Moa.Core.World;
using Moa.Unity.Player;
using Moa.Unity.World;
using UnityEngine;
using UnityEngine.UI;
using Text = Moa.Core.Localization.Text;
using UiText = UnityEngine.UI.Text;

namespace Moa.Unity.UI
{
    /// <summary>
    /// 지도 화면 (3-6, 간단판) — 키 Map(M)으로 열고 닫는다(열려 있으면 ESC도 닫음, 캐릭터 화면이 열려 있으면 안 열림). 열려 있는 동안 커서가 풀리고 게임 입력은 0 (IPlayerInput.MenuOpen),
    /// 시간은 흐른다. 위에서 본 평면(시험 지형 땅 범위) 위에 순간이동 지점 아이콘 · 이름과 플레이어 표시. 활성화된 지점을 누르면 그리로 순간이동하고 닫힌다
    /// (GameRoot.TeleportToWaypoint — 규칙은 Core WorldProgress.TryTeleport). 꺼진 지점은 버튼이 꺼지고 이름에 "미발견".
    /// 글은 텍스트 표(ui.map.* · 지점 nameKey), 크기 · 색은 Setup 표. GameRoot가 Bind로 연결한다.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class MapScreen : MonoBehaviour
    {
        [SerializeField] GameObject panel;
        [SerializeField] UiText title;
        [SerializeField] UiText hint;
        [Tooltip("Top-down plane (the world area below)")]
        [SerializeField] RectTransform plane;
        [SerializeField] RectTransform playerMark;
        [Tooltip("One per waypoint, same order as GameRoot waypoints")]
        [SerializeField] Button[] buttons = new Button[0];
        [SerializeField] UiText[] labels = new UiText[0];
        [Tooltip("World area shown: center (x, z) and size (m)")]
        [SerializeField] Vector2 worldCenter;
        [SerializeField] float worldSize;

        Waypoint[] _points = new Waypoint[0];
        WorldProgress _world;
        IPlayerInput _input;
        Transform _player;
        Func<string, bool> _teleport;
        bool _dirty = true;

        public bool Open => panel != null && panel.activeSelf;
        public int ButtonCount => buttons != null ? buttons.Length : 0;
        /// <summary>디버그 · 확인용: 누른 수 · 마지막으로 누른 지점이 이동했는지 · 보이는 글 · 버튼이 켜졌는지.</summary>
        public int PressCount { get; private set; }
        public bool LastPressMoved { get; private set; }
        public string LabelShown(int i) => i >= 0 && i < labels.Length && labels[i] != null ? labels[i].text : null;
        public bool ButtonEnabled(int i) => i >= 0 && i < buttons.Length && buttons[i] != null && buttons[i].interactable;
        public Vector2 ButtonPosition(int i) => i >= 0 && i < buttons.Length && buttons[i] != null ? ((RectTransform)buttons[i].transform).anchoredPosition : Vector2.zero;
        public Vector2 PlayerMarkPosition => playerMark != null ? playerMark.anchoredPosition : Vector2.zero;

        /// <param name="teleport">지점 id로 순간이동 (되면 true).</param>
        public void Bind(WorldProgress world, Waypoint[] points, Transform player, IPlayerInput input, Func<string, bool> teleport, Font font)
        {
            if (_world != null) _world.Changed -= MarkDirty;
            _world = world;
            _points = points ?? new Waypoint[0];
            _player = player;
            _input = input;
            _teleport = teleport;
            if (_world != null) _world.Changed += MarkDirty;
            if (font != null)
                foreach (var t in GetComponentsInChildren<UiText>(true)) t.font = font;
            for (int k = 0; k < buttons.Length; k++)
            {
                if (buttons[k] == null) continue;
                int i = k;
                buttons[k].onClick.RemoveAllListeners();
                buttons[k].onClick.AddListener(() => Press(i));
            }
            Text.LanguageChanged -= MarkDirty;
            Text.LanguageChanged += MarkDirty;
            SetOpen(false);
        }

        void OnDestroy()
        {
            if (_world != null) _world.Changed -= MarkDirty;
            Text.LanguageChanged -= MarkDirty;
        }

        void MarkDirty() => _dirty = true;

        void Update()
        {
            // 캐릭터 화면이 열려 있으면 열지 않는다 — 열린 화면만 닫힌다
            if (_input != null && _input.ConsumeMap() && (Open || !_input.MenuOpen)) SetOpen(!Open);
        }

        void LateUpdate()
        {
            if (!Open) return;
            if (_dirty) Refresh();
            if (playerMark != null && _player != null) playerMark.anchoredPosition = ToMap(_player.position);
        }

        /// <summary>[상태 변경] 화면을 열거나 닫는다 (열면 커서를 풀고 게임 입력을 막는다). 월드가 없으면 안 열린다.</summary>
        public void SetOpen(bool open)
        {
            bool was = Open;
            if (panel != null) panel.SetActive(open && _world != null);
            if (_input != null && was != Open) _input.MenuOpen = Open; // 이 화면이 열리거나 닫힐 때만 (다른 메뉴 상태는 건드리지 않는다)
            _dirty = true;
            if (Open) Refresh();
        }

        /// <summary>[상태 변경] i번째 지점 버튼을 누른다 (버튼 · 디버그 공통). 순간이동했으면 true (화면이 닫힌다).</summary>
        public bool Press(int i)
        {
            PressCount++;
            LastPressMoved = false;
            if (i < 0 || i >= _points.Length || _points[i] == null || _teleport == null) return false;
            LastPressMoved = _teleport(_points[i].Id);
            if (LastPressMoved) SetOpen(false);
            _dirty = true;
            return LastPressMoved;
        }

        /// <summary>월드 자리 → 평면 위 자리 (평면 가운데 기준 px, 위 = +z).</summary>
        public Vector2 ToMap(Vector3 world)
        {
            if (plane == null || worldSize <= 0f) return Vector2.zero;
            Vector2 size = plane.rect.size;
            return new Vector2((world.x - worldCenter.x) / worldSize * size.x, (world.z - worldCenter.y) / worldSize * size.y);
        }

        public void Refresh()
        {
            _dirty = false;
            if (title != null) title.text = Text.Get("ui.map.title");
            if (hint != null) hint.text = Text.Get("ui.map.hint");
            for (int i = 0; i < buttons.Length; i++)
            {
                Waypoint p = i < _points.Length ? _points[i] : null;
                if (buttons[i] == null) continue;
                buttons[i].gameObject.SetActive(p != null);
                if (p == null) continue;
                bool on = _world != null && _world.IsActivated(p.Id);
                buttons[i].interactable = on;
                ((RectTransform)buttons[i].transform).anchoredPosition = ToMap(p.transform.position);
                string name = !string.IsNullOrEmpty(p.NameKey) ? Text.Get(p.NameKey) : p.Id;
                if (i < labels.Length && labels[i] != null) labels[i].text = on ? name : Text.Get("ui.map.locked", name);
            }
        }

        // ---- 만들기 (Setup 메뉴가 부른다) ----

        /// <summary>
        /// HUD 캔버스 아래에 지도 화면을 만든다 (자기 Canvas(정렬 덮어쓰기) + GraphicRaycaster). 처음엔 닫혀 있다.
        /// planeSize = 평면 한 변 (px), iconSize = 지점 아이콘, labelSize = 아이콘 위 이름 칸, worldCenter · worldSize = 평면이 덮는 땅 (m).
        /// </summary>
        public static MapScreen CreateIn(Transform canvas, int sortingOrder, int waypointCount, float planeSize, float iconSize, Vector2 labelSize, float headerHeight,
            int titleFontSize, int labelFontSize, Vector2 worldCenter, float worldSize, Color dim, Color window, Color planeColor, Color icon, Color iconDisabled, Color player,
            Color text, Color outline)
        {
            var go = new GameObject("MapScreen", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(canvas, false);
            GrowthScreen.Stretch(rt);
            var c = go.AddComponent<Canvas>();
            c.overrideSorting = true;
            c.sortingOrder = sortingOrder;
            go.AddComponent<GraphicRaycaster>();
            var screen = go.AddComponent<MapScreen>();
            screen.worldCenter = worldCenter;
            screen.worldSize = worldSize;

            var panelGo = new GameObject("Panel", typeof(RectTransform));
            var prt = (RectTransform)panelGo.transform;
            prt.SetParent(rt, false);
            GrowthScreen.Stretch(prt);
            panelGo.AddComponent<Image>().color = dim;
            screen.panel = panelGo;

            float pad = iconSize;
            var winSize = new Vector2(planeSize + pad * 2f, planeSize + pad * 2f + headerHeight * 2f);
            var win = GrowthScreen.Rect(prt, "Window", new Vector2(0.5f, 0.5f), Vector2.zero, winSize);
            win.gameObject.AddComponent<Image>().color = window;
            screen.title = GrowthScreen.Label(win, "Title", titleFontSize, text, outline, TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(pad, -pad * 0.5f),
                new Vector2(winSize.x * 0.5f, headerHeight));
            screen.hint = GrowthScreen.Label(win, "Hint", labelFontSize, text, outline, TextAnchor.MiddleCenter, new Vector2(0.5f, 0f), new Vector2(0f, pad * 0.5f),
                new Vector2(winSize.x - pad * 2f, headerHeight));

            var planeRt = GrowthScreen.Rect(win, "Plane", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(planeSize, planeSize));
            planeRt.gameObject.AddComponent<Image>().color = planeColor;
            screen.plane = planeRt;

            var btns = new Button[waypointCount];
            var lbls = new UiText[waypointCount];
            for (int i = 0; i < waypointCount; i++)
            {
                var b = GrowthScreen.MakeButton(planeRt, "Waypoint" + (i + 1), Vector2.zero, new Vector2(iconSize, iconSize), icon, iconDisabled, labelFontSize, text, outline, out UiText inner);
                var brt = (RectTransform)b.transform;
                brt.anchorMin = brt.anchorMax = brt.pivot = new Vector2(0.5f, 0.5f);
                inner.text = "";
                lbls[i] = GrowthScreen.Label(brt, "Name", labelFontSize, text, outline, TextAnchor.LowerCenter, new Vector2(0.5f, 1f), new Vector2(0f, labelSize.y + 4f), labelSize);
                lbls[i].rectTransform.pivot = new Vector2(0.5f, 1f);
                btns[i] = b;
            }
            screen.buttons = btns;
            screen.labels = lbls;

            var mark = GrowthScreen.Rect(planeRt, "Player", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(iconSize * 0.5f, iconSize * 0.5f));
            mark.pivot = new Vector2(0.5f, 0.5f);
            var markImg = mark.gameObject.AddComponent<Image>();
            markImg.color = player;
            markImg.raycastTarget = false;
            screen.playerMark = mark;
            panelGo.SetActive(false);
            return screen;
        }
    }
}
