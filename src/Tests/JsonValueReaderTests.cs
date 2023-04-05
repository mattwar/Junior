using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Junior;

namespace Tests
{
    [TestClass]
    public class JsonValueReaderTests
    {
        [TestMethod]
        public async Task TestEmpty()
        {
            await TestReadNextJsonValueAsync("", null);
            await TestReadNextJsonValueAsync("   ", null);
        }

        [TestMethod]
        public async Task TestStrings()
        {
            // empty string
            await TestReadNextJsonValueAsync("\"\"",
                new JsonString(""));

            // string with no escapes
            await TestReadNextJsonValueAsync("\"abc\"",
                new JsonString("abc"));

            // string with escape
            await TestReadNextJsonValueAsync("\"ab\\ncd\"",
                new JsonString("ab\ncd"));
        }

        [TestMethod]
        public async Task TestNumbers()
        {
            await TestReadNextJsonValueAsync("0", new JsonNumber("0"));
            //await TestReadNextJsonValueAsync("123", new JsonNumber("123"));
            //await TestReadNextJsonValueAsync("123.45", new JsonNumber("123.45"));
        }

        [TestMethod]
        public async Task TestKeywords()
        {
            await TestReadNextJsonValueAsync("true", JsonTrue.Instance);
            await TestReadNextJsonValueAsync("false", JsonFalse.Instance);
            await TestReadNextJsonValueAsync("null", JsonNull.Instance);
        }

        [TestMethod]
        public async Task TestLists()
        {
            await TestReadNextJsonValueAsync("[]",
                new JsonList());

            await TestReadNextJsonValueAsync("[1, 2, 3]",
                new JsonList(
                    new JsonNumber("1"),
                    new JsonNumber("2"),
                    new JsonNumber("3")));
        }

        [TestMethod]
        public async Task TestObjects()
        {
            await TestReadNextJsonValueAsync("{}",
                new JsonObject());

            await TestReadNextJsonValueAsync("{\"name\": 1}",
                new JsonObject(
                    new JsonProperty("name", new JsonNumber("1"))
                    ));

            await TestReadNextJsonValueAsync("{\"name1\": 1, \"name2\": \"hello\"}",
                new JsonObject(
                    new JsonProperty("name1", new JsonNumber("1")),
                    new JsonProperty("name2", new JsonString("hello"))
                    ));

        }

        private async Task TestReadNextJsonValueAsync(string json, JsonValue? expectedValue)
        {
            var reader = new JsonValueReader(json);
            var actualValue = await reader.ReadNextValueAsync();
            if (expectedValue == null)
            {
                Assert.IsNull(actualValue);
            }
            else
            {
                Assert.IsNotNull(actualValue);
                AssertValuesEqual(expectedValue, actualValue);
            }
        }

        private static void AssertValuesEqual(JsonValue valueA, JsonValue valueB)
        {
            Assert.AreEqual(valueA.GetType().Name, valueB.GetType().Name, "value types");

            switch (valueA)
            {
                case JsonString stringA:
                    var stringB = (JsonString)valueB;
                    Assert.AreEqual(stringA.Value, stringB.Value, "strings");
                    break;
                case JsonNumber numberA:
                    var numberB = (JsonNumber)valueB;
                    Assert.AreEqual(numberA.Number, numberB.Number, "numbers");
                    break;
                case JsonTrue:
                case JsonFalse:
                case JsonNull:
                    // already equal based on type.
                    break;
                case JsonList listA:
                    var listB = (JsonList)valueB;
                    Assert.AreEqual(listA.Values.Count, listB.Values.Count, "list count");
                    for (int i = 0; i < listA.Values.Count; i++)
                    {
                        AssertValuesEqual(listA.Values[i], listB.Values[i]);
                    }
                    break;
                case JsonObject objectA:
                    var objectB = (JsonObject)valueB;
                    Assert.AreEqual(objectA.Properties.Count, objectB.Properties.Count, "property count");
                    for (int i = 0; i < objectA.Properties.Count; i++)
                    {
                        AssertPropertiesEqual(objectA.Properties[i], objectB.Properties[i]);
                    }
                    break;
            }
        }

        private static void AssertPropertiesEqual(JsonProperty propertyA, JsonProperty propertyB)
        {
            Assert.AreEqual(propertyA.Name, propertyB.Name, "property name");
            AssertValuesEqual(propertyA.Value, propertyB.Value);
        }
    }
}
