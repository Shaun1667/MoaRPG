using System;
using System.Collections.Generic;
using System.Text;

namespace Moa.Core.Localization
{
    /// <summary>
    /// 텍스트 표 (키 → 언어별 문장). CSV 첫 줄은 머리줄 `key,ko,en,...` — 첫 언어 열이 기본 언어.
    /// 새 언어 = 열 하나, 새 문장 = 줄 하나. `#`으로 시작하는 키 줄과 빈 줄은 건너뛴다.
    /// 칸에 쉼표 · 줄바꿈 · 따옴표가 있으면 큰따옴표로 감싸고 따옴표는 두 번("") 쓴다.
    /// </summary>
    public sealed class TextTable
    {
        const string KeyColumn = "key";
        const string CommentPrefix = "#";

        readonly string[] _languages;
        readonly Dictionary<string, string[]> _rows;
        readonly List<string> _keys;

        TextTable(string[] languages, Dictionary<string, string[]> rows, List<string> keys)
        {
            _languages = languages;
            _rows = rows;
            _keys = keys;
        }

        public IReadOnlyList<string> Languages => _languages;
        public string DefaultLanguage => _languages[0];
        public IReadOnlyList<string> Keys => _keys;

        public bool HasLanguage(string language) => Array.IndexOf(_languages, language) >= 0;
        public bool Contains(string key) => key != null && _rows.ContainsKey(key);

        /// <summary>그 언어 칸에 문장이 있으면 참 (대신 쓰기 없음).</summary>
        public bool TryGet(string key, string language, out string text)
        {
            text = null;
            if (key == null || !_rows.TryGetValue(key, out var cells)) return false;
            int col = Array.IndexOf(_languages, language);
            if (col < 0 || col >= cells.Length) return false;
            text = cells[col];
            return !string.IsNullOrEmpty(text);
        }

        public static TextTable ParseCsv(string csv)
        {
            if (csv == null) throw new ArgumentNullException(nameof(csv));
            var records = ReadRecords(csv);
            if (records.Count == 0) throw new FormatException("Text table is empty");

            var header = records[0];
            if (header.Count < 2 || header[0].Trim() != KeyColumn)
                throw new FormatException("Text table header must be 'key,<language>,...'");
            var languages = new string[header.Count - 1];
            for (int i = 1; i < header.Count; i++)
            {
                var code = header[i].Trim();
                if (code.Length == 0) throw new FormatException("Empty language code in header");
                if (Array.IndexOf(languages, code) >= 0) throw new FormatException("Duplicate language: " + code);
                languages[i - 1] = code;
            }

            var rows = new Dictionary<string, string[]>(StringComparer.Ordinal);
            var keys = new List<string>();
            for (int r = 1; r < records.Count; r++)
            {
                var rec = records[r];
                var key = rec.Count > 0 ? rec[0].Trim() : string.Empty;
                if (key.Length == 0 || key.StartsWith(CommentPrefix, StringComparison.Ordinal)) continue;
                if (rec.Count > header.Count)
                    throw new FormatException("Too many columns for key: " + key);
                if (rows.ContainsKey(key)) throw new FormatException("Duplicate key: " + key);
                var cells = new string[languages.Length];
                for (int c = 0; c < languages.Length; c++)
                    cells[c] = c + 1 < rec.Count ? rec[c + 1] : string.Empty;
                rows.Add(key, cells);
                keys.Add(key);
            }
            return new TextTable(languages, rows, keys);
        }

        static List<List<string>> ReadRecords(string csv)
        {
            var records = new List<List<string>>();
            var record = new List<string>();
            var field = new StringBuilder();
            bool inQuotes = false;
            bool any = false;
            int i = 0;
            if (csv.Length > 0 && csv[0] == '﻿') i = 1;

            for (; i < csv.Length; i++)
            {
                char ch = csv[i];
                if (inQuotes)
                {
                    if (ch == '"')
                    {
                        if (i + 1 < csv.Length && csv[i + 1] == '"') { field.Append('"'); i++; }
                        else inQuotes = false;
                    }
                    else field.Append(ch);
                    continue;
                }
                switch (ch)
                {
                    case '"': inQuotes = true; any = true; break;
                    case ',': record.Add(field.ToString()); field.Clear(); any = true; break;
                    case '\r': break;
                    case '\n':
                        record.Add(field.ToString()); field.Clear();
                        records.Add(record); record = new List<string>(); any = false;
                        break;
                    default: field.Append(ch); any = true; break;
                }
            }
            if (inQuotes) throw new FormatException("Unclosed quote in text table");
            if (any || field.Length > 0)
            {
                record.Add(field.ToString());
                records.Add(record);
            }
            return records;
        }
    }
}
