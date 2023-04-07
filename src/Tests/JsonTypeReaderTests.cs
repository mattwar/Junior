using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Junior;

namespace Tests
{
    [TestClass]
    public class JsonTypeReaderTests
    {
        [TestMethod]
        public void TestAny()
        {
            TestTypeReader("\"abc\"", "abc", JsonAnyReader.Instance);
            TestTypeReader("123", 123, JsonAnyReader.Instance);
            TestTypeReader("true", true, JsonAnyReader.Instance);
            TestTypeReader("false", false, JsonAnyReader.Instance);
            TestTypeReader("null", null, JsonAnyReader.Instance);
            TestTypeReader("[1, 2, 3]", new object[] { 1, 2, 3 }, JsonAnyReader.Instance);
            TestTypeReader("{\"x\": 1}", new Dictionary<string, object?> { { "x", 1 } }, JsonAnyReader.Instance);
        }

        [TestMethod]
        public void TestString()
        {
            TestTypeReader("\"abc\"", "abc");
            TestTypeReader("123", "123");
            TestTypeReader("true", "true");
            TestTypeReader("false", "false");
            TestTypeReader<string?>("null", null);
            TestTypeReader<string?>("[]", null);
            TestTypeReader<string?>("{}", null);
        }

        [TestMethod]
        public void TestBool()
        {
            TestTypeReader("true", true);
            TestTypeReader("false", false);
            TestTypeReader("null", false);
            TestTypeReader("\"True\"", true);
            TestTypeReader("\"true\"", true);
            TestTypeReader("\"false\"", false);
            TestTypeReader("\"abc\"", false);
            TestTypeReader("123", false);
            TestTypeReader("0", false);
            TestTypeReader("[]", false);
            TestTypeReader("{}", false);

            TestTypeReader("null", (bool?)null);
        }

        [TestMethod]
        public void TestNumbers()
        {
            // byte
            TestTypeReader("1", (byte)1);
            TestTypeReader($"{byte.MaxValue}", byte.MaxValue);
            TestTypeReader($"{byte.MinValue}", byte.MinValue);
            TestTypeReader("\"1\"", (byte)1);
            TestTypeReader("null", (byte)0);
            TestTypeReader("null", (byte?)null);
            TestTypeReader("256", (byte)0);  // TODO: deal with parsing failure??

            // int16
            TestTypeReader("1", (short)1);
            TestTypeReader($"{Int16.MaxValue}", Int16.MaxValue);
            TestTypeReader($"{Int16.MinValue}", Int16.MinValue);
            TestTypeReader("\"1\"", (short)1);
            TestTypeReader("null", (short)0);
            TestTypeReader("null", (short?)null);

            // int32
            TestTypeReader("1", 1);
            TestTypeReader($"{Int32.MaxValue}", Int32.MaxValue);
            TestTypeReader($"{Int32.MinValue}", Int32.MinValue);
            TestTypeReader("\"1\"", 1);
            TestTypeReader("null", (int?)null);

            // int64
            TestTypeReader("1", 1L);
            TestTypeReader($"{Int64.MaxValue}", Int64.MaxValue);
            TestTypeReader($"{Int64.MinValue}", Int64.MinValue);
            TestTypeReader("\"1\"", 1L);
            TestTypeReader("null", (long)0);
            TestTypeReader("null", (long?)null);

            // double
            TestTypeReader("1", 1.0);
            TestTypeReader("1.0", 1.0);
            TestTypeReader("\"1\"", 1.0);
            TestTypeReader("\"1.0\"", 1.0);
            TestTypeReader("null", (double)0);
            TestTypeReader("null", (double?)null);

            // decimal
            TestTypeReader("1", 1m);
            TestTypeReader("1.0", 1m);
            TestTypeReader("\"1\"", 1m);
            TestTypeReader("\"1.0\"", 1.0);
            TestTypeReader("null", (decimal)0);
            TestTypeReader("null", (decimal?)null);
        }

        [TestMethod]
        public void TestLists()
        {
            TestTypeReader("[1, 2, 3]", new[] { 1, 2, 3 });
            TestTypeReader("[1, 2, 3]", new List<int> { 1, 2, 3 });
            TestTypeReader<IEnumerable<int>>("[1, 2, 3]", new List<int> { 1, 2, 3 });
            TestTypeReader("[1, 2, 3]", new TestAddList<int> { 1, 2, 3 });
            TestTypeReader("[1, 2, 3]", new TestListConstructable<int>(new [] { 1, 2, 3 }));
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


        private void TestTypeReader<T>(string json, T? expectedValue, JsonTypeReader<T>? reader = null)
        {
            var tokenReader = JsonTokenReader.Create(new StringReader(json));
            reader = reader ?? (JsonTypeReader<T>?)JsonTypeReader.GetReader(typeof(T));
            Assert.IsNotNull(reader);

            var actualValue = reader.Read(tokenReader);

            AssertStructurallyEqual(expectedValue, actualValue);
        }

        private static void AssertStructurallyEqual(object? expected, object? actual)
        {
            if (object.Equals(expected, actual)) 
                return;

            if (expected == null)
                Assert.Fail($"Expected null not: {actual}");

            var expectedType = expected.GetType();
            var actualType = actual?.GetType();
            Assert.AreEqual(expectedType, actualType, "type");

            if (expected.GetType().IsPrimitive)
            {
                Assert.AreEqual(expected, actual);
            }
            else if (expected is IEnumerable expectedIE 
                && actual is IEnumerable actualIE)
            {
                var expectedList = expectedIE.OfType<object>().ToList();
                var actualList = actualIE.OfType<object>().ToList();
                Assert.AreEqual(expectedList.Count, actualList.Count, "list count");
                for (int i = 0; i < expectedList.Count; i++)
                {
                    AssertStructurallyEqual(expectedList[i], actualList[i]);
                }
            }
            else if (expected is IDictionary expectedD
                && actual is IDictionary actualD)
            {
                Assert.AreEqual(expectedD.Values.Count, actualD.Values.Count, "dictionary count");
                foreach (var key in expectedD.Keys.OfType<object>())
                {
                    var expectedValue = expectedD[key];
                    var actualValue = actualD[key];
                    AssertStructurallyEqual(expectedValue, actualValue);
                }
            }
            else
            {
                var props = expectedType.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                foreach (var p in props)
                {
                    var expectedPropValue = p.GetValue(expected, null);
                    var actualPropValue = p.GetValue(actual, null);
                    AssertStructurallyEqual(expectedPropValue, actualPropValue);
                }
            }
        }
    }
}
