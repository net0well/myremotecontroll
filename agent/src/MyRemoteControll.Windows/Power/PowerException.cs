namespace MyRemoteControll.Windows.Power;

public sealed class PowerException : Exception
{
    public PowerException()
    {
    }

    public PowerException(string message)
        : base(message)
    {
    }

    public PowerException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
