using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR;
using MyRemoteControll.Agent.Hubs;
using MyRemoteControll.Agent.Infrastructure.Authorization;
using MyRemoteControll.Contracts.Dashboard;
using MyRemoteControll.Contracts.Hub;

namespace MyRemoteControll.Agent.Infrastructure.Dashboard;

/// <summary>
/// Estado do painel em memória e envio por conexão (ADR-0021, spec 007). As features publicam cards inteiros; cada
/// inscrito recebe um snapshot e depois deltas com versão própria, filtrados pelas permissões do aparelho e com
/// throttle por card. O envio roda num laço em segundo plano, fora da thread do Hub.
/// </summary>
internal sealed partial class DashboardAggregator(
    IHubContext<RemoteHub, IRemoteClient> hubContext,
    DevicePermissionResolver permissions,
    TimeProvider timeProvider,
    ILogger<DashboardAggregator> logger) : BackgroundService
{
    public const int MinIntervalMs = 500;
    public const int MaxIntervalMs = 10_000;

    /// <summary>Cards que não são de métricas vão no máximo nesse ritmo; também é o ritmo da varredura de pendentes.</summary>
    public static readonly TimeSpan EventCardMinInterval = TimeSpan.FromMilliseconds(250);

    // Folga no throttle das métricas: a amostra chega com pequenas variações e não deve pular um ciclo por isso.
    private const double MetricsTolerance = 0.9;

    private readonly ConcurrentDictionary<string, Published> _cards = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Subscriber> _subscribers = new(StringComparer.Ordinal);
    private readonly Channel<bool> _wake = Channel.CreateBounded<bool>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite });
    private readonly Lock _timerGate = new();
    private long _revision;
    private ITimer? _sweep;

    /// <summary>Inscritos entraram, saíram ou mudaram de intervalo (o amostrador recalcula o ritmo).</summary>
    public event EventHandler? SubscribersChanged;

    public int SubscriberCount => _subscribers.Count;

    public IReadOnlyList<TimeSpan> MetricIntervals => [.. _subscribers.Values.Select(s => s.Interval)];

    public static TimeSpan ClampInterval(int intervalMs) =>
        TimeSpan.FromMilliseconds(Math.Clamp(intervalMs, MinIntervalMs, MaxIntervalMs));

    public void Publish(DashboardCard card)
    {
        _cards[card.CardId] = new Published(card, Interlocked.Increment(ref _revision));
        Wake();
    }

    /// <summary>Tira o card do painel (ex.: bateria removida); quem já o tinha recebe um patch de remoção.</summary>
    public void Remove(string cardId)
    {
        if (_cards.TryGetValue(cardId, out var current) && current.Card is not null)
        {
            _cards[cardId] = new Published(null, Interlocked.Increment(ref _revision));
            Wake();
        }
    }

    public DashboardCard? Get(string cardId) => _cards.TryGetValue(cardId, out var published) ? published.Card : null;

    public async Task<DashboardSnapshot> SubscribeAsync(string connectionId, Guid deviceId, DashboardOptions options, CancellationToken cancellationToken)
    {
        var subscriber = _subscribers.GetOrAdd(connectionId, id => new Subscriber(id, deviceId));
        subscriber.Interval = ClampInterval(options.MetricsIntervalMs);
        subscriber.LowData = options.LowDataMode;
        var snapshot = await SnapshotAsync(subscriber, cancellationToken).ConfigureAwait(false);

        UpdateSweep();
        SubscribersChanged?.Invoke(this, EventArgs.Empty);
        return snapshot;
    }

    public async Task<DashboardSnapshot?> RequestSnapshotAsync(string connectionId, CancellationToken cancellationToken) =>
        _subscribers.TryGetValue(connectionId, out var subscriber)
            ? await SnapshotAsync(subscriber, cancellationToken).ConfigureAwait(false)
            : null;

    public bool SetRate(string connectionId, int intervalMs)
    {
        if (!_subscribers.TryGetValue(connectionId, out var subscriber))
        {
            return false;
        }

        subscriber.Interval = ClampInterval(intervalMs);
        SubscribersChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public void Unsubscribe(string connectionId)
    {
        if (_subscribers.TryRemove(connectionId, out var subscriber))
        {
            subscriber.Removed = true;
            UpdateSweep();
            SubscribersChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public override void Dispose()
    {
        lock (_timerGate)
        {
            _sweep?.Dispose();
            _sweep = null;
        }

        base.Dispose();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var _ in _wake.Reader.ReadAllAsync(stoppingToken).ConfigureAwait(false))
            {
                var now = timeProvider.GetUtcNow();
                foreach (var subscriber in _subscribers.Values)
                {
                    await DispatchAsync(subscriber, now, stoppingToken).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Encerramento do agente.
        }
    }

    private static IReadOnlyList<string> AvailableActions(DashboardCard card, DeviceAccess access) =>
        [.. card.Actions.Where(a => a.Available && access.Has(a.Permission)).Select(a => a.Id)];

    private static CardState ToState(DashboardCard card, IReadOnlyList<string> actions, bool lowData) =>
        new(card.CardId, card.Kind, lowData && card.LiteData is { } lite ? lite : card.Data, actions);

    private void Wake() => _wake.Writer.TryWrite(true);

    /// <summary>Enquanto houver inscritos, acorda o laço a cada 250 ms para enviar o que estava segurado pelo throttle.</summary>
    private void UpdateSweep()
    {
        lock (_timerGate)
        {
            if (_subscribers.IsEmpty)
            {
                _sweep?.Dispose();
                _sweep = null;
            }
            else
            {
                _sweep ??= timeProvider.CreateTimer(_ => Wake(), null, EventCardMinInterval, EventCardMinInterval);
            }
        }
    }

    private async Task<DashboardSnapshot> SnapshotAsync(Subscriber subscriber, CancellationToken cancellationToken)
    {
        await subscriber.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var now = timeProvider.GetUtcNow();
            var access = await permissions.GetAsync(subscriber.DeviceId, cancellationToken).ConfigureAwait(false);
            List<CardState> cards = [];
            subscriber.Sent.Clear();
            foreach (var (cardId, published) in _cards)
            {
                if (published.Card is { } card && access is not null && access.Has(card.ReadPermission))
                {
                    var actions = AvailableActions(card, access);
                    cards.Add(ToState(card, actions, subscriber.LowData));
                    subscriber.Sent[cardId] = new SentCard(published.Revision, now, Visible: true, actions);
                }
                else
                {
                    subscriber.Sent[cardId] = new SentCard(published.Revision, now, Visible: false, []);
                }
            }

            // O snapshot ganha uma versão nova; o próximo delta desta conexão será Version + 1.
            subscriber.Version++;
            return new DashboardSnapshot(subscriber.Version, now, cards);
        }
        finally
        {
            subscriber.Gate.Release();
        }
    }

    private async Task DispatchAsync(Subscriber subscriber, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (subscriber.Removed)
        {
            return;
        }

        await subscriber.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Sem acesso (aparelho revogado): a conexão está sendo derrubada, nada a enviar.
            if (await permissions.GetAsync(subscriber.DeviceId, cancellationToken).ConfigureAwait(false) is not { } access)
            {
                return;
            }

            List<CardPatch> patches = [];
            foreach (var (cardId, published) in _cards)
            {
                if (Patch(subscriber, cardId, published, access, now) is { } patch)
                {
                    patches.Add(patch);
                }
            }

            if (patches.Count == 0)
            {
                return;
            }

            subscriber.Version++;
            await hubContext.Clients.Client(subscriber.ConnectionId).DashboardDelta(subscriber.Version, patches).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogDispatchFailed(ex);
        }
        finally
        {
            subscriber.Gate.Release();
        }
    }

    /// <summary>
    /// Patch de um card para um inscrito, ou nulo se nada mudou (ou se ainda está segurado pelo throttle). Muda quando
    /// a revisão do card muda, quando o aparelho ganha ou perde a permissão de leitura ou quando as ações mudam.
    /// </summary>
    private static CardPatch? Patch(Subscriber subscriber, string cardId, Published published, DeviceAccess access, DateTimeOffset now)
    {
        subscriber.Sent.TryGetValue(cardId, out var sent);
        if (published.Card is not { } card || !access.Has(card.ReadPermission))
        {
            if (sent is { Visible: true })
            {
                subscriber.Sent[cardId] = new SentCard(published.Revision, now, Visible: false, []);
                return new CardPatch(cardId, null);
            }

            return null;
        }

        var actions = AvailableActions(card, access);
        var newContent = sent is null || sent.Revision != published.Revision;
        if (sent is { Visible: true } && !newContent && sent.Actions.SequenceEqual(actions))
        {
            return null;
        }

        if (sent is { Visible: true } && newContent)
        {
            var minimum = card.Metrics ? subscriber.Interval * MetricsTolerance : EventCardMinInterval;
            if (now - sent.SentAt < minimum)
            {
                return null;
            }
        }

        subscriber.Sent[cardId] = new SentCard(published.Revision, now, Visible: true, actions);
        return new CardPatch(cardId, ToState(card, actions, subscriber.LowData));
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Falha ao enviar o delta do painel")]
    private partial void LogDispatchFailed(Exception exception);

    private sealed record Published(DashboardCard? Card, long Revision);

    private sealed record SentCard(long Revision, DateTimeOffset SentAt, bool Visible, IReadOnlyList<string> Actions);

    /// <summary>
    /// Inscrição de uma conexão. O semáforo não é descartado ao sair: o laço de envio pode estar esperando por ele, e
    /// sem <c>AvailableWaitHandle</c> não há recurso nativo a liberar.
    /// </summary>
    private sealed class Subscriber(string connectionId, Guid deviceId)
    {
        public string ConnectionId { get; } = connectionId;

        public Guid DeviceId { get; } = deviceId;

        /// <summary>Snapshot e deltas desta conexão saem um de cada vez, em ordem de versão.</summary>
        public SemaphoreSlim Gate { get; } = new(1, 1);

        public Dictionary<string, SentCard> Sent { get; } = new(StringComparer.Ordinal);

        public long Version { get; set; }

        public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(1);

        public bool LowData { get; set; }

        public bool Removed { get; set; }
    }
}
