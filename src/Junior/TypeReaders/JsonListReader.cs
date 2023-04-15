namespace Junior
{
    /// <summary>
    /// A <see cref="JsonTypeReader"/> for a type that is list-like
    /// </summary>
    public class JsonListReader<TType, TBuilder, TElement> : JsonTypeReader<TType>
    {
        private readonly JsonTypeReader<TElement> _elementReader;
        private readonly Func<TBuilder> _fnCreate;
        private readonly Action<TBuilder, TElement?> _fnAdd;
        private readonly Func<TBuilder, TType> _fnMap;

        public JsonListReader(
            JsonTypeReader<TElement> elementReader,
            Func<TBuilder> fnCreate,
            Action<TBuilder, TElement?> fnAdd,
            Func<TBuilder, TType> fnMap)
        {
            _elementReader = elementReader;
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
                case TokenKind.ListStart:
                    reader.MoveToNextToken();
                    var list = _fnCreate();

                    while (reader.HasToken)
                    {
                        if (reader.TokenKind == TokenKind.ListEnd)
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
                            var value = _elementReader.Read(reader);
                            _fnAdd(list, value);
                        }
                        else
                        {
                            break;
                        }
                    }

                    return _fnMap(list);

                default:
                    reader.MoveToNextToken();
                    return default;
            }
        }

        public override async ValueTask<TType?> ReadAsync(JsonTokenReader reader)
        {
            if (!reader.HasToken)
                await reader.MoveToNextTokenAsync().ConfigureAwait(false);

            switch (reader.TokenKind)
            {
                case TokenKind.ListStart:
                    await reader.MoveToNextTokenAsync().ConfigureAwait(false);
                    var list = _fnCreate();

                    while (reader.HasToken)
                    {
                        if (reader.TokenKind == TokenKind.ListEnd)
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
                            var value = await _elementReader.ReadAsync(reader).ConfigureAwait(false);
                            _fnAdd(list, value);
                        }
                        else
                        {
                            break;
                        }
                    }

                    return _fnMap(list);

                default:
                    await reader.MoveToNextTokenAsync().ConfigureAwait(false);
                    return default;
            }
        }
    }

    /// <summary>
    /// A <see cref="JsonTypeReader"/> for a type that can be constructed from a list.
    /// </summary>
    public class JsonListAssignableReader<TType, TElement> 
        : JsonListReader<TType, List<TElement>, TElement>
        where TType : class
    {
        public JsonListAssignableReader(JsonTypeReader<TElement> elementReader)
            : base(
                elementReader, 
                () => new List<TElement>(),
                (list, value) => list.Add(value!),
                (list) => (TType)(object)list)
        {
        }
    }

    /// <summary>
    /// A <see cref="JsonTypeReader"/> for mutable list-like types with an Add method.
    /// </summary>
    public class JsonListAddReader<TType, TElement> 
        : JsonListReader<TType, TType, TElement>
        where TType : class, new()
    {
        public JsonListAddReader(
            JsonTypeReader<TElement> elementReader,
            Action<TType, TElement?> fnAdd)
            : base(
                  elementReader,
                  () => new TType(),
                  fnAdd,
                  list => list)
        {
        }
    }

    /// <summary>
    /// A <see cref="JsonTypeReader"/> for a type that can be constructed from a list.
    /// </summary>
    public class JsonListConstructableReader<TType, TElement> 
        : JsonListReader<TType, List<TElement?>, TElement>
    {
        public JsonListConstructableReader(
            JsonTypeReader<TElement> elementReader,
            Func<IEnumerable<TElement?>, TType> fnConstructor)
            : base(
                  elementReader,
                  () => new List<TElement?>(),
                  (list, value) => list.Add(value),
                  list => fnConstructor(list))
        {
        }
    }

    /// <summary>
    /// A <see cref="JsonTypeReader"/> for arrays.
    /// </summary>
    public class JsonArrayAssignableReader<TType, TElement> 
        : JsonListReader<TType, List<TElement?>, TElement>
    {
        public JsonArrayAssignableReader(
            JsonTypeReader<TElement> elementReader)
            : base(
                  elementReader,
                  () => new List<TElement?>(),
                  (list, value) => list.Add(value!),
                  items => (TType)(object)items.ToArray())
        {
        }
    }
}
