using Moa.Unity.Data;
using UnityEngine;
using UiText = UnityEngine.UI.Text;

namespace Moa.Unity.UI
{
    /// <summary>
    /// 한글 + 영문이 나오는 OS 글꼴(맑은 고딕 등)을 동적 글꼴로 만들어 UI Text에 입힌다 (Pop KoreanUIFont 방식).
    /// 글꼴 파일을 프로젝트에 넣지 않는다. 후보 이름 · 크기는 GameConfig. 못 찾으면 원래 글꼴 그대로.
    /// </summary>
    public static class UiFont
    {
        static Font _font;

        public static Font Get(GameConfig config)
        {
            if (_font != null) return _font;
            if (config == null || config.UiOsFontNames == null || config.UiOsFontNames.Length == 0) return null;
            try { _font = Font.CreateDynamicFontFromOSFont(config.UiOsFontNames, config.UiDynamicFontSize); }
            catch (System.Exception e) { Debug.LogWarning("UiFont: OS font not created - " + e.Message); _font = null; }
            return _font;
        }

        public static void ApplyTo(Transform root, GameConfig config)
        {
            var font = Get(config);
            if (font == null || root == null) return;
            foreach (var t in root.GetComponentsInChildren<UiText>(true)) t.font = font;
        }
    }
}
