namespace Junior
{
    public delegate TType SpanMapper<TType>(ReadOnlySpan<char> span);

    /// <summary>
    /// A <see cref="JsonTypeReader"/> that can construct a type from a span.
    /// </summary>
    public class JsonSpanReader<TType> 
        : JsonTypeReader<TType>
    {
        private readonly SpanMapper<TType> _fnMap;

        public JsonSpanReader(
            SpanMapper<TType> fnMap)
        {
            _fnMap = fnMap;
        }

        public override TType? Read(JsonTokenReader reader)
        {
            if (!reader.HasToken)
                reader.MoveToNextToken();

            if (reader.TokenInBuffer)
            {
                var value = _fnMap(reader.CurrentValueSpan);
                reader.MoveToNextToken();
                return value;
            }
            else
            {
                var text = reader.ReadTokenValue();
                var value = _fnMap(text.AsSpan());
                return value;
            }
        }

        public override async ValueTask<TType?> ReadAsync(JsonTokenReader reader)
        {
            if (!reader.HasToken)
                await reader.MoveToNextTokenAsync().ConfigureAwait(false);

            if (reader.TokenInBuffer)
            {
                var value = _fnMap(reader.CurrentValueSpan);
                await reader.MoveToNextTokenAsync().ConfigureAwait(false);
                return value;
            }
            else
            {
                var text = await reader.ReadTokenValueAsync().ConfigureAwait(false);
                var value = _fnMap(text.AsSpan());
                return value;
            }
        }
    }

    public class JsonSpanParsableReader<TType> 
        : JsonSpanReader<TType>
        where TType : ISpanParsable<TType>
    {
        public static readonly JsonSpanParsableReader<TType> Instance = new JsonSpanParsableReader<TType>();

        public JsonSpanParsableReader()
            : base(span => Parse(span))
        {
        }

        private static TType Parse(ReadOnlySpan<char> span)
        {
            TType.TryParse(span, null, out var value);
            return value!;
        }
    }
}
