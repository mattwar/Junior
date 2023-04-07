namespace Junior
{
    public class JsonNullableReader<TStruct> : JsonTypeReader<TStruct?>
        where TStruct: struct
    {
        private readonly JsonTypeReader<TStruct> _structReader;

        public JsonNullableReader(JsonTypeReader<TStruct> structReader)
        {
            _structReader = structReader;
        }

        public override TStruct? Read(JsonTokenReader reader)
        {
            if (!reader.HasToken)
                reader.MoveToNextToken();

            switch (reader.TokenKind)
            {
                case TokenKind.Null:
                    reader.MoveToNextToken();
                    return null;
                default:
                    return _structReader.Read(reader);
            }
        }

        public override async ValueTask<TStruct?> ReadAsync(JsonTokenReader reader)
        {
            if (!reader.HasToken)
                await reader.MoveToNextTokenAsync().ConfigureAwait(false);

            switch (reader.TokenKind)
            {
                case TokenKind.Null:
                    await reader.MoveToNextTokenAsync().ConfigureAwait(false);
                    return null;
                default:
                    return await _structReader.ReadAsync(reader).ConfigureAwait(false);
            }
        }
    }
}
