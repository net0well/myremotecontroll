using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Mvc;
using MyRemoteControll.Agent.Features.Status;
using MyRemoteControll.Agent.Infrastructure.Auth;
using MyRemoteControll.Agent.Infrastructure.Dashboard;
using MyRemoteControll.Contracts.Dashboard;
using MyRemoteControll.Contracts.Errors;
using MyRemoteControll.Contracts.Hub;
using MyRemoteControll.Contracts.Security;
using MyRemoteControll.Contracts.Status;

namespace MyRemoteControll.Agent.Hubs;

/// <summary>Painel em tempo real e status do PC (spec 007, ADR-0021).</summary>
internal sealed partial class RemoteHub
{
    private const string IntervalMessage = "O intervalo deve ficar entre 0,5 e 10 segundos.";
    private const string NotSubscribedMessage = "Assine o painel antes de pedir um snapshot.";

    /// <summary>Qualquer aparelho pode assinar; os cards e as ações vêm filtrados pelas permissões dele.</summary>
    [AllowAnyDevice]
    public async Task<HubResponse<DashboardSnapshot>> SubscribeDashboard(DashboardOptions options)
    {
        if (!ValidInterval(options.MetricsIntervalMs) || Context.User.GetDeviceId() is not { } deviceId)
        {
            return HubResponse.Failure<DashboardSnapshot>(ErrorCodes.ValidationFailed, IntervalMessage);
        }

        var snapshot = await dashboard.SubscribeAsync(Context.ConnectionId, deviceId, options, Context.ConnectionAborted).ConfigureAwait(false);
        return HubResponse.Success(snapshot);
    }

    [AllowAnyDevice]
    public HubResponse<bool> UnsubscribeDashboard()
    {
        dashboard.Unsubscribe(Context.ConnectionId);
        return HubResponse.Success(true);
    }

    [AllowAnyDevice]
    public async Task<HubResponse<DashboardSnapshot>> RequestDashboardSnapshot() =>
        await dashboard.RequestSnapshotAsync(Context.ConnectionId, Context.ConnectionAborted).ConfigureAwait(false) is { } snapshot
            ? HubResponse.Success(snapshot)
            : HubResponse.Failure<DashboardSnapshot>(ErrorCodes.ValidationFailed, NotSubscribedMessage);

    [AllowAnyDevice]
    public HubResponse<bool> SetDashboardRate(int intervalMs)
    {
        if (!ValidInterval(intervalMs))
        {
            return HubResponse.Failure<bool>(ErrorCodes.ValidationFailed, IntervalMessage);
        }

        return dashboard.SetRate(Context.ConnectionId, intervalMs)
            ? HubResponse.Success(true)
            : HubResponse.Failure<bool>(ErrorCodes.ValidationFailed, NotSubscribedMessage);
    }

    [RequirePermission(PermissionKeys.StatusView)]
    [SuppressMessage("Performance", "CA1822", Justification = "O SignalR só expõe métodos de instância do Hub.")]
    public HubResponse<SystemStatus> GetStatus([FromServices] StatusPublisher status) => HubResponse.Success(status.GetStatus());

    [RequirePermission(PermissionKeys.MonitorView)]
    public HubResponse<bool> SubscribeMetrics(int intervalMs)
    {
        if (!ValidInterval(intervalMs))
        {
            return HubResponse.Failure<bool>(ErrorCodes.ValidationFailed, IntervalMessage);
        }

        sampler.SubscribeMetrics(Context.ConnectionId, intervalMs);
        return HubResponse.Success(true);
    }

    [RequirePermission(PermissionKeys.MonitorView)]
    public HubResponse<bool> UnsubscribeMetrics()
    {
        sampler.UnsubscribeMetrics(Context.ConnectionId);
        return HubResponse.Success(true);
    }

    private static bool ValidInterval(int intervalMs) =>
        intervalMs is >= DashboardAggregator.MinIntervalMs and <= DashboardAggregator.MaxIntervalMs;
}
