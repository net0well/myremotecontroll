using MyRemoteControll.Contracts.Power;

namespace MyRemoteControll.Agent.Features.Power;

/// <summary>Contagem de energia mostrada no PC, com "Cancelar". Termina por cancelamento, execução ou encerramento.</summary>
public sealed class PowerCountdownNotice
{
    private readonly Func<Task<bool>> _cancel;
    private int _ended;

    internal PowerCountdownNotice(PowerCountdownDto countdown, Func<Task<bool>> cancel)
    {
        Countdown = countdown;
        _cancel = cancel;
    }

    /// <summary>Disparado uma vez, quando a contagem deixa de existir (para fechar a janela e o item do menu).</summary>
    public event EventHandler? Ended;

    public PowerCountdownDto Countdown { get; }

    public bool IsEnded => Volatile.Read(ref _ended) == 1;

    /// <summary>Cancela pelo PC (auditado como bandeja). Falso se a contagem já tinha terminado.</summary>
    public Task<bool> CancelAsync() => _cancel();

    internal void End()
    {
        if (Interlocked.Exchange(ref _ended, 1) == 0)
        {
            Ended?.Invoke(this, EventArgs.Empty);
        }
    }
}

/// <summary>
/// Liga as contagens de energia a quem as mostra no PC: a bandeja em produção (janela de contagem e menu), um
/// assinante nos testes. Os eventos chegam em threads de segundo plano.
/// </summary>
public sealed class PowerCountdownBroker
{
    public event EventHandler<PowerCountdownNotice>? Started;

    internal void Publish(PowerCountdownNotice notice) => Started?.Invoke(this, notice);
}
