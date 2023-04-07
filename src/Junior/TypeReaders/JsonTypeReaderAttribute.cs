namespace Junior
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple=false, Inherited=false)]
    public class JsonTypeReaderAttribute : Attribute
    {
        public Type ReaderType { get; }

        public JsonTypeReaderAttribute(Type readerType)
        {
            this.ReaderType = readerType;
        }
    }
}
