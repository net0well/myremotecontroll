using MyRemoteControll.Contracts.Devices;

namespace MyRemoteControll.Agent.Features.Pairing;

public sealed record PairingRequestInfo(string DeviceName, DevicePlatform Platform, string? RemoteAddress, int ActiveDeviceCount);

public enum PairingDecision
{
    Approved,
    Rejected,
    TimedOut,
}

/// <summary>Um pedido aguardando "Permitir / Recusar" no PC. Fechado por decisão ou por tempo esgotado.</summary>
public sealed class PairingConfirmationRequest
{
    private readonly TaskCompletionSource<PairingDecision> _decision = new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal PairingConfirmationRequest(PairingRequestInfo info) => Info = info;

    /// <summary>Disparado quando o pedido deixa de aceitar decisão (ex.: para fechar o diálogo após o tempo esgotar).</summary>
    public event EventHandler? Closed;

    public PairingRequestInfo Info { get; }

    public bool IsClosed => _decision.Task.IsCompleted;

    internal Task<PairingDecision> Decision => _decision.Task;

    public bool Approve() => Complete(PairingDecision.Approved);

    public bool Reject() => Complete(PairingDecision.Rejected);

    internal void Expire() => Complete(PairingDecision.TimedOut);

    private bool Complete(PairingDecision decision)
    {
        if (!_decision.TrySetResult(decision))
        {
            return false;
        }

        Closed?.Invoke(this, EventArgs.Empty);
        return true;
    }
}

/// <summary>
/// Liga o pedido HTTP de pareamento a quem confirma no PC: a bandeja em produção, um assinante automático nos testes.
/// Sem ninguém para confirmar, o pedido não é aprovado (spec 002).
/// </summary>
public sealed class PairingConfirmationBroker
{
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    public event EventHandler<PairingConfirmationRequest>? Requested;

    public async Task<PairingDecision> RequestAsync(PairingRequestInfo info, TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        var handler = Requested;
        if (handler is null)
        {
            return PairingDecision.TimedOut;
        }

        var request = new PairingConfirmationRequest(info);
        try
        {
            handler(this, request);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var delay = Task.Delay(Timeout, timeProvider, timeout.Token);
            if (await Task.WhenAny(request.Decision, delay).ConfigureAwait(false) == request.Decision)
            {
                await timeout.CancelAsync().ConfigureAwait(false);
            }
            else
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
        }
        finally
        {
            // Tempo esgotado, pedido cancelado pelo cliente ou exceção: o diálogo do PC precisa fechar.
            request.Expire();
        }

        return await request.Decision.ConfigureAwait(false);
    }
}
