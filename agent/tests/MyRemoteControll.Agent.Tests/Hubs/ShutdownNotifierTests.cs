using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using MyRemoteControll.Agent.Hubs;
using MyRemoteControll.Contracts.Hub;

namespace MyRemoteControll.Agent.Tests.Hubs;

public sealed class ShutdownNotifierTests
{
    [Fact]
    public async Task AC8_Stopping_BroadcastsAgentShuttingDown()
    {
        var client = new RecordingClient();
        var notifier = new ShutdownNotifier(new FakeHubContext(client), NullLogger<ShutdownNotifier>.Instance);

        await notifier.StoppingAsync(TestContext.Current.CancellationToken);

        Assert.Equal([ShutdownNotifier.Reason], client.Reasons);
    }

    [Fact]
    public async Task Stopping_WhenClientsDoNotRespond_GivesUpWithoutThrowing()
    {
        var client = new RecordingClient { Delay = Timeout.InfiniteTimeSpan };
        var notifier = new ShutdownNotifier(new FakeHubContext(client), NullLogger<ShutdownNotifier>.Instance);

        await notifier.StoppingAsync(TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
    }

    private sealed class RecordingClient : IRemoteClient
    {
        public List<string> Reasons { get; } = [];

        public TimeSpan Delay { get; init; } = TimeSpan.Zero;

        public async Task AgentShuttingDown(string reason)
        {
            Reasons.Add(reason);
            await Task.Delay(Delay);
        }

        public Task DeviceRevoked(Guid deviceId) => Task.CompletedTask;

        public Task DevicePermissionsChanged(MyRemoteControll.Contracts.Devices.DevicePermissionsChangedDto change) => Task.CompletedTask;

        public Task AuditAppended(MyRemoteControll.Contracts.Audit.AuditEntryDto entry) => Task.CompletedTask;

        public Task PowerCountdown(MyRemoteControll.Contracts.Power.PowerCountdownDto countdown) => Task.CompletedTask;

        public Task PowerCancelled(MyRemoteControll.Contracts.Power.PowerCancelledDto cancelled) => Task.CompletedTask;

        public Task DashboardDelta(long version, IReadOnlyList<MyRemoteControll.Contracts.Dashboard.CardPatch> patches) => Task.CompletedTask;

        public Task StatusUpdated(MyRemoteControll.Contracts.Status.SystemStatus status) => Task.CompletedTask;

        public Task MetricsTick(MyRemoteControll.Contracts.Status.MetricsSnapshot metrics) => Task.CompletedTask;
    }

    private sealed class FakeHubContext(IRemoteClient all) : IHubContext<RemoteHub, IRemoteClient>
    {
        public IHubClients<IRemoteClient> Clients { get; } = new FakeHubClients(all);

        public IGroupManager Groups => throw new NotSupportedException();
    }

    private sealed class FakeHubClients(IRemoteClient all) : IHubClients<IRemoteClient>
    {
        public IRemoteClient All => all;

        public IRemoteClient AllExcept(IReadOnlyList<string> excludedConnectionIds) => throw new NotSupportedException();

        public IRemoteClient Client(string connectionId) => throw new NotSupportedException();

        public IRemoteClient Clients(IReadOnlyList<string> connectionIds) => throw new NotSupportedException();

        public IRemoteClient Group(string groupName) => throw new NotSupportedException();

        public IRemoteClient GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => throw new NotSupportedException();

        public IRemoteClient Groups(IReadOnlyList<string> groupNames) => throw new NotSupportedException();

        public IRemoteClient User(string userId) => throw new NotSupportedException();

        public IRemoteClient Users(IReadOnlyList<string> userIds) => throw new NotSupportedException();
    }
}
