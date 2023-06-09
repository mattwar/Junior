﻿using Junior;
using System.Collections;
using System.Collections.Immutable;
using System.Text;
using static Tests.Helpers.TestHelpers;

namespace Tests
{
    [TestClass]
    public class JsonTypeReaderTests
    {
        [TestMethod]
        public async Task TestAnyReader()
        {
            await TestTypeReader("\"abc\"", "abc", JsonAnyReader.Instance);
            await TestTypeReader("123", 123, JsonAnyReader.Instance);
            await TestTypeReader("true", true, JsonAnyReader.Instance);
            await TestTypeReader("false", false, JsonAnyReader.Instance);
            await TestTypeReader("null", null, JsonAnyReader.Instance);
            await TestTypeReader("[1, 2, 3]", new object[] { 1, 2, 3 }, JsonAnyReader.Instance);
            await TestTypeReader("{\"x\": 1}", new Dictionary<string, object?> { { "x", 1 } }, JsonAnyReader.Instance);
        }

        [TestMethod]
        public async Task TestStringReader()
        {
            await TestTypeReader("\"abc\"", "abc", JsonStringReader.Instance);
            await TestTypeReader("123", "123", JsonStringReader.Instance);
            await TestTypeReader("true", "true", JsonStringReader.Instance);
            await TestTypeReader("false", "false", JsonStringReader.Instance);
            await TestTypeReader("null", null, JsonStringReader.Instance);
            await TestTypeReader("[]", "[]", JsonStringReader.Instance);
            await TestTypeReader("{}", "{}", JsonStringReader.Instance);
        }

        [TestMethod]
        public async Task TestBoolReader()
        {
            await TestTypeReader("true", true, JsonBoolReader.Instance);
            await TestTypeReader("false", false, JsonBoolReader.Instance);
            await TestTypeReader("null", false, JsonBoolReader.Instance);
            await TestTypeReader("\"True\"", true, JsonBoolReader.Instance);
            await TestTypeReader("\"true\"", true, JsonBoolReader.Instance);
            await TestTypeReader("\"false\"", false, JsonBoolReader.Instance);
            await TestTypeReader("\"abc\"", false, JsonBoolReader.Instance);
            await TestTypeReader("123", false, JsonBoolReader.Instance);
            await TestTypeReader("0", false, JsonBoolReader.Instance);
            await TestTypeReader("[]", false, JsonBoolReader.Instance);
            await TestTypeReader("{}", false, JsonBoolReader.Instance);
            await TestTypeReader("null", null, JsonBoolReader.NullableInstance);
        }

        [TestMethod]
        public async Task TestNumberReaders()
        {
            // byte
            await TestInferredReader("1", (byte)1);
            await TestInferredReader($"{byte.MaxValue}", byte.MaxValue);
            await TestInferredReader($"{byte.MinValue}", byte.MinValue);
            await TestInferredReader("\"1\"", (byte)1);
            await TestInferredReader("null", (byte)0);
            await TestInferredReader("null", (byte?)null);
            await TestInferredReader("256", (byte)0);  // TODO: deal with parsing failure??

            // int16
            await TestInferredReader("1", (short)1);
            await TestInferredReader($"{Int16.MaxValue}", Int16.MaxValue);
            await TestInferredReader($"{Int16.MinValue}", Int16.MinValue);
            await TestInferredReader("\"1\"", (short)1);
            await TestInferredReader("null", (short)0);
            await TestInferredReader("null", (short?)null);

            // int32
            await TestInferredReader("1", 1);
            await TestInferredReader($"{Int32.MaxValue}", Int32.MaxValue);
            await TestInferredReader($"{Int32.MinValue}", Int32.MinValue);
            await TestInferredReader("\"1\"", 1);
            await TestInferredReader("null", (int?)null);

            // int64
            await TestInferredReader("1", 1L);
            await TestInferredReader($"{Int64.MaxValue}", Int64.MaxValue);
            await TestInferredReader($"{Int64.MinValue}", Int64.MinValue);
            await TestInferredReader("\"1\"", 1L);
            await TestInferredReader("null", (long)0);
            await TestInferredReader("null", (long?)null);

            // double
            await TestInferredReader("1", 1.0);
            await TestInferredReader("1.0", 1.0);
            await TestInferredReader("\"1\"", 1.0);
            await TestInferredReader("\"1.0\"", 1.0);
            await TestInferredReader("null", (double)0);
            await TestInferredReader("null", (double?)null);

            // decimal
            await TestInferredReader("1", 1m);
            await TestInferredReader("1.0", 1m);
            await TestInferredReader("\"1\"", 1m);
            await TestInferredReader("\"1.0\"", 1.0);
            await TestInferredReader("null", (decimal)0);
            await TestInferredReader("null", (decimal?)null);

            // other junk
            await TestInferredReader("true", 0);
            await TestInferredReader("false", 0);
            await TestInferredReader<int>("[]", 0);
            await TestInferredReader<int>("{}", 0);
        }

        [TestMethod]
        public async Task TestListReader()
        {
            await TestInferredReader("[1, 2, 3]", new[] { 1, 2, 3 });
            await TestInferredReader("[1, 2, 3]", new List<int> { 1, 2, 3 });
            await TestInferredReader<IEnumerable<int>>("[1, 2, 3]", new List<int> { 1, 2, 3 });
            await TestInferredReader("[1, 2, 3]", new TestAddList<int> { 1, 2, 3 });
            await TestInferredReader("[1, 2, 3]", new TestListConstructable<int>(new[] { 1, 2, 3 }));
            await TestInferredReader("[1, 2, 3]", ImmutableArray<int>.Empty.AddRange(new[] { 1, 2, 3 }));
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
        public async Task TestDictionaryReader()
        {
            await TestInferredReader(IdNameJsonText, new Dictionary<string, object> { ["id"] = 123, ["name"] = "Mot" });
            await TestInferredReader<IReadOnlyDictionary<string, object>>(IdNameJsonText, new Dictionary<string, object> { ["id"] = 123, ["name"] = "Mot" });
            await TestInferredReader<IDictionary<string, object>>(IdNameJsonText, new Dictionary<string, object> { ["id"] = 123, ["name"] = "Mot" });
            await TestInferredReader<IDictionary>(IdNameJsonText, new Dictionary<string, object> { ["id"] = 123, ["name"] = "Mot" });
            await TestInferredReader(IdNameJsonText, ImmutableDictionary<string, object>.Empty.Add("id", 123).Add("name", "Mot"));
        }

        [TestMethod]
        public async Task TestValueReader()
        {
            await TestTypeReader("true", JsonTrue.Instance, JsonValueReader.Instance);
            await TestTypeReader("false", JsonFalse.Instance, JsonValueReader.Instance);
            await TestTypeReader("null", JsonNull.Instance, JsonValueReader.Instance);
            await TestTypeReader("10", new JsonNumber("10"), JsonValueReader.Instance);
            await TestTypeReader("\"abc\"", new JsonString("abc"), JsonValueReader.Instance);

            await TestTypeReader("[1, 2, 3]",
                new JsonList(
                    new JsonNumber("1"),
                    new JsonNumber("2"),
                    new JsonNumber("3")),
                JsonValueReader.Instance);

            await TestTypeReader(IdNameJsonText,
                new JsonObject(
                    new JsonProperty("id", new JsonNumber("123")),
                    new JsonProperty("name", new JsonString("Mot"))),
                JsonValueReader.Instance);
        }

        [TestMethod]
        public async Task TestClassReader()
        {
            await TestInferredReader(IdNameJsonText, new InitializedRecord { Id = 123, Name = "Mot" });
            await TestInferredReader(IdNameJsonText, new ParameterizedRecord(123, "Mot"));
            await TestInferredReader(IdNameJsonText, new ParameterizedAndInitializedRecord(123) { Name = "Mot" });
            await TestInferredReader(IdNameJsonText, new InitializedFieldRecord { Id = 123, Name = "Mot" });
            await TestInferredReader(IdNameJsonText, new DefaultValueRecord(123, "Mot", 0.1m));
        }

        private static readonly string IdNameJsonText =
            """
            { 
                "id" : 123,
                "name": "Mot"
            }
            """;

        public record ParameterizedRecord(int Id, string Name);

        public record InitializedRecord
        {
            public int Id { get; init; }
            public string Name { get; init; } = null!;
        }

        public record ParameterizedAndInitializedRecord(int Id)
        {
            public string Name { get; init; } = null!;
        }

        public record InitializedFieldRecord
        {
            public int Id;
            public string Name = null!;
        }

        public record DefaultValueRecord(int Id, string Name, decimal Discount = 0.1m);


        [TestMethod]
        public async Task TestDeferredReader()
        {
            await TestInferredReader(CyclicJsonText,
                new CyclicRecord(123, "Mot",
                    new[]
                    {
                        new CyclicRecord(456, "Tom", null),
                        new CyclicRecord(789, "May", null)
                    }));
        }

        public record CyclicRecord(int Id, string Name, IReadOnlyList<CyclicRecord>? Reports);

        private static readonly string CyclicJsonText =
            """
            { 
                "id" : 123,
                "name": "Mot",
                "reports": 
                [
                    {
                        "id": 456,
                        "name": "Tom"
                    },
                    {
                        "id": 789,
                        "name": "May"
                    }
                ]
            }
            """;

        [TestMethod]
        public void TestStringBuilder()
        {
            TestStringBuilderLength(512);
            TestStringBuilderLength(512, 32);
            TestStringBuilderLength(8000);
            TestStringBuilderLength(1024*1024);
        }

        private void TestStringBuilderLength(int stringSize, int bufferSize = JsonTokenReader.DefaultBufferSize)
        {
            var textReader = GetJsonWithLargeString("", stringSize, "");
            var tokenReader = JsonTokenReader.Create(textReader, bufferSize);
            var typeReader = JsonStringBuilderReader.Instance;
            var builder = typeReader.Read(tokenReader);
            Assert.IsNotNull(builder);
            Assert.AreEqual(stringSize, builder.Length, "string size");
        }

        [TestMethod]
        public void TestLargeString()
        {
            TestLargeString(512);
            TestLargeString(512, 32);
            TestLargeString(8000);
            TestLargeString(1024 * 1024);
        }

        private void TestLargeString(int stringSize, int bufferSize = JsonTokenReader.DefaultBufferSize)
        {
            var textReader = GetJsonWithLargeString("", stringSize, "");
            var tokenReader = JsonTokenReader.Create(textReader, bufferSize);
            var typeReader = JsonLargeStringReader.Instance;
            var largeString = typeReader.Read(tokenReader);
            Assert.IsNotNull(largeString);
            Assert.AreEqual(stringSize, largeString.Length, "large string size");
        }

        private async ValueTask TestTypeReader<T>(string json, object? expectedValue, JsonTypeReader<T> typeReader)
        {
            var tokenReader = JsonTokenReader.Create(new StringReader(json));
            TestTypeReaderSync(tokenReader, expectedValue, typeReader);

            tokenReader = JsonTokenReader.Create(new StringReader(json));
            await TestTypeReaderAsync<T>(tokenReader, expectedValue, typeReader);
        }

        private ValueTask TestInferredReader<T>(string json, T expectedValue)
        {
            var reader = (JsonTypeReader<T>?)JsonTypeReader.GetReader(typeof(T));
            Assert.IsNotNull(reader);
            return TestTypeReader(json, expectedValue, reader);
        }

        private async ValueTask TestTypeReader<T>(IEnumerable<string> jsonParts, object? expectedValue, JsonTypeReader<T> typeReader)
        {
            var tokenReader = JsonTokenReader.Create(new EnumeratorReader(jsonParts.GetEnumerator()));
            await TestTypeReaderAsync<T>(tokenReader, expectedValue, typeReader);

            tokenReader = JsonTokenReader.Create(new EnumeratorReader(jsonParts.GetEnumerator()));
            TestTypeReaderSync(tokenReader, expectedValue, typeReader);
        }

        private ValueTask TestInferredReader<T>(IEnumerable<string> jsonParts, T expectedValue)
        {
            var reader = (JsonTypeReader<T>?)JsonTypeReader.GetReader(typeof(T));
            Assert.IsNotNull(reader);
            return TestTypeReader(jsonParts, expectedValue, reader);
        }

        private async ValueTask TestTypeReaderAsync<T>(
            JsonTokenReader tokenReader, object? expectedValue, JsonTypeReader<T> typeReader)
        {
            var actualValue = await typeReader.ReadAsync(tokenReader);
            AssertStructurallyEqual(expectedValue, actualValue);
        }

        private void TestTypeReaderSync<T>(JsonTokenReader tokenReader, object? expectedValue, JsonTypeReader<T> typeReader)
        {
            var actualValue = typeReader!.Read(tokenReader);
            AssertStructurallyEqual(expectedValue, actualValue);
        }
    }
}
