using Microsoft.AspNetCore.SignalR;
using MyRemoteControll.Contracts.Hub;

namespace MyRemoteControll.Agent.Hubs;

/// <summary>
/// Avisa os aparelhos conectados antes de o host parar. <see cref="IHostedLifecycleService.StoppingAsync"/> roda antes
/// de qualquer serviço parar, então as conexões ainda estão abertas.
/// </summary>
internal sealed partial class ShutdownNotifier(
    IHubContext<RemoteHub, IRemoteClient> hubContext,
    ILogger<ShutdownNotifier> logger) : IHostedLifecycleService
{
    public const string Reason = "O agente está sendo encerrado no PC.";
    private static readonly TimeSpan SendTimeout = TimeSpan.FromSeconds(2);

    public async Task StoppingAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(SendTimeout);
        try
        {
            await hubContext.Clients.All.AgentShuttingDown(Reason).WaitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            LogNotificationTimedOut();
        }
    }

    public Task StartingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(Level = LogLevel.Warning, Message = "Tempo esgotado ao avisar os aparelhos sobre o encerramento")]
    private partial void LogNotificationTimedOut();
}
