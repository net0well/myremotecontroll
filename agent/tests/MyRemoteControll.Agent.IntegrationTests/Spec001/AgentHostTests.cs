using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Time.Testing;
using MyRemoteControll.Agent.Hosting;
using MyRemoteControll.Agent.IntegrationTests.Support;
using MyRemoteControll.Contracts.Errors;

namespace MyRemoteControll.Agent.IntegrationTests.Spec001;

/// <summary>Spec 001 — host do agente (docs/specs/001-agent-host.md).</summary>
public sealed class AgentHostTests
{
    [Fact]
    public async Task AC3_MissingDatabase_IsCreatedByMigrations()
    {
        await using var agent = await TestAgent.StartAsync();

        Assert.True(File.Exists(agent.DatabasePath));
        await using var connection = new SqliteConnection($"Data Source={agent.DatabasePath};Pooling=False");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM __EFMigrationsHistory; SELECT name FROM sqlite_master WHERE name = 'AgentSettings';";
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        Assert.True(await reader.ReadAsync(TestContext.Current.CancellationToken));
        Assert.True(reader.GetInt64(0) > 0);
        Assert.True(await reader.NextResultAsync(TestContext.Current.CancellationToken));
        Assert.True(await reader.ReadAsync(TestContext.Current.CancellationToken), "A tabela AgentSettings não foi criada.");
    }

    [Fact]
    public async Task AC4_Health_Returns200OnLoopbackAnd404OnPublicListener()
    {
        await using var agent = await TestAgent.StartAsync();
        var ct = TestContext.Current.CancellationToken;

        using var loopback = await agent.CreateLoopbackClient().GetAsync("/health", ct);
        using var network = await agent.CreatePublicClient().GetAsync("/health", ct);

        Assert.Equal(HttpStatusCode.OK, loopback.StatusCode);
        Assert.Equal("Healthy", (await loopback.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.NotFound, network.StatusCode);
    }

    [Fact]
    public async Task HostHeaderSpoofing_DoesNotExposeLoopbackRoutesOnPublicListener()
    {
        await using var agent = await TestAgent.StartAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Host = $"127.0.0.1:{agent.Runtime.Endpoints!.LoopbackPort}";

        using var response = await agent.CreatePublicClient().SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AC5_PausedRemoteAccess_Returns503UntilResumed()
    {
        await using var agent = await TestAgent.StartAsync();
        var client = agent.CreatePublicClient();
        var ct = TestContext.Current.CancellationToken;

        agent.Runtime.Gate.Pause();
        using var paused = await client.GetAsync("/api/v1/info", ct);
        using var pausedHub = await client.PostAsync("/hubs/remote/negotiate?negotiateVersion=1", null, ct);
        agent.Runtime.Gate.Resume();
        using var resumed = await client.GetAsync("/api/v1/info", ct);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, paused.StatusCode);
        Assert.Equal(ErrorCodes.AgentPaused, (await paused.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.ServiceUnavailable, pausedHub.StatusCode);
        Assert.Equal(HttpStatusCode.OK, resumed.StatusCode);
        Assert.Equal(AgentStatus.Running, agent.Runtime.Status);
    }

    [Fact]
    public async Task AC6_Info_WithoutToken_ReturnsOnlyApiVersionAndRequiresPairing()
    {
        await using var agent = await TestAgent.StartAsync();

        var info = await agent.CreatePublicClient().GetFromJsonAsync<JsonElement>("/api/v1/info", TestContext.Current.CancellationToken);

        Assert.Equal(["apiVersion", "requiresPairing"], info.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
        Assert.Equal("1", info.GetProperty("apiVersion").GetString());
        Assert.True(info.GetProperty("requiresPairing").GetBoolean());
    }

    [Fact]
    public async Task EverythingElseOnPublicListener_RequiresAuthentication()
    {
        await using var agent = await TestAgent.StartAsync();

        using var response = await agent.CreatePublicClient()
            .PostAsync("/hubs/remote/negotiate?negotiateVersion=1", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AC7_CorruptDatabase_IsPreservedAndRecreated()
    {
        byte[] garbage = [.. Enumerable.Repeat((byte)0x5A, 4096)];
        await using var agent = await TestAgent.StartAsync(directory => File.WriteAllBytes(Path.Combine(directory, "data.db"), garbage));
        var ct = TestContext.Current.CancellationToken;

        var preserved = Assert.Single(Directory.GetFiles(agent.DataDirectory, "data.corrupt-*.db"));
        Assert.Equal(garbage, await File.ReadAllBytesAsync(preserved, ct));
        Assert.Contains(agent.Notices, n => n.Title == "Banco de dados recriado");
        using var health = await agent.CreateLoopbackClient().GetAsync("/health", ct);
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
    }

    [Fact]
    public async Task AC10_PublicListener_RejectsPlainHttpAndServesThePinnedCertificate()
    {
        await using var agent = await TestAgent.StartAsync();
        var ct = TestContext.Current.CancellationToken;

        using var secure = await agent.CreatePublicClient().GetAsync("/api/v1/info", ct);
        Assert.Equal(HttpStatusCode.OK, secure.StatusCode);

        // Recusa = Kestrel derruba a conexão (HttpRequestException) ou responde com erro; nunca 200.
        HttpStatusCode? plainStatus = null;
        var plainError = await Record.ExceptionAsync(async () =>
        {
            using var plain = await agent.CreatePlainHttpClientForPublicListener().GetAsync("/api/v1/info", ct);
            plainStatus = plain.StatusCode;
        });
        Assert.True(
            plainError is HttpRequestException || plainStatus is not HttpStatusCode.OK,
            $"O listener público aceitou HTTP sem TLS (status {plainStatus}).");
    }

    [Fact]
    public async Task AC11_PortInUse_FaultsWithPortAndRetriesAfter30Seconds()
    {
        var occupier = new TcpListener(IPAddress.IPv6Any, 0);
        occupier.Server.DualMode = true;
        occupier.Start();
        var busyPort = ((IPEndPoint)occupier.LocalEndpoint).Port;
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var agent = TestAgent.Create(publicPort: busyPort, timeProvider: time);

        var starting = agent.Runtime.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            await Eventually.TrueAsync(() => agent.Runtime.Status == AgentStatus.Faulted, "estado Faulted");
            Assert.Contains(busyPort.ToString(System.Globalization.CultureInfo.InvariantCulture), agent.Runtime.StatusMessage, StringComparison.Ordinal);
            Assert.Null(agent.Runtime.Endpoints);
        }
        finally
        {
            occupier.Dispose();
        }

        time.Advance(TimeSpan.FromSeconds(30));
        await starting.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);

        Assert.Equal(AgentStatus.Running, agent.Runtime.Status);
        Assert.Equal(busyPort, agent.Runtime.Endpoints!.PublicPort);
    }

    [Fact]
    public async Task AC12_PausedRemoteAccess_KeepsLoopbackWorking()
    {
        await using var agent = await TestAgent.StartAsync();
        agent.Runtime.Gate.Pause();

        using var response = await agent.CreateLoopbackClient().GetAsync("/health", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(AgentStatus.Paused, agent.Runtime.Status);
    }

    [Fact]
    public async Task InvalidConfiguration_FaultsWithClearMessage()
    {
        await using var agent = TestAgent.Create(publicPort: 70000);

        await agent.Runtime.StartAsync(TestContext.Current.CancellationToken);

        Assert.Equal(AgentStatus.Faulted, agent.Runtime.Status);
        Assert.Contains("Agent:PublicPort", agent.Runtime.StatusMessage, StringComparison.Ordinal);
    }
}
