namespace Junior
{
    /// <summary>
    /// A <see cref="JsonTypeReader"/> that constructs types from 
    /// reading a Json object as a dictionary.
    /// </summary>
    public class JsonDictionaryReader<TType, TDict, TKey, TValue> 
        : JsonTypeReader<TType>
        where TType : class
        where TKey : notnull
    {
        private readonly JsonTypeReader<TKey> _keyReader;
        private readonly JsonTypeReader<TValue> _valueReder;
        private readonly Func<TDict> _fnCreate;
        private readonly Action<TDict, TKey, TValue?> _fnAdd;
        private readonly Func<TDict, TType> _fnMap;

        public JsonDictionaryReader(
            JsonTypeReader<TKey> keyReader,
            JsonTypeReader<TValue> valueReader,
            Func<TDict> fnCreate,
            Action<TDict, TKey, TValue?> fnAdd,
            Func<TDict, TType> fnMap)
        {
            _keyReader = keyReader;
            _valueReder = valueReader;
            _fnCreate = fnCreate;
            _fnAdd = fnAdd;
            _fnMap = fnMap;
        }

        public override TType? Read(JsonTokenReader reader)
        {
            if (!reader.HasToken)
                reader.MoveToNextToken();

            switch (reader.TokenKind)
            {
                case TokenKind.Null:
                    reader.MoveToNextToken();
                    return null;

                case TokenKind.ObjectStart:
                    reader.MoveToNextToken();
                    var dict = _fnCreate();

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
                            var key = _keyReader.Read(reader);

                            if (reader.TokenKind == TokenKind.Colon)
                                reader.MoveToNextToken();

                            var value = _valueReder.Read(reader);
                            if (key != null)
                                _fnAdd(dict, key, value);
                        }
                        else
                        {
                            break;
                        }
                    }

                    return _fnMap(dict);

                default:
                    reader.MoveToNextElement();
                    return null;
            }
        }

        public override async ValueTask<TType?> ReadAsync(JsonTokenReader reader)
        {
            if (!reader.HasToken)
                await reader.MoveToNextTokenAsync().ConfigureAwait(false);

            switch (reader.TokenKind)
            {
                case TokenKind.Null:
                    await reader.MoveToNextTokenAsync().ConfigureAwait(false);
                    return null;

                case TokenKind.ObjectStart:
                    await reader.MoveToNextTokenAsync().ConfigureAwait(false);
                    var dict = _fnCreate();

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
                            var key = await _keyReader.ReadAsync(reader).ConfigureAwait(false);

                            if (reader.TokenKind == TokenKind.Colon)
                                await reader.MoveToNextTokenAsync().ConfigureAwait(false);

                            var value = _valueReder.Read(reader);
                            if (key != null)
                            {
                                _fnAdd(dict, key, value);
                            }
                        }
                        else
                        {
                            break;
                        }
                    }

                    return _fnMap(dict);

                default:
                    await reader.MoveToNextElementAsync().ConfigureAwait(false);
                    return null;
            }
        }
    }

    public class JsonDictionaryAssignableReader<TType, TKey, TValue>
        : JsonDictionaryReader<TType, Dictionary<TKey, TValue>, TKey, TValue>
        where TType : class
        where TKey : notnull
    {
        public JsonDictionaryAssignableReader(
            JsonTypeReader<TKey> keyReader,
            JsonTypeReader<TValue> valueReader)
            : base(
                  keyReader,
                  valueReader,
                  () => new Dictionary<TKey, TValue>(),
                  (d, k, v) => d[k] = v!, 
                  d => (TType)(object)d)
        {
        }
    }

    public class JsonDictionaryAddReader<TType, TKey, TValue>
        : JsonDictionaryReader<TType, TType, TKey, TValue>
        where TType : class, new()
        where TKey : notnull
    {
        public JsonDictionaryAddReader(
            JsonTypeReader<TKey> keyReader,
            JsonTypeReader<TValue> valueReader,
            Action<TType, TKey, TValue?> fnAdd)
            : base(
                  keyReader, 
                  valueReader, 
                  () => new TType(),
                  fnAdd, 
                  d => d)
        {
        }
    }

    public class JsonDictionaryConstructableReader<TType, TKey, TValue> 
        : JsonDictionaryReader<TType, Dictionary<TKey, TValue>, TKey, TValue>
        where TType : class
        where TKey : notnull
    {
        public JsonDictionaryConstructableReader(
            JsonTypeReader<TKey> keyReader,
            JsonTypeReader<TValue> valueReader,
            Func<Dictionary<TKey, TValue>, TType> fnConstructor)
            : base(
                  keyReader, 
                  valueReader, 
                  () => new Dictionary<TKey, TValue>(),
                  (d, k, v) => d[k] = v!, 
                  fnConstructor)
        {
        }
    }
}
