using Junior;
using static Tests.TestHelpers;

namespace Tests
{
    [TestClass]
    public class JsonTokenReaderTests
    {
        [TestMethod]
        public async Task TestEmpty()
        {
            await TestReadTokensAsync("");
        }

        [TestMethod]
        public async Task TestKeywords()
        {
            await TestReadTokensAsync("true",
                new TokenInfo(TokenKind.True, "true"));

            await TestReadTokensAsync("false",
                new TokenInfo(TokenKind.False, "false"));

            await TestReadTokensAsync("null",
                new TokenInfo(TokenKind.Null, "null"));
        }

        [TestMethod]
        public async Task TestStrings()
        {
            await TestReadTokensAsync("\"\"", new TokenInfo(TokenKind.String, ""));
            await TestReadTokensAsync("\"abc\"", new TokenInfo(TokenKind.String, "abc"));
            await TestReadTokensAsync("\"ab\\ncd\"", new TokenInfo(TokenKind.String, "ab\ncd"));
        }

        [TestMethod]
        public async Task TestNumbers()
        {
            await TestReadTokensAsync("0",
                new TokenInfo(TokenKind.Number, "0"));
            await TestReadTokensAsync("-0",
                new TokenInfo(TokenKind.Number, "-0"));
            await TestReadTokensAsync("  0",
                new TokenInfo(TokenKind.Number, "0"));
            await TestReadTokensAsync("12",
                new TokenInfo(TokenKind.Number, "12"));
            await TestReadTokensAsync("12.3",
                new TokenInfo(TokenKind.Number, "12.3"));
            await TestReadTokensAsync("12.3e4",
                new TokenInfo(TokenKind.Number, "12.3e4"));
            await TestReadTokensAsync("-12.3e4",
                new TokenInfo(TokenKind.Number, "-12.3e4"));
            await TestReadTokensAsync("12.3e+4",
                new TokenInfo(TokenKind.Number, "12.3e+4"));
            await TestReadTokensAsync("12.3e-4",
                new TokenInfo(TokenKind.Number, "12.3e-4"));
            await TestReadTokensAsync("12345678901234567890",
                new TokenInfo(TokenKind.Number, "12345678901234567890"));
        }

        [TestMethod]
        public async Task TestLists()
        {
            // empty list
            await TestReadTokensAsync("[]",
                new TokenInfo(TokenKind.ListStart, "["),
                new TokenInfo(TokenKind.ListEnd, "]"));

            // list with number
            await TestReadTokensAsync("[123]",
                new TokenInfo(TokenKind.ListStart, "["),
                new TokenInfo(TokenKind.Number, "123"),
                new TokenInfo(TokenKind.ListEnd, "]"));

            // list with multiple numbers
            await TestReadTokensAsync("[1, 2, 3]",
                new TokenInfo(TokenKind.ListStart, "["),
                new TokenInfo(TokenKind.Number, "1"),
                new TokenInfo(TokenKind.Comma, ","),
                new TokenInfo(TokenKind.Number, "2"),
                new TokenInfo(TokenKind.Comma, ","),
                new TokenInfo(TokenKind.Number, "3"),
                new TokenInfo(TokenKind.ListEnd, "]"));
        }

        [TestMethod]
        public async Task TestObjects()
        {
            // no members
            await TestReadTokenTextsAsync(
                "{}",
                "{", "}");

            // one member
            await TestReadTokenTextsAsync(
                "{\"p\": 123}",
                "{", "\"p\"", ":", "123", "}");

            // two members
            await TestReadTokenTextsAsync(
                "{\"a\": 123, \"b\": \"abc\"}",
                "{", "\"a\"", ":", "123", ",", "\"b\"", ":", "\"abc\"", "}");
        }

        [TestMethod]
        public async Task TestLargeStrings()
        {
            await TestLargeValueAsync(
                "\"012345678901234567890123\"",
                "012345678901234567890123");

            await TestLargeValueAsync(
                "\"AAAAAAAAAABBBBBBBBBBCCCCCCCCCCDDDDDDDDDDEEEEEEEEEEFFFFFFFFFFGGGGGGGGGG\"",
                "AAAAAAAAAABBBBBBBBBBCCCCCCCCCCDDDDDDDDDDEEEEEEEEEEFFFFFFFFFFGGGGGGGGGG");

            await TestLargeValueAsync(
                "\"AAAAAAAAAA\\nBBBBBBBBBB\\nCCCCCCCCCC\\nDDDDDDDDDD\\nEEEEEEEEEE\\nFFFFFFFFFF\\nGGGGGGGGGG\"",
                "AAAAAAAAAA\nBBBBBBBBBB\nCCCCCCCCCC\nDDDDDDDDDD\nEEEEEEEEEE\nFFFFFFFFFF\nGGGGGGGGGG");
        }

        [TestMethod]
        public async Task TestLargeStringLengths()
        {
            await TestHugeString(stringSize: 512, bufferSize: 1024);
            await TestHugeString(stringSize: 512, bufferSize: 256);
            await TestHugeString(stringSize: 8000, bufferSize: 256);
            await TestHugeString(stringSize: 1024*1024, bufferSize: 1024);
        }

        private async ValueTask TestHugeString(int stringSize, int bufferSize)
        {
            TestHugeStringSync(stringSize, bufferSize);
            await TestHugeStringAsync(stringSize, bufferSize);
        }

        private void TestHugeStringSync(int stringSize, int bufferSize)
        {
            var textReader = GetJsonWithLargeString("", stringSize, "");
            var tokenReader = JsonTokenReader.Create(textReader, bufferSize);

            int actualSize = 0;
            while (tokenReader.ReadNextSpan())
            {
                actualSize += tokenReader.CurrentValueSpan.Length;
            }

            Assert.AreEqual(stringSize, actualSize, "string size read");
        }

        private async ValueTask TestHugeStringAsync(int stringSize, int bufferSize)
        {
            var textReader = GetJsonWithLargeString("", stringSize, "");
            var tokenReader = JsonTokenReader.Create(textReader, bufferSize);

            int actualSize = 0;
            while (await tokenReader.ReadNextSpanAsync())
            {
                actualSize += tokenReader.CurrentValueSpan.Length;
            }

            Assert.AreEqual(stringSize, actualSize, "string size read");
        }

        [TestMethod]
        public async Task TestLargeNumbers()
        {
            await TestLargeValueAsync(
                "012345678901234567890123");

            await TestLargeValueAsync(
                "012345678901234567890123.5");

            await TestLargeValueAsync(
                "0.12345678901234567890123");

            await TestLargeValueAsync(
                "0.12345678901234567890123e11");

            await TestLargeValueAsync(
                "-0.12345678901234567890123e-11");

            await TestLargeValueAsync(
                "012345678901234567890123.012345678901234567890123e123456789e012345678901234567890123");
        }

        private async Task TestLargeValueAsync(string json, string? expectedValue = null)
        {
            await TestReadTokenValuesAsync(json, 20, expectedValue ?? json);
        }

        private Task TestReadTokensAsync(string json, params TokenInfo[] expectedTokens)
        {
            return TestReadTokensAsync(json, JsonTokenReader.DefaultBufferSize, expectedTokens);
        }

        private async Task TestReadTokensAsync(string json, int bufferSize, params TokenInfo[] expectedTokens)
        {
            var reader = JsonTokenReader.Create(new StringReader(json), bufferSize);

            var actualTokens = new List<TokenInfo>();
            while (reader.HasToken)
            {
                var kind = reader.TokenKind;
                var value = await reader.ReadTokenValueAsync();
                actualTokens.Add(new TokenInfo(kind, value));
            }

            Assert.AreEqual(expectedTokens.Length, actualTokens.Count, "token count");

            for (int i = 0; i < expectedTokens.Length; i++)
            {
                Assert.AreEqual(expectedTokens[i], actualTokens[i], "token");
            }
        }

        private Task TestReadTokenTextsAsync(string json, params string[] expectedTexts)
        {
            return TestReadTokenTextsAsync(json, JsonTokenReader.DefaultBufferSize, expectedTexts);
        }

        private async Task TestReadTokenTextsAsync(string json, int bufferSize, params string[] expectedTexts)
        {
            var reader = JsonTokenReader.Create(new StringReader(json), bufferSize);

            var actualTexts = new List<string>();
            while (reader.HasToken)
            {
                var value = await reader.ReadTokenTextAsync();
                actualTexts.Add(value);
            }

            Assert.AreEqual(expectedTexts.Length, actualTexts.Count, "text count");

            for (int i = 0; i < expectedTexts.Length; i++)
            {
                Assert.AreEqual(expectedTexts[i], actualTexts[i], "token text");
            }
        }

        private Task TestReadTokenValuesAsync(string json, params string[] expectedValues)
        {
            return TestReadTokenValuesAsync(json, JsonTokenReader.DefaultBufferSize, expectedValues);
        }

        private async Task TestReadTokenValuesAsync(string json, int bufferSize, params string[] expectedValues)
        {
            var reader = JsonTokenReader.Create(new StringReader(json), bufferSize);

            var actualValues = new List<string>();
            while (reader.HasToken)
            {
                var value = await reader.ReadTokenValueAsync();
                actualValues.Add(value);
            }

            Assert.AreEqual(expectedValues.Length, actualValues.Count, "value count");

            for (int i = 0; i < expectedValues.Length; i++)
            {
                Assert.AreEqual(expectedValues[i], actualValues[i], "token text");
            }
        }

        private record TokenInfo(TokenKind Kind, string Value);
    }
}