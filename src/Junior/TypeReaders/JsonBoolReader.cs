namespace Junior
{
    public class JsonBoolReader : JsonTypeReader<bool>
    {
        public static readonly JsonBoolReader Instance = new JsonBoolReader();
        public static readonly JsonTypeReader<bool?> NullableInstance = new JsonNullableReader<bool>(Instance);

        public override bool Read(JsonTokenReader reader)
        {
            if (!reader.HasToken)
                reader.MoveToNextToken();

            switch (reader.TokenKind)
            {
                case TokenKind.True:
                    reader.MoveToNextToken();
                    return true;
                case TokenKind.String:
                    if (reader.TokenInBuffer)
                    {
                        bool.TryParse(reader.CurrentValueChunk, out var value);
                        reader.MoveToNextToken();
                        return value;
                    }
                    else
                    {
                        var text = reader.ReadTokenValue();
                        bool.TryParse(text, out var value);
                        return value;
                    }
                default:
                    if (reader.TokenKind.IsValueStart())
                        reader.MoveToNextElement();
                    return false;
            }
        }

        public override async ValueTask<bool> ReadAsync(JsonTokenReader reader)
        {
            if (!reader.HasToken)
                await reader.MoveToNextTokenAsync().ConfigureAwait(false);

            switch (reader.TokenKind)
            {
                case TokenKind.True:
                    await reader.MoveToNextTokenAsync().ConfigureAwait(false);
                    return true;
                case TokenKind.String:
                    if (reader.TokenInBuffer)
                    {
                        bool.TryParse(reader.CurrentValueChunk, out var value);
                        await reader.MoveToNextTokenAsync().ConfigureAwait(false);
                        return value;
                    }
                    else
                    {
                        var text = await reader.ReadTokenValueAsync().ConfigureAwait(false);
                        bool.TryParse(text, out var value);
                        return value;
                    }
                default:
                    if (reader.TokenKind.IsValueStart())
                        await reader.MoveToNextElementAsync().ConfigureAwait(false);
                    return false;
            }
        }
    }
}
