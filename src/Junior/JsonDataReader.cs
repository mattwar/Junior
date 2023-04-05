using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Xml;

namespace Junior
{
    /// <summary>
    /// Reads tabular data in a specific format within json stream.
    /// { "name": "...", "columns": [...], "rows": [[...], [...], [...]] }
    /// Colum schema is eiter a list of column names or a list of objects with name & type.
    /// </summary>
    public class JsonDataReader
    {
        private readonly JsonTokenReader _tokenReader;
        private readonly JsonValueReader _valueReader;
        private string? _tableName;
        private JsonList? _tableSchema;
        private ReadState _state;

        public JsonDataReader(JsonTokenReader reader)
        {
            _tokenReader = reader;
            _valueReader = new JsonValueReader(reader);
            _state = ReadState.Start;
        }

        public JsonDataReader(TextReader textReader)
            : this(new JsonTokenReader(textReader))
        {
        }

        public JsonDataReader(Stream stream)
            : this(new JsonTokenReader(stream))
        {
        }

        public JsonDataReader(string text)
            : this(new JsonTokenReader(text))
        {
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

                    if (!(await _tokenReader.ReadNextTokenAsync().ConfigureAwait(false)))
                        return false;
                }

                if (_state == ReadState.RowSet
                    &&_tokenReader.TokenKind == TokenKind.ListEnd)
                {
                    _state = ReadState.Table;

                    if (!(await _tokenReader.ReadNextTokenAsync().ConfigureAwait(false)))
                        return false;
                }

                if (_state == ReadState.Table
                    && _tokenReader.TokenKind == TokenKind.ObjectEnd)
                {
                    _state = ReadState.TableSet;

                    if (!(await _tokenReader.ReadNextTokenAsync().ConfigureAwait(false)))
                        return false;
                }
            }
            else if (_state == ReadState.Start)
            {
                // read the first token
                _state = ReadState.TableSet;
                if (!(await _tokenReader.ReadNextTokenAsync().ConfigureAwait(false)))
                    return false;
            }

            // if there is no comma when we are reading tables, then there are no more tables
            if (_state == ReadState.TableSet
                && _tokenReader.TokenKind == TokenKind.Comma)
            {
                if (!(await _tokenReader.ReadNextTokenAsync().ConfigureAwait(false)))
                    return false;
            }

            // a new table starts
            if (_state == ReadState.TableSet
                && _tokenReader.TokenKind == TokenKind.ObjectStart)
            {
                if (!(await _tokenReader.ReadNextTokenAsync().ConfigureAwait(false)))
                    return false;

                _tableName = "";
                _tableSchema = null;

                while (_tokenReader.TokenKind != TokenKind.ObjectEnd)
                {
                    if (_tokenReader.TokenKind == TokenKind.Comma)
                    {
                        if (!(await _tokenReader.ReadNextTokenAsync().ConfigureAwait(false)))
                            return false;
                        continue;
                    }

                    var propertyName = await _valueReader.GetJsonPropertyNameAsync().ConfigureAwait(false);
                    if (propertyName == "columns")
                    {
                        _tableSchema = (await _valueReader.ReadValueAsync().ConfigureAwait(false)) as JsonList;
                        continue;
                    }
                    else if (propertyName == "rows")
                    {
                        if (_tokenReader.TokenKind != TokenKind.ListStart)
                            return false;

                        if (!(await _tokenReader.ReadNextTokenAsync().ConfigureAwait(false)))
                            return false;

                        _state = ReadState.RowSet;
                        return true;
                    }
                    else if (propertyName == "name")
                    {
                        var val = await _valueReader.ReadValueAsync().ConfigureAwait(false);
                        if (val is JsonString str)
                            _tableName = str.Value;

                        continue;
                    }
                    else
                    {
                        // skip this unknown property
                        var _ = _valueReader.ReadValueAsync().ConfigureAwait(false);
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
                    await _tokenReader.ReadNextTokenAsync().ConfigureAwait(false);
                    return false;
                }
            }
            else if (_state == ReadState.Start)
            {
                if (!(await MoveToNextTableAsync().ConfigureAwait(false)))
                    return false;
            }

            if (_state == ReadState.RowSet
                && _tokenReader.TokenKind == TokenKind.Comma)
            {
                if (!(await _tokenReader.ReadNextTokenAsync().ConfigureAwait(false)))
                    return false;
            }

            if (_state == ReadState.RowSet
                && _tokenReader.TokenKind == TokenKind.ListStart)
            {
                if (!(await _tokenReader.ReadNextTokenAsync().ConfigureAwait(false)))
                    return false;
                _state = ReadState.Row;
                return true;
            }

            if (_state == ReadState.RowSet
                && _tokenReader.TokenKind == TokenKind.ListEnd)
            {
                if (!(await _tokenReader.ReadNextTokenAsync().ConfigureAwait(false)))
                    return false;
                _state = ReadState.Table;
            }

            if (_state == ReadState.Table
                && _tokenReader.TokenKind == TokenKind.ObjectEnd)
            {
                if (!(await _tokenReader.ReadNextTokenAsync().ConfigureAwait(false)))
                    return false;
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
                    if (!(await MoveToNextTableAsync().ConfigureAwait(false)))
                        return false;
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
                _currentFieldIndex++;

                while (_tokenReader.TryReadNextToken()
                    || await _tokenReader.ReadNextTokenAsync().ConfigureAwait(false))
                {
                    if (_tokenReader.TokenKind == TokenKind.Comma)
                    {
                        continue;
                    }
                    else if (_tokenReader.TokenKind == TokenKind.ListEnd)
                    {
                        if (!(await _tokenReader.ReadNextTokenAsync().ConfigureAwait(false)))
                            return false;
                        _state = ReadState.RowSet;
                        return false;
                    }

                    return true;
                }
            }

            return false;
        }

        public object? ReadFieldValue()
        {
            var type = CurrentFieldType;
            if (type == null)
            {
                return ReadObjectValue();
            }
            else if (_typeToFieldMap.TryGetValue(type, out var field))
            {
                return field.GetValue(this);
            }

            return null;
        }

        internal object? ReadObjectValue()
        {
            switch (_tokenReader.TokenKind)
            {
                case TokenKind.True:
                    return true;
                case TokenKind.False:
                    return false;
                case TokenKind.Null:
                    return null;
                case TokenKind.String:
                    return _tokenReader.GetTokenValue();
                case TokenKind.Number:
                    if (_tokenReader.TryGetTokenValueAs<int>(out var intValue))
                        return intValue;
                    else if (_tokenReader.TryGetTokenValueAs<long>(out var longValue))
                        return longValue;
                    else if (_tokenReader.TryGetTokenValueAs<double>(out var doubleValue))
                        return doubleValue;
                    else if (_tokenReader.TryGetTokenValueAs<decimal>(out var decimalValue))
                        return decimalValue;
                    else
                        return _tokenReader.GetTokenValue();
                case TokenKind.ListStart:
                    var list = new List<object?>();
                    while (true)
                    {
                        // skip current token
                        if (!_tokenReader.ReadNextToken())
                            return list;

                        if (_tokenReader.TokenKind == TokenKind.ListEnd)
                        {
                            return list;
                        }
                        else if (_tokenReader.TokenKind == TokenKind.Comma)
                        {
                            if (!_tokenReader.ReadNextToken())
                                return list;
                            continue;
                        }
                        else
                        {
                            var value = ReadObjectValue();
                            list.Add(value);
                            continue;
                        }
                    }
                case TokenKind.ObjectStart:
                    var map = new Dictionary<string, object?>();
                    while (true)
                    {
                        // skip current token
                        if (!_tokenReader.ReadNextToken())
                            return map;

                        if (_tokenReader.TokenKind == TokenKind.ListEnd)
                        {
                            return map;
                        }
                        else if (_tokenReader.TokenKind == TokenKind.Comma)
                        {
                            if (!_tokenReader.ReadNextToken())
                                return map;
                            continue;
                        }
                        else if (_tokenReader.TokenKind == TokenKind.String)
                        {
                            var name = _tokenReader.GetTokenValue();

                            if (!_tokenReader.ReadNextToken())
                                return map;

                            if (_tokenReader.TokenKind != TokenKind.Colon)
                                return map;

                            var value = ReadObjectValue();

                            map.Add(name, value);
                            continue;
                        }
                    }
            }

            return null;
        }

#if false
        public async ValueTask<string> GetFieldStringAsync()
        {
            if (!_tokenReader.TryGetTokenValue(out var value))
            {
                value = await _tokenReader.GetTokenValueAsync().ConfigureAwait(false);
            }

            return value;
        }

        public async ValueTask<StringBuilder> GetFieldStringBuilderAsync()
        {
            var builder = new StringBuilder();
            await WriteFieldAsync(builder);
            return builder;
        }

        public async ValueTask WriteFieldAsync(StringBuilder builder)
        {
            while (await ReadFieldCharsAsync().ConfigureAwait(false))
            {
                builder.Append(this.CurrentFieldChars);
            }
        }

        public async ValueTask WriteFieldValueAsync(TextWriter writer)
        {
            while (await ReadFieldCharsAsync().ConfigureAwait(false))
            {
                writer.Write(this.CurrentFieldChars);
            }
        }
#endif

        public ReadOnlySpan<char> CurrentFieldChars =>
            _tokenReader.CurrentChars;

        public async Task<bool> ReadFieldCharsAsync()
        {
            if (await _tokenReader.ReadNextTokenValueCharsAsync().ConfigureAwait(false))
            {
                return true;
            }

            return false;
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

        private static readonly Dictionary<string, FieldReader> _typeToFieldMap =
            new Dictionary<string, FieldReader>(StringComparer.OrdinalIgnoreCase)
            {
                {"object", ObjectFieldReader.Instance },
                {"string", StringFieldReader.Instance },
                {"byte", SpanParsableFieldReader<byte>.Instance },
                {"uint8", SpanParsableFieldReader<byte>.Instance },
                {"sbyte", SpanParsableFieldReader<sbyte>.Instance },
                {"int8", SpanParsableFieldReader<sbyte>.Instance },
                {"short", SpanParsableFieldReader<short>.Instance },
                {"int16", SpanParsableFieldReader<short>.Instance },
                {"ushort", SpanParsableFieldReader<ushort>.Instance },
                {"uint16", SpanParsableFieldReader<ushort>.Instance },
                {"int", SpanParsableFieldReader<int>.Instance },
                {"int32", SpanParsableFieldReader<int>.Instance },
                {"uint", SpanParsableFieldReader<uint>.Instance },
                {"uint32", SpanParsableFieldReader<uint>.Instance },
                {"long", SpanParsableFieldReader<long>.Instance },
                {"int64", SpanParsableFieldReader<long>.Instance },
                {"ulong", SpanParsableFieldReader<ulong>.Instance },
                {"uint64", SpanParsableFieldReader<ulong>.Instance },
                {"double", SpanParsableFieldReader<double>.Instance },
                {"real", SpanParsableFieldReader<double>.Instance },
                {"float", SpanParsableFieldReader<float>.Instance },
                {"single", SpanParsableFieldReader<float>.Instance },
                {"decimal", SpanParsableFieldReader<decimal>.Instance },
                {"datetime", SpanParsableFieldReader<DateTime>.Instance },
                {"timespan", SpanParsableFieldReader<TimeSpan>.Instance },
                {"guid", SpanParsableFieldReader<Guid>.Instance },
                {"bool", BoolFieldReader.Instance },
                {"boolean", BoolFieldReader.Instance }
            };

        private abstract class FieldReader
        {
            public abstract object? GetValue(JsonDataReader reader);
            public abstract ValueTask<object?> GetValueAsync(JsonDataReader reader);
        }

        private class StringFieldReader : FieldReader
        {
            public static readonly StringFieldReader Instance = new StringFieldReader();

            public override object? GetValue(JsonDataReader reader)
            {
                return reader.TokenReader.GetTokenValue();
            }

            public override async ValueTask<object?> GetValueAsync(JsonDataReader reader)
            {
                return await reader.TokenReader.GetTokenValueAsync().ConfigureAwait(false);
            }
        }

        private class BoolFieldReader : FieldReader
        {
            public static readonly BoolFieldReader Instance = new BoolFieldReader();

            private static readonly object _true = true;
            private static readonly object _false = false;

            public override object? GetValue(JsonDataReader reader)
            {
                switch (reader.TokenReader.TokenKind)
                {
                    case TokenKind.True:
                        return _true;
                    case TokenKind.False:
                        return _false;
                    case TokenKind.Null:
                        return null;
                    default:
                        return null;
                }
            }

            public override ValueTask<object?> GetValueAsync(JsonDataReader reader)
            {
                return new ValueTask<object?>(GetValue(reader));
            }
        }

        private class ObjectFieldReader : FieldReader
        {
            public static readonly ObjectFieldReader Instance = new ObjectFieldReader();

            public override object? GetValue(JsonDataReader reader)
            {
                return reader.ReadObjectValue();
            }

            public override ValueTask<object?> GetValueAsync(JsonDataReader reader)
            {
                return new ValueTask<object?>(GetValue(reader));
            }
        }

        private class SpanParsableFieldReader<TValue> : FieldReader
            where TValue : ISpanParsable<TValue>
        {
            public static readonly SpanParsableFieldReader<TValue> Instance = new SpanParsableFieldReader<TValue>();

            public override object? GetValue(JsonDataReader reader)
            {
                if (reader.TokenReader.TryGetTokenValueAs<TValue>(out var tvalue))
                {
                    return tvalue;
                }

                return null;
            }

            public override ValueTask<object?> GetValueAsync(JsonDataReader reader)
            {
                return new ValueTask<object?>(GetValue(reader));
            }
        }

        private class StringParsableFieldReader<TValue> : FieldReader
            where TValue : IParsable<TValue>
        {
            public static readonly StringParsableFieldReader<TValue> Instance = new StringParsableFieldReader<TValue>();

            public override object? GetValue(JsonDataReader reader)
            {
                var value = reader.TokenReader.GetTokenValue();
                if (TValue.TryParse(value, null, out var result))
                    return result;
                return null;
            }

            public override async ValueTask<object?> GetValueAsync(JsonDataReader reader)
            {
                var value = await reader.TokenReader.GetTokenValueAsync();
                if (TValue.TryParse(value, null, out var result))
                    return result;
                return null;
            }
        }
    }
}
