namespace Junior
{
    public class JsonDeserializer
    {
        public static object? Deserialize(Type type, JsonTokenReader tokenReader)
        {
            var typeReader = JsonTypeReader.GetReader(type);
            if (typeReader != null)
            {
                return typeReader.ReadObject(tokenReader);
            }

            return null;
        }

        public static async ValueTask<object?> DeserializeAsync(Type type, JsonTokenReader tokenReader)
        {
            var typeReader = JsonTypeReader.GetReader(type);
            if (typeReader != null)
            {
                return await typeReader.ReadObjectAsync(tokenReader).ConfigureAwait(false);
            }

            return null;
        }

        public static T? Deserialize<T>(JsonTokenReader tokenReader)
        {
            if (JsonTypeReader.GetReader(typeof(T)) is JsonTypeReader<T> typeReader)
            {
                return typeReader.Read(tokenReader);
            }

            return default;
        }

        public static async ValueTask<T?> DeserializeAsync<T>(JsonTokenReader tokenReader)
        {
            if (JsonTypeReader.GetReader(typeof(T)) is JsonTypeReader<T> typeReader)
            {
                return await typeReader.ReadAsync(tokenReader).ConfigureAwait(false);
            }

            return default;
        }
    }
}
