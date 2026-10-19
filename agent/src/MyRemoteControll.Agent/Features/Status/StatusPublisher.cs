using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using MyRemoteControll.Agent.Hubs;
using MyRemoteControll.Agent.Infrastructure.Dashboard;
using MyRemoteControll.Contracts.Dashboard;
using MyRemoteControll.Contracts.Hub;
using MyRemoteControll.Contracts.Security;
using MyRemoteControll.Contracts.Status;
using MyRemoteControll.Windows.Monitoring;

namespace MyRemoteControll.Agent.Features.Status;

/// <summary>
/// Cards "PC e conexão" e "Bateria" (spec 007). O status é relido a cada 5 s enquanto alguém olha o painel; se mudou,
/// os cards são republicados e <c>StatusUpdated</c> vai para <c>perm:status.view</c>.
/// </summary>
internal sealed partial class StatusPublisher : IHostedLifecycleService, IDisposable
{
    public static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(5);

    private readonly ISystemStatusSource _source;
    private readonly DashboardAggregator _aggregator;
    private readonly IHubContext<RemoteHub, IRemoteClient> _hubContext;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<StatusPublisher> _logger;
    private readonly Lock _gate = new();
    private ITimer? _timer;
    private string? _lastStatusJson;

    public StatusPublisher(
        ISystemStatusSource source,
        DashboardAggregator aggregator,
        IHubContext<RemoteHub, IRemoteClient> hubContext,
        TimeProvider timeProvider,
        ILogger<StatusPublisher> logger)
    {
        _source = source;
        _aggregator = aggregator;
        _hubContext = hubContext;
        _timeProvider = timeProvider;
        _logger = logger;
        _aggregator.SubscribersChanged += (_, _) => UpdateTimer();
    }

    public SystemStatus GetStatus() => MonitoringMapper.ToStatus(_source.GetStatus());

    public Task StartedAsync(CancellationToken cancellationToken)
    {
        // Os cards existem desde o início: o primeiro snapshot já os traz.
        _ = Task.Run(() => RefreshAsync(CancellationToken.None), CancellationToken.None);
        return Task.CompletedTask;
    }

    public Task StartingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppingAsync(CancellationToken cancellationToken)
    {
        StopTimer();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public void Dispose() => StopTimer();

    /// <summary>Relê o status; publica o que mudou.</summary>
    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        try
        {
            var status = GetStatus();
            var json = JsonSerializer.Serialize(status, DashboardJson.Options);
            var changed = !string.Equals(Interlocked.Exchange(ref _lastStatusJson, json), json, StringComparison.Ordinal);
            if (changed)
            {
                _aggregator.Publish(DashboardCard.Create(DashboardCards.Pc, PermissionKeys.StatusView, MonitoringMapper.ToPcCard(status)));
                if (status.Battery is { } battery)
                {
                    _aggregator.Publish(DashboardCard.Create(DashboardCards.Battery, PermissionKeys.StatusView, battery));
                }
                else
                {
                    _aggregator.Remove(DashboardCards.Battery);
                }

                await _hubContext.Clients.Group(PermissionGroups.For(PermissionKeys.StatusView)).StatusUpdated(status).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogRefreshFailed(ex);
        }
    }

    private void UpdateTimer()
    {
        lock (_gate)
        {
            if (_aggregator.SubscriberCount == 0)
            {
                _timer?.Dispose();
                _timer = null;
            }
            else
            {
                _timer ??= _timeProvider.CreateTimer(_ => _ = RefreshAsync(CancellationToken.None), null, RefreshInterval, RefreshInterval);
            }
        }
    }

    private void StopTimer()
    {
        lock (_gate)
        {
            _timer?.Dispose();
            _timer = null;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Falha ao atualizar o status do PC")]
    private partial void LogRefreshFailed(Exception exception);
}
