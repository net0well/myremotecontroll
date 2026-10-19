namespace MyRemoteControll.Agent.Infrastructure.Persistence;

public sealed class DatabaseInitializationException : Exception
{
    public DatabaseInitializationException()
    {
    }

    public DatabaseInitializationException(string message)
        : base(message)
    {
    }

    public DatabaseInitializationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
