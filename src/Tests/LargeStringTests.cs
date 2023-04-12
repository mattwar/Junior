using Junior;
using System.Text;
using static Tests.Helpers.TestHelpers;

namespace Tests
{
    [TestClass]
    public class LargeStringTests
    {

        [TestMethod]
        public void TestEmpty()
        {
            Assert.AreEqual(LargeString.Empty.Length, 0);
            Assert.AreEqual(new LargeString().Length, 0);
            Assert.AreEqual(new LargeString("").Length, 0);
        }

        [TestMethod]
        public void TestEqualsString()
        {
            TestEquals(Large("abc"), "abc");
            TestEquals(Large("0123456789|ABCDEFGHIJ|KLMNOPQRSTUVWXYZ", 10), "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ");
            TestNotEquals(Large("0123456789|ABCDEFGHIJ|KLMNOPQRSTUVWXYZ", 10), "0123456789ABCDEFGHIJKLMNOPQRSTUVWXY?");
            TestNotEquals(Large("0123456789|ABCDEFGHIJ|KLMNOPQRSTUVWXYZ", 10), "0123456789ABCDEFGHIJKLMNOPQRSTUVWXY");
            TestNotEquals(Large("0123456789|ABCDEFGHIJ|KLMNOPQRSTUVWXYZ", 10), "");
        }

        [TestMethod]
        public void TestEquals()
        {
            TestEquals(Large("abc"), Large("abc"));
            TestEquals(Large("0123456789|ABCDEFGHIJ|KLMNOPQRSTUVWXYZ", 10), Large("0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ"));
            TestNotEquals(Large("0123456789|ABCDEFGHIJ|KLMNOPQRSTUVWXYZ", 10), Large("0123456789ABCDEFGHIJKLMNOPQRSTUVWXY?"));
            TestNotEquals(Large("0123456789|ABCDEFGHIJ|KLMNOPQRSTUVWXYZ", 10), Large("0123456789ABCDEFGHIJKLMNOPQRSTUVWXY"));
            TestNotEquals(Large("0123456789|ABCDEFGHIJ|KLMNOPQRSTUVWXYZ", 10), Large(""));
        }

        public void TestEquals(LargeString large, string text)
        {
            var isEqual = large.Equals(text);
            Assert.IsTrue(isEqual, $"expected equal: {large.ToString()}, {text}");
        }

        public void TestNotEquals(LargeString large, string text)
        {
            Assert.IsFalse(large.Equals(text), $"expected not equal: {large}, {text}");
        }

        public void TestEquals(LargeString largeA, LargeString largeB)
        {
            Assert.IsTrue(largeA.Equals(largeB), $"expected equal: {largeA}, {largeB}");
            Assert.IsTrue(largeB.Equals(largeA), $"expected equal: {largeA}, {largeB}");
        }

        public void TestNotEquals(LargeString largeA, LargeString largeB)
        {
            Assert.IsFalse(largeA.Equals(largeB), $"expected not equal: {largeA}, {largeB}");
            Assert.IsFalse(largeB.Equals(largeA), $"expected not equal: {largeB}, {largeA}");
        }

        [TestMethod]
        public void TestAppendString()
        {
            TestAppend(Large(""), "abc");
            TestAppend(Large("0123456789|ABCDEFGHIJ|KLMNOPQRSTUVWXYZ", 10), "abc");
        }

        public void TestAppend(LargeString large, string append)
        {
            var actual = large.Append(append);
            var expected = large.ToString() + append;
            TestEquals(actual, expected);
        }

        public void TestAppend(LargeString largeA, LargeString largeB, LargeString expected)
        {
            var actualLarge = largeA.Append(largeB);
            TestEquals(actualLarge, expected);
        }

        [TestMethod]
        public void TestInsertString()
        {
            TestInsert(Large(""), 0, "abc");
            TestInsert(Large("abcd"), 2, "123");
            TestInsert(Large("abcd"), 0, "123");
            TestInsert(Large("abcd"), 4, "123");

            TestInsert(
                Large("0123456789|ABCDEFGHIJ|KLMNOPQRSTUVWXYZ", 10),
                10, "[moo]");

            TestInsert(
                Large("0123456789|ABCDEFGHIJ|KLMNOPQRSTUVWXYZ", 10),
                15, "[moo]");

            TestInsert(
                Large("0123456789|ABCDEFGHIJ|KLMNOPQRSTUVWXYZ", 10),
                0, "[moo]");

            TestInsert(
                Large("0123456789|ABCDEFGHIJ|KLMNOPQRSTUVWXYZ", 10),
                36, "[moo]");
        }

        public void TestInsert(LargeString large, int start, string text)
        {
            var expected = large.ToString().Insert(start, text);
            var actual = large.Insert(start, text).ToString();
            Assert.AreEqual(expected, actual, $"Insert({start}, \"{text}\")");
        }

        [TestMethod]
        public void TestInsert()
        {
            TestInsert(Large(""), 0, Large("abc"));
            TestInsert(Large("abcd"), 2, Large("123"));
            TestInsert(Large("abcd"), 0, Large("123"));
            TestInsert(Large("abcd"), 4, Large("123"));

            TestInsert(
                Large("0123456789|ABCDEFGHIJ|KLMNOPQRSTUVWXYZ", 10),
                10, Large("[moo]"));

            TestInsert(
                Large("0123456789|ABCDEFGHIJ|KLMNOPQRSTUVWXYZ", 10),
                15, Large("[moo]"));

            TestInsert(
                Large("0123456789|ABCDEFGHIJ|KLMNOPQRSTUVWXYZ", 10),
                0, Large("[moo]"));

            TestInsert(
                Large("0123456789|ABCDEFGHIJ|KLMNOPQRSTUVWXYZ", 10),
                36, Large("[moo]"));

            TestInsert(
                Large("0123456789|ABCDEFGHIJ|KLMNOPQRSTUVWXYZ", 10),
                15,
                Large("abcdefghij|klmnopqrstuvwxyz", 10));
        }

        public void TestInsert(LargeString largeA, int start, LargeString largeB)
        {
            var expected = largeA.ToString().Insert(start, largeB.ToString());
            var actual = largeA.Insert(start, largeB).ToString();
            Assert.AreEqual(expected, actual, $"Insert({start}, \"{largeB}\")");
        }

        [TestMethod]
        public void TestRemove()
        {
            TestRemove(Large(""), 0, 0);
            TestRemove(Large("abc"), 1, 2);
            TestRemove(Large("abc"), 0, 2);
            TestRemove(Large("abc"), 1, 1);

            TestRemove(
                Large("0123456789|ABCDEFGHIJ|KLMNOPQRSTUVWXYZ", 10),
                0, 5);

            TestRemove(
                Large("0123456789|ABCDEFGHIJ|KLMNOPQRSTUVWXYZ", 10),
                0, 15);

            TestRemove(
                Large("0123456789|ABCDEFGHIJ|KLMNOPQRSTUVWXYZ", 10),
                0, 26);

            TestRemove(
                Large("0123456789|ABCDEFGHIJ|KLMNOPQRSTUVWXYZ", 10),
                0, 36);

            TestRemove(
                Large("0123456789|ABCDEFGHIJ|KLMNOPQRSTUVWXYZ", 10),
                5, 10);

            TestRemove(
                Large("0123456789|ABCDEFGHIJ|KLMNOPQRSTUVWXYZ", 10),
                5, 15);

            TestRemove(
                Large("0123456789|ABCDEFGHIJ|KLMNOPQRSTUVWXYZ", 10),
                5, 20);

            TestRemove(
                Large("0123456789|ABCDEFGHIJ|KLMNOPQRSTUVWXYZ", 10),
                15, 10);

            TestRemove(
                Large("0123456789|ABCDEFGHIJ|KLMNOPQRSTUVWXYZ", 10),
                15, 21);

            TestRemove(
                Large("0123456789|ABCDEFGHIJ|KLMNOPQRSTUVWXYZ", 10),
                20, 16);
        }

        public void TestRemove(LargeString large, int start, int length)
        {
            var expected = large.ToString().Remove(start, length);
            var actual = large.Remove(start, length).ToString();
            Assert.AreEqual(expected, actual, $"Remove({start}, {length})");       
        }

        [TestMethod]
        public void TestSubstring()
        {
            TestSubstring(Large(""), 0, 0);
            TestSubstring(Large("abc"), 0, 2);
            TestSubstring(Large("abc"), 0, 3);
            TestSubstring(Large("abc"), 1, 2);
            TestSubstring(Large("abc"), 1, 1);
            TestSubstring(Large("abc"), 2, 1);

            TestSubstring(
                Large("0123456789|ABCDEFGHIJ|KLMNOPQRSTUVWXYZ", 10),
                0, 5);

            TestSubstring(
                Large("0123456789|ABCDEFGHIJ|KLMNOPQRSTUVWXYZ", 10),
                0, 15);

            TestSubstring(
                Large("0123456789|ABCDEFGHIJ|KLMNOPQRSTUVWXYZ", 10),
                0, 26);

            TestSubstring(
                Large("0123456789|ABCDEFGHIJ|KLMNOPQRSTUVWXYZ", 10),
                0, 36);

            TestSubstring(
                Large("0123456789|ABCDEFGHIJ|KLMNOPQRSTUVWXYZ", 10),
                5, 10);

            TestSubstring(
                Large("0123456789|ABCDEFGHIJ|KLMNOPQRSTUVWXYZ", 10),
                5, 15);

            TestSubstring(
                Large("0123456789|ABCDEFGHIJ|KLMNOPQRSTUVWXYZ", 10),
                5, 20);

            TestSubstring(
                Large("0123456789|ABCDEFGHIJ|KLMNOPQRSTUVWXYZ", 10),
                15, 10);

            TestSubstring(
                Large("0123456789|ABCDEFGHIJ|KLMNOPQRSTUVWXYZ", 10),
                15, 21);

            TestSubstring(
                Large("0123456789|ABCDEFGHIJ|KLMNOPQRSTUVWXYZ", 10),
                20, 16);
        }

        public void TestSubstring(LargeString large, int start, int length)
        {
            var expected = large.ToString().Substring(start, length);
            var actual = large.Substring(start, length).ToString();
            Assert.AreEqual(expected, actual, $"Substring({start}, {length})");
        }

        [TestMethod]
        public void TestToString()
        {
            TestToString(Large(""), "");
            TestToString(Large("abc"), "abc");
            TestToString(Large("0123456789|ABCDEFGHIJ|KLMNOPQRSTUVWXYZ", 10), "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ");
        }

        public void TestToString(LargeString large, string expected)
        {
            var actual = large.ToString();
            Assert.AreEqual(expected, actual, "ToString");
        }

        [TestMethod]
        public void TestToStringRange()
        {
            TestToStringRange(Large(""), 0, "");
            TestToStringRange(Large("abc"), 1, "bc");
            TestToStringRange(Large("abc"), 0, "ab");
            TestToStringRange(Large("abc"), 1, "b");

            TestToStringRange(
                Large("0123456789|ABCDEFGHIJ|KLMNOPQRSTUVWXYZ", 10),
                0, "01234");

            TestToStringRange(
                Large("0123456789|ABCDEFGHIJ|KLMNOPQRSTUVWXYZ", 10),
                5, "56789ABCDE");

            TestToStringRange(
                Large("0123456789|ABCDEFGHIJ|KLMNOPQRSTUVWXYZ", 10),
                8, "89ABCDEFGHIJKLMNO");

        }

        public void TestToStringRange(LargeString large, int start, string expected)
        {
            var actual = large.ToString(start, expected.Length);
            Assert.AreEqual(expected, actual, $"ToString({start}, {expected.Length})");
        }


        private const int DefaultTestSegmentSize = 32;

        private static LargeString Large(string segmentedString, int segmentSize = DefaultTestSegmentSize)
        {
            return Large(segmentedString.Split('|'), segmentSize);
        }

        private static LargeString Large(string[] segments, int segmentSize = DefaultTestSegmentSize)
        {
            var builder = new LargeString(segmentSize).ToBuilder();
            
            foreach (var seg in segments)
            {
                builder.Add(seg);
            }

            return builder.ToLargeString();
        }
    }
}
