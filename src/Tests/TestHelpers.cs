﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tests
{
    public class TestHelpers
    {
        public static void AssertStructurallyEqual(object? expected, object? actual)
        {
            if (object.Equals(expected, actual))
                return;

            if (expected == null)
                Assert.Fail($"Expected null not: {actual}");

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
                var expectedType = expected.GetType();
                var actualType = actual?.GetType();
                Assert.AreEqual(expectedType, actualType, "type");

                var props = expectedType.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                foreach (var p in props)
                {
                    // ignore indexers
                    var getMethod = p.GetGetMethod();
                    if (getMethod == null || getMethod.GetParameters().Length > 0)
                        continue;

                    var expectedPropValue = p.GetValue(expected, null);
                    var actualPropValue = p.GetValue(actual, null);
                    AssertStructurallyEqual(expectedPropValue, actualPropValue);
                }
            }
        }

    }
}
