namespace Junior
{
    /// <summary>
    /// A <see cref="JsonTypeReader"/> that reads <see cref="JsonValue"/>.
    /// </summary>
    public class JsonValueReader
        : JsonTypeReader<JsonValue>
    {
        public static readonly JsonValueReader Instance = new JsonValueReader();

        public override JsonValue? Read(JsonTokenReader reader)
        {
            if (!reader.HasToken)
                reader.MoveToNextToken();

            switch (reader.TokenKind)
            {
                case TokenKind.True:
                    reader.MoveToNextToken();
                    return JsonTrue.Instance;
                case TokenKind.False:
                    reader.MoveToNextToken();
                    return JsonFalse.Instance;
                case TokenKind.Null:
                    reader.MoveToNextToken();
                    return JsonNull.Instance;
                case TokenKind.Number:
                    return new JsonNumber(reader.ReadTokenValue());
                case TokenKind.String:
                    return new JsonString(reader.ReadTokenValue());
                case TokenKind.ListStart:
                    reader.MoveToNextToken();
                    var valueList = new List<JsonValue>();

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
                            var value = Read(reader);
                            if (value != null)
                                valueList.Add(value);
                        }
                        else
                        {
                            break;
                        }
                    }

                    return new JsonList(valueList);

                case TokenKind.ObjectStart:
                    reader.MoveToNextToken();
                    var propertyList = new List<JsonProperty>();

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
                            var property = ReadJsonProperty(reader);
                            if (property != null)
                                propertyList.Add(property);
                        }
                        else
                        {
                            break;
                        }
                    }

                    return new JsonObject(propertyList);
            }

            return null;
        }

        /// <summary>
        /// Gets the entire <see cref="JsonProperty"/> starting at the current token.
        /// </summary>
        public JsonProperty? ReadJsonProperty(JsonTokenReader reader)
        {
            var propertyName = ReadJsonPropertyName(reader);
            var value = Read(reader);
            return new JsonProperty(propertyName ?? "", value ?? JsonNull.Instance);
        }

        /// <summary>
        /// Gets the property name (and skips over the colon) starting at the current token.
        /// </summary>
        public string? ReadJsonPropertyName(JsonTokenReader reader)
        {
            var propertyName = reader.ReadTokenValue();

            if (reader.TokenKind == TokenKind.Colon)
                reader.MoveToNextToken();

            return propertyName;
        }

        public override async ValueTask<JsonValue?> ReadAsync(JsonTokenReader reader)
        {
            if (!reader.HasToken)
                await reader.MoveToNextTokenAsync().ConfigureAwait(false);

            switch (reader.TokenKind)
            {
                case TokenKind.True:
                    await reader.MoveToNextTokenAsync().ConfigureAwait(false);
                    return JsonTrue.Instance;
                case TokenKind.False:
                    await reader.MoveToNextTokenAsync().ConfigureAwait(false);
                    return JsonFalse.Instance;
                case TokenKind.Null:
                    await reader.MoveToNextTokenAsync().ConfigureAwait(false);
                    return JsonNull.Instance;
                case TokenKind.Number:
                    return new JsonNumber(await reader.ReadTokenValueAsync().ConfigureAwait(false));
                case TokenKind.String:
                    return new JsonString(await reader.ReadTokenValueAsync().ConfigureAwait(false));
                case TokenKind.ListStart:
                    await reader.MoveToNextTokenAsync().ConfigureAwait(false);
                    var valueList = new List<JsonValue>();

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
                            var value = await ReadAsync(reader).ConfigureAwait(false);
                            if (value != null)
                                valueList.Add(value);
                        }
                        else
                        {
                            break;
                        }
                    }

                    return new JsonList(valueList);

                case TokenKind.ObjectStart:
                    await reader.MoveToNextTokenAsync().ConfigureAwait(false);
                    var propertyList = new List<JsonProperty>();

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
                            var property = await ReadJsonPropertyAsync(reader).ConfigureAwait(false);
                            if (property != null)
                                propertyList.Add(property);
                        }
                        else
                        {
                            break;
                        }
                    }

                    return new JsonObject(propertyList);
            }

            return null;
        }

        /// <summary>
        /// Gets the entire <see cref="JsonProperty"/> starting at the current token.
        /// </summary>
        public async ValueTask<JsonProperty?> ReadJsonPropertyAsync(JsonTokenReader reader)
        {
            var propertyName = await ReadJsonPropertyNameAsync(reader).ConfigureAwait(false);
            var value = await ReadAsync(reader).ConfigureAwait(false);
            return new JsonProperty(propertyName ?? "", value ?? JsonNull.Instance);
        }

        /// <summary>
        /// Gets the property name (and skips over the colon) starting at the current token.
        /// </summary>
        public async ValueTask<string?> ReadJsonPropertyNameAsync(JsonTokenReader reader)
        {
            var propertyName = await reader.ReadTokenValueAsync().ConfigureAwait(false);

            if (reader.TokenKind == TokenKind.Colon)
                await reader.MoveToNextTokenAsync().ConfigureAwait(false);

            return propertyName;
        }
    }

    [JsonTypeReader(typeof(JsonValueReader))]
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
                if (index >= 0 && index < Values.Count)
                {
                    return Values[index];
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
                var property = Properties.FirstOrDefault(p => p.Name == propertyName);
                return property?.Value;
            }
        }
    }
}