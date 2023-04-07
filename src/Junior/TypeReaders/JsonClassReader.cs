namespace Junior
{
    public class JsonClassReader<TType> : JsonTypeReader<TType>
        where TType : class, new()
    {
        private readonly Dictionary<string, JsonClassMemberWriter<TType>> _memberWriterMap;

        public JsonClassReader(IReadOnlyList<JsonClassMemberWriter<TType>> memberWriters)
        {
            _memberWriterMap = memberWriters.ToDictionary(m => m.Name, StringComparer.OrdinalIgnoreCase);
        }

        public override TType? Read(JsonTokenReader reader)
        {
            if (!reader.HasToken)
                reader.MoveToNextToken();

            switch (reader.TokenKind)
            {
                case TokenKind.ObjectStart:
                    reader.MoveToNextToken();
                    var instance = new TType();

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
                            var memberName = reader.ReadTokenValue();

                            if (reader.TokenKind == TokenKind.Colon)
                                reader.MoveToNextToken();

                            if (!_memberWriterMap.TryGetValue(memberName, out var memberWriter))
                            {
                                reader.MoveToNextElement();
                                continue;
                            }

                            memberWriter.ReadAndAssign(instance, reader);
                        }
                        else
                        {
                            break;
                        }
                    }

                    return instance;

                default:
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
                    var instance = new TType();

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
                            var memberName = reader.ReadTokenValue();

                            if (reader.TokenKind == TokenKind.Colon)
                                await reader.MoveToNextTokenAsync().ConfigureAwait(false);

                            if (!_memberWriterMap.TryGetValue(memberName, out var memberWriter))
                            {
                                // skip this value and move to next property
                                await reader.MoveToNextElementAsync().ConfigureAwait(false);
                                continue;
                            }

                            await memberWriter.ReadAndAssignAsync(instance, reader).ConfigureAwait(false);
                        }
                        else
                        {
                            break;
                        }
                    }

                    return instance;

                default:
                    await reader.MoveToNextTokenAsync().ConfigureAwait(false);
                    return default;
            }
        }
    }

    public abstract class JsonClassMemberWriter
    {
        public abstract string Name { get; }
    }

    public abstract class JsonClassMemberWriter<TType> : JsonClassMemberWriter
    {
        public abstract void ReadAndAssign(TType instance, JsonTokenReader tokenReader);
        public abstract ValueTask ReadAndAssignAsync(TType instance, JsonTokenReader tokenReader);
    }

    public class JsonClassMemberWriter<TType, TMember> : JsonClassMemberWriter<TType>
    {
        private readonly string _name;
        private readonly JsonTypeReader<TMember> _typeReader;
        private readonly Action<TType, TMember?> _fnWriter;

        public JsonClassMemberWriter(string name, JsonTypeReader<TMember> typeReader, Action<TType, TMember?> fnWriter)
        {
            _name = name;
            _typeReader = typeReader;
            _fnWriter = fnWriter;
        }

        public override string Name => _name;

        public override void ReadAndAssign(TType instance, JsonTokenReader tokenReader)
        {
            var value = _typeReader.Read(tokenReader);
            _fnWriter(instance, value);
        }

        public override async ValueTask ReadAndAssignAsync(TType instance, JsonTokenReader tokenReader)
        {
            var value = await _typeReader.ReadAsync(tokenReader).ConfigureAwait(false);
            _fnWriter(instance, value);
        }
    }
}
