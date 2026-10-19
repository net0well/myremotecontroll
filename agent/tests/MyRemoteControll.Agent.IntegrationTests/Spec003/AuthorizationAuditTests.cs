using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;
using MyRemoteControll.Agent.Domain.Audit;
using MyRemoteControll.Agent.Features.Audit;
using MyRemoteControll.Agent.Infrastructure.Audit;
using MyRemoteControll.Agent.Infrastructure.Persistence;
using MyRemoteControll.Agent.IntegrationTests.Support;
using MyRemoteControll.Contracts.Audit;
using MyRemoteControll.Contracts.Devices;
using MyRemoteControll.Contracts.Errors;
using MyRemoteControll.Contracts.Hub;
using MyRemoteControll.Contracts.Security;

namespace MyRemoteControll.Agent.IntegrationTests.Spec003;

/// <summary>Spec 003 — papéis, permissões e auditoria (docs/specs/003-authorization-audit.md), lado do agente.</summary>
public sealed class AuthorizationAuditTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AC1_ReadOnlyDevice_IsForbiddenAndTheDenialIsAudited()
    {
        await using var agent = await TestAgent.StartAsync();
        var admin = await agent.PairAsync("Admin");
        var readOnly = await agent.PairAsync("Somente leitura");
        var hub = await agent.ConnectHubAsync(readOnly.Token);

        using var rest = await agent.CreatePublicClient(readOnly.Token).DeleteAsync($"/api/v1/devices/{admin.DeviceId}", Ct);
        var viaHub = await hub.InvokeAsync<HubResponse<DeviceDto>>(
            "UpdateDevicePermissions", readOnly.DeviceId, new UpdateDevicePermissionsRequest(DeviceRole.Administrator, []), Ct);

        Assert.Equal(HttpStatusCode.Forbidden, rest.StatusCode);
        Assert.Equal(ErrorCodes.AuthForbidden, await ErrorCodeAsync(rest));
        Assert.False(viaHub.Ok);
        Assert.Equal(ErrorCodes.AuthForbidden, viaHub.Error!.Code);
        await Eventually.TrueAsync(
            () => Audit(agent).Count(a => a.Action == AuditActions.AccessDenied && a.DeviceId == readOnly.DeviceId) == 2,
            "duas negações auditadas (REST e Hub)");
    }

    [Fact]
    public async Task AC2_SuccessfulChange_IsAuditedWithDeviceTargetResultAndCorrelationId()
    {
        await using var agent = await TestAgent.StartAsync();
        var admin = await agent.PairAsync("Admin");
        var tablet = await agent.PairAsync("Tablet");
        var hub = await agent.ConnectHubAsync(admin.Token);

        var result = await hub.InvokeAsync<HubResponse<DeviceDto>>(
            "UpdateDevicePermissions", tablet.DeviceId, new UpdateDevicePermissionsRequest(DeviceRole.Operator, [PermissionKeys.FilesDelete]), Ct);

        Assert.True(result.Ok);
        Assert.Contains(PermissionKeys.FilesDelete, result.Data!.EffectivePermissions);
        await Eventually.TrueAsync(() => Audit(agent).Any(a => a.Action == "devices.updatePermissions"), "auditoria da alteração");
        var entry = Audit(agent).Single(a => a.Action == "devices.updatePermissions");
        Assert.Equal(admin.DeviceId, entry.DeviceId);
        Assert.Equal(AuditResult.Success, entry.Result);
        Assert.Contains(tablet.DeviceId.ToString(), entry.TargetJson, StringComparison.Ordinal);
        Assert.False(string.IsNullOrEmpty(entry.CorrelationId));
    }

    [Fact]
    public async Task AC3_DemotingOrRevokingTheOnlyAdministrator_IsRefused()
    {
        await using var agent = await TestAgent.StartAsync();
        var admin = await agent.PairAsync("Admin");
        var hub = await agent.ConnectHubAsync(admin.Token);

        var demote = await hub.InvokeAsync<HubResponse<DeviceDto>>(
            "UpdateDevicePermissions", admin.DeviceId, new UpdateDevicePermissionsRequest(DeviceRole.ReadOnly, []), Ct);
        using var revoke = await agent.CreatePublicClient(admin.Token).DeleteAsync($"/api/v1/devices/{admin.DeviceId}", Ct);

        Assert.Equal(ErrorCodes.SecurityLastAdmin, demote.Error?.Code);
        Assert.Equal(HttpStatusCode.Conflict, revoke.StatusCode);
        Assert.Equal(ErrorCodes.SecurityLastAdmin, await ErrorCodeAsync(revoke));
    }

    [Fact]
    public async Task AC4_PermissionChange_AppliesOnTheNextCallOfTheConnectedDevice()
    {
        await using var agent = await TestAgent.StartAsync();
        var admin = await agent.PairAsync("Admin");
        var tablet = await agent.PairAsync("Tablet");
        var adminHub = await agent.ConnectHubAsync(admin.Token);
        var tabletHub = await agent.ConnectHubAsync(tablet.Token);
        var changed = new TaskCompletionSource<DevicePermissionsChangedDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        tabletHub.On<DevicePermissionsChangedDto>("DevicePermissionsChanged", change => changed.TrySetResult(change));

        var before = await tabletHub.InvokeAsync<HubResponse<IReadOnlyList<DeviceDto>>>("ListDevices", Ct);
        await adminHub.InvokeAsync<HubResponse<DeviceDto>>(
            "UpdateDevicePermissions", tablet.DeviceId, new UpdateDevicePermissionsRequest(DeviceRole.Administrator, []), Ct);
        var after = await tabletHub.InvokeAsync<HubResponse<IReadOnlyList<DeviceDto>>>("ListDevices", Ct);

        Assert.Equal(ErrorCodes.AuthForbidden, before.Error?.Code);
        Assert.True(after.Ok);
        Assert.Equal(2, after.Data!.Count(d => d.Connected));
        var notification = await changed.Task.WaitAsync(TimeSpan.FromSeconds(5), Ct);
        Assert.Equal(DeviceRole.Administrator, notification.Role);
        Assert.Contains(PermissionKeys.SecurityManageDevices, notification.EffectivePermissions);
    }

    [Fact]
    public async Task AC6_CsvExport_ContainsOnlyTheRequestedPeriod()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero));
        await using var agent = await TestAgent.StartAsync(timeProvider: time);
        var admin = await agent.PairAsync("Admin");
        await Eventually.TrueAsync(() => Audit(agent).Any(a => a.Action == "pairing.complete"), "auditoria do pareamento");

        time.Advance(TimeSpan.FromDays(2));
        var tablet = await agent.PairAsync("Tablet");
        await Eventually.TrueAsync(() => Audit(agent).Count(a => a.Action == "pairing.complete") == 2, "auditoria do segundo pareamento");

        var from = Uri.EscapeDataString(new DateTimeOffset(2026, 10, 2, 0, 0, 0, TimeSpan.Zero).ToString("O"));
        using var response = await agent.CreatePublicClient(admin.Token).GetAsync($"/api/v1/audit/export.csv?from={from}", Ct);
        var csv = Encoding.UTF8.GetString(await response.Content.ReadAsByteArrayAsync(Ct));
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
        Assert.StartsWith("﻿Id,DataHoraUtc", csv, StringComparison.Ordinal);
        Assert.Equal(2, lines.Length);
        Assert.Contains(tablet.DeviceId.ToString(), lines[1], StringComparison.Ordinal);
        Assert.Contains("2026-10-03", lines[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task AC7_QueuedAuditEntries_AreNotLostOnShutdown()
    {
        var agent = await TestAgent.StartAsync();
        await using (agent)
        {
            var auditLog = agent.Services.GetRequiredService<IAuditLog>();
            for (var i = 0; i < 500; i++)
            {
                await auditLog.WriteAsync(new AuditRecord("test.burst", AuditResult.Success) { Source = AuditSource.System }, Ct);
            }

            await agent.Runtime.StopAsync(Ct);

            await using var connection = new SqliteConnection($"Data Source={agent.DatabasePath};Pooling=False");
            await connection.OpenAsync(Ct);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM AuditEntries WHERE Action = 'test.burst'";
            Assert.Equal(500L, (long)(await command.ExecuteScalarAsync(Ct))!);
        }
    }

    [Fact]
    public async Task AuditQuery_PagesWithCursorFromNewestToOldest()
    {
        await using var agent = await TestAgent.StartAsync();
        var admin = await agent.PairAsync("Admin");
        await agent.PairAsync("Tablet");
        await agent.PairAsync("Notebook");
        await Eventually.TrueAsync(() => Audit(agent).Count(a => a.Action == "pairing.complete") == 3, "três pareamentos auditados");
        var client = agent.CreatePublicClient(admin.Token);

        var first = await client.GetFromJsonAsync<AuditPage>("/api/v1/audit?action=pairing.complete&limit=2", TestAgent.Json, Ct);
        var second = await client.GetFromJsonAsync<AuditPage>($"/api/v1/audit?action=pairing.complete&limit=2&cursor={first!.NextCursor}", TestAgent.Json, Ct);

        Assert.Equal(2, first.Items.Count);
        Assert.True(first.Items[0].Id > first.Items[1].Id);
        Assert.Single(second!.Items);
        Assert.Null(second.NextCursor);
        Assert.Equal("Admin", second.Items[0].DeviceName);
    }

    [Fact]
    public async Task AuditViewers_ReceiveAuditAppendedLive()
    {
        await using var agent = await TestAgent.StartAsync();
        var admin = await agent.PairAsync("Admin");
        var hub = await agent.ConnectHubAsync(admin.Token);
        var appended = new TaskCompletionSource<AuditEntryDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        hub.On<AuditEntryDto>("AuditAppended", entry =>
        {
            if (entry.Action == "pairing.complete")
            {
                appended.TrySetResult(entry);
            }
        });

        await agent.PairAsync("Tablet");

        var entry = await appended.Task.WaitAsync(TimeSpan.FromSeconds(5), Ct);
        Assert.Equal("Success", entry.Result);
    }

    [Fact]
    public async Task Retention_RemovesEntriesOlderThan90Days()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var agent = await TestAgent.StartAsync(timeProvider: time);
        await agent.Services.GetRequiredService<IAuditLog>().WriteAsync(new AuditRecord("test.old", AuditResult.Success), Ct);
        await Eventually.TrueAsync(() => Audit(agent).Any(a => a.Action == "test.old"), "registro gravado");

        // Avançar o relógio também dispara a limpeza diária em segundo plano; qualquer uma das duas pode remover.
        time.Advance(TimeSpan.FromDays(91));
        await agent.Services.GetServices<IHostedService>().OfType<AuditRetention>().Single().PurgeAsync(Ct);

        await Eventually.TrueAsync(() => !Audit(agent).Any(a => a.Action == "test.old"), "registro antigo removido");
    }

    private static List<AuditEntry> Audit(TestAgent agent)
    {
        using var db = agent.Services.GetRequiredService<IDbContextFactory<AgentDbContext>>().CreateDbContext();
        return [.. db.AuditEntries.AsNoTracking()];
    }

    private static async Task<string?> ErrorCodeAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        return body.TryGetProperty("code", out var code) ? code.GetString() : null;
    }
}
