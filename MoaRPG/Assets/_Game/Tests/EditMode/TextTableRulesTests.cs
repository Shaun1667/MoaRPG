using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Moa.Core.Localization;
using NUnit.Framework;
using UnityEngine;

namespace Moa.Tests
{
    /// <summary>
    /// 텍스트 표 규칙 3종 (MY_DEV_GUIDE 1번):
    /// ① 코드 · 데이터 · 씬이 쓰는 키가 표에 모두 있다 ② 모든 키에 기본 언어 문장이 있다 ③ 게임 코드에 한글 문자열이 없다.
    /// 키를 담는 직렬화 필드는 소문자로 시작하고 `Key`로 끝나는 이름(textKey, nameKey ...)이어야 ①이 찾는다.
    /// </summary>
    public class TextTableRulesTests
    {
        const string TablePath = "_Game/Text/strings.csv";
        static readonly string[] GameCodeFolders = { "_Game/Scripts/Core", "_Game/Scripts/Unity" };
        static readonly string[] SerializedExtensions = { ".asset", ".unity", ".prefab" };
        const string GameFolder = "_Game";

        static readonly Regex CodeKeyPattern = new Regex("Text\\.Get\\(\\s*\"([^\"]+)\"");
        static readonly Regex SerializedKeyPattern = new Regex("^\\s*([a-z]\\w*Key):[ \\t]*(\\S+)[ \\t]*\\r?$", RegexOptions.Multiline);
        static readonly Regex DebugLogLine = new Regex("Debug\\.Log");

        static string Abs(string assetsRelative) => Path.Combine(Application.dataPath, assetsRelative);

        static TextTable LoadTable()
        {
            var path = Abs(TablePath);
            Assert.That(File.Exists(path), Is.True, "text table missing: " + path);
            return TextTable.ParseCsv(File.ReadAllText(path, Encoding.UTF8));
        }

        static IEnumerable<string> GameCodeFiles()
        {
            foreach (var folder in GameCodeFolders)
            {
                var dir = Abs(folder);
                if (!Directory.Exists(dir)) continue;
                foreach (var f in Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories)) yield return f;
            }
        }

        [Test]
        public void UsedKeys_AllExistInTable()
        {
            var table = LoadTable();
            var missing = new List<string>();
            int found = 0;

            foreach (var file in GameCodeFiles())
                foreach (Match m in CodeKeyPattern.Matches(File.ReadAllText(file)))
                {
                    found++;
                    if (!table.Contains(m.Groups[1].Value)) missing.Add(m.Groups[1].Value + " (" + Path.GetFileName(file) + ")");
                }

            foreach (var file in Directory.GetFiles(Abs(GameFolder), "*.*", SearchOption.AllDirectories))
            {
                if (System.Array.IndexOf(SerializedExtensions, Path.GetExtension(file)) < 0) continue;
                foreach (Match m in SerializedKeyPattern.Matches(File.ReadAllText(file)))
                {
                    found++;
                    if (!table.Contains(m.Groups[2].Value)) missing.Add(m.Groups[2].Value + " (" + Path.GetFileName(file) + " " + m.Groups[1].Value + ")");
                }
            }

            Assert.That(found, Is.GreaterThan(0), "no text keys found in code or assets - scan pattern broken?");
            Assert.That(missing, Is.Empty, "keys used but not in table:\n" + string.Join("\n", missing));
        }

        [Test]
        public void EveryKey_HasDefaultLanguageText()
        {
            var table = LoadTable();
            var empty = new List<string>();
            foreach (var key in table.Keys)
                if (!table.TryGet(key, table.DefaultLanguage, out _)) empty.Add(key);
            Assert.That(table.Keys.Count, Is.GreaterThan(0));
            Assert.That(empty, Is.Empty, "keys without " + table.DefaultLanguage + " text:\n" + string.Join("\n", empty));
        }

        [Test]
        public void GameCode_HasNoHangulStringLiterals()
        {
            var bad = new List<string>();
            foreach (var file in GameCodeFiles())
            {
                var src = File.ReadAllText(file);
                foreach (var lit in CSharpLiterals.Read(src))
                {
                    if (!CSharpLiterals.HasHangul(lit.Value)) continue;
                    if (DebugLogLine.IsMatch(lit.Line)) continue; // 개발자 로그는 예외
                    bad.Add(Path.GetFileName(file) + ":" + lit.LineNumber + "  \"" + lit.Value + "\"");
                }
            }
            Assert.That(bad, Is.Empty, "Korean text in game code (move to Text/strings.csv):\n" + string.Join("\n", bad));
        }
    }

    /// <summary>C# 소스에서 주석을 빼고 문자열 리터럴만 꺼낸다 (테스트용 간이 분석).</summary>
    public static class CSharpLiterals
    {
        public struct Literal
        {
            public string Value;
            public int LineNumber;
            public string Line;
        }

        public static bool HasHangul(string s)
        {
            foreach (var c in s)
                if ((c >= '가' && c <= '힣') || (c >= 'ᄀ' && c <= 'ᇿ') || (c >= '㄰' && c <= '㆏'))
                    return true;
            return false;
        }

        public static List<Literal> Read(string src)
        {
            var result = new List<Literal>();
            var lines = src.Split('\n');
            int i = 0, line = 1;
            while (i < src.Length)
            {
                char c = src[i];
                char n = i + 1 < src.Length ? src[i + 1] : '\0';
                if (c == '\n') { line++; i++; continue; }
                if (c == '/' && n == '/') { while (i < src.Length && src[i] != '\n') i++; continue; }
                if (c == '/' && n == '*')
                {
                    i += 2;
                    while (i < src.Length && !(src[i] == '*' && i + 1 < src.Length && src[i + 1] == '/')) { if (src[i] == '\n') line++; i++; }
                    i += 2; continue;
                }
                if (c == '\'')
                {
                    i++;
                    while (i < src.Length && src[i] != '\'') { if (src[i] == '\\') i++; i++; }
                    i++; continue;
                }
                bool verbatim = (c == '@' && n == '"') || (c == '$' && n == '@') || (c == '@' && n == '$');
                if (verbatim || c == '"' || (c == '$' && n == '"'))
                {
                    int startLine = line;
                    while (i < src.Length && src[i] != '"') i++;
                    i++;
                    var sb = new StringBuilder();
                    while (i < src.Length)
                    {
                        char ch = src[i];
                        if (verbatim)
                        {
                            if (ch == '"' && i + 1 < src.Length && src[i + 1] == '"') { sb.Append('"'); i += 2; continue; }
                            if (ch == '"') { i++; break; }
                        }
                        else
                        {
                            if (ch == '\\' && i + 1 < src.Length) { sb.Append(ch).Append(src[i + 1]); i += 2; continue; }
                            if (ch == '"' || ch == '\n') { i++; break; }
                        }
                        if (ch == '\n') line++;
                        sb.Append(ch);
                        i++;
                    }
                    result.Add(new Literal { Value = sb.ToString(), LineNumber = startLine, Line = lines[startLine - 1] });
                    continue;
                }
                i++;
            }
            return result;
        }
    }
}
