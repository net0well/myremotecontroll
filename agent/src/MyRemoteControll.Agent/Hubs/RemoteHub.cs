using Microsoft.AspNetCore.SignalR;
using MyRemoteControll.Agent.Features.Status;
using MyRemoteControll.Agent.Infrastructure.Auth;
using MyRemoteControll.Agent.Infrastructure.Authorization;
using MyRemoteControll.Agent.Infrastructure.Dashboard;
using MyRemoteControll.Contracts.Hub;

namespace MyRemoteControll.Agent.Hubs;

/// <summary>
/// Hub único <c>/hubs/remote</c> (ADR-0005). Os métodos de cada área entram como arquivos <c>partial</c> nesta pasta,
/// finos, delegando aos serviços em <c>Features/</c>. Todo método público declara <see cref="RequirePermissionAttribute"/>
/// ou <see cref="AllowAnyDeviceAttribute"/> (verificado por teste de arquitetura).
/// </summary>
internal sealed partial class RemoteHub(
    DeviceConnections connections,
    DevicePermissionResolver permissions,
    DashboardAggregator dashboard,
    MetricsSampler sampler) : Hub<IRemoteClient>
{
    public const string Path = "/hubs/remote";

    public override async Task OnConnectedAsync()
    {
        if (Context.User.GetDeviceId() is { } deviceId)
        {
            connections.Add(deviceId, Context);
            await Groups.AddToGroupAsync(Context.ConnectionId, DeviceConnections.GroupFor(deviceId)).ConfigureAwait(false);

            if (await permissions.GetAsync(deviceId, Context.ConnectionAborted).ConfigureAwait(false) is { } access)
            {
                foreach (var permission in access.Permissions)
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, PermissionGroups.For(permission)).ConfigureAwait(false);
                }
            }
        }

        await base.OnConnectedAsync().ConfigureAwait(false);
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        connections.Remove(Context.ConnectionId);
        dashboard.Unsubscribe(Context.ConnectionId);
        sampler.UnsubscribeMetrics(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}
