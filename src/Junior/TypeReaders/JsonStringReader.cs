namespace Junior
{
    public class JsonStringReader<TValue> : JsonTypeReader<TValue>
    {
        private readonly Func<string, TValue> _fnMap;

        public JsonStringReader(Func<string, TValue> fnMap)
        {
            _fnMap = fnMap;
        }

        public override TValue? Read(JsonTokenReader reader)
        {
            if (!reader.HasToken)
                reader.MoveToNextToken();

            switch (reader.TokenKind)
            {
                case TokenKind.Null:
                    reader.MoveToNextToken();
                    return default;

                case TokenKind.True:
                case TokenKind.False:
                case TokenKind.String:
                case TokenKind.Number:
                    var value = reader.ReadTokenValue();
                    return _fnMap(value);

                default:
                    // TODO: should return json of element?
                    reader.MoveToNextElement();
                    return default;
            }
        }

        public override async ValueTask<TValue?> ReadAsync(JsonTokenReader reader)
        {
            if (!reader.HasToken)
                await reader.MoveToNextTokenAsync().ConfigureAwait(false);

            switch (reader.TokenKind)
            {
                case TokenKind.Null:
                    await reader.MoveToNextTokenAsync().ConfigureAwait(false);
                    return default;

                case TokenKind.True:
                case TokenKind.False:
                case TokenKind.String:
                case TokenKind.Number:
                    var value = await reader.ReadTokenValueAsync().ConfigureAwait(false);
                    return _fnMap(value);

                default:
                    // TODO: should return json of element?
                    await reader.MoveToNextElementAsync().ConfigureAwait(false);
                    return default;
            }
        }
    }

    public class JsonStringParsableReader<TValue>
        : JsonStringReader<TValue>
        where TValue : IParsable<TValue>
    {
        public JsonStringParsableReader()
            : base(Parse)
        {
        }

        private static TValue Parse(string text)
        {
            TValue.TryParse(text, null, out var value);
            return value!;
        }
    }

    public class JsonStringAssignableReader<TValue>
        : JsonStringReader<TValue>
        where TValue : class
    {
        public static readonly JsonStringAssignableReader<TValue> Instance = 
            new JsonStringAssignableReader<TValue>();

        public JsonStringAssignableReader()
            : base(text => (TValue)(object)text)
        {
        }
    }
}
