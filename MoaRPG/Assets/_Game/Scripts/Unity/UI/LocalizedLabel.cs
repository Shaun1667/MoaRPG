using Moa.Core.Localization;
using UnityEngine;
using UiText = UnityEngine.UI.Text;

namespace Moa.Unity.UI
{
    /// <summary>
    /// 텍스트 표 키 하나를 보여 주는 라벨. 씬 · 프리팹에는 키만 저장하고 문장은 저장하지 않는다.
    /// 언어가 바뀌면 다시 그린다. 자리표시자 값은 <see cref="SetArgs"/>.
    /// </summary>
    [RequireComponent(typeof(UiText))]
    public sealed class LocalizedLabel : MonoBehaviour
    {
        [Tooltip("Text table key (category.target.use)")]
        [SerializeField] string textKey;

        object[] _args;
        UiText _label;

        public string TextKey => textKey;

        public void SetArgs(params object[] args)
        {
            _args = args;
            Refresh();
        }

        void OnEnable()
        {
            Text.LanguageChanged += Refresh;
            Refresh();
        }

        void OnDisable()
        {
            Text.LanguageChanged -= Refresh;
        }

        public void Refresh()
        {
            if (!Text.IsLoaded) return;
            if (_label == null) _label = GetComponent<UiText>();
            _label.text = _args == null ? Text.Get(textKey) : Text.Get(textKey, _args);
        }
    }
}
