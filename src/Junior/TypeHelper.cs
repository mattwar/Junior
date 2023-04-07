using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Junior
{
    internal static class TypeHelper
    {
        public static Type GetNonNullableType(Type type)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                return type.GetGenericArguments()[0];
            }

            return type;
        }

        public static Type? GetElementType(Type type)
        {
            TryGetElementType(type, out var elementType);
            return elementType;
        }

        public static bool TryGetElementType(Type type, [NotNullWhen(true)] out Type? elementType)
        {
            if (type.IsArray)
            {
                elementType = type.GetElementType();
                return elementType != null;
            }
            else if (typeof(IEnumerable).IsAssignableFrom(type))
            {
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                {
                    elementType = type.GetGenericArguments()[0];
                    return true;
                }
                else
                {
                    elementType = type.GetInterfaces()
                        .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                        .Select(i => i.GetGenericArguments()[0]).FirstOrDefault()
                        ?? typeof(object);
                    return true;
                }
            }
            else
            {
                elementType = null;
                return false;
            }
        }

        public static bool TryGetDictionaryTypes(
            Type type,
            [NotNullWhen(true)]
            out Type? keyType,
            [NotNullWhen(true)]
            out Type? valueType)
        {
            foreach (var i in type.GetInterfaces())
            {
                if (i.IsGenericType)
                {
                    var gtd = i.GetGenericTypeDefinition();
                    if (gtd == typeof(IReadOnlyDictionary<,>) || gtd == typeof(IDictionary<,>))
                    {
                        var args = i.GetGenericArguments();
                        keyType = args[0];
                        valueType = args[1];
                        return true;
                    }
                }
            }

            if (typeof(IDictionary).IsAssignableFrom(type))
            {
                keyType = typeof(object);
                valueType = typeof(object);
                return true;
            }

            keyType = null;
            valueType = null;
            return false;
        }
    }
}