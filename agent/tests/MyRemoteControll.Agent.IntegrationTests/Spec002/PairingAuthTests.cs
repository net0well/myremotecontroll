using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using MyRemoteControll.Agent.Domain.Audit;
using MyRemoteControll.Agent.Features.Devices;
using MyRemoteControll.Agent.Infrastructure.Audit;
using MyRemoteControll.Agent.Infrastructure.Persistence;
using MyRemoteControll.Agent.Infrastructure.Security;
using MyRemoteControll.Agent.IntegrationTests.Support;
using MyRemoteControll.Contracts.Devices;
using MyRemoteControll.Contracts.Errors;
using MyRemoteControll.Contracts.Info;
using MyRemoteControll.Contracts.Pairing;

namespace MyRemoteControll.Agent.IntegrationTests.Spec002;

/// <summary>Spec 002 — pareamento, TLS e autenticação (docs/specs/002-pairing-auth.md), lado do agente.</summary>
public sealed class PairingAuthTests
{
    private const string AuthenticatedProbe = "/hubs/remote/negotiate?negotiateVersion=1";

    [Fact]
    public async Task AC1_ValidQr_GivesTokenThatAuthenticatesRestAndHub()
    {
        await using var agent = await TestAgent.StartAsync();

        var offer = await agent.CreateOfferAsync();
        var paired = await agent.PairAsync();

        Assert.StartsWith("mrc://pair?v=1&", offer.Payload, StringComparison.Ordinal);
        Assert.Contains($"&c={offer.Code}&", offer.Payload, StringComparison.Ordinal);
        var info = await agent.CreatePublicClient(paired.Token)
            .GetFromJsonAsync<AgentInfoResponse>("/api/v1/info", TestAgent.Json, TestContext.Current.CancellationToken);
        Assert.False(info!.RequiresPairing);
        Assert.Equal(paired.DeviceId, info.DeviceId);
        var hub = await agent.ConnectHubAsync(paired.Token);
        Assert.Equal(HubConnectionState.Connected, hub.State);
    }

    [Fact]
    public async Task AC2_ReusedCode_Returns410AndIsAudited()
    {
        await using var agent = await TestAgent.StartAsync();
        var offer = await agent.CreateOfferAsync();
        using (var first = await agent.CompletePairingAsync(offer.Code))
        {
            Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        }

        using var reused = await agent.CompletePairingAsync(offer.Code, "Outro celular");

        Assert.Equal(HttpStatusCode.Gone, reused.StatusCode);
        Assert.Equal(ErrorCodes.PairingConsumed, await ErrorCodeAsync(reused));
        await Eventually.TrueAsync(
            () => AuditEntries(agent).Any(a => a.Action == "pairing.complete" && a.ErrorCode == ErrorCodes.PairingConsumed),
            "auditoria da reutilização do código");
    }

    [Fact]
    public async Task AC3_QrCarriesThePinOfTheServedCertificate()
    {
        await using var agent = await TestAgent.StartAsync();

        var offer = await agent.CreateOfferAsync();

        // O cliente de teste só aceita o certificado cujo pin é agent.Pin; se a conexão abriu, o pin servido é esse.
        using var response = await agent.CreatePublicClient().GetAsync("/api/v1/info", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains($"&f={agent.Pin}&", offer.Payload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AC4_RevokedDevice_Gets401AndIsDisconnectedWithinTwoSeconds()
    {
        await using var agent = await TestAgent.StartAsync();
        var paired = await agent.PairAsync();
        var hub = await agent.ConnectHubAsync(paired.Token);
        var revokedEvent = new TaskCompletionSource<Guid>(TaskCreationOptions.RunContinuationsAsynchronously);
        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        hub.On<Guid>("DeviceRevoked", id => revokedEvent.TrySetResult(id));
        hub.Closed += _ =>
        {
            closed.TrySetResult();
            return Task.CompletedTask;
        };

        var revoked = await agent.Services.GetRequiredService<DeviceRevocation>().RevokeAsync(
            paired.DeviceId,
            new AuditRecord("devices.revoke", AuditResult.Success) { Source = AuditSource.Tray },
            TestContext.Current.CancellationToken);

        Assert.True(revoked);
        await closed.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        Assert.Equal(paired.DeviceId, await revokedEvent.Task.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken));
        using var rest = await agent.CreatePublicClient(paired.Token).PostAsync(AuthenticatedProbe, null, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, rest.StatusCode);
        Assert.Equal(ErrorCodes.AuthRevoked, await ErrorCodeAsync(rest));

        // O app sonda o PC por /info com o token: revogado precisa aparecer como tal, não como "precisa parear".
        using var info = await agent.CreatePublicClient(paired.Token).GetAsync("/api/v1/info", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, info.StatusCode);
        Assert.Equal(ErrorCodes.AuthRevoked, await ErrorCodeAsync(info));
    }

    [Fact]
    public async Task AC5_FiveInvalidAttemptsInTenMinutes_LockPairingForFifteenMinutes()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var agent = await TestAgent.StartAsync(timeProvider: time);

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            using var invalid = await agent.CompletePairingAsync(SecureTokens.Create(SecureTokens.PairingCodeBytes));
            Assert.Equal(ErrorCodes.PairingInvalidCode, await ErrorCodeAsync(invalid));
        }

        var offer = await agent.CreateOfferAsync();
        using var sixth = await agent.CompletePairingAsync(offer.Code);
        Assert.Equal(HttpStatusCode.TooManyRequests, sixth.StatusCode);
        Assert.Equal(ErrorCodes.PairingLocked, await ErrorCodeAsync(sixth));
        Assert.Contains(agent.Notices, n => n.Title == "Pareamento bloqueado");

        time.Advance(TimeSpan.FromMinutes(15));
        var afterLock = await agent.CreateOfferAsync();
        using var allowed = await agent.CompletePairingAsync(afterLock.Code);
        Assert.Equal(HttpStatusCode.Created, allowed.StatusCode);
    }

    [Fact]
    public async Task AC6_FirstDeviceIsAdministrator_NextOnesAreReadOnly()
    {
        await using var agent = await TestAgent.StartAsync();

        var first = await agent.PairAsync("Celular principal");
        var second = await agent.PairAsync("Tablet");

        Assert.Equal(DeviceRole.Administrator, first.Role);
        Assert.Equal(DeviceRole.ReadOnly, second.Role);
    }

    [Fact]
    public async Task AC7_RotatedToken_PreviousOneStillWorksFor24HoursOnly()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var agent = await TestAgent.StartAsync(timeProvider: time);
        var paired = await agent.PairAsync();
        var ct = TestContext.Current.CancellationToken;

        using var rotation = await agent.CreatePublicClient(paired.Token).PostAsync("/api/v1/devices/me/rotate-token", null, ct);
        var rotated = await rotation.Content.ReadFromJsonAsync<RotateTokenResponse>(TestAgent.Json, ct);
        Assert.Equal(HttpStatusCode.OK, rotation.StatusCode);
        Assert.NotEqual(paired.Token, rotated!.Token);

        using (var withinGrace = await agent.CreatePublicClient(paired.Token).PostAsync(AuthenticatedProbe, null, ct))
        {
            Assert.Equal(HttpStatusCode.OK, withinGrace.StatusCode);
        }

        time.Advance(TimeSpan.FromHours(24) + TimeSpan.FromSeconds(1));
        using var oldToken = await agent.CreatePublicClient(paired.Token).PostAsync(AuthenticatedProbe, null, ct);
        using var newToken = await agent.CreatePublicClient(rotated.Token).PostAsync(AuthenticatedProbe, null, ct);
        Assert.Equal(HttpStatusCode.Unauthorized, oldToken.StatusCode);
        Assert.Equal(HttpStatusCode.OK, newToken.StatusCode);
    }

    [Fact]
    public async Task AC8_DatabaseHoldsOnlyKeyedHashes_NeverTheToken()
    {
        await using var agent = await TestAgent.StartAsync();
        var paired = await agent.PairAsync();

        var device = await Db(agent).Devices.AsNoTracking().SingleAsync(d => d.Id == paired.DeviceId, TestContext.Current.CancellationToken);
        Assert.False(device.TokenHash.AsSpan().SequenceEqual(SHA256.HashData(Encoding.UTF8.GetBytes(paired.Token))));

        var tokenBytes = Encoding.UTF8.GetBytes(paired.Token);
        foreach (var file in Directory.GetFiles(agent.DataDirectory, "data.db*"))
        {
            Assert.Equal(-1, ReadShared(file).AsSpan().IndexOf(tokenBytes));
        }

        Assert.True(File.Exists(Path.Combine(agent.DataDirectory, PepperStore.FileName)));
    }

    [Fact]
    public async Task AC9_PairingCompletesOnlyAfterConfirmationOnThePc()
    {
        await using var agent = await TestAgent.StartAsync();
        agent.Confirmation = ConfirmationPolicy.Ignore;
        var offer = await agent.CreateOfferAsync();

        var pairing = agent.CompletePairingAsync(offer.Code, "Celular aguardando");
        await Eventually.TrueAsync(() => agent.ConfirmationRequests.Count == 1, "pedido de confirmação na bandeja");

        Assert.Equal("Celular aguardando", agent.ConfirmationRequests[0].Info.DeviceName);
        Assert.Equal(0, await Db(agent).Devices.CountAsync(TestContext.Current.CancellationToken));
        Assert.False(pairing.IsCompleted);

        agent.ConfirmationRequests[0].Approve();
        using var response = await pairing.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task AC10_RejectedOnThePc_Returns403AndInvalidatesTheCode()
    {
        await using var agent = await TestAgent.StartAsync();
        agent.Confirmation = ConfirmationPolicy.Reject;
        var offer = await agent.CreateOfferAsync();

        using var rejected = await agent.CompletePairingAsync(offer.Code);
        agent.Confirmation = ConfirmationPolicy.Approve;
        using var retry = await agent.CompletePairingAsync(offer.Code);

        Assert.Equal(HttpStatusCode.Forbidden, rejected.StatusCode);
        Assert.Equal(ErrorCodes.PairingRejected, await ErrorCodeAsync(rejected));
        Assert.Equal(ErrorCodes.PairingConsumed, await ErrorCodeAsync(retry));
    }

    [Fact]
    public async Task AC10_UnansweredFor60Seconds_Returns408()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var agent = await TestAgent.StartAsync(timeProvider: time);
        agent.Confirmation = ConfirmationPolicy.Ignore;
        var offer = await agent.CreateOfferAsync();

        var pairing = agent.CompletePairingAsync(offer.Code);
        await Eventually.TrueAsync(() => agent.ConfirmationRequests.Count == 1, "pedido de confirmação na bandeja");
        time.Advance(TimeSpan.FromSeconds(60));
        using var response = await pairing.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.RequestTimeout, response.StatusCode);
        Assert.Equal(ErrorCodes.PairingConfirmationTimeout, await ErrorCodeAsync(response));
        Assert.True(agent.ConfirmationRequests[0].IsClosed);
    }

    [Fact]
    public async Task TokensNeverReachTheLogFiles()
    {
        await using var agent = await TestAgent.StartAsync();
        var offer = await agent.CreateOfferAsync();
        using (await agent.CompletePairingAsync(offer.Code))
        {
        }

        var paired = await agent.PairAsync();
        await agent.ConnectHubAsync(paired.Token);
        using (await agent.CreatePublicClient(paired.Token).PostAsync(AuthenticatedProbe, null, TestContext.Current.CancellationToken))
        {
        }

        var logs = string.Concat(Directory.GetFiles(Path.Combine(agent.DataDirectory, "logs")).Select(f => Encoding.UTF8.GetString(ReadShared(f))));
        Assert.NotEmpty(logs);
        Assert.DoesNotContain(paired.Token, logs, StringComparison.Ordinal);
        Assert.DoesNotContain(offer.Code, logs, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvalidRequest_ReturnsValidationProblem()
    {
        await using var agent = await TestAgent.StartAsync();
        var offer = await agent.CreateOfferAsync();

        using var response = await agent.CompletePairingAsync(offer.Code, deviceName: "");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorCodes.ValidationFailed, await ErrorCodeAsync(response));
    }

    private static async Task<string?> ErrorCodeAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        return body.TryGetProperty("code", out var code) ? code.GetString() : null;
    }

    private static AgentDbContext Db(TestAgent agent) =>
        agent.Services.GetRequiredService<IDbContextFactory<AgentDbContext>>().CreateDbContext();

    private static List<AuditEntry> AuditEntries(TestAgent agent)
    {
        using var db = Db(agent);
        return [.. db.AuditEntries.AsNoTracking()];
    }

    private static byte[] ReadShared(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            return memory.ToArray();
        }
        catch (FileNotFoundException)
        {
            // O SQLite remove os arquivos -wal/-shm ao fechar conexões; se sumiu, não há o que verificar.
            return [];
        }
    }
}
