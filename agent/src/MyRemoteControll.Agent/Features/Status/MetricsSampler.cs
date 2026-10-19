using System.Collections.Concurrent;
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
/// Amostrador único de métricas (spec 007): liga com o primeiro inscrito (painel ou <c>SubscribeMetrics</c>), para
/// com o último (AC-2) e roda no menor intervalo pedido. Cada amostra vira os cards de métricas e o <c>MetricsTick</c>.
/// </summary>
internal sealed partial class MetricsSampler : IHostedLifecycleService, IDisposable
{
    private const double TickTolerance = 0.9;

    private readonly Lazy<IMetricsSource> _source;
    private readonly DashboardAggregator _aggregator;
    private readonly IHubContext<RemoteHub, IRemoteClient> _hubContext;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<MetricsSampler> _logger;
    private readonly ConcurrentDictionary<string, MetricsSubscription> _metricsSubscribers = new(StringComparer.Ordinal);
    private readonly Lock _gate = new();
    private ITimer? _timer;
    private TimeSpan _interval;
    private int _sampling;

    public MetricsSampler(
        Lazy<IMetricsSource> source,
        DashboardAggregator aggregator,
        IHubContext<RemoteHub, IRemoteClient> hubContext,
        TimeProvider timeProvider,
        ILogger<MetricsSampler> logger)
    {
        _source = source;
        _aggregator = aggregator;
        _hubContext = hubContext;
        _timeProvider = timeProvider;
        _logger = logger;
        _aggregator.SubscribersChanged += (_, _) => Reschedule();
    }

    public bool IsRunning
    {
        get
        {
            lock (_gate)
            {
                return _timer is not null;
            }
        }
    }

    public void SubscribeMetrics(string connectionId, int intervalMs)
    {
        _metricsSubscribers[connectionId] = new MetricsSubscription(DashboardAggregator.ClampInterval(intervalMs));
        Reschedule();
    }

    public void UnsubscribeMetrics(string connectionId)
    {
        if (_metricsSubscribers.TryRemove(connectionId, out _))
        {
            Reschedule();
        }
    }

    /// <summary>
    /// Cria a fonte em segundo plano depois que o agente sobe: montar os contadores leva quase 1 s neste PC e não
    /// deve atrasar a inicialização nem o primeiro painel.
    /// </summary>
    public Task StartedAsync(CancellationToken cancellationToken)
    {
        _ = Task.Run(WarmUp, CancellationToken.None);
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

    private void WarmUp()
    {
        try
        {
            _ = _source.Value;
        }
        catch (InvalidOperationException ex)
        {
            LogSourceUnavailable(ex);
        }
    }

    private void Reschedule()
    {
        lock (_gate)
        {
            List<TimeSpan> intervals = [.. _aggregator.MetricIntervals, .. _metricsSubscribers.Values.Select(m => m.Interval)];
            if (intervals.Count == 0)
            {
                _timer?.Dispose();
                _timer = null;
                return;
            }

            var interval = intervals.Min();
            if (_timer is null)
            {
                // A primeira amostra sai na hora, para o painel não ficar vazio até o primeiro intervalo.
                _timer = _timeProvider.CreateTimer(_ => Tick(), null, TimeSpan.Zero, interval);
            }
            else if (interval != _interval)
            {
                _timer.Change(interval, interval);
            }

            _interval = interval;
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

    private void Tick()
    {
        // Uma amostra por vez: se a anterior ainda está rodando, esta é pulada.
        if (Interlocked.Exchange(ref _sampling, 1) == 1)
        {
            return;
        }

        try
        {
            var now = _timeProvider.GetUtcNow();
            var metrics = MonitoringMapper.ToMetrics(_source.Value.Sample(), now);
            PublishCards(metrics);
            SendTicks(metrics, now);
        }
        catch (InvalidOperationException ex)
        {
            LogSourceUnavailable(ex);
        }
        finally
        {
            Volatile.Write(ref _sampling, 0);
        }
    }

    private void PublishCards(MetricsSnapshot metrics)
    {
        const string permission = PermissionKeys.MonitorView;
        _aggregator.Publish(DashboardCard.Create(DashboardCards.Cpu, permission, metrics.Cpu, metrics: true, lite: MonitoringMapper.Lite(metrics.Cpu)));
        _aggregator.Publish(DashboardCard.Create(DashboardCards.Memory, permission, metrics.Memory, metrics: true));
        _aggregator.Publish(DashboardCard.Create(DashboardCards.Network, permission, metrics.Network, metrics: true, lite: MonitoringMapper.Lite(metrics.Network)));
        _aggregator.Publish(DashboardCard.Create(DashboardCards.Disks, permission, new DisksCardData(metrics.Disks), metrics: true));
        if (metrics.Gpu is not null)
        {
            _aggregator.Publish(DashboardCard.Create(DashboardCards.Gpu, permission, metrics.Gpu, metrics: true));
        }
        else
        {
            _aggregator.Remove(DashboardCards.Gpu);
        }
    }

    private void SendTicks(MetricsSnapshot metrics, DateTimeOffset now)
    {
        foreach (var (connectionId, subscription) in _metricsSubscribers)
        {
            if (now - subscription.LastSentAt < subscription.Interval * TickTolerance)
            {
                continue;
            }

            subscription.LastSentAt = now;
            _ = SendTickAsync(connectionId, metrics);
        }
    }

    private async Task SendTickAsync(string connectionId, MetricsSnapshot metrics)
    {
        try
        {
            await _hubContext.Clients.Client(connectionId).MetricsTick(metrics).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogTickFailed(ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Fonte de métricas indisponível")]
    private partial void LogSourceUnavailable(Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Falha ao enviar MetricsTick")]
    private partial void LogTickFailed(Exception exception);

    private sealed class MetricsSubscription(TimeSpan interval)
    {
        public TimeSpan Interval { get; } = interval;

        public DateTimeOffset LastSentAt { get; set; } = DateTimeOffset.MinValue;
    }
}
