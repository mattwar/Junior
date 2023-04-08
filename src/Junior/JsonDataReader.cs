namespace Junior
{
    /// <summary>
    /// Reads tabular data in a specific format within json stream.
    /// { "name": "...", "columns": [...], "rows": [[...], [...], [...]] }
    /// Colum schema is either a list of column names or a list of objects with name & type.
    /// </summary>
    public class JsonDataReader
    {
        private readonly JsonTokenReader _tokenReader;
        private readonly IReadOnlyDictionary<string, JsonTypeReader>? _typeReaderMap;
        private readonly JsonTypeReader<object> _defaultReader;
        private string? _tableName;
        private JsonList? _tableSchema;
        private ReadState _state;

        public JsonDataReader(
            JsonTokenReader reader, 
            IReadOnlyDictionary<string, JsonTypeReader>? readerMap = null,
            JsonTypeReader<object>? defaultReader = null)
        {
            _tokenReader = reader;
            _typeReaderMap = readerMap;
            _defaultReader = defaultReader ?? JsonAnyReader.Instance;
            _state = ReadState.Start;
        }

        public JsonTokenReader TokenReader => _tokenReader;

        public string TableName => _tableName ?? "";

        private int _currentFieldIndex;
        public int CurrentFieldIndex => _currentFieldIndex;

        public string CurrentFieldName => GetFieldName(_currentFieldIndex);
        public string CurrentFieldType => GetFieldType(_currentFieldIndex);

        public int FieldCount => _tableSchema != null ? _tableSchema.Values.Count : 0;

        /// <summary>
        /// Gets the name of the field at the specified index.
        /// </summary>
        public string GetFieldName(int index)
        {
            if (_tableSchema != null && index < _tableSchema.Values.Count)
            {
                var value = _tableSchema.Values[index];
                if (value is JsonObject obj)
                {
                    value = obj["name"];
                }
                if (value is JsonString str)
                {
                    return str.Value;
                }
            }

            return "";
        }

        /// <summary>
        /// Gets the type of the field at the specified index.
        /// </summary>
        public string GetFieldType(int index)
        {
            if (_tableSchema != null && index < _tableSchema.Values.Count)
            {
                var value = _tableSchema.Values[index];
                if (value is JsonObject obj
                    && obj["type"] is JsonString str)
                {
                    return str.Value;
                }
            }

            return "";
        }

        /// <summary>
        /// Move to the the start of the next table in the stream.
        /// Returns false if there are no more tables.
        /// </summary>
        public async ValueTask<bool> MoveToNextTableAsync()
        {
            // skip over remaining rows
            if (_state == ReadState.RowSet
                || _state == ReadState.Row
                || _state == ReadState.Field
                || _state == ReadState.Value)
            {
                while (await MoveToNextRowAsync().ConfigureAwait(false))
                {
                }

                if (_state == ReadState.Row
                    && _tokenReader.TokenKind == TokenKind.ListEnd)
                {
                    _state = ReadState.RowSet;

                    if (!(await _tokenReader.MoveToNextTokenAsync().ConfigureAwait(false)))
                        return false;
                }

                if (_state == ReadState.RowSet
                    &&_tokenReader.TokenKind == TokenKind.ListEnd)
                {
                    _state = ReadState.Table;

                    if (!(await _tokenReader.MoveToNextTokenAsync().ConfigureAwait(false)))
                        return false;
                }

                if (_state == ReadState.Table
                    && _tokenReader.TokenKind == TokenKind.ObjectEnd)
                {
                    _state = ReadState.TableSet;

                    if (!(await _tokenReader.MoveToNextTokenAsync().ConfigureAwait(false)))
                        return false;
                }
            }
            else if (_state == ReadState.Start)
            {
                // read the first token if not already read
                if (!_tokenReader.HasToken)
                    await _tokenReader.MoveToNextTokenAsync().ConfigureAwait(false);
                _state = ReadState.TableSet;
            }

            // if there is no comma when we are reading tables, then there are no more tables
            if (_state == ReadState.TableSet
                && _tokenReader.TokenKind == TokenKind.Comma)
            {
                if (!(await _tokenReader.MoveToNextTokenAsync().ConfigureAwait(false)))
                    return false;
            }

            // a new table starts
            if (_state == ReadState.TableSet
                && _tokenReader.TokenKind == TokenKind.ObjectStart)
            {
                await _tokenReader.MoveToNextTokenAsync().ConfigureAwait(false);

                _tableName = "";
                _tableSchema = null;

                while (_tokenReader.HasToken)
                {
                    if (_tokenReader.TokenKind == TokenKind.ObjectEnd)
                    {
                        await _tokenReader.MoveToNextTokenAsync().ConfigureAwait(false);
                        break;
                    }
                    else if (_tokenReader.TokenKind == TokenKind.Comma)
                    {
                        await _tokenReader.MoveToNextTokenAsync().ConfigureAwait(false);
                        continue;
                    }
                    else if (_tokenReader.TokenKind.IsValueStart())
                    {
                        var propertyName = await JsonValueReader.Instance.ReadJsonPropertyNameAsync(_tokenReader).ConfigureAwait(false);
                        if (propertyName == "name")
                        {
                            var val = await JsonValueReader.Instance.ReadAsync(_tokenReader).ConfigureAwait(false);
                            if (val is JsonString str)
                                _tableName = str.Value;
                            continue;
                        }
                        else if (propertyName == "columns")
                        {
                            _tableSchema = (await JsonValueReader.Instance.ReadAsync(_tokenReader).ConfigureAwait(false)) as JsonList;
                            continue;
                        }
                        else if (propertyName == "rows"
                            && _tokenReader.TokenKind == TokenKind.ListStart)
                        {
                            await _tokenReader.MoveToNextTokenAsync().ConfigureAwait(false);
                            _state = ReadState.RowSet;
                            return true;
                        }
                        else
                        {
                            // skip this unknown property value
                            await _tokenReader.MoveToNextElementAsync().ConfigureAwait(false);
                        }
                    }
                    else
                    {
                        break;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Move to the next row in the current table in the stream.
        /// Returns false if there are no more rows in the current table.
        /// </summary>
        public async ValueTask<bool> MoveToNextRowAsync()
        {
            if (_state == ReadState.Row
                || _state == ReadState.Field
                || _state == ReadState.Value)
            {
                // skip to end of row
                while (await MoveToNextFieldAsync().ConfigureAwait(false))
                {
                }

                if (_tokenReader.TokenKind == TokenKind.ListEnd)
                {
                    _state = ReadState.Row;
                    await _tokenReader.MoveToNextTokenAsync().ConfigureAwait(false);
                    return false;
                }
            }
            else if (_state == ReadState.Start
                && !_tokenReader.HasToken)
            {
                await MoveToNextTableAsync().ConfigureAwait(false);
            }

            if (_state == ReadState.RowSet
                && _tokenReader.TokenKind == TokenKind.Comma)
            {
                await _tokenReader.MoveToNextTokenAsync().ConfigureAwait(false);
            }

            if (_state == ReadState.RowSet
                && _tokenReader.TokenKind == TokenKind.ListStart)
            {
                await _tokenReader.MoveToNextTokenAsync().ConfigureAwait(false);
                _state = ReadState.Row;
                return true;
            }

            if (_state == ReadState.RowSet
                && _tokenReader.TokenKind == TokenKind.ListEnd)
            {
                await _tokenReader.MoveToNextTokenAsync().ConfigureAwait(false);
                _state = ReadState.Table;
            }

            if (_state == ReadState.Table
                && _tokenReader.TokenKind == TokenKind.ObjectEnd)
            {
                await _tokenReader.MoveToNextTokenAsync().ConfigureAwait(false);
                _state = ReadState.TableSet;
            }

            return false;
        }

        /// <summary>
        /// Moves to the next field in the current row.
        /// Returns false if there are no more fields.
        /// </summary>
        public async ValueTask<bool> MoveToNextFieldAsync()
        {
            if (_state == ReadState.Row)
            {
                if (_tokenReader.TokenKind == TokenKind.ListEnd)
                {
                    await MoveToNextTableAsync().ConfigureAwait(false);
                    _state = ReadState.RowSet;
                    return false;
                }

                _state = ReadState.Field;
                _currentFieldIndex = 0;
                return true;
            }           
            else if (_state == ReadState.Field)
            {
                // move to next field
                if (_tokenReader.TokenKind == TokenKind.ListEnd)
                {
                    await _tokenReader.MoveToNextTokenAsync().ConfigureAwait(false);
                    _state = ReadState.RowSet;
                    return false;
                }
                else if (_tokenReader.TokenKind == TokenKind.Comma)
                {
                    await _tokenReader.MoveToNextTokenAsync().ConfigureAwait(false);
                }

                _currentFieldIndex++;
                return true;
            }

            return false;
        }

        public async ValueTask<object?> ReadFieldValueAsync()
        {
            var type = CurrentFieldType;
            if (type == null)
            {
                await _defaultReader.ReadAsync(_tokenReader).ConfigureAwait(false);
            }
            else if (
                (_typeReaderMap != null && _typeReaderMap.TryGetValue(type, out var typeReader))
                || _defaultTypeReaderMap.TryGetValue(type, out typeReader))
            {
                return await typeReader.ReadObjectAsync(_tokenReader).ConfigureAwait(false);
            }

            return null;
        }

        public ReadOnlySpan<char> CurrentFieldValueSpan => 
            _tokenReader.CurrentValueChunk;

        public ValueTask<bool> ReadNextFieldValueSpan()
        {
            return _tokenReader.ReadNextTokenChunkAsync();
        }

        private record ColumnSchema(string Name, string Type);

        private enum ReadState
        {
            Start,

            /// <summary>
            /// Expecting { to start new table or ] to end table set
            /// </summary>
            TableSet,

            /// <summary>
            /// Current reading tables, expecting , { to start new table or ] to end TableSet
            /// </summary>
            Table,

            /// <summary>
            /// Expected ] } to end rowset and table
            /// </summary>
            RowSet,

            /// <summary>
            /// Currently reading rows: expecting , [ to start new row or ] to end RowSet
            /// </summary>
            Row,

            /// <summary>
            /// Currently reading fields: expecting , to start new field or ] to end Row
            /// </summary>
            Field,

            /// <summary>
            /// Currently reading field value parts
            /// </summary>
            Value
        }

        private static readonly Dictionary<string, JsonTypeReader> _defaultTypeReaderMap =
            new Dictionary<string, JsonTypeReader>(StringComparer.OrdinalIgnoreCase)
            {
                {"object", JsonAnyReader.Instance },
                {"string", JsonStringReader.Instance },
                {"byte", JsonSpanParsableReader<byte>.Instance },
                {"uint8", JsonSpanParsableReader<byte>.Instance },
                {"sbyte", JsonSpanParsableReader<sbyte>.Instance },
                {"int8", JsonSpanParsableReader<sbyte>.Instance },
                {"short", JsonSpanParsableReader<short>.Instance },
                {"int16", JsonSpanParsableReader<short>.Instance },
                {"ushort", JsonSpanParsableReader<ushort>.Instance },
                {"uint16", JsonSpanParsableReader<ushort>.Instance },
                {"int", JsonSpanParsableReader<int>.Instance },
                {"int32", JsonSpanParsableReader<int>.Instance },
                {"uint", JsonSpanParsableReader<uint>.Instance },
                {"uint32", JsonSpanParsableReader<uint>.Instance },
                {"long", JsonSpanParsableReader<long>.Instance },
                {"int64", JsonSpanParsableReader<long>.Instance },
                {"ulong", JsonSpanParsableReader<ulong>.Instance },
                {"uint64", JsonSpanParsableReader<ulong>.Instance },
                {"double", JsonSpanParsableReader<double>.Instance },
                {"real", JsonSpanParsableReader<double>.Instance },
                {"float", JsonSpanParsableReader<float>.Instance },
                {"single", JsonSpanParsableReader<float>.Instance },
                {"decimal", JsonSpanParsableReader<decimal>.Instance },
                {"datetime", JsonSpanParsableReader<DateTime>.Instance },
                {"timespan", JsonSpanParsableReader<TimeSpan>.Instance },
                {"guid", JsonSpanParsableReader<Guid>.Instance },
                {"bool", JsonBoolReader.Instance },
                {"boolean", JsonBoolReader.Instance },
                {"json", JsonValueReader.Instance }
            };
    }
}
