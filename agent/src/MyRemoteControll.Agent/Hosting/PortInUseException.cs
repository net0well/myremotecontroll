namespace MyRemoteControll.Agent.Hosting;

public sealed class PortInUseException : Exception
{
    public PortInUseException()
    {
    }

    public PortInUseException(string message)
        : base(message)
    {
    }

    public PortInUseException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public PortInUseException(IReadOnlyList<int> ports, Exception innerException)
        : base(Describe(ports), innerException)
    {
        Ports = ports;
    }

    public IReadOnlyList<int> Ports { get; } = [];

    private static string Describe(IReadOnlyList<int> ports) =>
        ports.Count switch
        {
            0 => "Uma das portas do agente está em uso por outro programa.",
            1 => $"A porta {ports[0]} está em uso por outro programa.",
            _ => $"As portas {string.Join(" e ", ports)} estão em uso por outros programas.",
        };
}
