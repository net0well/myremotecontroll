using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using MyRemoteControll.Agent.Features.Power;
using MyRemoteControll.Agent.Features.Status;
using MyRemoteControll.Agent.Infrastructure.Dashboard;
using MyRemoteControll.Agent.IntegrationTests.Support;
using MyRemoteControll.Contracts.Dashboard;
using MyRemoteControll.Contracts.Errors;
using MyRemoteControll.Contracts.Hub;
using MyRemoteControll.Contracts.Pairing;
using MyRemoteControll.Contracts.Power;
using MyRemoteControll.Contracts.Status;
using MyRemoteControll.Windows.Monitoring;
using MyRemoteControll.Windows.Power;

namespace MyRemoteControll.Agent.IntegrationTests.Spec007;

/// <summary>Spec 007 — status e painel em tempo real (docs/specs/007-status-dashboard.md), lado do agente.</summary>
public sealed class DashboardTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AC1_Dashboard_SendsMetricDeltasAtTheChosenInterval()
    {
        await using var scenario = await Scenario.StartAsync();
        var fast = await scenario.ConnectAsync("Admin");
        var slow = await scenario.ConnectAsync("Tablet");
        await fast.SubscribeAsync(1000);
        await slow.SubscribeAsync(3000);
        fast.ResetCounts();
        slow.ResetCounts();

        for (var second = 1; second <= 9; second++)
        {
            scenario.Time.Advance(TimeSpan.FromSeconds(1));
            var expected = second;
            await Eventually.TrueAsync(() => fast.CardPatches(DashboardCards.Cpu) >= expected, $"CPU do inscrito de 1 s no segundo {second}");
            await Task.Delay(50, Ct);
        }

        Assert.Equal(9, fast.CardPatches(DashboardCards.Cpu));
        Assert.Equal(3, slow.CardPatches(DashboardCards.Cpu));
    }

    [Fact]
    public async Task AC1_SetDashboardRate_ChangesTheIntervalOfTheConnection()
    {
        await using var scenario = await Scenario.StartAsync();
        var client = await scenario.ConnectAsync("Admin");
        await client.SubscribeAsync(1000);

        var changed = await client.Hub.InvokeAsync<HubResponse<bool>>("SetDashboardRate", 2000, Ct);
        client.ResetCounts();
        for (var second = 1; second <= 6; second++)
        {
            scenario.Time.Advance(TimeSpan.FromSeconds(1));
            await Task.Delay(50, Ct);
        }

        Assert.True(changed.Ok);
        await Eventually.TrueAsync(() => client.CardPatches(DashboardCards.Cpu) == 3, "CPU a cada 2 s");
    }

    [Fact]
    public async Task AC2_WithoutSubscribers_TheSamplerStops()
    {
        await using var scenario = await Scenario.StartAsync();
        var sampler = scenario.Agent.Services.GetRequiredService<MetricsSampler>();
        var client = await scenario.ConnectAsync("Admin");
        await client.SubscribeAsync(1000);
        scenario.Time.Advance(TimeSpan.FromSeconds(3));
        Assert.True(scenario.Metrics.Samples >= 3);

        await client.Hub.InvokeAsync<HubResponse<bool>>("UnsubscribeDashboard", Ct);
        var samples = scenario.Metrics.Samples;
        scenario.Time.Advance(TimeSpan.FromSeconds(10));

        Assert.False(sampler.IsRunning);
        Assert.Equal(samples, scenario.Metrics.Samples);

        // A conexão que cai também deixa de contar como inscrita.
        await client.SubscribeAsync(1000);
        Assert.True(sampler.IsRunning);
        await client.Hub.DisposeAsync();
        await Eventually.TrueAsync(() => !sampler.IsRunning, "amostrador parado depois da desconexão");
    }

    [Fact]
    public async Task Versions_StartAfterTheSnapshotAndGrowOneByOne()
    {
        await using var scenario = await Scenario.StartAsync();
        var client = await scenario.ConnectAsync("Admin");
        var snapshot = await client.SubscribeAsync(1000);

        for (var second = 1; second <= 3; second++)
        {
            scenario.Time.Advance(TimeSpan.FromSeconds(1));
            await Eventually.TrueAsync(() => client.Versions.Count >= second, "delta");
        }

        var again = await client.Hub.InvokeAsync<HubResponse<DashboardSnapshot>>("RequestDashboardSnapshot", Ct);
        Assert.Equal(Enumerable.Range(1, client.Versions.Count).Select(i => snapshot.Version + i), client.Versions);
        Assert.True(again.Data!.Version > client.Versions.Max());
    }

    [Fact]
    public async Task AC7_IdleDashboard_AverageTrafficStaysUnder5KBps()
    {
        await using var scenario = await Scenario.StartAsync();
        var full = await scenario.ConnectAsync("Wi-Fi");
        var lite = await scenario.ConnectAsync("Dados móveis");
        await full.SubscribeAsync(1000);
        await lite.SubscribeAsync(3000, lowData: true);
        full.ResetCounts();
        lite.ResetCounts();

        for (var second = 1; second <= 60; second++)
        {
            scenario.Time.Advance(TimeSpan.FromSeconds(1));
            var expected = second;
            await Eventually.TrueAsync(() => full.CardPatches(DashboardCards.Cpu) >= expected, "delta do segundo");
        }

        await Task.Delay(100, Ct);
        var fullPerSecond = full.Bytes / 60.0;
        var litePerSecond = lite.Bytes / 60.0;
        TestContext.Current.SendDiagnosticMessage($"Painel: {fullPerSecond:F0} B/s (1 s) e {litePerSecond:F0} B/s (3 s, dados reduzidos)");
        Assert.InRange(fullPerSecond, 1, 5 * 1024);
        Assert.InRange(litePerSecond, 1, 2 * 1024);
    }

    /// <summary>
    /// AC-7 com as fontes reais deste PC e o relógio real (60 s). Explícito porque depende da máquina e demora:
    /// <c>dotnet test --project tests/MyRemoteControll.Agent.IntegrationTests -- --filter-method "*RealSources*" --explicit only</c>.
    /// </summary>
    [Fact(Explicit = true)]
    public async Task AC7_RealSources_IdleTrafficOnThisPc()
    {
        await using var agent = await TestAgent.StartAsync();
        var device = await agent.PairAsync("Wi-Fi");
        var client = new DashboardClient(await agent.ConnectHubAsync(device.Token), device);
        await client.SubscribeAsync(1000);
        await Task.Delay(TimeSpan.FromSeconds(3), Ct);
        client.ResetCounts();

        await Task.Delay(TimeSpan.FromSeconds(60), Ct);

        var perSecond = client.Bytes / 60.0;
        TestContext.Current.SendDiagnosticMessage(
            $"Painel com fontes reais: {perSecond:F0} B/s; CPU {client.CardPatches(DashboardCards.Cpu)} deltas em 60 s");
        Assert.InRange(perSecond, 1, 5 * 1024);
        Assert.InRange(client.CardPatches(DashboardCards.Cpu), 55, 61);
    }

    [Fact]
    public async Task LowDataMode_DropsCoresAndInterfaces()
    {
        await using var scenario = await Scenario.StartAsync();
        var client = await scenario.ConnectAsync("Admin");
        await client.SubscribeAsync(1000, lowData: true);

        scenario.Time.Advance(TimeSpan.FromSeconds(1));
        await Eventually.TrueAsync(() => client.Latest(DashboardCards.Cpu) is not null, "card de CPU");

        var cpu = client.Latest(DashboardCards.Cpu)!.Data;
        Assert.True(cpu.TryGetProperty("usagePercent", out _));
        Assert.False(cpu.TryGetProperty("coreUsagePercent", out _));
        Assert.False(client.Latest(DashboardCards.Network)!.Data.TryGetProperty("interfaces", out _));
    }

    [Fact]
    public async Task PowerCard_ActionsFollowPermissionsAndCapabilities()
    {
        await using var scenario = await Scenario.StartAsync();
        scenario.Power.Capabilities = new PowerCapabilities(SleepAvailable: false, HibernateAvailable: true);
        await scenario.RefreshStatusAsync();
        var admin = await scenario.ConnectAsync("Admin");
        var readOnly = await scenario.ConnectAsync("Somente leitura");

        var adminSnapshot = await admin.SubscribeAsync(1000);
        var readOnlySnapshot = await readOnly.SubscribeAsync(1000);

        Assert.Equal(
            [PowerCardActions.Lock, PowerCardActions.Restart, PowerCardActions.Hibernate, PowerCardActions.Shutdown],
            Card(adminSnapshot, DashboardCards.Power).AvailableActions);
        Assert.Empty(Card(readOnlySnapshot, DashboardCards.Power).AvailableActions);
        Assert.False(Card(adminSnapshot, DashboardCards.Power).Data.GetProperty("sleepAvailable").GetBoolean());
    }

    [Fact]
    public async Task PowerCountdown_ArrivesAsDeltaAndIsInTheSnapshotAfterReconnecting()
    {
        await using var scenario = await Scenario.StartAsync();
        await scenario.RefreshStatusAsync();
        var admin = await scenario.ConnectAsync("Admin");
        await admin.SubscribeAsync(1000);

        var scheduled = await admin.Hub.InvokeAsync<HubResponse<PowerCountdownDto>>(
            "PowerAction", new PowerActionRequest(PowerActionKind.Shutdown, 300, false, false, true), Ct);

        // O card de energia acabou de sair no snapshot: a mudança espera o intervalo mínimo de 250 ms.
        scenario.Time.Advance(DashboardAggregator.EventCardMinInterval);
        await Eventually.TrueAsync(
            () => admin.Latest(DashboardCards.Power)?.Data.TryGetProperty("pending", out _) == true,
            "contagem no card de energia");
        Assert.Contains(PowerCardActions.Cancel, admin.Latest(DashboardCards.Power)!.AvailableActions);

        // Reconectar (nova conexão): a contagem vem no snapshot.
        var reconnected = new DashboardClient(await scenario.Agent.ConnectHubAsync(admin.Device.Token), admin.Device);
        var snapshot = await reconnected.SubscribeAsync(1000);
        var pending = Card(snapshot, DashboardCards.Power).Data.GetProperty("pending");

        Assert.True(scheduled.Ok);
        Assert.Equal("shutdown", pending.GetProperty("action").GetString());
        Assert.Equal(scheduled.Data!.ExecutesAt, pending.GetProperty("executesAt").GetDateTimeOffset());
    }

    [Fact]
    public async Task BatteryCard_ExistsOnlyWithBattery()
    {
        await using var scenario = await Scenario.StartAsync();
        scenario.Status.Battery = new BatterySample(83, Charging: true, SecondsRemaining: null);
        await scenario.RefreshStatusAsync();
        var client = await scenario.ConnectAsync("Admin");

        var snapshot = await client.SubscribeAsync(1000);
        Assert.Equal(83, Card(snapshot, DashboardCards.Battery).Data.GetProperty("percent").GetInt32());

        scenario.Status.Battery = null;
        await scenario.RefreshStatusAsync();
        await Eventually.TrueAsync(() => client.Removed.Contains(DashboardCards.Battery), "card de bateria removido");
    }

    [Fact]
    public async Task StatusChange_UpdatesThePcCardAndSendsStatusUpdated()
    {
        await using var scenario = await Scenario.StartAsync();
        await scenario.RefreshStatusAsync();
        var client = await scenario.ConnectAsync("Admin");
        var updates = new ConcurrentBag<SystemStatus>();
        client.Hub.On<SystemStatus>("StatusUpdated", updates.Add);
        await client.SubscribeAsync(1000);

        scenario.Status.Addresses = [new InterfaceAddress("Wi-Fi", InterfaceKind.Wifi, "192.168.0.20")];
        scenario.Time.Advance(StatusPublisher.RefreshInterval);

        await Eventually.TrueAsync(() => !updates.IsEmpty, "StatusUpdated");
        Assert.Equal(NetworkKind.Wifi, Assert.Single(updates.First().Addresses).Kind);
        await Eventually.TrueAsync(
            () => client.Latest(DashboardCards.Pc)?.Data.GetProperty("addresses")[0].GetProperty("address").GetString() == "192.168.0.20",
            "card do PC atualizado");
    }

    [Fact]
    public async Task GetStatusAndMetricsTick_FollowTheContract()
    {
        await using var scenario = await Scenario.StartAsync();
        var client = await scenario.ConnectAsync("Admin");
        var ticks = new ConcurrentBag<MetricsSnapshot>();
        client.Hub.On<MetricsSnapshot>("MetricsTick", ticks.Add);

        var status = await client.Hub.InvokeAsync<HubResponse<SystemStatus>>("GetStatus", Ct);
        var subscribed = await client.Hub.InvokeAsync<HubResponse<bool>>("SubscribeMetrics", 1000, Ct);
        scenario.Time.Advance(TimeSpan.FromSeconds(1));
        await Eventually.TrueAsync(() => !ticks.IsEmpty, "MetricsTick");
        await client.Hub.InvokeAsync<HubResponse<bool>>("UnsubscribeMetrics", Ct);

        Assert.Equal("PC-TESTE", status.Data!.Hostname);
        Assert.True(subscribed.Ok);
        Assert.Equal(24, ticks.First().Cpu!.CoreUsagePercent!.Count);
        Assert.False(scenario.Agent.Services.GetRequiredService<MetricsSampler>().IsRunning);
    }

    [Fact]
    public async Task IntervalOutsideHalfToTenSeconds_IsRefused()
    {
        await using var scenario = await Scenario.StartAsync();
        var client = await scenario.ConnectAsync("Admin");

        var result = await client.Hub.InvokeAsync<HubResponse<DashboardSnapshot>>(
            "SubscribeDashboard", new DashboardOptions(100, false, false), Ct);

        Assert.Equal(ErrorCodes.ValidationFailed, result.Error!.Code);
    }

    private static CardState Card(DashboardSnapshot snapshot, string cardId) => snapshot.Cards.Single(c => c.CardId == cardId);

    private sealed class Scenario : IAsyncDisposable
    {
        private Scenario(TestAgent agent, FakeTimeProvider time, FakeMetrics metrics, FakeStatus status, FakePower power)
        {
            Agent = agent;
            Time = time;
            Metrics = metrics;
            Status = status;
            Power = power;
        }

        public TestAgent Agent { get; }

        public FakeTimeProvider Time { get; }

        public FakeMetrics Metrics { get; }

        public FakeStatus Status { get; }

        public FakePower Power { get; }

        public static async Task<Scenario> StartAsync()
        {
            var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
            var metrics = new FakeMetrics();
            var status = new FakeStatus(time);
            var power = new FakePower();
            var agent = await TestAgent.StartAsync(timeProvider: time, configureServices: services =>
            {
                services.AddSingleton<IMetricsSource>(metrics);
                services.AddSingleton<ISystemStatusSource>(status);
                services.AddSingleton<IPowerService>(power);
            });
            return new Scenario(agent, time, metrics, status, power);
        }

        /// <summary>Relê status e energia na hora (em produção, a cada 5 s com o painel aberto).</summary>
        public async Task RefreshStatusAsync()
        {
            await Agent.Services.GetRequiredService<StatusPublisher>().RefreshAsync(Ct);
            Agent.Services.GetRequiredService<PowerCardPublisher>().Publish();
        }

        public async Task<DashboardClient> ConnectAsync(string name)
        {
            var device = await Agent.PairAsync(name);
            return new DashboardClient(await Agent.ConnectHubAsync(device.Token), device);
        }

        public ValueTask DisposeAsync() => Agent.DisposeAsync();
    }

    /// <summary>Uma conexão inscrita no painel, guardando o último estado de cada card e o tamanho dos deltas.</summary>
    private sealed class DashboardClient
    {
        // Moldura do protocolo JSON do SignalR em volta dos argumentos de cada delta.
        private const int FrameOverhead = 64;

        private readonly ConcurrentDictionary<string, int> _patches = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, CardState> _latest = new(StringComparer.Ordinal);
        private long _bytes;

        public DashboardClient(HubConnection hub, CompletePairingResponse device)
        {
            Hub = hub;
            Device = device;
            hub.On<long, JsonElement>("DashboardDelta", (version, patches) =>
            {
                lock (Versions)
                {
                    Versions.Add(version);
                }

                Interlocked.Add(ref _bytes, patches.GetRawText().Length + FrameOverhead);
                foreach (var patch in patches.Deserialize<List<CardPatch>>(TestAgent.Json)!)
                {
                    _patches.AddOrUpdate(patch.CardId, 1, (_, count) => count + 1);
                    if (patch.Card is null)
                    {
                        Removed.Add(patch.CardId);
                    }
                    else
                    {
                        _latest[patch.CardId] = patch.Card;
                    }
                }
            });
        }

        public HubConnection Hub { get; }

        public CompletePairingResponse Device { get; }

        public List<long> Versions { get; } = [];

        public ConcurrentBag<string> Removed { get; } = [];

        public long Bytes => Interlocked.Read(ref _bytes);

        public int CardPatches(string cardId) => _patches.TryGetValue(cardId, out var count) ? count : 0;

        public CardState? Latest(string cardId) => _latest.TryGetValue(cardId, out var card) ? card : null;

        public void ResetCounts()
        {
            _patches.Clear();
            Interlocked.Exchange(ref _bytes, 0);
        }

        public async Task<DashboardSnapshot> SubscribeAsync(int intervalMs, bool lowData = false)
        {
            var result = await Hub.InvokeAsync<HubResponse<DashboardSnapshot>>(
                "SubscribeDashboard", new DashboardOptions(intervalMs, false, lowData), Ct);
            Assert.True(result.Ok, result.Error?.Message);
            foreach (var card in result.Data!.Cards)
            {
                _latest[card.CardId] = card;
            }

            return result.Data;
        }
    }

    /// <summary>Amostras realistas em tamanho (24 núcleos, 3 discos, 3 interfaces) e que mudam a cada coleta.</summary>
    private sealed class FakeMetrics : IMetricsSource
    {
        private readonly Random _random = new(7);
        private int _samples;

        public int Samples => Volatile.Read(ref _samples);

        public MetricsSample Sample()
        {
            Interlocked.Increment(ref _samples);
            lock (_random)
            {
                return new MetricsSample(
                    new CpuSample(Percent(), [.. Enumerable.Range(0, 24).Select(_ => Percent())], 3600 + _random.Next(1200)),
                    new MemorySample(11_000_000_000 + _random.Next(50_000_000), 34_200_000_000),
                    [
                        new DiskSample("C:", null, 439_000_000_000, 499_000_000_000, Rate(), Rate()),
                        new DiskSample("D:", "Dados", 1_200_000_000_000, 2_000_000_000_000, Rate(), Rate()),
                        new DiskSample("E:", "Jogos", 800_000_000_000, 1_000_000_000_000, Rate(), Rate()),
                    ],
                    [
                        new NetworkInterfaceSample("Ethernet", InterfaceKind.Ethernet, Rate(), Rate()),
                        new NetworkInterfaceSample("Wi-Fi", InterfaceKind.Wifi, Rate(), Rate()),
                        new NetworkInterfaceSample("Tailscale", InterfaceKind.Tailscale, Rate(), Rate()),
                    ],
                    new GpuSample("AMD Radeon RX 7600", Percent(), 750_000_000 + _random.Next(10_000_000)));
            }
        }

        private double Percent() => _random.NextDouble() * 100;

        private double Rate() => _random.NextDouble() * 100_000;
    }

    private sealed class FakeStatus(TimeProvider time) : ISystemStatusSource
    {
        private readonly DateTimeOffset _bootedAt = time.GetUtcNow().AddHours(-5);

        public BatterySample? Battery { get; set; }

        public IReadOnlyList<InterfaceAddress> Addresses { get; set; } = [new InterfaceAddress("Ethernet", InterfaceKind.Ethernet, "192.168.1.3")];

        public SystemStatusSample GetStatus() =>
            new("PC-TESTE", "Windows 11 Pro", "25H2 (build 26200.9711)", _bootedAt, @"PC-TESTE\usuario", Addresses, Battery, OnAcPower: true, "Equilibrado");
    }

    private sealed class FakePower : IPowerService
    {
        public PowerCapabilities Capabilities { get; set; } = new(SleepAvailable: true, HibernateAvailable: true);

        public PowerCapabilities GetCapabilities() => Capabilities;

        public bool IsUpdateRebootPending() => false;

        public Task ExecuteAsync(PowerOperation operation, bool force, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
