using UnityEngine;

namespace Moa.Unity.Data
{
    /// <summary>
    /// 전역 설정 표 하나 (Assets/_Game/Data/GameConfig.asset). 값은 에셋에만 있고 코드에는 기본값을 두지 않는다.
    /// 시스템별 표는 Data/&lt;분류&gt;/ 아래 따로 만든다 (CLAUDE.md 코딩 규칙).
    /// </summary>
    [CreateAssetMenu(menuName = "MoaRPG/Data/Game Config", fileName = "GameConfig")]
    public sealed class GameConfig : ScriptableObject
    {
        [Header("Run")]
        [Tooltip("Default master seed for a new run")]
        [SerializeField] int defaultSeed;

        [Header("Text")]
        [Tooltip("Text table CSV (key,ko,en)")]
        [SerializeField] TextAsset textTable;
        [Tooltip("Language code used at start (a column of the text table)")]
        [SerializeField] string defaultLanguage;

        [Header("UI Font")]
        [Tooltip("OS font names tried in order for UI text (Korean + Latin glyphs)")]
        [SerializeField] string[] uiOsFontNames;
        [Tooltip("Dynamic OS font base size")]
        [SerializeField] int uiDynamicFontSize;

        public int DefaultSeed => defaultSeed;
        public TextAsset TextTable => textTable;
        public string DefaultLanguage => defaultLanguage;
        public string[] UiOsFontNames => uiOsFontNames;
        public int UiDynamicFontSize => uiDynamicFontSize;
    }
}
