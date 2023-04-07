using Junior;
using static Tests.TestHelpers;

namespace Tests
{
    [TestClass]
    public class JsonTypeReaderTests
    {
        [TestMethod]
        public async Task TestAny()
        {
            await TestTypeReaderAsync("\"abc\"", "abc", JsonAnyReader.Instance);
            await TestTypeReaderAsync("123", 123, JsonAnyReader.Instance);
            await TestTypeReaderAsync("true", true, JsonAnyReader.Instance);
            await TestTypeReaderAsync("false", false, JsonAnyReader.Instance);
            await TestTypeReaderAsync("null", null, JsonAnyReader.Instance);
            await TestTypeReaderAsync("[1, 2, 3]", new object[] { 1, 2, 3 }, JsonAnyReader.Instance);
            await TestTypeReaderAsync("{\"x\": 1}", new Dictionary<string, object?> { { "x", 1 } }, JsonAnyReader.Instance);
        }

        [TestMethod]
        public async Task TestString()
        {
            await TestTypeReaderAsync("\"abc\"", "abc");
            await TestTypeReaderAsync("123", "123");
            await TestTypeReaderAsync("true", "true");
            await TestTypeReaderAsync("false", "false");
            await TestTypeReaderAsync<string?>("null", null);
            await TestTypeReaderAsync<string?>("[]", null);
            await TestTypeReaderAsync<string?>("{}", null);
        }

        [TestMethod]
        public async Task TestBool()
        {
            await TestTypeReaderAsync("true", true);
            await TestTypeReaderAsync("false", false);
            await TestTypeReaderAsync("null", false);
            await TestTypeReaderAsync("\"True\"", true);
            await TestTypeReaderAsync("\"true\"", true);
            await TestTypeReaderAsync("\"false\"", false);
            await TestTypeReaderAsync("\"abc\"", false);
            await TestTypeReaderAsync("123", false);
            await TestTypeReaderAsync("0", false);
            await TestTypeReaderAsync("[]", false);
            await TestTypeReaderAsync("{}", false);

            await TestTypeReaderAsync("null", (bool?)null);
        }

        [TestMethod]
        public async Task TestNumbers()
        {
            // byte
            await TestTypeReaderAsync("1", (byte)1);
            await TestTypeReaderAsync($"{byte.MaxValue}", byte.MaxValue);
            await TestTypeReaderAsync($"{byte.MinValue}", byte.MinValue);
            await TestTypeReaderAsync("\"1\"", (byte)1);
            await TestTypeReaderAsync("null", (byte)0);
            await TestTypeReaderAsync("null", (byte?)null);
            await TestTypeReaderAsync("256", (byte)0);  // TODO: deal with parsing failure??

            // int16
            await TestTypeReaderAsync("1", (short)1);
            await TestTypeReaderAsync($"{Int16.MaxValue}", Int16.MaxValue);
            await TestTypeReaderAsync($"{Int16.MinValue}", Int16.MinValue);
            await TestTypeReaderAsync("\"1\"", (short)1);
            await TestTypeReaderAsync("null", (short)0);
            await TestTypeReaderAsync("null", (short?)null);

            // int32
            await TestTypeReaderAsync("1", 1);
            await TestTypeReaderAsync($"{Int32.MaxValue}", Int32.MaxValue);
            await TestTypeReaderAsync($"{Int32.MinValue}", Int32.MinValue);
            await TestTypeReaderAsync("\"1\"", 1);
            await TestTypeReaderAsync("null", (int?)null);

            // int64
            await TestTypeReaderAsync("1", 1L);
            await TestTypeReaderAsync($"{Int64.MaxValue}", Int64.MaxValue);
            await TestTypeReaderAsync($"{Int64.MinValue}", Int64.MinValue);
            await TestTypeReaderAsync("\"1\"", 1L);
            await TestTypeReaderAsync("null", (long)0);
            await TestTypeReaderAsync("null", (long?)null);

            // double
            await TestTypeReaderAsync("1", 1.0);
            await TestTypeReaderAsync("1.0", 1.0);
            await TestTypeReaderAsync("\"1\"", 1.0);
            await TestTypeReaderAsync("\"1.0\"", 1.0);
            await TestTypeReaderAsync("null", (double)0);
            await TestTypeReaderAsync("null", (double?)null);

            // decimal
            await TestTypeReaderAsync("1", 1m);
            await TestTypeReaderAsync("1.0", 1m);
            await TestTypeReaderAsync("\"1\"", 1m);
            await TestTypeReaderAsync("\"1.0\"", 1.0);
            await TestTypeReaderAsync("null", (decimal)0);
            await TestTypeReaderAsync("null", (decimal?)null);
        }

        [TestMethod]
        public async Task TestLists()
        {
            await TestTypeReaderAsync("[1, 2, 3]", new[] { 1, 2, 3 });
            await TestTypeReaderAsync("[1, 2, 3]", new List<int> { 1, 2, 3 });
            await TestTypeReaderAsync<IEnumerable<int>>("[1, 2, 3]", new List<int> { 1, 2, 3 });
            await TestTypeReaderAsync("[1, 2, 3]", new TestAddList<int> { 1, 2, 3 });
            await TestTypeReaderAsync("[1, 2, 3]", new TestListConstructable<int>(new [] { 1, 2, 3 }));
        }

        public class TestAddList<T> : List<T>
        {
            public TestAddList() { }
        }

        public class TestListConstructable<T>
        {
            public TestListConstructable(IEnumerable<T> items)
            {
                this.Values = items.ToList();
            }

            public IReadOnlyList<T> Values { get; }
        }

        [TestMethod]
        public async Task TestJsonValue()
        {
            await TestTypeReaderAsync("true", JsonTrue.Instance, JsonValueReader.Instance);
            await TestTypeReaderAsync("false", JsonFalse.Instance, JsonValueReader.Instance);
            await TestTypeReaderAsync("null", JsonNull.Instance, JsonValueReader.Instance);
            await TestTypeReaderAsync("10", new JsonNumber("10"), JsonValueReader.Instance);
            await TestTypeReaderAsync("\"abc\"", new JsonString("abc"), JsonValueReader.Instance);

            await TestTypeReaderAsync("[1, 2, 3]",
                new JsonList(
                    new JsonNumber("1"),
                    new JsonNumber("2"),
                    new JsonNumber("3")),
                JsonValueReader.Instance);

            await TestTypeReaderAsync(IdNameJsonText,
                new JsonObject(
                    new JsonProperty("id", new JsonNumber("123")),
                    new JsonProperty("name", new JsonString("Mot"))),
                JsonValueReader.Instance);
        }

        [TestMethod]
        public async Task TestClass()
        {
            await TestTypeReaderAsync(IdNameJsonText, new TestInitializedRecord { Id = 123, Name = "Mot" });
            await TestTypeReaderAsync(IdNameJsonText, new TestParameterizedRecord(123, "Mot"));
            await TestTypeReaderAsync(IdNameJsonText, new TestParameterizedAndInitializedRecord(123) { Name = "Mot" });
        }

        private static readonly string IdNameJsonText =
            """
                { 
                    "id" : 123,
                    "name": "Mot"
                }
                """;

        public record TestParameterizedRecord(int Id, string Name);

        public record TestInitializedRecord
        {
            public int Id { get; init; }
            public string Name { get; init; } = null!;
        }

        public record TestParameterizedAndInitializedRecord(int Id)
        {
            public string Name { get; init; } = null!;
        }

        private async ValueTask TestTypeReaderAsync<T>(string json, T? expectedValue, JsonTypeReader<T>? reader = null)
        {
            reader = reader ?? (JsonTypeReader<T>?)JsonTypeReader.GetReader(typeof(T));

            var tokenReader = await JsonTokenReader.CreateAsync(new StringReader(json));
            Assert.IsNotNull(reader);

            var actualValue = await reader.ReadAsync(tokenReader);

            AssertStructurallyEqual(expectedValue, actualValue);

            // also test sync
            TestTypeReaderSync(json, expectedValue, reader);
        }

        private void TestTypeReaderSync<T>(string json, T? expectedValue, JsonTypeReader<T>? reader = null)
        {
            reader = reader ?? (JsonTypeReader<T>?)JsonTypeReader.GetReader(typeof(T));

            var tokenReader = JsonTokenReader.Create(new StringReader(json));
            Assert.IsNotNull(reader);

            var actualValue = reader.Read(tokenReader);

            AssertStructurallyEqual(expectedValue, actualValue);
        }
    }
}
