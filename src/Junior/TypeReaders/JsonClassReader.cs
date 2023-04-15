namespace Junior
{
    /// <summary>
    /// A <see cref="JsonTypeReader"/> that reads json objects and
    /// constructs Types via constructor parameters and member initialization.
    /// </summary>
    public class JsonClassReader<TType> : JsonTypeReader<TType>
        where TType: class
    {
        private readonly IReadOnlyList<JsonConstructorParameter> _parameters;
        private readonly Dictionary<string, (JsonConstructorParameter Info, int Index)> _parameterMap;
        private readonly Dictionary<string, JsonMemberInitializer<TType>> _initializerMap;
        private readonly Func<object?[], TType> _fnConstruct;
        private readonly bool _initArguments;

        public JsonClassReader(
            IReadOnlyList<JsonConstructorParameter> parameters,
            IReadOnlyList<JsonMemberInitializer> initializers,
            Func<object?[], TType> fnConstruct)
        {
            _parameters = parameters;

            _parameterMap = parameters
                .Select((p, i) => (Info: p, Index: i))
                .ToDictionary(
                    p => p.Info.Name,
                    StringComparer.OrdinalIgnoreCase);

            _initializerMap = initializers
                .ToDictionary(
                    m => m.Name,
                    m => (JsonMemberInitializer<TType>)m,
                    StringComparer.OrdinalIgnoreCase);

            _fnConstruct = fnConstruct;
            _initArguments = parameters.Any(p => p.DefaultValue != null);
        }

        private void Initialize(
            out object?[]? arguments,
            out Dictionary<string, object?>? memberValues,
            out TType? instance)
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

        private TType Construct(object?[]? arguments, Dictionary<string, object?>? memberValues, TType? instance)
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


        public override TType? Read(JsonTokenReader reader)
        {
            if (!reader.HasToken)
                reader.MoveToNextToken();

            switch (reader.TokenKind)
            {
                case TokenKind.ObjectStart:
                    reader.MoveToNextToken();

                    Initialize(out var arguments, out var memberValues, out var instance);

                    while (reader.HasToken)
                    {
                        if (reader.TokenKind == TokenKind.ObjectEnd)
                        {
                            reader.MoveToNextToken();
                            break;
                        }
                        else if (reader.TokenKind == TokenKind.Comma)
                        {
                            reader.MoveToNextToken();
                            continue;
                        }
                        else if (reader.TokenKind.IsValueStart())
                        {
                            var name = reader.ReadTokenValue();

                            if (reader.TokenKind == TokenKind.Colon)
                                reader.MoveToNextToken();

                            if (arguments != null
                                && _parameterMap.TryGetValue(name, out var parameter))
                            {
                                var arg = parameter.Info.Reader.ReadObject(reader);
                                arguments[parameter.Index] = arg;
                            }
                            else if (_initializerMap.TryGetValue(name, out var member))
                            {
                                if (memberValues != null)
                                {
                                    var value = member.Read(reader);
                                    memberValues[member.Name] = value;
                                }
                                else if (instance != null)
                                {
                                    // better perf when only initializers are used
                                    member.ReadAndAssign(instance, reader);
                                }
                            }
                            else
                            {
                                // skip unrecognized data
                                reader.MoveToNextElement();
                                continue;
                            }
                        }
                        else
                        {
                            break;
                        }
                    }

                    return Construct(arguments, memberValues, instance);

                default:
                    // consume unrecognized values
                    if (reader.TokenKind.IsValueStart())
                        reader.MoveToNextElement();
                    return default;
            }
        }

        public override async ValueTask<TType?> ReadAsync(JsonTokenReader reader)
        {
            if (!reader.HasToken)
                await reader.MoveToNextTokenAsync().ConfigureAwait(false);

            switch (reader.TokenKind)
            {
                case TokenKind.ObjectStart:
                    await reader.MoveToNextTokenAsync().ConfigureAwait(false);

                    Initialize(out var arguments, out var memberValues, out var instance);

                    while (reader.HasToken)
                    {
                        if (reader.TokenKind == TokenKind.ObjectEnd)
                        {
                            await reader.MoveToNextTokenAsync().ConfigureAwait(false);
                            break;
                        }
                        else if (reader.TokenKind == TokenKind.Comma)
                        {
                            await reader.MoveToNextTokenAsync().ConfigureAwait(false);
                            continue;
                        }
                        else if (reader.TokenKind.IsValueStart())
                        {
                            var name = await reader.ReadTokenValueAsync().ConfigureAwait(false);

                            if (reader.TokenKind == TokenKind.Colon)
                                await reader.MoveToNextTokenAsync().ConfigureAwait(false);

                            if (arguments != null
                                && _parameterMap.TryGetValue(name, out var parameter))
                            {
                                var arg = await parameter.Info.Reader.ReadObjectAsync(reader).ConfigureAwait(false);
                                arguments[parameter.Index] = arg;
                            }
                            else if (_initializerMap.TryGetValue(name, out var member))
                            {
                                if (memberValues != null)
                                {
                                    var value = await member.ReadAsync(reader).ConfigureAwait(false);
                                    memberValues[member.Name] = value;
                                }
                                else if (instance != null)
                                {
                                    // better perf when only initializers are used
                                    await member.ReadAndAssignAsync(instance, reader).ConfigureAwait(false);
                                }
                            }
                            else
                            {
                                // skip unrecognized data
                                reader.MoveToNextElement();
                                continue;
                            }
                        }
                        else
                        {
                            break;
                        }
                    }

                    return Construct(arguments, memberValues, instance);

                default:
                    // consume unrecognized values
                    if (reader.TokenKind.IsValueStart())
                        await reader.MoveToNextTokenAsync().ConfigureAwait(false);
                    return default;
            }
        }
    }

    /// <summary>
    /// A <see cref="JsonTypeReader"/> that reads json objects and constructs types 
    /// via member initialization only.
    /// </summary>
    public class JsonClassDefaultConstructableReader<TType> : JsonClassReader<TType>
        where TType : class, new()
    {
        public JsonClassDefaultConstructableReader(
            IReadOnlyList<JsonMemberInitializer> members)
            : base(Array.Empty<JsonConstructorParameter>(), members, args => new TType())
        {
        }
    }

    public abstract class JsonMemberInitializer
    {
        public abstract string Name { get; }
    }

    public abstract class JsonMemberInitializer<TType> : JsonMemberInitializer
    {
        public abstract object? Read(JsonTokenReader reader);
        public abstract ValueTask<object?> ReadAsync(JsonTokenReader reader);
        public abstract void Assign(TType instance, object? value);

        public abstract void ReadAndAssign(TType instance, JsonTokenReader reader);
        public abstract ValueTask ReadAndAssignAsync(TType instance, JsonTokenReader reader);
    }

    public class JsonMemberInitializer<TType, TMember> : JsonMemberInitializer<TType>
    {
        private readonly string _name;
        private readonly JsonTypeReader<TMember> _reader;
        private readonly Action<TType, TMember> _setter;

        public JsonMemberInitializer(string name, JsonTypeReader<TMember> typeReader, Action<TType, TMember> fnWriter)
        {
            _name = name;
            _reader = typeReader;
            _setter = fnWriter;
        }

        public override string Name => _name;

        public override void ReadAndAssign(TType instance, JsonTokenReader tokenReader)
        {
            var value = _reader.Read(tokenReader);
            _setter(instance, value!);
        }

        public override async ValueTask ReadAndAssignAsync(TType instance, JsonTokenReader tokenReader)
        {
            var value = await _reader.ReadAsync(tokenReader).ConfigureAwait(false);
            _setter(instance, value!);
        }

        public override object? Read(JsonTokenReader reader)
        {
            return _reader.ReadObject(reader);
        }

        public override ValueTask<object?> ReadAsync(JsonTokenReader reader)
        {
            return _reader.ReadObjectAsync(reader);
        }

        public override void Assign(TType instance, object? value)
        {
            _setter(instance, value == null ? default! : (TMember)value!);
        }
    }

    public record JsonConstructorParameter(string Name, JsonTypeReader Reader, Object? DefaultValue);
}
