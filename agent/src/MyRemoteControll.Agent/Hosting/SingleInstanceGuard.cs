namespace MyRemoteControll.Agent.Hosting;

/// <summary>
/// Garante uma instância por sessão do usuário. A segunda instância sinaliza a primeira (que mostra o menu) e sai.
/// </summary>
internal sealed class SingleInstanceGuard : IDisposable
{
    public const string DefaultName = @"Local\MyRemoteControll.Agent";

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _signal;
    private RegisteredWaitHandle? _registration;

    private SingleInstanceGuard(Mutex mutex, EventWaitHandle signal)
    {
        _mutex = mutex;
        _signal = signal;
    }

    /// <summary>Retorna <c>null</c> se outra instância já está rodando.</summary>
    public static SingleInstanceGuard? TryAcquire(string name = DefaultName)
    {
        var mutex = new Mutex(initiallyOwned: true, name, out var createdNew);
        if (!createdNew)
        {
            mutex.Dispose();
            return null;
        }

        return new SingleInstanceGuard(mutex, CreateSignal(name));
    }

    /// <summary>Acorda a instância que já está rodando.</summary>
    public static void SignalExisting(string name = DefaultName)
    {
        using var signal = CreateSignal(name);
        signal.Set();
    }

    public void OnSignal(Action callback)
    {
        _registration?.Unregister(null);
        _registration = ThreadPool.RegisterWaitForSingleObject(
            _signal, (_, _) => callback(), state: null, Timeout.Infinite, executeOnlyOnce: false);
    }

    public void Dispose()
    {
        _registration?.Unregister(null);
        _signal.Dispose();
        _mutex.Dispose();
    }

    private static EventWaitHandle CreateSignal(string name) =>
        new(initialState: false, EventResetMode.AutoReset, name + ".Show");
}
