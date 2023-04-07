using System;

namespace Junior
{
    public class JsonDeserializer
    {
        public object? Deserialize(Type type, JsonTokenReader tokenReader)
        {
            var typeReader = JsonTypeReader.GetReader(type);
            if (typeReader != null)
            {
                return typeReader.ReadObject(tokenReader);
            }

            return null;
        }

        public async ValueTask<object?> DeserializeAsync(Type type, JsonTokenReader tokenReader)
        {
            var typeReader = JsonTypeReader.GetReader(type);
            if (typeReader != null)
            {
                return await typeReader.ReadObjectAsync(tokenReader).ConfigureAwait(false);
            }

            return null;
        }

        public T? Deserialize<T>(JsonTokenReader tokenReader)
        {
            var typeReader = (JsonTypeReader<T>)JsonTypeReader.GetReader(typeof(T));
            if (typeReader != null)
            {
                return typeReader.Read(tokenReader);
            }

            return default;
        }

        public async ValueTask<T?> DeserializeAsync<T>(JsonTokenReader tokenReader)
        {
            var typeReader = (JsonTypeReader<T>)JsonTypeReader.GetReader(typeof(T));
            if (typeReader != null)
            {
                return await typeReader.ReadAsync(tokenReader).ConfigureAwait(false);
            }

            return default;
        }
    }
}
