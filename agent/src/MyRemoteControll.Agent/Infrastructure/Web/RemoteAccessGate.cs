namespace MyRemoteControll.Agent.Infrastructure.Web;

/// <summary>Estado de "acesso remoto pausado" controlado pela bandeja (e, na spec 026, pelo pânico).</summary>
public sealed class RemoteAccessGate
{
    private int _paused;

    public event EventHandler? Changed;

    public bool IsPaused => Volatile.Read(ref _paused) == 1;

    public void Pause() => Set(paused: true);

    public void Resume() => Set(paused: false);

    private void Set(bool paused)
    {
        var value = paused ? 1 : 0;
        if (Interlocked.Exchange(ref _paused, value) != value)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
