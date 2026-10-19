using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using MyRemoteControll.Agent.Domain.Audit;
using MyRemoteControll.Agent.Features.Power;
using MyRemoteControll.Agent.Infrastructure.Audit;
using MyRemoteControll.Agent.Infrastructure.Persistence;
using MyRemoteControll.Agent.IntegrationTests.Support;
using MyRemoteControll.Contracts.Devices;
using MyRemoteControll.Contracts.Errors;
using MyRemoteControll.Contracts.Hub;
using MyRemoteControll.Contracts.Pairing;
using MyRemoteControll.Contracts.Power;
using MyRemoteControll.Contracts.Security;
using MyRemoteControll.Windows.Power;

namespace MyRemoteControll.Agent.IntegrationTests.Spec005;

/// <summary>Spec 005 — ações de energia (docs/specs/005-power-actions.md), lado do agente, com o Windows falso.</summary>
public sealed class PowerActionsTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AC1_ShutdownIn60s_BroadcastsTheCountdownAndShutsDownAt60s()
    {
        await using var scenario = await Scenario.StartAsync();
        var (admin, adminHub) = await scenario.ConnectAsync("Admin");
        var (_, readOnlyHub) = await scenario.ConnectAsync("Tablet");
        var received = new ConcurrentBag<PowerCountdownDto>();
        adminHub.On<PowerCountdownDto>("PowerCountdown", received.Add);
        readOnlyHub.On<PowerCountdownDto>("PowerCountdown", received.Add);
        var requestedAt = scenario.Time.GetUtcNow();

        var result = await RequestAsync(adminHub, PowerActionKind.Shutdown, delaySeconds: 60, localConfirmation: true);

        Assert.True(result.Ok);
        Assert.Equal(requestedAt.AddSeconds(60), result.Data!.ExecutesAt);
        Assert.Equal(admin.DeviceId, result.Data.RequestedByDeviceId);
        Assert.Equal("Admin", result.Data.RequestedByDeviceName);
        await Eventually.TrueAsync(() => received.Count == 2, "PowerCountdown nos dois aparelhos");

        scenario.Time.Advance(TimeSpan.FromSeconds(59));
        await Task.Delay(100, Ct);
        Assert.Empty(scenario.Power.Executed);

        scenario.Time.Advance(TimeSpan.FromSeconds(1));
        await Eventually.TrueAsync(() => !scenario.Power.Executed.IsEmpty, "desligamento no prazo");
        Assert.Equal((PowerOperation.Shutdown, false), Assert.Single(scenario.Power.Executed));

        await Eventually.TrueAsync(() => scenario.Audit().Any(a => a.Action == PowerService.ExecuteAction), "execução auditada");
        var request = scenario.Audit().Single(a => a.Action == "power.shutdown");
        Assert.Equal(AuditResult.Success, request.Result);
        Assert.Contains("\"action\":\"shutdown\"", scenario.Audit().Single(a => a.Action == PowerService.ExecuteAction).TargetJson, StringComparison.Ordinal);
        Assert.True(request.LocalConfirmation);
        Assert.Equal(admin.DeviceId, request.DeviceId);
    }

    [Fact]
    public async Task AC2_Cancel_AbortsTheShutdownAndEveryoneReceivesPowerCancelled()
    {
        await using var scenario = await Scenario.StartAsync();
        var (admin, adminHub) = await scenario.ConnectAsync("Admin");
        var (_, readOnlyHub) = await scenario.ConnectAsync("Tablet");
        var cancelled = new ConcurrentBag<PowerCancelledDto>();
        adminHub.On<PowerCancelledDto>("PowerCancelled", cancelled.Add);
        readOnlyHub.On<PowerCancelledDto>("PowerCancelled", cancelled.Add);
        await RequestAsync(adminHub, PowerActionKind.Shutdown, delaySeconds: 60);

        var result = await adminHub.InvokeAsync<HubResponse<PowerCancelledDto>>("CancelPowerAction", Ct);
        scenario.Time.Advance(TimeSpan.FromSeconds(120));

        Assert.True(result.Ok);
        await Eventually.TrueAsync(() => cancelled.Count == 2, "PowerCancelled nos dois aparelhos");
        Assert.All(cancelled, c => Assert.Equal(new PowerCancelledDto(PowerActionKind.Shutdown, admin.DeviceId, PowerCancelSource.Device), c));
        await Task.Delay(100, Ct);
        Assert.Empty(scenario.Power.Executed);

        var again = await adminHub.InvokeAsync<HubResponse<PowerCancelledDto>>("CancelPowerAction", Ct);
        Assert.Equal(ErrorCodes.PowerNothingToCancel, again.Error!.Code);
        await Eventually.TrueAsync(
            () => scenario.Audit().Count(a => a.Action == PowerService.CancelAction) == 2,
            "cancelamento e tentativa sem pendente auditados");
    }

    [Fact]
    public async Task AC3_ActiveWorkWithoutAcknowledge_ReturnsPowerActiveWorkWithTheList()
    {
        var work = new FakeActiveWork([new ActiveWorkItemDto("claudeSession", "Projeto api: Claude trabalhando")]);
        await using var scenario = await Scenario.StartAsync(work);
        var (_, hub) = await scenario.ConnectAsync("Admin");

        var refused = await RequestAsync(hub, PowerActionKind.Shutdown, delaySeconds: 30);
        var acknowledged = await RequestAsync(hub, PowerActionKind.Shutdown, delaySeconds: 30, acknowledgeActiveWork: true);

        Assert.False(refused.Ok);
        Assert.Equal(ErrorCodes.PowerActiveWork, refused.Error!.Code);
        var details = Assert.IsType<JsonElement>(refused.Error.Details);
        Assert.Equal("Projeto api: Claude trabalhando", details[0].GetProperty("description").GetString());
        Assert.True(acknowledged.Ok);
    }

    [Fact]
    public async Task AC3_LockDoesNotInterruptWork_SoItNeedsNoAcknowledge()
    {
        var work = new FakeActiveWork([new ActiveWorkItemDto("build", "Build do api")]);
        await using var scenario = await Scenario.StartAsync(work);
        var (_, hub) = await scenario.ConnectAsync("Admin");

        var result = await RequestAsync(hub, PowerActionKind.Lock, delaySeconds: 0);

        Assert.True(result.Ok);
    }

    [Fact]
    public async Task AC4_HibernateOrSleepUnavailable_ReturnsTheSpecificError()
    {
        await using var scenario = await Scenario.StartAsync();
        scenario.Power.Capabilities = new PowerCapabilities(SleepAvailable: false, HibernateAvailable: false);
        var (_, hub) = await scenario.ConnectAsync("Admin");

        var hibernate = await RequestAsync(hub, PowerActionKind.Hibernate, delaySeconds: 0);
        var sleep = await RequestAsync(hub, PowerActionKind.Sleep, delaySeconds: 0);

        Assert.Equal(ErrorCodes.PowerHibernateUnavailable, hibernate.Error!.Code);
        Assert.Equal(ErrorCodes.PowerSleepUnavailable, sleep.Error!.Code);
        await Eventually.TrueAsync(
            () => scenario.Audit().Count(a => a.ErrorCode is ErrorCodes.PowerHibernateUnavailable or ErrorCodes.PowerSleepUnavailable) == 2,
            "recusas auditadas");
    }

    [Fact]
    public async Task AC6_ReadOnlyDevice_LockIsForbiddenAndAudited()
    {
        await using var scenario = await Scenario.StartAsync();
        await scenario.ConnectAsync("Admin");
        var (readOnly, hub) = await scenario.ConnectAsync("Somente leitura");

        var result = await RequestAsync(hub, PowerActionKind.Lock, delaySeconds: 0);

        Assert.Equal(ErrorCodes.AuthForbidden, result.Error!.Code);
        await Eventually.TrueAsync(
            () => scenario.Audit().Any(a => a.Action == AuditActions.AccessDenied && a.DeviceId == readOnly.DeviceId && a.TargetJson!.Contains("PowerAction", StringComparison.Ordinal)),
            "negação auditada");
        Assert.Null(scenario.Agent.Services.GetRequiredService<PowerService>().Current);
    }

    [Fact]
    public async Task DeviceWithOnlyPowerBasic_CanLockButCannotShutDown()
    {
        await using var scenario = await Scenario.StartAsync();
        var (_, adminHub) = await scenario.ConnectAsync("Admin");
        var (limited, hub) = await scenario.ConnectAsync("Limitado");
        var granted = await adminHub.InvokeAsync<HubResponse<DeviceDto>>(
            "UpdateDevicePermissions", limited.DeviceId, new UpdateDevicePermissionsRequest(DeviceRole.ReadOnly, [PermissionKeys.PowerBasic]), Ct);
        Assert.True(granted.Ok);

        var shutdown = await RequestAsync(hub, PowerActionKind.Shutdown, delaySeconds: 60);
        var lockNow = await RequestAsync(hub, PowerActionKind.Lock, delaySeconds: 60);

        Assert.Equal(ErrorCodes.AuthForbidden, shutdown.Error!.Code);
        Assert.True(lockNow.Ok);
        await Eventually.TrueAsync(
            () => scenario.Audit().Any(a => a.Action == AuditActions.AccessDenied && a.TargetJson!.Contains(PermissionKeys.PowerShutdown, StringComparison.Ordinal)),
            "negação com a permissão exigida");
    }

    [Fact]
    public async Task AC7_CancelFromThePc_DevicesReceivePowerCancelled()
    {
        await using var scenario = await Scenario.StartAsync();
        var (_, hub) = await scenario.ConnectAsync("Admin");
        var cancelled = new ConcurrentBag<PowerCancelledDto>();
        hub.On<PowerCancelledDto>("PowerCancelled", cancelled.Add);
        await RequestAsync(hub, PowerActionKind.Restart, delaySeconds: 300);
        var notice = Assert.Single(scenario.Agent.PowerCountdowns);
        Assert.Equal("Admin", notice.Countdown.RequestedByDeviceName);

        Assert.True(await notice.CancelAsync());

        await Eventually.TrueAsync(() => !cancelled.IsEmpty, "PowerCancelled no aparelho");
        Assert.Equal(new PowerCancelledDto(PowerActionKind.Restart, null, PowerCancelSource.Tray), Assert.Single(cancelled));
        Assert.True(notice.IsEnded);
        Assert.False(await notice.CancelAsync());
        await Eventually.TrueAsync(
            () => scenario.Audit().Any(a => a.Action == PowerService.CancelAction && a.Source == AuditSource.Tray && a.Result == AuditResult.Success),
            "cancelamento pela bandeja auditado");
    }

    [Fact]
    public async Task AC8_SecondRequest_IsRefusedAndReplaceCancelsThenSchedules()
    {
        await using var scenario = await Scenario.StartAsync();
        var (_, hub) = await scenario.ConnectAsync("Admin");
        await RequestAsync(hub, PowerActionKind.Lock, delaySeconds: 300);

        var second = await RequestAsync(hub, PowerActionKind.Shutdown, delaySeconds: 60);
        Assert.Equal(ErrorCodes.PowerAlreadyScheduled, second.Error!.Code);

        // "Substituir" no app: cancela a pendente e envia a nova.
        Assert.True((await hub.InvokeAsync<HubResponse<PowerCancelledDto>>("CancelPowerAction", Ct)).Ok);
        var replaced = await RequestAsync(hub, PowerActionKind.Shutdown, delaySeconds: 60);

        Assert.True(replaced.Ok);
        Assert.Equal(PowerActionKind.Shutdown, scenario.Agent.Services.GetRequiredService<PowerService>().Current!.Action);
    }

    [Fact]
    public async Task ImmediateAction_RunsAfterOneSecondSoTheAnswerArrivesFirst()
    {
        await using var scenario = await Scenario.StartAsync();
        var (_, hub) = await scenario.ConnectAsync("Admin");

        var result = await RequestAsync(hub, PowerActionKind.Lock, delaySeconds: 0);
        scenario.Time.Advance(TimeSpan.FromMilliseconds(999));
        await Task.Delay(100, Ct);
        Assert.Empty(scenario.Power.Executed);
        scenario.Time.Advance(TimeSpan.FromMilliseconds(1));

        Assert.True(result.Ok);
        await Eventually.TrueAsync(() => !scenario.Power.Executed.IsEmpty, "bloqueio em 1 s");
        Assert.Equal((PowerOperation.Lock, false), Assert.Single(scenario.Power.Executed));
    }

    [Fact]
    public async Task ForceAndUpdatePending_AreCarriedToTheCountdownAndTheExecution()
    {
        await using var scenario = await Scenario.StartAsync();
        scenario.Power.UpdatePending = true;
        var (_, hub) = await scenario.ConnectAsync("Admin");

        var result = await RequestAsync(hub, PowerActionKind.Restart, delaySeconds: 5, force: true);
        scenario.Time.Advance(TimeSpan.FromSeconds(5));

        Assert.True(result.Data!.Force);
        Assert.True(result.Data.UpdatePending);
        await Eventually.TrueAsync(() => !scenario.Power.Executed.IsEmpty, "reinício forçado");
        Assert.Equal((PowerOperation.Restart, true), Assert.Single(scenario.Power.Executed));
    }

    [Fact]
    public async Task WindowsRefusal_IsAuditedAndDevicesReceivePowerCancelledFailed()
    {
        await using var scenario = await Scenario.StartAsync();
        scenario.Power.Failure = new PowerException("O Windows não suspendeu.");
        var (_, hub) = await scenario.ConnectAsync("Admin");
        var cancelled = new ConcurrentBag<PowerCancelledDto>();
        hub.On<PowerCancelledDto>("PowerCancelled", cancelled.Add);

        await RequestAsync(hub, PowerActionKind.Sleep, delaySeconds: 10);
        scenario.Time.Advance(TimeSpan.FromSeconds(10));

        await Eventually.TrueAsync(() => !cancelled.IsEmpty, "aviso de falha");
        Assert.Equal(PowerCancelSource.Failed, Assert.Single(cancelled).Source);
        await Eventually.TrueAsync(
            () => scenario.Audit().Any(a => a.Action == PowerService.ExecuteAction && a.Result == AuditResult.Failed),
            "falha auditada");
    }

    [Fact]
    public async Task AgentShutdownDuringCountdown_DiscardsTheActionAndAuditsIt()
    {
        var scenario = await Scenario.StartAsync();
        await using (scenario)
        {
            var (_, hub) = await scenario.ConnectAsync("Admin");
            await RequestAsync(hub, PowerActionKind.Hibernate, delaySeconds: 600);
            var notice = Assert.Single(scenario.Agent.PowerCountdowns);

            await scenario.Agent.Runtime.StopAsync(Ct);

            Assert.True(notice.IsEnded);
            Assert.Empty(scenario.Power.Executed);
            await using var connection = new SqliteConnection($"Data Source={scenario.Agent.DatabasePath};Pooling=False");
            await connection.OpenAsync(Ct);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM AuditEntries WHERE Action = 'power.discard'";
            Assert.Equal(1L, (long)(await command.ExecuteScalarAsync(Ct))!);
        }
    }

    private static Task<HubResponse<PowerCountdownDto>> RequestAsync(
        HubConnection hub,
        PowerActionKind action,
        int delaySeconds,
        bool force = false,
        bool acknowledgeActiveWork = false,
        bool localConfirmation = false) =>
        hub.InvokeAsync<HubResponse<PowerCountdownDto>>(
            "PowerAction",
            new PowerActionRequest(action, delaySeconds, force, acknowledgeActiveWork, localConfirmation),
            Ct);

    /// <summary>Agente com relógio falso e Windows falso; o primeiro aparelho pareado é administrador.</summary>
    private sealed class Scenario : IAsyncDisposable
    {
        private Scenario(TestAgent agent, FakePower power, FakeTimeProvider time)
        {
            Agent = agent;
            Power = power;
            Time = time;
        }

        public TestAgent Agent { get; }

        public FakePower Power { get; }

        public FakeTimeProvider Time { get; }

        public static async Task<Scenario> StartAsync(FakeActiveWork? work = null)
        {
            var power = new FakePower();
            var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
            var agent = await TestAgent.StartAsync(timeProvider: time, configureServices: services =>
            {
                services.AddSingleton<IPowerService>(power);
                if (work is not null)
                {
                    services.AddSingleton<IActiveWorkSource>(work);
                }
            });
            return new Scenario(agent, power, time);
        }

        public async Task<(CompletePairingResponse Device, HubConnection Hub)> ConnectAsync(string name)
        {
            var device = await Agent.PairAsync(name);
            return (device, await Agent.ConnectHubAsync(device.Token));
        }

        public List<AuditEntry> Audit()
        {
            using var db = Agent.Services.GetRequiredService<IDbContextFactory<AgentDbContext>>().CreateDbContext();
            return [.. db.AuditEntries.AsNoTracking()];
        }

        public ValueTask DisposeAsync() => Agent.DisposeAsync();
    }

    private sealed class FakePower : IPowerService
    {
        public PowerCapabilities Capabilities { get; set; } = new(SleepAvailable: true, HibernateAvailable: true);

        public bool UpdatePending { get; set; }

        public Exception? Failure { get; set; }

        public ConcurrentQueue<(PowerOperation Operation, bool Force)> Executed { get; } = new();

        public PowerCapabilities GetCapabilities() => Capabilities;

        public bool IsUpdateRebootPending() => UpdatePending;

        public Task ExecuteAsync(PowerOperation operation, bool force, CancellationToken cancellationToken)
        {
            if (Failure is not null)
            {
                return Task.FromException(Failure);
            }

            Executed.Enqueue((operation, force));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeActiveWork(IReadOnlyList<ActiveWorkItemDto> items) : IActiveWorkSource
    {
        public ValueTask<IReadOnlyList<ActiveWorkItemDto>> GetActiveWorkAsync(CancellationToken cancellationToken) => new(items);
    }
}
