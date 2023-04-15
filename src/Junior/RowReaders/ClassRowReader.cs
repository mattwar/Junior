using System;

namespace Junior
{
    public class ClassRowReader<T> : RowReader<T>
    {
        private readonly IReadOnlyList<RowConstructorParameter> _parameters;
        private readonly IReadOnlyList<RowMemberInitializer<T>> _initializers;
        private readonly Dictionary<string, (RowConstructorParameter Info, int Index)> _parameterMap;
        private readonly Dictionary<string, RowMemberInitializer<T>> _initializerMap;
        private readonly Func<object?[], T> _fnConstruct;
        private readonly bool _initArguments;

        public ClassRowReader(
            IReadOnlyList<RowConstructorParameter> parameters,
            IReadOnlyList<RowMemberInitializer> initializers,
            Func<object?[], T> fnConstruct)
        {
            _parameters = parameters;

            _initializers = initializers
                .OfType<RowMemberInitializer<T>>()
                .ToList();

            _parameterMap = parameters
                .Select((p, i) => (Info: p, Index: i))
                .ToDictionary(x => x.Info.Name, StringComparer.OrdinalIgnoreCase);

            _initializerMap = _initializers
                .ToDictionary(
                    i => i.Name,
                    StringComparer.OrdinalIgnoreCase);

            _fnConstruct = fnConstruct;
            _initArguments = _parameters.Any(p => p.DefaultValue != null);
        }

        private void Initialize(
            out object?[]? arguments,
            out Dictionary<string, object?>? memberValues,
            out T? instance)
        {
            arguments = _parameterMap.Count > 0
                    ? new object?[_parameterMap.Values.Count]
                    : null;

            if (_initArguments && arguments != null)
            {
                for (int i = 0; i < arguments.Length; i++)
                {
                    arguments[i] = _parameters[i].DefaultValue;
                }
            }

            memberValues = arguments != null
                ? new Dictionary<string, object?>()
                : null;

            instance = arguments == null
                ? _fnConstruct(Array.Empty<object?>())
                : default;
        }

        private T Construct(object?[]? arguments, Dictionary<string, object?>? memberValues, T? instance)
        {
            if (instance == null
                && arguments != null)
            {
                instance = _fnConstruct(arguments!);
            }

            if (memberValues != null
                && instance != null)
            {
                foreach (var kvp in memberValues)
                {
                    if (_initializerMap.TryGetValue(kvp.Key, out var initailizer))
                    {
                        initailizer.Assign(instance, kvp.Value);
                    }
                }
            }

            return instance!;
        }

        public override async ValueTask<T> ReadAsync(StreamingDataReader dataReader)
        {
            Initialize(out var arguments, out var memberValues, out var instance);

            while (await dataReader.MoveToNextFieldAsync().ConfigureAwait(false))
            {
                var fieldName = dataReader.CurrentFieldName;
                if (fieldName == "")
                {
                    // map by field index to constructor parameters then members
                    var index = dataReader.CurrentFieldIndex;
                    if (index < _parameters.Count)
                    {
                        var info = _parameters[index];
                        var value = await dataReader.ReadFieldValueAsync(info.Type).ConfigureAwait(false);
                        arguments![index] = value;
                    }
                    else if (index - _parameters.Count < _initializers.Count)
                    {
                        var initializer = _initializers[index - _parameters.Count];
                        if (memberValues != null)
                        {
                            var value = await initializer.ReadAsync(dataReader).ConfigureAwait(false);
                            memberValues[fieldName] = value;
                        }
                        else
                        {
                            await initializer.ReadAndAssignAsync(instance!, dataReader).ConfigureAwait(false);
                        }
                    }
                }
                else if (_parameterMap.TryGetValue(fieldName, out var parameter))
                {
                    var value = await dataReader.ReadFieldValueAsync(parameter.Info.Type).ConfigureAwait(false);
                    arguments![parameter.Index] = value;
                }
                else if (_initializerMap.TryGetValue(dataReader.CurrentFieldName, out var initializer))
                {
                    if (memberValues != null)
                    {
                        var value = await initializer.ReadAsync(dataReader).ConfigureAwait(false);
                        memberValues[fieldName] = value;
                    }
                    else
                    {
                        await initializer.ReadAndAssignAsync(instance!, dataReader).ConfigureAwait(false);
                    }
                }
                else
                {
                    // field name is not associated with any constructor parameter or member
                    continue;
                }
            }

            return Construct(arguments, memberValues, instance);
        }
    }

    public class ClassDefaultConstructableRowReader<T> : ClassRowReader<T>
        where T : class, new()
    {
        public ClassDefaultConstructableRowReader(
            IReadOnlyList<RowMemberInitializer> initializers)
            : base(
                  Array.Empty<RowConstructorParameter>(),
                  initializers,
                  list => new T())
        {
        }
    }

    public record RowConstructorParameter(string Name, Type Type, object? DefaultValue);

    public abstract class RowMemberInitializer
    {
        public abstract string Name { get; }
    }

    public abstract class RowMemberInitializer<TType> : RowMemberInitializer
    {
        public abstract ValueTask<object?> ReadAsync(StreamingDataReader reader);
        public abstract void Assign(TType instance, object? value);
        public abstract ValueTask ReadAndAssignAsync(TType instance, StreamingDataReader reader);
    }

    public class RowMemberInitializer<TType, TMember> : RowMemberInitializer<TType>
    {
        private readonly string _name;
        private readonly Action<TType, TMember> _setter;

        public RowMemberInitializer(string name, Action<TType, TMember> fnWriter)
        {
            _name = name;
            _setter = fnWriter;
        }

        public override string Name => _name;

        public override async ValueTask ReadAndAssignAsync(TType instance, StreamingDataReader dataReader)
        {
            var value = await dataReader.ReadFieldValueAsync<TMember>().ConfigureAwait(false);
            _setter(instance, value!);
        }

        public async override ValueTask<object?> ReadAsync(StreamingDataReader dataReader)
        {
            return await dataReader.ReadFieldValueAsync<TMember>().ConfigureAwait(false);
        }

        public override void Assign(TType instance, object? value)
        {
            _setter(instance, value == null ? default! : (TMember)value!);
        }
    }
}
