namespace Junior
{
    public class JsonAnyReader : JsonTypeReader<object>
    {
        public static readonly JsonAnyReader Instance = new JsonAnyReader();

        public override object? Read(JsonTokenReader reader)
        {
            if (!reader.HasToken)
                reader.MoveToNextToken();

            switch (reader.TokenKind)
            {
                case TokenKind.True:
                    reader.MoveToNextToken();
                    return true;
                case TokenKind.False:
                    reader.MoveToNextToken();
                    return false;
                case TokenKind.Null:
                    reader.MoveToNextToken();
                    return null;
                case TokenKind.String:
                    return reader.ReadTokenValue();
                case TokenKind.Number:
                    if (reader.TryGetTokenValueAs<int>(out var intValue))
                    {
                        reader.MoveToNextToken();
                        return intValue;
                    }
                    else if (reader.TryGetTokenValueAs<long>(out var longValue))
                    {
                        reader.MoveToNextToken();
                        return longValue;
                    }
                    else if (reader.TryGetTokenValueAs<double>(out var doubleValue))
                    {
                        reader.MoveToNextToken();
                        return doubleValue;
                    }
                    else if (reader.TryGetTokenValueAs<decimal>(out var decimalValue))
                    {
                        reader.MoveToNextToken();
                        return decimalValue;
                    }
                    else
                    {
                        return reader.ReadTokenValue();
                    }

                case TokenKind.ListStart:
                    reader.MoveToNextToken();

                    var list = new List<object?>();
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
                            list.Add(value);
                        }
                        else
                        {
                            break;
                        }
                    }

                    return list.ToArray();

                case TokenKind.ObjectStart:
                    reader.MoveToNextToken();
                    var map = new Dictionary<string, object?>();

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
                            var key = reader.ReadTokenValue();

                            if (reader.TokenKind == TokenKind.Colon)
                                reader.MoveToNextToken();

                            var value = Read(reader);
                            map[key] = value;
                        }
                        else
                        {
                            break;
                        }
                    }

                    return map;
            }

            return null;
        }

        public override async ValueTask<object?> ReadAsync(JsonTokenReader reader)
        {
            if (!reader.HasToken)
                await reader.MoveToNextTokenAsync().ConfigureAwait(false);

            switch (reader.TokenKind)
            {
                case TokenKind.True:
                    await reader.MoveToNextTokenAsync().ConfigureAwait(false);
                    return true;
                case TokenKind.False:
                    await reader.MoveToNextTokenAsync().ConfigureAwait(false);
                    return false;
                case TokenKind.Null:
                    await reader.MoveToNextTokenAsync().ConfigureAwait(false);
                    return null;
                case TokenKind.String:
                    return await reader.ReadTokenValueAsync().ConfigureAwait(false);
                case TokenKind.Number:
                    if (reader.TryGetTokenValueAs<int>(out var intValue))
                    {
                        await reader.MoveToNextTokenAsync().ConfigureAwait(false);
                        return intValue;
                    }
                    else if (reader.TryGetTokenValueAs<long>(out var longValue))
                    {
                        await reader.MoveToNextTokenAsync().ConfigureAwait(false);
                        return longValue;
                    }
                    else if (reader.TryGetTokenValueAs<double>(out var doubleValue))
                    {
                        await reader.MoveToNextTokenAsync().ConfigureAwait(false);
                        return doubleValue;
                    }
                    else if (reader.TryGetTokenValueAs<decimal>(out var decimalValue))
                    {
                        await reader.MoveToNextTokenAsync().ConfigureAwait(false);
                        return decimalValue;
                    }
                    else
                    {
                        return await reader.ReadTokenValueAsync().ConfigureAwait(false);
                    }
                case TokenKind.ListStart:
                    await reader.MoveToNextTokenAsync().ConfigureAwait(false);
                    var list = new List<object?>();

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
                            list.Add(value);
                        }
                        else
                        {
                            break;
                        }
                    }

                    return list.ToArray();

                case TokenKind.ObjectStart:
                    await reader.MoveToNextTokenAsync().ConfigureAwait(false);
                    var map = new Dictionary<string, object?>();

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
                            var key = await reader.ReadTokenValueAsync().ConfigureAwait(false);

                            if (reader.TokenKind == TokenKind.Colon)
                                await reader.MoveToNextTokenAsync().ConfigureAwait(false);

                            var value = Read(reader);
                            map[key] = value;
                        }
                        else
                        {
                            break;
                        }
                    }

                    return map;
            }

            return null;
        }
    }
}
