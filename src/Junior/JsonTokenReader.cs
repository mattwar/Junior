using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Junior
{
    public enum TokenKind : byte
    {
        Unknown,    // not yet read
        None,       // not a token, at end of stream
        ListStart,
        ListEnd,
        ObjectStart,
        ObjectEnd,
        Number,
        String,
        True,
        False,
        Null,
        Comma,
        Colon,
        Error
    }

    public static class TokenKindExtensions
    {
        public static bool IsValueStart(this TokenKind kind)
        {
            switch (kind)
            {
                case TokenKind.ListStart:
                case TokenKind.ListEnd:
                case TokenKind.ObjectStart:
                case TokenKind.ObjectEnd:
                case TokenKind.Number:
                case TokenKind.String:
                case TokenKind.True:
                case TokenKind.False:
                case TokenKind.Null:
                    return true;
                default:
                    return false;
            }
        }
    }

    /// <summary>
    /// A reader that reads single JSON tokens.
    /// </summary>
    public class JsonTokenReader
    {
        private readonly TextReader _reader;
        private char[] _buffer;
        private char[] _decodeBuffer;
        private long _bufferStart;
        private int _bufferOffset;
        private int _bufferLength;
        private TokenInfo _token; // the current token
        private bool _decoded; // token value is already decode in buffer
        private bool _done;

        private enum TokenStage
        {
            /// <summary>
            /// The reader has not yet advanced to the next token
            /// </summary>
            Unread,

            /// <summary>
            /// The reader has advanced to the next token and identified that the 
            /// entire token is in the buffer.
            /// </summary>
            InBuffer,

            /// <summary>
            /// The reader has advanced to the start of the next token and identified
            /// its kind, but it is too large to fit in the buffer and will require
            /// reading through <see cref="ReadNextTokenValueCharsAsync"/>.
            /// </summary>
            Start,

            /// <summary>
            /// The reader had advanced beyond the start of the current token, 
            /// as the values characters are being read with <see cref="ReadNextTokenValueCharsAsync"/>.
            /// </summary>
            Interior,

            /// <summary>
            /// The reader has advanced to the end of the current token.
            /// All value characters have been read with <see cref="ReadNextTokenValueCharsAsync"/>.
            /// </summary>
            End
        }

        public const int DefaultBufferSize = 4096;

        /// <summary>
        /// Creates a new instance of <see cref="JsonTokenReader"/>.
        /// The reader is positioned before the first token.
        /// </summary>
        public JsonTokenReader(TextReader reader, int bufferSize = DefaultBufferSize)
        {
            _reader = reader;
            _buffer = new char[bufferSize];
            _decodeBuffer = new char[bufferSize];
            _token = default;
        }

        /// <summary>
        /// Creates a new instance of <see cref="JsonTokenReader"/>.
        /// The reader is moved to the first token.
        /// </summary>
        public static JsonTokenReader Create(TextReader textReader, int bufferSize = DefaultBufferSize)
        {
            var reader = new JsonTokenReader(textReader, bufferSize);
            reader.MoveToNextToken();
            return reader;
        }

        /// <summary>
        /// Creates a new instance of <see cref="JsonTokenReader"/>.
        /// The reader is moved to the first token.
        /// </summary>
        public static async ValueTask<JsonTokenReader> CreateAsync(TextReader textReader, int bufferSize = DefaultBufferSize)
        {
            var reader = new JsonTokenReader(textReader, bufferSize);
            await reader.MoveToNextTokenAsync().ConfigureAwait(false);
            return reader;
        }

        /// <summary>
        /// The position within the text stream.
        /// </summary>
        public long Position => _bufferStart + _bufferOffset;

        /// <summary>
        /// Returns true if the reader is positioned on a token.
        /// Returns false if the reader is positioned before the start of the stream
        /// or after the end of the stream.
        /// </summary>
        public bool HasToken =>
            _token.Stage != TokenStage.Unread
            && _token.Kind != TokenKind.None;

        /// <summary>
        /// The kind of token just read.
        /// </summary>
        public TokenKind TokenKind => _token.Kind;

        /// <summary>
        /// The character length the current token.
        /// </summary>
        public int TokenLength => _token.Length;

        /// <summary>
        /// True if the number token has a decimal
        /// </summary>
        public bool HasDecimal => _token.HasDecimal;

        /// <summary>
        /// true if the number token has an exponent
        /// </summary>
        public bool HasExponent => _token.HasExponent;

        /// <summary>
        /// True if the string token has escapes.
        /// </summary>
        public bool HasEscapes => _token.HasEscapes;

        /// <summary>
        /// Trur if the entire token is available in the buffer
        /// </summary>
        public bool TokenInBuffer => _token.Stage == TokenStage.InBuffer;

        /// <summary>
        /// The latest read span of token text characters.
        /// </summary>
        public ReadOnlySpan<char> CurrentTextSpan =>
            _buffer.AsSpan().Slice(_token.Start, _token.Length);

        /// <summary>
        /// The latest read span of token value characters.
        /// </summary>
        public ReadOnlySpan<char> CurrentValueSpan
        {
            get
            {
                if (this.TokenKind == TokenKind.String)
                {
                    if (!_decoded)
                    {
                        DecodeStringInBuffer(_decodeBuffer, out var decodedLength);
                        _token.DecodedLength = decodedLength;
                        _decoded = true;
                    }

                    return _decodeBuffer.AsSpan().Slice(0, _token.DecodedLength);
                }
                else
                {
                    return CurrentTextSpan;
                }
            }
        }

        /// <summary>
        /// Gets the text of the current token text.
        /// Returns false if the entire token is not fully held within in the buffer.
        /// </summary>
        public bool TryGetTokenText([NotNullWhen(true)] out string? value)
        {
            switch (_token.Kind)
            {
                case TokenKind.ListStart:
                    value = "[";
                    return true;
                case TokenKind.ListEnd:
                    value = "]";
                    return true;
                case TokenKind.ObjectStart:
                    value = "{";
                    return true;
                case TokenKind.ObjectEnd:
                    value = "}";
                    return true;
                case TokenKind.Comma:
                    value = ",";
                    return true;
                case TokenKind.Colon:
                    value = ":";
                    return true;
                case TokenKind.True:
                    value = "true";
                    return true;
                case TokenKind.False:
                    value = "false";
                    return true;
                case TokenKind.Null:
                    value = "null";
                    return true;
                default:
                    if (this.TokenInBuffer)
                    {
                        value = new string(_buffer, _token.Start, _token.Length);
                        return true;
                    }
                    break;
            }

            value = null;
            return false;
        }

        /// <summary>
        /// Reads the text of the current token.
        /// After reading the text, the reader is moved to the next token.
        /// </summary>
        public string ReadTokenText(bool moveToNextToken = true)
        {
            if (this.TryGetTokenText(out var tokenText))
            {
                MoveToNextToken();
                return tokenText;
            }
            else
            {
                var builder = new System.Text.StringBuilder();

                while (ReadNextTokenChars())
                {
                    builder.Append(this.CurrentValueSpan);
                }

                MoveToNextToken();
                return builder.ToString();
            }
        }

        /// <summary>
        /// Gets the text of the current token, asynchronously if necessary.
        /// After reading the text, the reader is moved to the next token.
        /// </summary>
        public async ValueTask<string> ReadTokenTextAsync()
        {
            if (this.TryGetTokenText(out var tokenText))
            {
                await MoveToNextTokenAsync().ConfigureAwait(false);
                return tokenText;
            }
            else
            {
                var builder = new System.Text.StringBuilder();

                while (await this.ReadNextTokenCharsAsync().ConfigureAwait(false))
                {
                    builder.Append(this.CurrentValueSpan);
                }

                await MoveToNextTokenAsync().ConfigureAwait(false);
                return builder.ToString();
            }
        }

        /// <summary>
        /// Reads the next block of characters of the current token.
        /// Returns false if there are no more characters.
        /// </summary>
        public bool ReadNextTokenChars()
        {
            if (_token.Stage == TokenStage.InBuffer)
            {
                AdvanceInBuffer(_token.Length);
                _token.Stage = TokenStage.End;
                return true;
            }
            else if (_token.Kind == TokenKind.String
                && (_token.Stage == TokenStage.Start || _token.Stage == TokenStage.InBuffer))
            {
                while (true)
                {
                    var length = 0;
                    if (_token.Stage == TokenStage.Start)
                    {
                        length = 1; // skip first quote
                        _token.Stage = TokenStage.Interior;
                    }

                    int interiorLength = ScanStringInteriorInBuffer(length, out var decodedSegmentLength, out var segmentHasEscapes);
                    length += interiorLength;
                    if (length > 0)
                    {
                        var ch = PeekInBuffer(length);
                        if (ch == '"')
                        {
                            length++;
                            _token.Stage = TokenStage.End;
                        }
                        else if (ch == '\0' && _done)
                        {
                            _token.Stage = TokenStage.End;
                        }

                        _token.Start = _bufferOffset;
                        _token.Length = length;
                        _token.DecodedLength = decodedSegmentLength;
                        _token.HasEscapes |= segmentHasEscapes;
                        AdvanceInBuffer(length);
                        return true;
                    }

                    ReadBuffer();
                }
            }
            else if (_token.Stage == TokenStage.Start
                || _token.Stage == TokenStage.Interior)
            {
                _token.Stage = TokenStage.Interior;

                while (true)
                {
                    _token.Start = _bufferOffset;
                    _token.Length = 0;

                    if (ScanUntilMatchOrEndOfBuffer(0, IsTokenEnd, out var segmentLength)
                        || _done)
                    {
                        AdvanceInBuffer(segmentLength);
                        _token.Length = segmentLength;
                        _token.DecodedLength = segmentLength;
                        _token.Stage = TokenStage.End;
                        return segmentLength > 0;
                    }
                    else if (segmentLength > 0)
                    {
                        AdvanceInBuffer(segmentLength);
                        _token.Length = segmentLength;
                        _token.DecodedLength = segmentLength;
                        return true;
                    }

                    ReadBuffer();
                }
            }

            return false;
        }

        /// <summary>
        /// Reads the next block of characters of the current token, asynchronously if necessary.
        /// Returns false if there are no more characters.
        /// </summary>
        public async ValueTask<bool> ReadNextTokenCharsAsync()
        {
            if (_token.Stage == TokenStage.InBuffer)
            {
                AdvanceInBuffer(_token.Length);
                _token.Stage = TokenStage.End;
                return true;
            }
            else if (_token.Kind == TokenKind.String
                && (_token.Stage == TokenStage.Start || _token.Stage == TokenStage.InBuffer))
            {
                while (true)
                {
                    var length = 0;
                    if (_token.Stage == TokenStage.Start)
                    {
                        length = 1; // skip first quote
                        _token.Stage = TokenStage.Interior;
                    }

                    int interiorLength = ScanStringInteriorInBuffer(length, out var decodedSegmentLength, out var segmentHasEscapes);
                    length += interiorLength;
                    if (length > 0)
                    {
                        var ch = PeekInBuffer(length);
                        if (ch == '"')
                        {
                            length++;
                            _token.Stage = TokenStage.End;
                        }
                        else if (ch == '\0' && _done)
                        {
                            _token.Stage = TokenStage.End;
                        }

                        _token.Start = _bufferOffset;
                        _token.Length = length;
                        _token.DecodedLength = decodedSegmentLength;
                        _token.HasEscapes |= segmentHasEscapes;
                        AdvanceInBuffer(length);
                        return true;
                    }

                    await ReadBufferAsync().ConfigureAwait(false);
                }
            }
            else if (_token.Stage == TokenStage.Start
                || _token.Stage == TokenStage.Interior)
            {
                _token.Stage = TokenStage.Interior;

                while (true)
                {
                    _token.Start = _bufferOffset;
                    if (ScanUntilMatchOrEndOfBuffer(0, IsTokenEnd, out var segmentLength)
                        || _done)
                    {
                        AdvanceInBuffer(segmentLength);
                        _token.Length = segmentLength;
                        _token.DecodedLength = segmentLength;
                        _token.Stage = TokenStage.End;
                        return segmentLength > 0;
                    }
                    else if (segmentLength > 0)
                    {
                        AdvanceInBuffer(segmentLength);
                        _token.Length = segmentLength;
                        _token.DecodedLength = segmentLength;
                        return true;
                    }

                    await ReadBufferAsync().ConfigureAwait(false);
                }
            }

            return false;
        }

        private static bool IsTokenEnd(char ch)
        {
            switch (ch)
            {
                case ',':
                case ':':
                case '[':
                case ']':
                case '{':
                case '}':
                    return true;
                default:
                    return char.IsWhiteSpace(ch);
            }
        }

        /// <summary>
        /// Gets the current token value as a string, if it is available in the buffer.
        /// Returns false if the token value is not fully in the buffer.
        /// </summary>
        public bool TryGetTokenValue([NotNullWhen(true)] out string? value)
        {
            switch (_token.Kind)
            {
                case TokenKind.String:
                    if (this.TokenInBuffer)
                    {
                        value = new string(this.CurrentValueSpan);
                        return true;
                    }
                    break;

                default:
                    return TryGetTokenText(out value);
            }

            value = null;
            return false;
        }

        /// <summary>
        /// Gets the current token value as the type <see cref="TValue"/>,
        /// if it is available in the buffer.
        /// </summary>
        public bool TryGetTokenValueAs<TValue>([MaybeNullWhen(false)] out TValue value)
            where TValue : ISpanParsable<TValue>
        {
            if (this.TokenInBuffer)
            {
                return TValue.TryParse(this.CurrentValueSpan, null, out value);
            }
            else
            {
                value = default;
                return false;
            }
        }

        /// <summary>
        /// </summary>
        public TValue? ReadTokenValueAs<TValue>()
            where TValue : ISpanParsable<TValue>
        {
            if (TryGetTokenValueAs<TValue>(out var value))
            {
                MoveToNextToken();
                return value;
            }
            else
            {
                MoveToNextToken();
                return default;
            }
        }

        /// <summary>
        /// Reads the value of the current token, asynchronously if necessary.
        /// This is the same as <see cref="ReadTokenTextAsync"/> except string literals are converted to 
        /// After reading the value, the reader is moved to the next token.
        /// </summary>
        public string ReadTokenValue()
        {
            if (this.TryGetTokenValue(out var stringValue))
            {
                MoveToNextToken();
                return stringValue;
            }
            else
            {
                var builder = new System.Text.StringBuilder();

                while (ReadNextTokenValueChars())
                {
                    builder.Append(this.CurrentValueSpan);
                }

                MoveToNextToken();
                return builder.ToString();
            }
        }

        /// <summary>
        /// Reads the value text of the current token, asynchronously if necessary.
        /// This is the same as <see cref="ReadTokenTextAsync"/> except string literals are converted to 
        /// After reading the value, the reader is moved to the next token.
        /// </summary>
        public async ValueTask<string> ReadTokenValueAsync()
        {
            if (this.TryGetTokenValue(out var stringValue))
            {
                await MoveToNextTokenAsync().ConfigureAwait(false);
                return stringValue;
            }
            else
            {
                var builder = new System.Text.StringBuilder();

                while (await this.ReadNextTokenValueCharsAsync().ConfigureAwait(false))
                {
                    builder.Append(this.CurrentValueSpan);
                }

                await MoveToNextTokenAsync().ConfigureAwait(false);

                return builder.ToString();
            }
        }

        /// <summary>
        /// Reads the next block of decoded value chars from the current token.
        /// </summary>
        public bool ReadNextTokenValueChars()
        {
            // only strings need to be decoded
            if (_token.Kind != TokenKind.String)
                return ReadNextTokenChars();

            var segmentLength = 0;

            _decoded = false;
            _token.DecodedLength = 0;
            _token.Start = _bufferOffset;

            if (_token.Stage == TokenStage.Start)
            {
                AdvanceInBuffer(1); // skip start quote;
                segmentLength++;
                _token.Stage = TokenStage.Interior;
            }

            while (_token.Stage == TokenStage.Interior)
            {
                var interiorLength = DecodeStringInteriorInBuffer(0, _decodeBuffer, out var segmentDecodedLength);
                _decoded = true;

                if (interiorLength > 0)
                {
                    AdvanceInBuffer(interiorLength);
                    segmentLength += interiorLength;
                }

                if (segmentLength > 0)
                {
                    var ch = PeekInBuffer();
                    if (ch == '"')
                    {
                        AdvanceInBuffer(1);
                        segmentLength++;
                        _token.Length = segmentLength;
                        _token.DecodedLength = segmentDecodedLength;
                        _token.Stage = TokenStage.End;
                        return true;
                    }
                    else if (ch == '\0' && _done)
                    {
                        _token.Length = segmentLength;
                        _token.DecodedLength = segmentDecodedLength;
                        _token.Stage = TokenStage.End;
                        return segmentLength > 0;
                    }
                    break;
                }

                ReadBuffer();
            }

            return false;
        }

        /// <summary>
        /// Reads the next block of decoded value chars from the current token, asynchronously if necessary.
        /// </summary>
        public async ValueTask<bool> ReadNextTokenValueCharsAsync()
        {
            if (_token.Kind != TokenKind.String)
                return await ReadNextTokenCharsAsync().ConfigureAwait(false);

            var length = 0;
            _token.Start = _bufferOffset;
            _token.Length = 0;

            if (_token.Stage == TokenStage.Start)
            {
                AdvanceInBuffer(1); // skip start quote;
                length++;
                _token.Stage = TokenStage.Interior;
            }

            _token.DecodedLength = 0;
            _decoded = false;

            while (_token.Stage == TokenStage.Interior)
            {
                var interiorLength = DecodeStringInteriorInBuffer(0, _decodeBuffer, out var segmentDecodedLength);
                _decoded = true;
                _token.DecodedLength = segmentDecodedLength;

                if (interiorLength > 0)
                {
                    AdvanceInBuffer(interiorLength);
                    length += interiorLength;
                }

                if (length > 0)
                {
                    var ch = PeekInBuffer();
                    if (ch == '"')
                    {
                        AdvanceInBuffer(1);
                        length++;

                        _token.Length = length;
                        _token.Stage = TokenStage.End;
                    }
                    else if (ch == '\0' && _done)
                    {
                        _token.Length = length;
                        _token.Stage = TokenStage.End;
                    }
                    else
                    {
                        _token.Length = length;
                    }
                    break;
                }

                await ReadBufferAsync().ConfigureAwait(false);
            }

            return _token.DecodedLength > 0;
        }

        /// <summary>
        /// Reads the next token if it fully available within the read buffer.
        /// Returns false if the next token is not fully within the buffer
        /// or there are no more tokens.
        /// </summary>
        public bool TryMoveNextInBuffer()
        {
            if (_token.Kind == TokenKind.None)
            {
                return false;
            }

            // skip over current token
            if (_token.Stage == TokenStage.InBuffer)
            {
                AdvanceInBuffer(_token.Length);
            }
            else if (_token.Stage == TokenStage.Start
                || _token.Stage == TokenStage.Interior)
            {
                // must use ReadNextTokenAsync
                return false;
            }

            _decoded = false;

            if (ScanTokenInBuffer(0, out _token))
            {
                _bufferOffset = _token.Start;
                return _token.Kind != TokenKind.None;
            }

            return false;
        }

        /// <summary>
        /// Moves forward to the next token, asynchronously if necessary.
        /// </summary>
        public bool MoveToNextToken()
        {
            // skip over current token
            if (_token.Stage == TokenStage.InBuffer)
            {
                AdvanceInBuffer(_token.Length);
            }
            else if (_token.Stage == TokenStage.Start
                || _token.Stage == TokenStage.Interior)
            {
                // read past remaining token chars
                while (ReadNextTokenChars())
                {
                }
            }

            _decoded = false;
            while (true)
            {
                if (ScanTokenInBuffer(0, out _token)
                    && IsInBufferOrFillsBuffer(in _token))
                {
                    _bufferOffset = _token.Start;
                    return _token.Kind != TokenKind.None;
                }

                if (_done)
                    return false;

                ReadBuffer();
            }
        }

        /// <summary>
        /// Moves forward to the next token, asynchronously if necessary.
        /// </summary>
        public async ValueTask<bool> MoveToNextTokenAsync()
        {
            // skip over current token
            if (_token.Stage == TokenStage.InBuffer)
            {
                AdvanceInBuffer(_token.Length);
            }
            else if (_token.Stage == TokenStage.Start
                || _token.Stage == TokenStage.Interior)
            {
                // read past remaining token chars
                while (await ReadNextTokenCharsAsync().ConfigureAwait(false))
                {
                }
            }

            _decoded = false;

            while (true)
            {
                if (ScanTokenInBuffer(0, out _token)
                    && IsInBufferOrFillsBuffer(in _token))
                {
                    _bufferOffset = _token.Start;
                    return _token.Kind != TokenKind.None;
                }

                if (_done)
                    return false;

                await ReadBufferAsync().ConfigureAwait(false);
            }
        }

        private bool IsInBufferOrFillsBuffer(in TokenInfo token) =>
            token.Stage == TokenStage.InBuffer
            || (token.Stage == TokenStage.Start && token.Length == _buffer.Length);

        /// <summary>
        /// Moves past the current token, list or object.
        /// </summary>
        public bool MoveToNextElement()
        {
            if (_token.Kind == TokenKind.ListStart
                || _token.Kind == TokenKind.ObjectStart)
            {
                var count = 1;
                while (count > 0)
                {
                    if (!MoveToNextToken())
                        return false;

                    if (_token.Kind == TokenKind.ListStart
                        || _token.Kind == TokenKind.ObjectStart)
                    {
                        count++;
                    }
                    else if (_token.Kind == TokenKind.ListEnd
                        || _token.Kind == TokenKind.ObjectEnd)
                    {
                        count--;
                        if (count == 0)
                            break;
                    }
                }
            }

            return MoveToNextToken();
        }

        /// <summary>
        /// Moves past the current token, list or object.
        /// </summary>
        public async ValueTask<bool> MoveToNextElementAsync()
        {
            if (_token.Kind == TokenKind.ListStart
                || _token.Kind == TokenKind.ObjectStart)
            {
                var count = 1;
                while (count > 0)
                {
                    if (!await MoveToNextTokenAsync().ConfigureAwait(false))
                        return false;

                    if (_token.Kind == TokenKind.ListStart
                        || _token.Kind == TokenKind.ObjectStart)
                    {
                        count++;
                    }
                    else if (_token.Kind == TokenKind.ListEnd
                        || _token.Kind == TokenKind.ObjectEnd)
                    {
                        count--;
                        if (count == 0)
                            break;
                    }
                }
            }

            return await MoveToNextTokenAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Identifies the next token's kind if it can be reached within the buffer.
        /// </summary>
        public TokenKind PeekTokenKind(int index = 0)
        {
            if (PeekTokenInBuffer(out var token, index))
                return token.Kind;

            ReadBuffer();

            if (_token.Stage == TokenStage.Start)
                MoveToNextToken();

            if (PeekTokenInBuffer(out token, index))
                return token.Kind;
    
            return TokenKind.Unknown;
        }

        /// <summary>
        /// Identifies the next token's kind if it can be reached within the buffer.
        /// </summary>
        public async ValueTask<TokenKind> PeekTokenKindAsync(int index = 0)
        {
            if (PeekTokenInBuffer(out var token, index))
                return token.Kind;

            await ReadBufferAsync().ConfigureAwait(false);

            if (_token.Stage == TokenStage.Start)
                await MoveToNextTokenAsync().ConfigureAwait(false);

            if (PeekTokenInBuffer(out token, index))
                return token.Kind;
 
            return TokenKind.Unknown;
        }

        /// <summary>
        /// Identifies the token info if it can be reached within the buffer.
        /// </summary>
        private bool PeekTokenInBuffer(out TokenInfo token, int index = 0)
        {
            token = _token;

            while (index > 0)
            {
                // we need to have an in-buffer token to skip over it
                if (token.Stage != TokenStage.InBuffer
                    && token.Stage != TokenStage.End)
                {
                    token = default;
                    return false;
                }

                // move to end of current token
                var scanOffset = token.Start + token.Length - _bufferOffset;

                if (!ScanTokenInBuffer(scanOffset, out token))
                    return false;
            }

            return true;
        }

        private record struct TokenInfo(
            TokenKind Kind,
            TokenStage Stage,
            int Start,
            int Length,
            int DecodedLength,
            bool HasDecimal,
            bool HasExponent,
            bool HasEscapes)
        {
            public static TokenInfo Unknown(int start) =>
                new TokenInfo(TokenKind.Unknown, TokenStage.InBuffer, start, 0, 0, false, false, false);

            public static TokenInfo None(int start) =>
                new TokenInfo(TokenKind.None, TokenStage.InBuffer, start, 0, 0, false, false, false);

            public static TokenInfo Punctuation(TokenKind kind, int start) =>
                new TokenInfo(kind, TokenStage.InBuffer, start, 1, 1, false, false, false);

            public static TokenInfo Error(int start, int length) =>
                new TokenInfo(TokenKind.Error, TokenStage.Interior, start, length, length, false, false, false);

            public static TokenInfo Number(TokenStage stage, int start, int length, bool hasDecimal, bool hasExponent) =>
                new TokenInfo(TokenKind.Number, stage, start, length, length, hasDecimal, hasExponent, false);

            public static TokenInfo String(TokenStage stage, int start, int length, int decodedLength, bool hasEscapes) =>
                new TokenInfo(TokenKind.String, stage, start, length, decodedLength, false, false, hasEscapes);

            public static TokenInfo True(int start) =>
                new TokenInfo(TokenKind.True, TokenStage.InBuffer, start, 4, 4, false, false, false);

            public static TokenInfo False(int start) =>
                new TokenInfo(TokenKind.False, TokenStage.InBuffer, start, 5, 5, false, false, false);

            public static TokenInfo Null(int start) =>
                new TokenInfo(TokenKind.Null, TokenStage.InBuffer, start, 4, 4, false, false, false);
        }

        /// <summary>
        /// Scans the next token in the buffer.
        /// Returns false if the token is not fully within the read buffer.
        /// </summary>
        private bool ScanTokenInBuffer(
            int offset, 
            out TokenInfo token)
        {
            if (!ScanWhitespaceInBuffer(0, out var whitespaceLen) && !_done)
            {
                token = default;
                return false;
            }

            offset += whitespaceLen;
            var tokenStart = _bufferOffset + offset;

            char ch = PeekInBuffer(offset);
            switch (ch)
            {
                case '\0':
                    if (!_done)
                    {
                        token = TokenInfo.Unknown(tokenStart);
                        return false;
                    }
                    token = TokenInfo.None(tokenStart);
                    return true;
                case '[':
                    token = TokenInfo.Punctuation(TokenKind.ListStart, tokenStart);
                    return true;
                case ']':
                    token = TokenInfo.Punctuation(TokenKind.ListEnd, tokenStart);
                    return true;
                case '{':
                    token = TokenInfo.Punctuation(TokenKind.ObjectStart, tokenStart);
                    return true;
                case '}':
                    token = TokenInfo.Punctuation(TokenKind.ObjectEnd, tokenStart);
                    return true;
                case '-':
                    return ScanNumberInBuffer(offset, out token);
                case ',':
                    token = TokenInfo.Punctuation(TokenKind.Comma, tokenStart);
                    return true;
                case ':':
                    token = TokenInfo.Punctuation(TokenKind.Colon, tokenStart);
                    return true;
                case '"':
                    return ScanStringInBuffer(offset, out token);
                default:
                    if (char.IsDigit(ch))
                    {
                        return ScanNumberInBuffer(offset, out token);
                    }
                    else if (char.IsLetter(ch))
                    {
                        var wordLength = ScanWordInBuffer(offset);
                        if (wordLength == 0)
                        {
                            token = TokenInfo.Unknown(tokenStart);
                        }

                        if (wordLength == 4 && MatchesInBuffer("true"))
                        {
                            token = TokenInfo.True(tokenStart);
                            return true;
                        }
                        else if (wordLength == 5 && MatchesInBuffer("false"))
                        {
                            token = TokenInfo.False(tokenStart);
                            return true;
                        }
                        else if (wordLength == 4 && MatchesInBuffer("null"))
                        {
                            token = TokenInfo.Null(tokenStart);
                            return true;
                        }
                        else
                        {
                            token = TokenInfo.Error(tokenStart, wordLength);
                            return true;
                        }
                    }
                    else
                    {
                        token = TokenInfo.Error(tokenStart, 1);
                        return true;
                    }
            }
        }

        private int ScanWordInBuffer(int offset)
        {
            var start = offset;
            while (char.IsLetter(PeekInBuffer(offset)))
            {
                offset++;
            }

            if (_bufferOffset + offset + 1 < _bufferLength || _done)
            {
                return offset - start;
            }
            else
            {
                return 0;
            }
        }

        /// <summary>
        /// Returns true if the entire number is in the buffer.
        /// </summary>
        private bool ScanNumberInBuffer(int offset, out TokenInfo token)
        {
            var startOffset = offset;
            var hasDecimal = false;
            var hasExponent = false;

            if (PeekInBuffer(offset) == '-')
            {
                offset++;
            }

            while (Char.IsNumber(PeekInBuffer(offset)))
            {
                offset++;
            }

            if ((PeekInBuffer(offset)) == '.')
            {
                offset++;
                hasDecimal = true;

                while (Char.IsNumber(PeekInBuffer(offset)))
                {
                    offset++;
                }
            }

            var ch = PeekInBuffer(offset);
            if (ch == 'e' || ch == 'E')
            {
                offset++;
                hasExponent = true;

                ch = PeekInBuffer(offset);
                if (ch == '+' || ch == '-')
                {
                    offset++;
                }

                while (Char.IsNumber(ch = PeekInBuffer(offset)))
                {
                    offset++;
                }
            }

            var length = offset - startOffset;
            var start = _bufferOffset + startOffset;
            var inBuffer = IsTokenEnd(ch) || (ch == '\0' && _done);
            token = TokenInfo.Number(inBuffer ? TokenStage.InBuffer : TokenStage.Start, start, length, hasDecimal, hasExponent);
            return true;
        }

        /// <summary>
        /// Scans the length of the string literal in the buffer.
        /// Returns true if the entire string is available in the buffer.
        /// </summary>
        private bool ScanStringInBuffer(int offset, out TokenInfo token)
        {
            var startOffset = offset;

            if (PeekInBuffer(offset) != '"')
            {
                token = TokenInfo.Unknown(offset);
                return false;
            }

            offset++; // skip "

            var interiorLen = ScanStringInteriorInBuffer(offset, out var decodedLength, out var containsEscapes);
            offset += interiorLen;

            var ch = PeekInBuffer(offset);

            if (ch == '"')
            {
                offset++;
                var length = offset - startOffset;
                var start = _bufferOffset + startOffset;
                token = TokenInfo.String(TokenStage.InBuffer, start, length, decodedLength, containsEscapes);
                return true;
            }
            else
            {
                var length = offset - startOffset;
                var start = _bufferOffset + startOffset;
                token = TokenInfo.String(TokenStage.Start, start, length, decodedLength, containsEscapes);
                return true;
            }
        }

        /// <summary>
        /// Scans the interior of a string literal that is currently in the buffer.
        /// Returns the number of character/code-points it would have produced.
        /// </summary>
        private int ScanStringInteriorInBuffer(int offset, out int decodedCharCount, out bool containsEscapes)
        {
            var start = offset;
            decodedCharCount = 0;
            containsEscapes = false;

            while (true)
            {
                var ch = PeekInBuffer(offset);
                if (ch == '\0' && !_done)
                    break;

                if (ch == '"')
                {
                    break;
                }
                else if (ch == '\\')
                {
                    containsEscapes = true;
                    var escapeLen = ScanEscapedCharInBuffer(offset);
                    if (escapeLen == 0)
                        return 0;
                    decodedCharCount++;
                    offset += escapeLen;
                }
                else
                {
                    decodedCharCount++;
                    offset++;
                }
            }

            return offset - start;
        }

        private int ScanEscapedCharInBuffer(int offset)
        {
            offset++; // skip first \

            var ch = PeekInBuffer(offset);
            switch (ch)
            {
                case '\0':
                case '"':
                case '\\':
                case '/':
                case 'b':
                case 'f':
                case 'r':
                case 'n':
                case 't':
                default:
                    return 1;
                case 'u':
                    return ScanHexNumberInBuffer(offset, 4);
            }
        }

        private int ScanHexNumberInBuffer(int offset, int maxLength)
        {
            var start = offset;

            while (true)
            {
                var ch = PeekInBuffer(offset);

                if (ch == '\0' && !_done)
                    return 0;

                if (offset - start < maxLength && IsHexDigit(ch))
                {
                    offset++;
                }
                else
                {
                    return offset - start;
                }
            }
        }

        private static bool IsHexDigit(char ch)
        {
            if (ch >= '0' && ch <= '9')
            {
                return true;
            }
            else if (ch >= 'A' && ch <= 'F')
            {
                return true;
            }
            else if (ch >= 'a' && ch <= 'f')
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Decodes a string that is fully in the buffer into the decode buffer.
        /// </summary>
        private int DecodeStringInBuffer(char[] decodeBuffer, out int decodeLength)
        {
            int length = 0;

            if (PeekInBuffer(length) == '"')
            {
                // skip open quote
                length++;
            }

            var len = DecodeStringInteriorInBuffer(length, decodeBuffer, out decodeLength);
            if (len == 0 && !_done)
                return 0;

            length += len;

            var ch = PeekInBuffer(length);

            if (ch == '"')
            {
                length++;
            }
            else if (ch == '\0' && !_done)
            {
                return 0;
            }

            return length;
        }

        /// <summary>
        /// Decoded string literal interior chars into supplied buffer.
        /// Returns 0 when no more chars are available in buffer.
        /// Consumes end quote.
        /// </summary>
        private int DecodeStringInteriorInBuffer(int offset, char[] targetBuffer, out int decodedLength)
        {
            var start = offset;
            decodedLength = 0;

            while (decodedLength < targetBuffer.Length)
            {
                var ch = PeekInBuffer(offset);
                if (ch == '"' || ch == '\0')
                {
                    break;
                }
                else if (ch == '\\')
                {
                    int escapeLen = DecodeEscapedCharInBuffer(offset, out var decodedChar);
                    if (escapeLen == 0) // char not fully formed
                        break;
                    targetBuffer[decodedLength] = decodedChar;
                    offset += escapeLen;
                    decodedLength++;
                }
                else
                {
                    targetBuffer[decodedLength] = ch;
                    offset++;
                    decodedLength++;
                }
            }

            return offset - start;
        }

        private int DecodeEscapedCharInBuffer(int offset, out char decodedChar)
        {
            var ch = PeekInBuffer(offset + 1); // skip leading \
            switch (ch)
            {
                case '\0':
                    decodedChar = '\0';
                    return 0;
                case '"':
                    decodedChar = '"';
                    return 2;
                case '\\':
                    decodedChar = '\\';
                    return 2;
                case '/':
                    decodedChar = '/';
                    return 2;
                case 'b':
                    decodedChar = '\b';
                    return 2;
                case 'f':
                    decodedChar = '\f';
                    return 2;
                case 'r':
                    decodedChar = '\r';
                    return 2;
                case 'n':
                    decodedChar = '\n';
                    return 2;
                case 't':
                    decodedChar = '\t';
                    return 2;
                case 'u':
                    var len = DecodeHexNumberInBuffer(offset + 2, 4, out var number);
                    decodedChar = (char)number;
                    return len + 2;
                default:
                    // bad escaped: throw exception here?
                    decodedChar = ch;
                    return 2;
            }
        }

        private int DecodeHexNumberInBuffer(int offset, int maxLength, out uint number)
        {
            var len = 0;
            number = 0;

            char ch;
            while ((ch = PeekInBuffer(offset + len)) != '\0' && len < maxLength)
            {
                uint digit;
                if (TryGetHexDigitValue(ch, out digit))
                {
                    len++;
                    number = (number << 4) + digit;
                }
                else
                {
                    break;
                }
            }

            return len;
        }

        private static bool TryGetHexDigitValue(char ch, out uint value)
        {
            if (ch >= '0' && ch <= '9')
            {
                value = (uint)ch - (uint)'0';
                return true;
            }
            else if (ch >= 'A' && ch <= 'F')
            {
                value = ((uint)ch - (uint)'A') + 10;
                return true;
            }
            else if (ch >= 'a' && ch <= 'f')
            {
                value = ((uint)ch - (uint)'a') + 10;
                return true;
            }
            else
            {
                value = 0;
                return false;
            }
        }

        /// <summary>
        /// Returns true if the next characters in the buffer match the text 
        /// and there are more characters remaining in the buffer.
        /// </summary>
        private bool MatchesInBuffer(string text, int offset = 0)
        {
            if (_bufferOffset + offset + text.Length < _buffer.Length)
            {
                for (int i = 0; i < text.Length; i++)
                {
                    // not a match
                    if (text[i] != _buffer[_bufferOffset + offset + i])
                        return false;
                }

                // if more in buffer then match is good
                if (_bufferOffset + offset + text.Length + 1 < _bufferLength || _done)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Advances the buffer offset by count.
        /// </summary>
        private void AdvanceInBuffer(int count)
        {
            _bufferOffset += count;
            if (_bufferOffset > _bufferLength)
                _bufferOffset = _bufferLength;
        }

        /// <summary>
        /// Returns the next character at the offset delta within the buffer.
        /// If the offset is outside the buffer it returns '\0'.
        /// </summary>
        private char PeekInBuffer(int offset = 0)
        {
            var pos = _bufferOffset + offset;
            if (pos < _bufferLength)
            {
                return _buffer[pos];
            }

            return '\0';
        }

        /// <summary>
        /// Scans the number of contiguous whitespace in the buffer.
        /// Returns false if it reaches the end of buffer before finding a non-whitespace character.
        /// </summary>
        private bool ScanWhitespaceInBuffer(int offset, out int length)
        {
            var start = offset;

            char ch;
            while (char.IsWhiteSpace(ch = PeekInBuffer(offset)))
            {
                offset++;
            }

            length = offset - start;

            if (ch == '\0' && !_done)
                return false;

            return true;
        }


        /// <summary>
        /// Scans until reaching a matching character
        /// Returns true if found the matching character in buffer.
        /// Returns false if not found the matching character in buffer or there is just no more buffer.
        /// </summary>
        private bool ScanUntilMatchOrEndOfBuffer(int offset, Func<char, bool> match, out int length)
        {
            var start = offset;

            while (true)
            {
                var ch = PeekInBuffer(offset);

                if (ch == '\0')
                {
                    // no more buffer
                    length = offset - start;
                    return false;
                }
                else if (match(ch))
                {
                    length = offset - start;
                    return true;
                }

                offset++;
            }
        }

        /// <summary>
        /// Reads more characters from the stream into the buffer.
        /// If 'grow' is specified and the buffer is full, the buffer is allowed to grow to accomodate more characters.
        /// If 'grow' is not specified and the buffer is full, no more characters will be read.
        /// </summary>
        private void ReadBuffer(bool grow = false)
        {
            // don't try to read more if there is no more
            if (_done)
                return;

            int remaining;

            // if not all buffer read yet, shift remaining characters left.
            if (_bufferOffset < _bufferLength)
            {
                remaining = _bufferLength - _bufferOffset;

                if (_bufferOffset > 0 && remaining > 0)
                {
                    Array.Copy(_buffer, _bufferOffset, _buffer, 0, remaining);
                    _token.Start -= _bufferOffset;
                    _bufferStart += _bufferOffset;
                    _bufferOffset = 0;
                    _bufferLength = remaining;
                }
            }
            else
            {
                remaining = 0;
                _token.Start -= _bufferOffset;
                _bufferStart += _bufferOffset;
                _bufferOffset = 0;
                _bufferLength = 0;
            }

            // if no more room in buffer
            if (_bufferOffset == 0
                && _bufferLength == _buffer.Length)
            {
                if (grow)
                {
                    GrowBuffer();
                }
                else
                {
                    return;
                }
            }

            // try to read as much as possible
            var count = _reader.Read(_buffer, remaining, _buffer.Length - remaining);

            if (count == 0)
            {
                // nothing read, so stream is done.
                _done = true;
            }
            else
            {
                _bufferLength = remaining + count;
            }
        }

        /// <summary>
        /// Reads more characters from the stream into the buffer.
        /// If 'grow' is specified and the buffer is full, the buffer is allowed to grow to accomodate more characters.
        /// If 'grow' is not specified and the buffer is full, no more characters will be read.
        /// </summary>
        private async ValueTask ReadBufferAsync(bool grow = false)
        {
            // don't try to read more if there is no more
            if (_done)
                return;

            int remaining;

            // if not all buffer read yet, shift remaining characters left.
            if (_bufferOffset < _bufferLength)
            {
                remaining = _bufferLength - _bufferOffset;
                if (_bufferOffset > 0 && remaining > 0)
                {
                    Array.Copy(_buffer, _bufferOffset, _buffer, 0, remaining);
                    _token.Start -= _bufferOffset;
                    _bufferStart += _bufferOffset;
                    _bufferOffset = 0;
                    _bufferLength = remaining;
                }
            }
            else
            {
                remaining = 0;
                _token.Start -= _bufferOffset;
                _bufferStart += _bufferOffset;
                _bufferOffset = 0;
                _bufferLength = 0;
            }

            // if no more room in buffer
            if (_bufferOffset == 0
                && _bufferLength == _buffer.Length)
            {
                if (grow)
                {
                    GrowBuffer();
                }
                else
                {
                    return;
                }
            }

            // try to read as much as possible
            var count = await _reader.ReadAsync(_buffer, remaining, _buffer.Length - remaining).ConfigureAwait(false);

            if (count == 0)
            {
                // nothing read, so stream is done.
                _done = true;
            }
            else
            {
                _bufferLength = remaining + count;
            }
        }

        private void GrowBuffer()
        {
            var newBuffer = new char[_buffer.Length * 2];
            Array.Copy(_buffer, 0, newBuffer, 0, _bufferLength);
            _buffer = newBuffer;
        }
    }
}