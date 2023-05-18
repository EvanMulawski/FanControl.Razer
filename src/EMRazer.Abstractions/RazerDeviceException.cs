namespace EMRazer
{
    [Serializable]
    public sealed class RazerDeviceException : Exception
    {
        public RazerDeviceException()
        {
        }

        public RazerDeviceException(string message) : base(message)
        {
        }

        public RazerDeviceException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}