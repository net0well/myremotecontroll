using MyRemoteControll.Agent.Infrastructure.Dashboard;
using MyRemoteControll.Contracts.Dashboard;
using MyRemoteControll.Contracts.Security;

namespace MyRemoteControll.Agent.Features.Power;

/// <summary>
/// Card "Energia" do painel (spec 005): contagem ativa, o que o PC oferece e as ações que cada aparelho pode usar.
/// Republicado na hora quando uma contagem começa ou termina e, enquanto alguém olha o painel, a cada 5 s (capacidades
/// e atualização pendente mudam sem aviso).
/// </summary>
internal sealed partial class PowerCardPublisher : IHostedLifecycleService, IDisposable
{
    public static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(5);

    private readonly PowerService _power;
    private readonly DashboardAggregator _aggregator;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<PowerCardPublisher> _logger;
    private readonly Lock _gate = new();
    private ITimer? _timer;

    public PowerCardPublisher(PowerService power, DashboardAggregator aggregator, TimeProvider timeProvider, ILogger<PowerCardPublisher> logger)
    {
        _power = power;
        _aggregator = aggregator;
        _timeProvider = timeProvider;
        _logger = logger;
        _power.Changed += (_, _) => Publish();
        _aggregator.SubscribersChanged += (_, _) => UpdateTimer();
    }

    /// <summary>Relê capacidades e contagem e publica se algo mudou.</summary>
    public void Publish()
    {
        try
        {
            var capabilities = _power.Capabilities;
            var data = new PowerCardData(_power.Current, capabilities.SleepAvailable, capabilities.HibernateAvailable, _power.UpdatePending);
            var card = DashboardCard.Create(DashboardCards.Power, PermissionKeys.StatusView, data, Actions(data));

            // Republicar sem mudança geraria um delta à toa a cada 5 s.
            if (_aggregator.Get(DashboardCards.Power) is { } current
                && current.Data.GetRawText() == card.Data.GetRawText()
                && current.Actions.SequenceEqual(card.Actions))
            {
                return;
            }

            _aggregator.Publish(card);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            LogPublishFailed(ex);
        }
    }

    public Task StartedAsync(CancellationToken cancellationToken)
    {
        // O card existe desde o início: o primeiro snapshot já o traz.
        _ = Task.Run(Publish, CancellationToken.None);
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

    private static CardAction[] Actions(PowerCardData data) =>
    [
        new(PowerCardActions.Lock, PermissionKeys.PowerBasic),
        new(PowerCardActions.Sleep, PermissionKeys.PowerBasic, data.SleepAvailable),
        new(PowerCardActions.Restart, PermissionKeys.PowerShutdown),
        new(PowerCardActions.Hibernate, PermissionKeys.PowerShutdown, data.HibernateAvailable),
        new(PowerCardActions.Shutdown, PermissionKeys.PowerShutdown),
        new(PowerCardActions.Cancel, PermissionKeys.PowerBasic, data.Pending is not null),
    ];

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
                _timer ??= _timeProvider.CreateTimer(_ => Publish(), null, RefreshInterval, RefreshInterval);
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

    [LoggerMessage(Level = LogLevel.Warning, Message = "Falha ao atualizar o card de energia")]
    private partial void LogPublishFailed(Exception exception);
}
