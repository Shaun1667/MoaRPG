using System;
using System.Globalization;

namespace Moa.Core.Localization
{
    /// <summary>
    /// 화면 글의 유일한 입구. 게임 코드는 <c>Text.Get(키)</c> / <c>Text.Get(키, 값...)</c>만 부른다 (MY_DEV_GUIDE 1번).
    /// 문장을 이어 붙이지 않고 표에 <c>{0}</c> 자리표시자로 적는다.
    /// 지금 언어 칸이 비면 기본 언어, 키가 없으면 <see cref="MissingMarker"/>를 돌려준다.
    /// </summary>
    public static class Text
    {
        static TextTable _table;
        static string _language;

        /// <summary>언어가 바뀌면 (화면 라벨이 다시 그린다).</summary>
        public static event Action LanguageChanged;

        public static TextTable Table => _table;
        public static string Language => _language;
        public static bool IsLoaded => _table != null;

        public static void Load(TextTable table, string language)
        {
            _table = table ?? throw new ArgumentNullException(nameof(table));
            _language = table.HasLanguage(language) ? language : table.DefaultLanguage;
            LanguageChanged?.Invoke();
        }

        public static void SetLanguage(string language)
        {
            if (_table == null) throw new InvalidOperationException("Text table is not loaded");
            if (!_table.HasLanguage(language)) throw new ArgumentException("Unknown language: " + language, nameof(language));
            if (_language == language) return;
            _language = language;
            LanguageChanged?.Invoke();
        }

        public static string Get(string key)
        {
            if (_table == null) return MissingMarker(key);
            if (_table.TryGet(key, _language, out var text)) return text;
            if (_table.TryGet(key, _table.DefaultLanguage, out text)) return text;
            return MissingMarker(key);
        }

        public static string Get(string key, params object[] args)
        {
            var template = Get(key);
            if (args == null || args.Length == 0) return template;
            return string.Format(CultureInfo.InvariantCulture, template, args);
        }

        /// <summary>표에 없는 키를 화면에서 바로 알아보게 하는 표시.</summary>
        public static string MissingMarker(string key) => "#" + key + "#";
    }
}
