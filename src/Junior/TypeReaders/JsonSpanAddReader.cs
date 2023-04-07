using System.Text;

namespace Junior
{
    public delegate void SpanAdder<T>(T instance, ReadOnlySpan<char> span);

    /// <summary>
    /// A <see cref="JsonTypeReader"/> that can constructs a type from a series of spans.
    /// </summary>
    public class JsonSpanAddReader<TType, TSpanAddable>
        : JsonTypeReader<TType>
    {
        private readonly Func<TSpanAddable> _fnCreate;
        private readonly SpanAdder<TSpanAddable> _fnAdd;
        private readonly Func<TSpanAddable, TType> _fnMap;

        public JsonSpanAddReader(
            Func<TSpanAddable> fnCreate,
            SpanAdder<TSpanAddable> fnAdd,
            Func<TSpanAddable, TType> fnMap)
        {
            _fnCreate = fnCreate;
            _fnAdd = fnAdd;
            _fnMap = fnMap;
        }

        public override TType? Read(JsonTokenReader reader)
        {
            if (!reader.HasToken)
                reader.MoveToNextToken();

            var list = _fnCreate();

            while (reader.ReadNextTokenChars())
            {
                _fnAdd(list, reader.CurrentValueSpan);
            }

            reader.MoveToNextToken();

            return _fnMap(list);
        }

        public override async ValueTask<TType?> ReadAsync(JsonTokenReader reader)
        {
            if (!reader.HasToken)
                await reader.MoveToNextTokenAsync().ConfigureAwait(false);

            var list = _fnCreate();

            while (await reader.ReadNextTokenCharsAsync().ConfigureAwait(false))
            {
                _fnAdd(list, reader.CurrentValueSpan);
            }

            await reader.MoveToNextTokenAsync().ConfigureAwait(false);

            return _fnMap(list);
        }
    }

    public class JsonStringBuilderReader
        : JsonSpanAddReader<StringBuilder, StringBuilder>
    {
        public static readonly JsonStringBuilderReader Instance = new JsonStringBuilderReader();

        public JsonStringBuilderReader()
            : base(
                  () => new StringBuilder(),
                  (sb, span) => sb.Append(span),
                  sb => sb)
        {
        }
    }

    public class JsonStreamReader
        : JsonSpanAddReader<Stream, StreamWriter>
    {
        public static readonly JsonStreamReader Instance = new JsonStreamReader();

        public JsonStreamReader()
            : base(
                  () => new StreamWriter(new MemoryStream()),
                  (sw, span) => sw.Write(span),
                  (sw) => { sw.Flush(); return sw.BaseStream; })
        {
        }
    }
}
