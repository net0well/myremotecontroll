namespace MyRemoteControll.Agent.Features.Pairing;

/// <summary>
/// 5 falhas de pareamento em 10 minutos bloqueiam o pareamento por 15 minutos (spec 002, AC-5). O contador é global:
/// por IP não adiantaria, porque um atacante na rede troca de endereço.
/// </summary>
internal sealed class PairingLockout(TimeProvider timeProvider)
{
    public const int MaxFailures = 5;
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(15);

    private readonly Lock _sync = new();
    private readonly Queue<DateTimeOffset> _failures = new();
    private DateTimeOffset? _lockedUntil;

    public bool IsLocked(out DateTimeOffset lockedUntil)
    {
        lock (_sync)
        {
            var now = timeProvider.GetUtcNow();
            if (_lockedUntil > now)
            {
                lockedUntil = _lockedUntil.Value;
                return true;
            }

            _lockedUntil = null;
            lockedUntil = default;
            return false;
        }
    }

    /// <returns>Verdadeiro se esta falha acabou de causar o bloqueio.</returns>
    public bool RegisterFailure()
    {
        lock (_sync)
        {
            var now = timeProvider.GetUtcNow();
            _failures.Enqueue(now);
            while (_failures.Count > 0 && _failures.Peek() <= now - Window)
            {
                _failures.Dequeue();
            }

            if (_failures.Count < MaxFailures)
            {
                return false;
            }

            _failures.Clear();
            _lockedUntil = now + LockDuration;
            return true;
        }
    }
}
