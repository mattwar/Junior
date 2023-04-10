namespace Junior
{
    public abstract class StreamingDataReader
    {
        public abstract string TableName { get; }
        public abstract int FieldCount { get; }
        public abstract string GetFieldName(int index);
        public abstract string GetFieldType(int index);

        public abstract int CurrentFieldIndex { get; }
        public abstract string CurrentFieldName { get; }
        public abstract string CurrentFieldType { get; }
        public abstract ReadOnlySpan<char> CurrentFieldValueChunk { get; }

        public abstract ValueTask<bool> MoveToNextTableAsync();
        public abstract ValueTask<bool> MoveToNextRowAsync();
        public abstract ValueTask<bool> MoveToNextFieldAsync();
        public abstract ValueTask<object?> ReadFieldValueAsync();
        public abstract ValueTask<T> ReadFieldValueAsync<T>();
        public abstract ValueTask<object?> ReadFieldValueAsync(Type type);
        public abstract ValueTask<bool> ReadNextFieldValueChunkAsync();


        public async virtual Task<IReadOnlyList<object?>> ReadRowAsync()
        {
            var values = new object?[this.FieldCount];

            while (await this.MoveToNextFieldAsync().ConfigureAwait(false))
            {
                var value = await this.ReadFieldValueAsync().ConfigureAwait(false);
                values[this.CurrentFieldIndex] = value;
            }

            return values;
        }

        public async virtual Task<object?> ReadRowAsync(Type rowType)
        {
            if (RowReader.GetReader(rowType) is RowReader reader)
            {
                return await reader.ReadObjectAsync(this).ConfigureAwait(false)!;
            }
            else
            {
                return default;
            }
        }

        public async virtual Task<TRow> ReadRowAsync<TRow>()
        {
            if (RowReader.GetReader(typeof(TRow)) is RowReader<TRow> reader)
            {
                return await reader.ReadAsync(this).ConfigureAwait(false);
            }
            else
            {
                return default!;
            }
        }

        public async virtual IAsyncEnumerable<IReadOnlyList<object?>> ReadRowsAsync()
        {
            while (await this.MoveToNextRowAsync().ConfigureAwait(false))
            {
                var row = await ReadRowAsync().ConfigureAwait(false);
                if (row == null)
                    break;
                yield return row;
            }
        }

        public async virtual IAsyncEnumerable<object> ReadRowsAsync(Type rowType)
        {
            if (RowReader.GetReader(rowType) is RowReader reader)
            {
                while (await this.MoveToNextRowAsync().ConfigureAwait(false))
                {
                    var row = await reader.ReadObjectAsync(this).ConfigureAwait(false);
                    if (row == null)
                        break;
                    yield return row;
                }
            }
        }

        public async virtual IAsyncEnumerable<TRow> ReadRowsAsync<TRow>()
        {
            if (RowReader.GetReader(typeof(TRow)) is RowReader<TRow> reader)
            {
                while (await this.MoveToNextRowAsync().ConfigureAwait(false))
                {
                    var row = await reader.ReadAsync(this).ConfigureAwait(false);
                    if (row == null)
                        break;
                    yield return row;
                }
            }
        }
    }
}
