using System;
using System.Collections;
using System.Collections.Immutable;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.PortableExecutable;
using System.Text;
using Junior.Helpers;
using static Junior.Helpers.TypeHelper;

namespace Junior
{
    /// <summary>
    /// A class that reads and constructs types from <see cref="JsonTokenReader"/>.
    /// </summary>
    public abstract class JsonTypeReader
    {
        public abstract object? ReadObject(JsonTokenReader reader);
        public abstract ValueTask<object?> ReadObjectAsync(JsonTokenReader reader);

        /// <summary>
        /// Gets a <see cref="JsonTypeReader"/> for the specified type.
        /// </summary>
        public static JsonTypeReader? GetReader(Type type)
        {
            if (s_defaultTypeReaders.TryGetValue(type, out var reader))
                return reader;

            if (s_typeReader.TryGetValue(type, out reader))
                return reader;

            return ImmutableInterlocked.GetOrAdd(ref s_typeReader, type, CreateTypeReader);
        }

        private static ImmutableDictionary<Type, JsonTypeReader?> s_typeReader
            = ImmutableDictionary<Type, JsonTypeReader?>.Empty;

        /// <summary>
        /// Create a <see cref="JsonTypeReader"/> for the specified type.
        /// </summary>
        private static JsonTypeReader? CreateTypeReader(Type type)
        {
            return CreateNullableReader(type)
                ?? GetReaderFromAttribute(type)
                ?? CreateDictionaryAssignableReader(type)
                ?? CreateListAssignableReader(type)
                ?? CreateArrayAssignableReader(type)
                ?? CreateDictionaryAddReader(type)
                ?? CreateImmutableDictionaryReader(type)
                ?? CreateListAddReader(type)
                ?? CreateImmutableListReader(type)
                ?? CreateClassReader(type)
                ?? CreateDictionaryConstructableReader(type)
                ?? CreateListConstructableReader(type)
                ?? CreateSpanParsableReader(type)
                ?? CreateStringParsableReader(type)
                ?? CreateStringConstructableReader(type);
        }

        /// <summary>
        /// Finds the corresponding <see cref="JsonTypeReader"/> designated by the
        /// attribute <see cref="JsonTypeReaderAttribute"/>.
        /// </summary>
        private static JsonTypeReader? GetReaderFromAttribute(Type type)
        {
            // look for custom attribute designating the reader to use.
            var attr = type.GetCustomAttribute<JsonTypeReaderAttribute>();
            if (attr != null)
            {
                // if it has a singleton instance field, use that
                if (attr.ReaderType.GetField("Instance", BindingFlags.Public | BindingFlags.Static) is FieldInfo field
                    && field.GetValue(null) is JsonTypeReader instance)
                    return instance;

                // otherwise construct a new instance if there is a default constructor
                if (HasPublicDefaultConstructor(attr.ReaderType))
                    return Activator.CreateInstance(attr.ReaderType) as JsonTypeReader;
            }

            return null;
        }

        /// <summary>
        /// Creates a <see cref="JsonTypeReader"/> for nullable struct types.
        /// </summary>
        private static JsonTypeReader? CreateNullableReader(Type type)
        {
            // type is nullable struct?
            var nnType = TypeHelper.GetNonNullableType(type);
            if (nnType != type)
            {
                var nnElementReader = GetReader(nnType);
                return (JsonTypeReader)Activator.CreateInstance(typeof(JsonNullableReader<>).MakeGenericType(nnType), nnElementReader)!;
            }

            return null;
        }

        /// <summary>
        /// Creates a <see cref="JsonTypeReader"/> for types that can be parsed from spans.
        /// </summary>
        private static JsonTypeReader? CreateSpanParsableReader(Type type)
        {
            if (type.GetInterfaces()
                .Any(i => i.IsGenericType 
                    && i.GetGenericTypeDefinition() == typeof(ISpanParsable<>)))
            {
                return (JsonTypeReader?)Activator.CreateInstance(
                    typeof(JsonSpanParsableReader<>).MakeGenericType(type), NoArgs);
            }

            return null;
        }

        /// <summary>
        /// Creates a <see cref="JsonTypeReader"/> for types that can be parsed from strings.
        /// </summary>
        private static JsonTypeReader? CreateStringParsableReader(Type type)
        {
            if (type.GetInterfaces()
                .Any(i => i.IsGenericType
                    && i.GetGenericTypeDefinition() == typeof(IParsable<>)))
            {
                return (JsonTypeReader?)Activator.CreateInstance(
                    typeof(JsonStringParsableReader<>).MakeGenericType(type), NoArgs);
            }

            return null;
        }

        /// <summary>
        /// Creates a <see cref="JsonTypeReader"/> for types that can be assigned a list.
        /// </summary>
        private static JsonTypeReader? CreateListAssignableReader(Type type)
        {
            if (TypeHelper.TryGetElementType(type, out var elementType))
            {
                var listType = typeof(List<>).MakeGenericType(elementType);
                if (type.IsAssignableFrom(listType))
                {
                    var elementReader = GetReader(elementType);
                    return (JsonTypeReader?)Activator.CreateInstance(
                        typeof(JsonListAssignableReader<,>).MakeGenericType(type, elementType),
                        elementReader);
                }
            }

            return null;
        }

        /// <summary>
        /// Creates a <see cref="JsonTypeReader"/> for a type that can be assigned an array.
        /// </summary>
        private static JsonTypeReader? CreateArrayAssignableReader(Type type)
        {
            if (TypeHelper.TryGetElementType(type, out var elementType))
            {
                var arrayType = elementType.MakeArrayType();
                if (type.IsAssignableFrom(arrayType)
                    && GetReader(elementType) is JsonTypeReader elementReader)
                {
                    return (JsonTypeReader?)Activator.CreateInstance(
                        typeof(JsonArrayAssignableReader<,>).MakeGenericType(type, elementType),
                        elementReader);
                }
            }

            return null;
        }

        /// <summary>
        /// Creates a <see cref="JsonTypeReader"/> for types that can be constructed from a list.
        /// </summary>
        private static JsonTypeReader? CreateListConstructableReader(Type type)
        {
            if (GetListCompatibleConstructor(type) is ConstructorInfo constructor
                && constructor.GetParameters()[0].ParameterType is Type paramType
                && TypeHelper.GetElementType(paramType) is Type elementType
                && GetReader(elementType) is JsonTypeReader elementReader)
            {
                var constructorFunc = CreateConstructorDelegate(constructor, new Type[] { paramType });
                return (JsonTypeReader)Activator.CreateInstance(
                    typeof(JsonListConstructableReader<,>).MakeGenericType(type, elementType),
                    new object[] { elementReader, constructorFunc })!;
            }

            return null;
        }

        /// <summary>
        /// Creates a <see cref="JsonTypeReader"/> for types that are list-like and have an Add method.
        /// </summary>
        private static JsonTypeReader? CreateListAddReader(Type type)
        {
            if (TypeHelper.GetElementType(type) is Type elementType
                && HasPublicDefaultConstructor(type))
            {
                var addMethodArgTypes = new Type[] { elementType };
                var addMethod = type.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                    .FirstOrDefault(m => IsMatchingMethod(m, "Add", addMethodArgTypes));

                if (addMethod != null
                    && GetReader(elementType) is JsonTypeReader elementReader)
                {
                    var actionType = typeof(Action<,>).MakeGenericType(type, elementType);
                    var valueAdder = Delegate.CreateDelegate(actionType, addMethod);
                    return (JsonTypeReader)Activator.CreateInstance(
                        typeof(JsonListAddReader<,>).MakeGenericType(type, elementType),
                        new object[] { elementReader, valueAdder })!;
                }
            }

            return null;
        }

        /// <summary>
        /// Creates a <see cref="JsonTypeReader"/> for types that can be assigned dictionary.
        /// </summary>
        private static JsonTypeReader? CreateDictionaryAssignableReader(Type type)
        {
            if (TypeHelper.TryGetDictionaryTypes(type, out var keyType, out var valueType))
            {
                // type is assignable from Dictionary<K,V> ?
                var dictionaryType = typeof(Dictionary<,>).MakeGenericType(keyType, valueType);
                if (type.IsAssignableFrom(dictionaryType)
                    && GetReader(keyType) is JsonTypeReader keyReader
                    && GetReader(valueType) is JsonTypeReader valueReader)
                {
                    return (JsonTypeReader)Activator.CreateInstance(
                        typeof(JsonDictionaryAssignableReader<,,>).MakeGenericType(type, keyType, valueType), 
                        keyReader, valueReader)!;
                }
            }

            return null;
        }

        /// <summary>
        /// Creates a <see cref="JsonTypeReader"/> for dictionary-like types with an Add method.
        /// </summary>
        private static JsonTypeReader? CreateDictionaryAddReader(Type type)
        {
            if (TypeHelper.TryGetDictionaryTypes(type, out var keyType, out var valueType)
                && HasPublicDefaultConstructor(type))
            {

                var addMethodArgTypes = new Type[] { keyType, valueType };
                var addMethod = type.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                    .FirstOrDefault(m => IsMatchingMethod(m, "Add", addMethodArgTypes));

                if (addMethod != null
                    && GetReader(keyType) is JsonTypeReader keyReader
                    && GetReader(valueType) is JsonTypeReader valueReader)
                {
                    var actionType = typeof(Action<,>).MakeGenericType(type, keyType, valueType);
                    var valueAdder = Delegate.CreateDelegate(actionType, addMethod);

                    return (JsonTypeReader?)Activator.CreateInstance(
                        typeof(JsonDictionaryAddReader<,,>).MakeGenericType(type, keyType, valueType),
                        keyReader, valueReader, valueAdder);
                }
            }

            return null;
        }

        /// <summary>
        /// Creates a <see cref="JsonTypeReader"/> for types that can be constructed from a dictionary.
        /// </summary>
        private static JsonTypeReader? CreateDictionaryConstructableReader(Type type)
        {
            if (GetDictionaryCompatibleConstructor(type) is ConstructorInfo constructor
                && constructor.GetParameters()[0].ParameterType is Type paramType
                && TypeHelper.TryGetDictionaryTypes(paramType, out var keyType, out var valueType)
                && GetReader(keyType) is JsonTypeReader keyReader
                && GetReader(valueType) is JsonTypeReader valueReader)
            {
                var constructorArgTypes = new Type[] { typeof(Dictionary<,>).MakeGenericType(keyType, valueType) };
                var constructorFunc = CreateConstructorDelegate(constructor, constructorArgTypes);
                return (JsonTypeReader?)Activator.CreateInstance(
                    typeof(JsonDictionaryConstructableReader<,,>).MakeGenericType(type, keyType, valueType),
                    new object[] { keyReader, valueReader, constructorFunc });
            }

            return null;
        }

        /// <summary>
        /// Creates a <see cref="JsonTypeReader"/> for immutable dictionary types that follow the immutable construction pattern.
        /// </summary>
        private static JsonTypeReader? CreateImmutableDictionaryReader(Type type)
        {
            return null;
        }

        /// <summary>
        /// Creates a <see cref="JsonTypeReader"/> for immutable list that follow the immutable construction pattern.
        /// </summary>
        private static JsonTypeReader? CreateImmutableListReader(Type type)
        {
            return null;
        }

        /// <summary>
        /// Creates a <see cref="JsonTypeReader"/> for types that can be constructed with a string.
        /// </summary>
        public static JsonTypeReader? CreateStringConstructableReader(Type type)
        {
            if (GetStringCompatibleConstructor(type) is ConstructorInfo constructor)
            {
                var fnConstruct = CreateConstructorDelegate(constructor, new Type[] { typeof(string) });
                return (JsonTypeReader?)Activator.CreateInstance(
                    typeof(JsonStringConstructableReader<>).MakeGenericType(typeof(string)),
                    fnConstruct);
            }

            return null;
        }

        /// <summary>
        /// Creates a reader that assigns writable class members.
        /// </summary>
        private static JsonTypeReader? CreateClassReader(Type type)
        {
            // type has default constructor and assignable properties?
            if (HasPublicDefaultConstructor(type))
            {
                var members = type
                    .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.CanWrite)
                    .Select(p => CreateMemberInitializer(type, p))
                    .Where(p => p != null)
                    .ToList();

                if (members.Count > 0)
                {
                    return (JsonTypeReader?)Activator.CreateInstance(
                        typeof(JsonClassDefaultConstructableReader<>).MakeGenericType(type), 
                        new object[] { members });
                }
            }
            else if (GetPublicPropertyConstructor(type) is ConstructorInfo constructor)
            {
                var parameters =
                    constructor.GetParameters()
                    .Select(p => CreateConstructorParameter(p))
                    .ToList();

                if (parameters.Any(p => p == null))
                    return null;

                // additional assignable members
                var names = parameters.Select(p => p!.Name).ToHashSet();
                var members = type
                    .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.CanWrite && !names.Contains(p.Name))
                    .Select(p => CreateMemberInitializer(type, p))
                    .Where(p => p != null)
                    .ToList();

                if (members.Any(m => m == null))
                    return null;

                var fnConstruct = CreateObjectArrayDelegate(constructor);

                return (JsonTypeReader?)Activator.CreateInstance(
                    typeof(JsonClassReader<>).MakeGenericType(type),
                    new object[] { parameters, members, fnConstruct });
            }

            return null;
        }

        public static JsonConstructorParameter? CreateConstructorParameter(ParameterInfo parameter)
        {
            var reader = GetReader(parameter.ParameterType);
            if (reader != null)
            {
                return new JsonConstructorParameter(parameter.Name!, reader);
            }

            return null;
        }

        /// <summary>
        /// Creates a <see cref="JsonMemberInitializer"/> for a property.
        /// </summary>
        private static JsonMemberInitializer? CreateMemberInitializer(Type type, PropertyInfo property)
        {
            if (GetReader(property.PropertyType) is JsonTypeReader propertyReader)
            {
                var propertySetter = CreatePropertySetterDelegate(type, property);
                return (JsonMemberInitializer?)Activator.CreateInstance(
                    typeof(JsonMemberInitializer<,>).MakeGenericType(type, property.PropertyType),
                        new object[] { property.Name, propertyReader, propertySetter });
            }

            return null;
        }

        private static readonly object[] NoArgs = new object[0];

        private static readonly Dictionary<Type, JsonTypeReader> s_defaultTypeReaders =
            new Dictionary<Type, JsonTypeReader>()
            {
                { typeof(object), JsonAnyReader.Instance },
                { typeof(string), JsonStringReader.Instance },
                { typeof(bool), JsonBoolReader.Instance },
                { typeof(byte), JsonSpanParsableReader<byte>.Instance },
                { typeof(sbyte), JsonSpanParsableReader<sbyte>.Instance },
                { typeof(short), JsonSpanParsableReader<short>.Instance },
                { typeof(ushort), JsonSpanParsableReader<ushort>.Instance },
                { typeof(int), JsonSpanParsableReader<int>.Instance },
                { typeof(uint), JsonSpanParsableReader<uint>.Instance },
                { typeof(long), JsonSpanParsableReader<long>.Instance },
                { typeof(ulong), JsonSpanParsableReader<ulong>.Instance },
                { typeof(double), JsonSpanParsableReader<double>.Instance },
                { typeof(float), JsonSpanParsableReader<float>.Instance },
                { typeof(decimal), JsonSpanParsableReader<decimal>.Instance },
                { typeof(DateTime), JsonSpanParsableReader<DateTime>.Instance },
                { typeof(DateTimeOffset), JsonSpanParsableReader<DateTimeOffset>.Instance },
                { typeof(TimeSpan), JsonSpanParsableReader<TimeSpan>.Instance },
                { typeof(Guid), JsonSpanParsableReader<Guid>.Instance },
                { typeof(StringBuilder), JsonStringBuilderReader.Instance },
                { typeof(Stream), JsonStreamReader<Stream>.Instance },
                { typeof(MemoryStream), JsonStreamReader<MemoryStream>.Instance },
                { typeof(LargeString), JsonLargeStringReader.Instance }
            };
    }

    public abstract class JsonTypeReader<T> : JsonTypeReader
    {
        public abstract T? Read(JsonTokenReader reader);
        public abstract ValueTask<T?> ReadAsync(JsonTokenReader reader);

        public override object? ReadObject(JsonTokenReader reader) =>
            Read(reader);

        public override async ValueTask<object?> ReadObjectAsync(JsonTokenReader reader) =>
            await ReadAsync(reader).ConfigureAwait(false);
    }
}
