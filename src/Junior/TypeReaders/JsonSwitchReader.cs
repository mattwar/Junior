namespace Junior
{
    /// <summary>
    /// A <see cref="JsonTypeReader"/> that can be read in 
    /// different ways depending of the json element available.
    /// </summary>
    public class JsonSwitchReader<T> : JsonTypeReader<T>
    {
        private readonly JsonTypeReader<T>? _stringReader;
        private readonly JsonTypeReader<T>? _numberReader;
        private readonly JsonTypeReader<T>? _boolReader;
        private readonly JsonTypeReader<T>? _listReader;
        private readonly JsonTypeReader<T>? _objectReader;

        public JsonSwitchReader(
            JsonTypeReader<T>? stringReader,
            JsonTypeReader<T>? numberReader,
            JsonTypeReader<T>? boolReader, 
            JsonTypeReader<T>? listReader,
            JsonTypeReader<T>? objectReader)
        {
            _stringReader = stringReader;
            _numberReader = numberReader;
            _boolReader = boolReader;
            _listReader = listReader;
            _objectReader = objectReader;
        }

        public override T? Read(JsonTokenReader tokenReader)
        {
            if (!tokenReader.HasToken)
                tokenReader.MoveToNextToken();

            switch (tokenReader.TokenKind)
            {
                case TokenKind.String:
                    if (_stringReader != null)
                        return _stringReader.Read(tokenReader);
                    break;

                case TokenKind.Number:
                    if (_numberReader != null)
                        return _numberReader.Read(tokenReader);
                    break;

                case TokenKind.True:
                case TokenKind.False:
                    if (_boolReader != null)
                        return _boolReader.Read(tokenReader);
                    break;

                case TokenKind.ListStart:
                    if (_listReader != null)
                        return _listReader.Read(tokenReader);
                    break;

                case TokenKind.ObjectStart:
                    if (_objectReader != null)
                        return _objectReader.Read(tokenReader);
                    break;
            }

            if (tokenReader.TokenKind.IsValueStart())
                tokenReader.MoveToNextElement();

            return default;
        }

        public override async ValueTask<T?> ReadAsync(JsonTokenReader tokenReader)
        {
            if (!tokenReader.HasToken)
                await tokenReader.MoveToNextTokenAsync().ConfigureAwait(false);

            switch (tokenReader.TokenKind)
            {
                case TokenKind.String:
                    if (_stringReader != null)
                        return await _stringReader.ReadAsync(tokenReader).ConfigureAwait(false);
                    break;

                case TokenKind.Number:
                    if (_numberReader != null)
                        return await _numberReader.ReadAsync(tokenReader).ConfigureAwait(false);
                    break;

                case TokenKind.True:
                case TokenKind.False:
                    if (_boolReader != null)
                        return await _boolReader.ReadAsync(tokenReader).ConfigureAwait(false);
                    break;

                case TokenKind.ListStart:
                    if (_listReader != null)
                        return await _listReader.ReadAsync(tokenReader).ConfigureAwait(false);
                    break;

                case TokenKind.ObjectStart:
                    if (_objectReader != null)
                        return await _objectReader.ReadAsync(tokenReader).ConfigureAwait(false);
                    break;
            }

            if (tokenReader.TokenKind.IsValueStart())
                tokenReader.MoveToNextElement();

            return default;
        }
    }
}
