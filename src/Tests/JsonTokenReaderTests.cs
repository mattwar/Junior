using Junior;
using static Tests.Helpers.TestHelpers;

namespace Tests
{
    [TestClass]
    public class JsonTokenReaderTests
    {
        [TestMethod]
        public async Task TestReadTokenValue_Empty()
        {
            await TestReadTokens("");
        }

        [TestMethod]
        public async Task TestReadTokenValue_Keywords()
        {
            await TestReadTokens("true",
                new TokenInfo(TokenKind.True, "true"));

            await TestReadTokens("false",
                new TokenInfo(TokenKind.False, "false"));

            await TestReadTokens("null",
                new TokenInfo(TokenKind.Null, "null"));
        }

        [TestMethod]
        public async Task TestReadTokenValue_Strings()
        {
            await TestReadTokens("\"\"", new TokenInfo(TokenKind.String, ""));
            await TestReadTokens("\"abc\"", new TokenInfo(TokenKind.String, "abc"));
            await TestReadTokens("\"ab\\ncd\"", new TokenInfo(TokenKind.String, "ab\ncd"));
        }

        [TestMethod]
        public async Task TestReadTokenValue_Numbers()
        {
            await TestReadTokens("0",
                new TokenInfo(TokenKind.Number, "0"));
            await TestReadTokens("-0",
                new TokenInfo(TokenKind.Number, "-0"));
            await TestReadTokens("  0",
                new TokenInfo(TokenKind.Number, "0"));
            await TestReadTokens("12",
                new TokenInfo(TokenKind.Number, "12"));
            await TestReadTokens("12.3",
                new TokenInfo(TokenKind.Number, "12.3"));
            await TestReadTokens("12.3e4",
                new TokenInfo(TokenKind.Number, "12.3e4"));
            await TestReadTokens("-12.3e4",
                new TokenInfo(TokenKind.Number, "-12.3e4"));
            await TestReadTokens("12.3e+4",
                new TokenInfo(TokenKind.Number, "12.3e+4"));
            await TestReadTokens("12.3e-4",
                new TokenInfo(TokenKind.Number, "12.3e-4"));
            await TestReadTokens("12345678901234567890",
                new TokenInfo(TokenKind.Number, "12345678901234567890"));
        }

        [TestMethod]
        public async Task TestReadTokenValue_Lists()
        {
            // empty list
            await TestReadTokens("[]",
                new TokenInfo(TokenKind.ListStart, "["),
                new TokenInfo(TokenKind.ListEnd, "]"));

            // list with number
            await TestReadTokens("[123]",
                new TokenInfo(TokenKind.ListStart, "["),
                new TokenInfo(TokenKind.Number, "123"),
                new TokenInfo(TokenKind.ListEnd, "]"));

            // list with multiple numbers
            await TestReadTokens("[1, 2, 3]",
                new TokenInfo(TokenKind.ListStart, "["),
                new TokenInfo(TokenKind.Number, "1"),
                new TokenInfo(TokenKind.Comma, ","),
                new TokenInfo(TokenKind.Number, "2"),
                new TokenInfo(TokenKind.Comma, ","),
                new TokenInfo(TokenKind.Number, "3"),
                new TokenInfo(TokenKind.ListEnd, "]"));
        }

        [TestMethod]
        public async Task TestReadTokenValue_Objects()
        {
            // no members
            await TestReadTokenValue(
                "{}",
                "{", "}");

            // one member
            await TestReadTokenValue(
                "{\"p\": 123}",
                "{", "p", ":", "123", "}");

            // two members
            await TestReadTokenValue(
                "{\"a\": 123, \"b\": \"abc\"}",
                "{", "a", ":", "123", ",", "b", ":", "abc", "}");
        }

        [TestMethod]
        public async Task TestReadTokenValue_LargeStrings()
        {
            await TestReadTokenValueInSmallBuffer(
                "\"012345678901234567890123\"",
                "012345678901234567890123");

            await TestReadTokenValueInSmallBuffer(
                "\"AAAAAAAAAABBBBBBBBBBCCCCCCCCCCDDDDDDDDDDEEEEEEEEEEFFFFFFFFFFGGGGGGGGGG\"",
                "AAAAAAAAAABBBBBBBBBBCCCCCCCCCCDDDDDDDDDDEEEEEEEEEEFFFFFFFFFFGGGGGGGGGG");

            await TestReadTokenValueInSmallBuffer(
                "\"AAAAAAAAAA\\nBBBBBBBBBB\\nCCCCCCCCCC\\nDDDDDDDDDD\\nEEEEEEEEEE\\nFFFFFFFFFF\\nGGGGGGGGGG\"",
                "AAAAAAAAAA\nBBBBBBBBBB\nCCCCCCCCCC\nDDDDDDDDDD\nEEEEEEEEEE\nFFFFFFFFFF\nGGGGGGGGGG");
        }

        [TestMethod]
        public async Task TestLargeNumbers()
        {
            await TestReadTokenValueInSmallBuffer(
                "012345678901234567890123");

            await TestReadTokenValueInSmallBuffer(
                "012345678901234567890123.5");

            await TestReadTokenValueInSmallBuffer(
                "0.12345678901234567890123");

            await TestReadTokenValueInSmallBuffer(
                "0.12345678901234567890123e11");

            await TestReadTokenValueInSmallBuffer(
                "-0.12345678901234567890123e-11");

            await TestReadTokenValueInSmallBuffer(
                "012345678901234567890123.012345678901234567890123e123456789e012345678901234567890123");
        }

        [TestMethod]
        public async Task TestReadNextTokenChunk_LargeStrings()
        {
            await TestReadNextTokenChunk(stringSize: 512, bufferSize: 1024);
            await TestReadNextTokenChunk(stringSize: 512, bufferSize: 256);
            await TestReadNextTokenChunk(stringSize: 8000, bufferSize: 256);
            await TestReadNextTokenChunk(stringSize: 1024 * 1024, bufferSize: 1024);
        }

        [TestMethod]
        public void TestReadElementText()
        {
            TestReadElementText("1");
            TestReadElementText("\"abc\"");
            TestReadElementText("true");
            TestReadElementText("false");
            TestReadElementText("null");

            TestReadElementText(
                "[1, 2, 3]");

            TestReadElementText(
                "  [1, 2, 3]  ",
                "[1, 2, 3]");

            TestReadElementText(
                """{ "a": [1, 2, 3], "b": { "z": 25 } }""");

            TestReadElementText(
                """
                { $"a": [1, 2, 3], "b": { "z": 25 } }
                """, 
                "\"a\"");

            TestReadElementText(
                """
                { "a": $[1, 2, 3], "b": { "z": 25 } }
                """,
                "[1, 2, 3]");

            TestReadElementText(
                """
                { "a": [1, 2, 3], "b": ${ "z": 25 } }
                """,
                """
                { "z": 25 }
                """);

            TestReadElementText(
                """
                { "a": [1, 2, 3], "b": { "z": $25 } }
                """, 
                "25");
        }

        #region test processors

        private static void TestReadElementText(string json, string? expectedText = null)
        {
            int position;
            (json, position) = StripMarker(json);
            expectedText = expectedText ?? json;

            var tokenReader = JsonTokenReader.Create(new StringReader(json));
            
            // move up to marker position within the token stream
            while (tokenReader.Position < position)
                tokenReader.MoveToNextToken();

            var actualText = tokenReader.ReadElementText();
            Assert.AreEqual(expectedText, actualText, "element text");
        }

        private static (string textWithoutMarker, int markerPosition) StripMarker(string textWithMarker, string marker = "$")
        {
            var index = textWithMarker.IndexOf(marker);
            if (index == -1)
            {
                return (textWithMarker, 0);
            }
            else if (index == 0)
            {
                return (textWithMarker.Substring(1), 0);
            }
            else if (index == textWithMarker.Length - 1)
            {
                return (textWithMarker.Substring(0, index), index);
            }
            {
                var textWithoutMarker = textWithMarker.Substring(0, index) + textWithMarker.Substring(index + 1);
                return (textWithoutMarker, index);
            }
        }

        private static async ValueTask TestReadNextTokenChunk(int stringSize, int bufferSize)
        {
            TestSync();
            await TestAsync();

            void TestSync()
            {
                var textReader = GetJsonWithLargeString("", stringSize, "");
                var tokenReader = JsonTokenReader.Create(textReader, bufferSize);

                int actualSize = 0;
                while (tokenReader.ReadNextTokenChunk())
                {
                    actualSize += tokenReader.CurrentValueChunk.Length;
                }

                Assert.AreEqual(stringSize, actualSize, "string size read");
            }

            async ValueTask TestAsync()
            {
                var textReader = GetJsonWithLargeString("", stringSize, "");
                var tokenReader = JsonTokenReader.Create(textReader, bufferSize);

                int actualSize = 0;
                while (await tokenReader.ReadNextTokenChunkAsync())
                {
                    actualSize += tokenReader.CurrentValueChunk.Length;
                }

                Assert.AreEqual(stringSize, actualSize, "string size read");
            }
        }

        private static Task TestReadTokens(string json, params TokenInfo[] expectedTokens)
        {
            return TestReadTokens(json, JsonTokenReader.DefaultBufferSize, expectedTokens);
        }

        private static async Task TestReadTokens(string json, int bufferSize, params TokenInfo[] expectedTokens)
        {
            TestSync();
            await TestAsync();

            async Task TestAsync()
            {
                var reader = JsonTokenReader.Create(new StringReader(json), bufferSize);

                var actualTokens = new List<TokenInfo>();
                while (reader.HasToken)
                {
                    var kind = reader.TokenKind;
                    var value = await reader.ReadTokenValueAsync();
                    actualTokens.Add(new TokenInfo(kind, value));
                }

                AssertStructurallyEqual(expectedTokens, actualTokens);
            }

            void TestSync()
            {
                var reader = JsonTokenReader.Create(new StringReader(json), bufferSize);

                var actualTokens = new List<TokenInfo>();
                while (reader.HasToken)
                {
                    var kind = reader.TokenKind;
                    var value = reader.ReadTokenValue();
                    actualTokens.Add(new TokenInfo(kind, value));
                }

                AssertStructurallyEqual(expectedTokens, actualTokens);
            }
        }

        private static Task TestReadTokenText(string json, params string[] expectedTexts)
        {
            return TestReadTokenTexts(json, JsonTokenReader.DefaultBufferSize, expectedTexts);
        }

        private static async Task TestReadTokenTexts(string json, int bufferSize, params string[] expectedTexts)
        {
            TestSync();
            await TestAsync();

            void TestSync()
            {
                var reader = JsonTokenReader.Create(new StringReader(json), bufferSize);

                var actualTexts = new List<string>();
                while (reader.HasToken)
                {
                    var value = reader.ReadTokenText();
                    actualTexts.Add(value);
                }

                AssertStructurallyEqual(expectedTexts, actualTexts);
            }

            async Task TestAsync()
            {
                var reader = JsonTokenReader.Create(new StringReader(json), bufferSize);

                var actualTexts = new List<string>();
                while (reader.HasToken)
                {
                    var value = await reader.ReadTokenTextAsync();
                    actualTexts.Add(value);
                }

                AssertStructurallyEqual(expectedTexts, actualTexts);
            }
        }

        private static async Task TestReadTokenValueInSmallBuffer(string json, string? expectedValue = null)
        {
            await TestReadTokenValue(json, 20, expectedValue ?? json);
        }

        private static Task TestReadTokenValue(string json, params string[] expectedValues)
        {
            return TestReadTokenValue(json, JsonTokenReader.DefaultBufferSize, expectedValues);
        }

        private static async Task TestReadTokenValue(string json, int bufferSize, params string[] expectedValues)
        {
            TestSync();
            await TestAsync();

            void TestSync()
            {
                var reader = JsonTokenReader.Create(new StringReader(json), bufferSize);

                var actualValues = new List<string>();
                while (reader.HasToken)
                {
                    var value = reader.ReadTokenValue();
                    actualValues.Add(value);
                }

                AssertStructurallyEqual(expectedValues, actualValues);
            }

            async Task TestAsync()
            {
                var reader = JsonTokenReader.Create(new StringReader(json), bufferSize);

                var actualValues = new List<string>();
                while (reader.HasToken)
                {
                    var value = await reader.ReadTokenValueAsync();
                    actualValues.Add(value);
                }

                AssertStructurallyEqual(expectedValues, actualValues);
            }
        }

        private record TokenInfo(TokenKind Kind, string Value);

        #endregion
    }
}