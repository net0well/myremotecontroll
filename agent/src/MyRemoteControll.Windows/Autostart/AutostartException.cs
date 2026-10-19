namespace MyRemoteControll.Windows.Autostart;

public sealed class AutostartException : Exception
{
    public AutostartException()
    {
    }

    public AutostartException(string message)
        : base(message)
    {
    }

    public AutostartException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
