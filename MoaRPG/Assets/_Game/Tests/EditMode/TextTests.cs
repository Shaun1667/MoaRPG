using System;
using Moa.Core.Localization;
using NUnit.Framework;

namespace Moa.Tests
{
    /// <summary>Text.Get · 자리표시자 · 언어 전환 · 대신 쓰기 (표 파일과 무관한 단위 테스트).</summary>
    public class TextTests
    {
        const string Csv =
            "key,ko,en\n" +
            "t.hello,안녕,Hello\n" +
            "t.place,\"{1}, {0}\",\"{0}, {1}\"\n" +
            "t.only_ko,한국어만,\n" +
            "# comment line\n" +
            "\n" +
            "t.quoted,\"따옴표 \"\"q\"\" 쉼표, 줄\n바꿈\",x\n";

        string _prevLanguage;
        TextTable _prevTable;

        [SetUp]
        public void SetUp()
        {
            _prevTable = Text.Table;
            _prevLanguage = Text.Language;
            Text.Load(TextTable.ParseCsv(Csv), "ko");
        }

        [TearDown]
        public void TearDown()
        {
            if (_prevTable != null) Text.Load(_prevTable, _prevLanguage);
        }

        [Test]
        public void Parse_ReadsHeaderAndRows()
        {
            var t = Text.Table;
            Assert.That(t.Languages, Is.EqualTo(new[] { "ko", "en" }));
            Assert.That(t.DefaultLanguage, Is.EqualTo("ko"));
            Assert.That(t.Keys, Is.EqualTo(new[] { "t.hello", "t.place", "t.only_ko", "t.quoted" }));
        }

        [Test]
        public void Get_ReturnsCurrentLanguage_AndSwitches()
        {
            Assert.That(Text.Get("t.hello"), Is.EqualTo("안녕"));
            Text.SetLanguage("en");
            Assert.That(Text.Get("t.hello"), Is.EqualTo("Hello"));
        }

        [Test]
        public void Get_Placeholders_CanReorder()
        {
            Assert.That(Text.Get("t.place", "A", "B"), Is.EqualTo("B, A"));
            Text.SetLanguage("en");
            Assert.That(Text.Get("t.place", "A", "B"), Is.EqualTo("A, B"));
        }

        [Test]
        public void Get_EmptyCell_FallsBackToDefaultLanguage()
        {
            Text.SetLanguage("en");
            Assert.That(Text.Get("t.only_ko"), Is.EqualTo("한국어만"));
        }

        [Test]
        public void Get_MissingKey_ReturnsMarker()
        {
            Assert.That(Text.Get("t.none"), Is.EqualTo(Text.MissingMarker("t.none")));
        }

        [Test]
        public void Parse_QuotedCell_KeepsCommaQuoteAndNewline()
        {
            Assert.That(Text.Get("t.quoted"), Is.EqualTo("따옴표 \"q\" 쉼표, 줄\n바꿈"));
        }

        [Test]
        public void LanguageChanged_Fires()
        {
            int count = 0;
            Action h = () => count++;
            Text.LanguageChanged += h;
            try { Text.SetLanguage("en"); }
            finally { Text.LanguageChanged -= h; }
            Assert.That(count, Is.EqualTo(1));
        }

        [Test]
        public void SetLanguage_Unknown_Throws()
        {
            Assert.Throws<ArgumentException>(() => Text.SetLanguage("xx"));
        }

        [Test]
        public void Parse_DuplicateKey_Throws()
        {
            Assert.Throws<FormatException>(() => TextTable.ParseCsv("key,ko\na,1\na,2\n"));
        }
    }
}
