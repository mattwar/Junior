using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Junior
{
    /// <summary>
    /// A reader that reads <see cref="JsonValue"/>.
    /// </summary>
    public class JsonValueReader
    {
        private readonly JsonTokenReader _tokenReader;

        public JsonValueReader(JsonTokenReader tokenReader)
        {
            _tokenReader = tokenReader;
        }

        public JsonValueReader(TextReader textReader)
            : this(new JsonTokenReader(textReader))
        {
        }

        public JsonValueReader(Stream stream)
            : this(new JsonTokenReader(stream))
        {
        }

        public JsonValueReader(string text)
            : this(new JsonTokenReader(text))
        {
        }

        /// <summary>
        /// Reads the json value starting at the next token.
        /// </summary>
        public async ValueTask<JsonValue?> ReadNextValueAsync()
        {
            await _tokenReader.ReadNextTokenAsync().ConfigureAwait(false);
            return await ReadValueAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Reads the entire <see cref="JsonValue"/> starting at the current token.
        /// </summary>
        public async ValueTask<JsonValue?> ReadValueAsync()
        {
            JsonValue? result = null;

            switch (_tokenReader.TokenKind)
            {
                case TokenKind.True:
                    result = JsonTrue.Instance;
                    break;
                case TokenKind.False:
                    result = JsonFalse.Instance;
                    break;
                case TokenKind.Null:
                    result = JsonNull.Instance;
                    break;
                case TokenKind.Number:
                    result = new JsonNumber(await _tokenReader.GetTokenValueAsync().ConfigureAwait(false));
                    break;
                case TokenKind.String:
                    result = new JsonString(await _tokenReader.GetTokenValueAsync().ConfigureAwait(false));
                    break;
                case TokenKind.ListStart:
                    // skip open paren
                    await _tokenReader.ReadNextTokenAsync().ConfigureAwait(false);

                    var valueList = new List<JsonValue>();
                    while (true)
                    {
                        if (_tokenReader.TokenKind == TokenKind.ListEnd
                            || _tokenReader.TokenKind == TokenKind.None)
                        {
                            result = new JsonList(valueList);
                            break;
                        }
                        else if (_tokenReader.TokenKind == TokenKind.Comma)
                        {
                            // skip comma
                            await _tokenReader.ReadNextTokenAsync().ConfigureAwait(false);
                            continue;
                        }
                        else 
                        {
                            var value = await ReadValueAsync().ConfigureAwait(false);
                            if (value != null)
                                valueList.Add(value);
                        }
                    }
                    break;
                case TokenKind.ObjectStart:
                    // skip open brace
                    await _tokenReader.ReadNextTokenAsync().ConfigureAwait(false);

                    var propertyList = new List<JsonProperty>();
                    while (true)
                    {
                        if (_tokenReader.TokenKind == TokenKind.ObjectEnd
                            || _tokenReader.TokenKind == TokenKind.None)
                        {
                            result = new JsonObject(propertyList);
                            break;
                        }
                        else if (_tokenReader.TokenKind == TokenKind.String)
                        {
                            var property = await ReadJsonPropertyAsync().ConfigureAwait(false);
                            if (property != null)
                                propertyList.Add(property);
                        }
                        else if (_tokenReader.TokenKind == TokenKind.Comma)
                        {
                            // skip comma
                            await _tokenReader.ReadNextTokenAsync().ConfigureAwait(false);
                            continue;
                        }
                    }
                    break;
            }

            if (result != null)
            {
                await _tokenReader.ReadNextTokenAsync().ConfigureAwait(false);
            }

            return result;
        }

        /// <summary>
        /// Gets the entire <see cref="JsonProperty"/> starting at the current token.
        /// </summary>
        public async ValueTask<JsonProperty?> ReadJsonPropertyAsync()
        {
            var propertyName = await GetJsonPropertyNameAsync().ConfigureAwait(false);
            if (propertyName == null)
                return null;
            var value = await ReadValueAsync().ConfigureAwait(false);
            if (value == null)
                return null;
            return new JsonProperty(propertyName, value);
        }

        /// <summary>
        /// Gets the property name (and skips over the colon) starting at the current token.
        /// </summary>
        public async ValueTask<string?> GetJsonPropertyNameAsync()
        {
            if (_tokenReader.TokenKind != TokenKind.String)
                return null;
            var propertyName = await _tokenReader.GetTokenValueAsync().ConfigureAwait(false);
            if (propertyName == null)
                return null;
            await _tokenReader.ReadNextTokenAsync().ConfigureAwait(false);
            if (_tokenReader.TokenKind != TokenKind.Colon)
                return null;
            await _tokenReader.ReadNextTokenAsync().ConfigureAwait(false);
            return propertyName;
        }

        public ValueTask SkipJsonValueAsync()
        {
            throw new NotImplementedException();

#if false  // code for skipping entire subtrees
            switch (_tokenKind)
            {
                case TokenKind.ListStart:
                    while (true)
                    {
                        if (!ReadNextTokenInBuffer())
                            await ReadTokenAsync().ConfigureAwait(false);

                        if (IsValue(_tokenKind))
                        {
                            await SkipTokenAsync().ConfigureAwait(false);

                            if (_tokenKind == TokenKind.Comma)
                            {
                                AdvanceInBuffer(_tokenLength);
                                continue;
                            }
                            else
                            {
                                _tokenKind = TokenKind.Error;
                                return;
                            }
                        }
                        else if (_tokenKind == TokenKind.ListEnd)
                        {
                            AdvanceInBuffer(_tokenLength);
                            break;
                        }
                        else
                        {
                            _tokenKind = TokenKind.Error;
                            return;
                        }
                    }
                    break;

                case TokenKind.ObjectStart:
                    while (true)
                    {
                        if (!ReadNextTokenInBuffer())
                            await ReadTokenAsync().ConfigureAwait(false);

                        // property name string literal
                        if (_tokenKind == TokenKind.String)
                        {
                            if (_tokenStage == TokenStage.InBuffer)
                            {
                                AdvanceInBuffer(_tokenLength);

                                if (!ReadNextTokenInBuffer())
                                    await ReadTokenAsync().ConfigureAwait(false);
                            }
                            else
                            {
                                await SkipTokenAsync().ConfigureAwait(false);
                            }

                            if (_tokenKind == TokenKind.Colon)
                            {
                                AdvanceInBuffer(_tokenLength);

                                if (!ReadNextTokenInBuffer())
                                    await ReadTokenAsync().ConfigureAwait(false);
                            }
                            else
                            {
                                _tokenKind = TokenKind.Error;
                                return;
                            }

                            if (IsValue(_tokenKind))
                            {
                                if (_tokenStage == TokenStage.InBuffer)
                                {
                                    AdvanceInBuffer(_tokenLength);

                                    if (!ReadNextTokenInBuffer())
                                        await ReadTokenAsync().ConfigureAwait(false);
                                }
                                else
                                {
                                    await SkipTokenAsync().ConfigureAwait(false);
                                }
                            }
                            else
                            {
                                _tokenKind = TokenKind.Error;
                                return;
                            }

                            if (_tokenKind == TokenKind.Comma)
                            {
                                AdvanceInBuffer(_tokenLength);
                                continue;
                            }
                        }
                        else if (_tokenKind == TokenKind.ObjectEnd)
                        {
                            AdvanceInBuffer(_tokenLength);
                            break;
                        }
                        else
                        {
                            _tokenKind = TokenKind.None;
                            return;
                        }
                    }
                    break;

                case TokenKind.String:
                    break;
            }
#endif
        }

        private static bool IsLiteral(TokenKind kind)
        {
            switch (kind)
            {
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

    public abstract record JsonValue { }
    public sealed record JsonString(string Value) : JsonValue;
    public sealed record JsonNumber(string Number) : JsonValue;
    public sealed record JsonNull() : JsonValue { public static readonly JsonNull Instance = new JsonNull(); }
    public sealed record JsonTrue() : JsonValue { public static readonly JsonTrue Instance = new JsonTrue(); }
    public sealed record JsonFalse() : JsonValue { public static readonly JsonFalse Instance = new JsonFalse(); }

    public sealed record JsonList(IReadOnlyList<JsonValue> Values) : JsonValue
    {
        public JsonList(params JsonValue[] values)
            : this((IReadOnlyList<JsonValue>)values)
        {
        }

        public JsonValue? this[int index]
        {
            get
            {
                if (index >= 0 && index < this.Values.Count)
                {
                    return this.Values[index];
                }

                return null;
            }
        }
    }

    public sealed record JsonProperty(string Name, JsonValue Value);

    public sealed record JsonObject(IReadOnlyList<JsonProperty> Properties) : JsonValue
    {
        public JsonObject(params JsonProperty[] properties)
            : this((IReadOnlyList<JsonProperty>)properties)
        {
        }

        public JsonValue? this[string propertyName]
        {
            get
            {
                var property = this.Properties.FirstOrDefault(p => p.Name == propertyName);
                return property?.Value;
            }
        }
    }
}