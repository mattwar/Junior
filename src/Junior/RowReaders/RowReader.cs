using System.Collections.Immutable;
using System.Reflection;
using static Junior.Helpers.TypeHelper;

namespace Junior
{
    public abstract class RowReader
    {
        public abstract ValueTask<object?> ReadObjectAsync(StreamingDataReader dataReader);

        /// <summary>
        /// Gets a <see cref="JsonTypeReader"/> for the specified type.
        /// </summary>
        public static RowReader? GetReader(Type type)
        {
            if (s_rowReader.TryGetValue(type, out var reader))
                return reader;

            return ImmutableInterlocked.GetOrAdd(ref s_rowReader, type, CreateRowReader);
        }

        private static ImmutableDictionary<Type, RowReader?> s_rowReader
            = ImmutableDictionary<Type, RowReader?>.Empty;

        private static RowReader? CreateRowReader(Type type)
        {
            return CreateClassReader(type);
        }

        /// <summary>
        /// Creates a reader that assigns writable class members.
        /// </summary>
        private static RowReader? CreateClassReader(Type type)
        {
            // type has default constructor and assignable properties?
            if (HasPublicDefaultConstructor(type))
            {
                var members = type
                    .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.CanWrite)
                    .Select(p => CreateMemberInitializer(type, p))
                    .OfType<RowMemberInitializer>()
                    .ToList();

                if (members.Count > 0)
                {
                    return (RowReader?)Activator.CreateInstance(
                        typeof(ClassDefaultConstructableRowReader<>).MakeGenericType(type),
                        new object[] { members });
                }
            }
            else if (GetPublicPropertyConstructor(type) is ConstructorInfo constructor)
            {
                var parameters =
                    constructor.GetParameters()
                    .Select(p => new RowConstructorParameter(p.Name!, p.ParameterType))
                    .ToList();

                if (parameters.Any(p => p == null))
                    return null;

                // additional assignable members
                var names = parameters.Select(p => p!.Name).ToHashSet();
                var members = type
                    .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.CanWrite && !names.Contains(p.Name))
                    .Select(p => CreateMemberInitializer(type, p))
                    .OfType<RowMemberInitializer>()
                    .ToList();

                if (members.Any(m => m == null))
                    return null;

                var fnConstruct = CreateObjectArrayDelegate(constructor);

                return (RowReader?)Activator.CreateInstance(
                    typeof(ClassRowReader<>).MakeGenericType(type),
                    new object[] { parameters, members, fnConstruct });
            }

            return null;
        }

        /// <summary>
        /// Creates a <see cref="RowMemberInitializer"/> for a property.
        /// </summary>
        private static RowMemberInitializer? CreateMemberInitializer(Type type, PropertyInfo property)
        {
            var propertySetter = CreatePropertySetterDelegate(type, property);
            return (RowMemberInitializer?)Activator.CreateInstance(
                typeof(RowMemberInitializer<,>).MakeGenericType(type, property.PropertyType),
                    new object[] { property.Name, propertySetter });
        }
    }

    public abstract class RowReader<T> : RowReader
    {
        public abstract ValueTask<T> ReadAsync(StreamingDataReader dataReader);

        public async override ValueTask<object?> ReadObjectAsync(StreamingDataReader dataReader)
        {
            return await ReadAsync(dataReader).ConfigureAwait(false);
        }
    }
}
