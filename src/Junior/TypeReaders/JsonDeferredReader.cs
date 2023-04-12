namespace Junior
{
    public class JsonDeferredReader<TType> : JsonTypeReader<TType>
    {
        private JsonTypeReader<TType>? _reader;

        public JsonDeferredReader()
        {
        }

        private JsonTypeReader<TType> GetReader()
        {
            if (_reader == null)
            {
                var tmp = JsonTypeReader.GetReader(typeof(TType)) as JsonTypeReader<TType>;
                // It is possible that another thread gains access to this reader
                // before the true reader has been updated.
                // In this case, the returned reader will be this deferred reader instance.
                // Note: maybe this should go into a spin lock to wait for the 
                // reader to get updated?
                if (tmp != this)
                    _reader = tmp;
            }

            return _reader!;
        }

        public override TType? Read(JsonTokenReader tokenReader)
        {
            var reader = GetReader();
            if (reader != null)
                return reader.Read(tokenReader);
            return default;
        }

        public override ValueTask<TType?> ReadAsync(JsonTokenReader tokenReader)
        {
            var reader = GetReader();
            if (reader != null)
                return reader.ReadAsync(tokenReader);
            return default;
        }
    }
}
