using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using MyRemoteControll.Agent.Features.Pairing;
using MyRemoteControll.Agent.Features.Power;
using MyRemoteControll.Agent.Hosting;
using MyRemoteControll.Agent.Infrastructure.Tls;
using MyRemoteControll.Contracts.Devices;
using MyRemoteControll.Contracts.Pairing;

namespace MyRemoteControll.Agent.IntegrationTests.Support;

internal enum ConfirmationPolicy
{
    Approve,
    Reject,

    /// <summary>Ninguém responde: o pedido só termina por tempo esgotado.</summary>
    Ignore,
}

/// <summary>
/// Agente real (Kestrel com os dois listeners) em portas efêmeras, com diretório de dados temporário e certificado
/// efêmero, para exercitar os critérios de aceite de ponta a ponta.
/// </summary>
internal sealed class TestAgent : IAsyncDisposable
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly X509Certificate2 _certificate;
    private readonly List<HttpClient> _clients = [];
    private readonly List<HubConnection> _hubConnections = [];

    private TestAgent(
        string dataDirectory,
        AgentRuntime runtime,
        X509Certificate2 certificate,
        PairingConfirmationBroker broker,
        PowerCountdownBroker powerCountdowns)
    {
        powerCountdowns.Started += (_, notice) =>
        {
            lock (PowerCountdowns)
            {
                PowerCountdowns.Add(notice);
            }
        };
        DataDirectory = dataDirectory;
        Runtime = runtime;
        _certificate = certificate;
        broker.Requested += (_, request) =>
        {
            ConfirmationRequests.Add(request);
            switch (Confirmation)
            {
                case ConfirmationPolicy.Approve:
                    request.Approve();
                    break;
                case ConfirmationPolicy.Reject:
                    request.Reject();
                    break;
            }
        };
    }

    public string DataDirectory { get; }

    public AgentRuntime Runtime { get; }

    public ConfirmationPolicy Confirmation { get; set; } = ConfirmationPolicy.Approve;

    public List<PairingConfirmationRequest> ConfirmationRequests { get; } = [];

    public List<AgentNotice> Notices { get; } = [];

    /// <summary>Contagens de energia avisadas ao PC (o que a bandeja mostraria).</summary>
    public List<PowerCountdownNotice> PowerCountdowns { get; } = [];

    public string DatabasePath => Path.Combine(DataDirectory, "data.db");

    public string Pin => CertificatePin.Compute(_certificate);

    public IServiceProvider Services => Runtime.Services ?? throw new InvalidOperationException("O agente não está rodando.");

    public static TestAgent Create(
        Action<string>? prepareDataDirectory = null,
        int publicPort = 0,
        TimeProvider? timeProvider = null,
        Action<IServiceCollection>? configureServices = null)
    {
        var dataDirectory = Path.Combine(Path.GetTempPath(), "myremotecontroll-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dataDirectory);
        prepareDataDirectory?.Invoke(dataDirectory);

        using var temporary = SelfSignedCertificateFactory.Create("localhost", DateTimeOffset.UtcNow);
        var certificate = X509CertificateLoader.LoadPkcs12(temporary.Export(X509ContentType.Pkcs12), password: null);
        var broker = new PairingConfirmationBroker();
        var powerCountdowns = new PowerCountdownBroker();

        var runtime = new AgentRuntime(new AgentRuntimeOptions
        {
            ConfigurationOverrides = new Dictionary<string, string?>
            {
                ["Agent:DataDirectory"] = dataDirectory,
                ["Agent:PublicPort"] = publicPort.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["Agent:LoopbackPort"] = "0",
            },
            TimeProvider = timeProvider ?? TimeProvider.System,
            CertificateProviderFactory = _ => new FixedCertificateProvider(certificate),
            PairingConfirmation = broker,
            PowerCountdowns = powerCountdowns,
            ConfigureServices = configureServices,
        });

        var agent = new TestAgent(dataDirectory, runtime, certificate, broker, powerCountdowns);
        runtime.NoticeRaised += (_, notice) =>
        {
            lock (agent.Notices)
            {
                agent.Notices.Add(notice);
            }
        };
        return agent;
    }

    public static async Task<TestAgent> StartAsync(
        Action<string>? prepareDataDirectory = null,
        TimeProvider? timeProvider = null,
        Action<IServiceCollection>? configureServices = null)
    {
        var agent = Create(prepareDataDirectory, timeProvider: timeProvider, configureServices: configureServices);
        await agent.Runtime.StartAsync(TestContext.Current.CancellationToken);
        Assert.Equal(AgentStatus.Running, agent.Runtime.Status);
        return agent;
    }

    public Task<PairingOffer> CreateOfferAsync() =>
        Services.GetRequiredService<PairingService>().CreateOfferAsync(TestContext.Current.CancellationToken);

    public Task<HttpResponseMessage> CompletePairingAsync(string code, string deviceName = "Celular de teste", DevicePlatform platform = DevicePlatform.Android) =>
        CreatePublicClient().PostAsJsonAsync(
            "/api/v1/pairing/complete",
            new CompletePairingRequest(code, deviceName, platform),
            Json,
            TestContext.Current.CancellationToken);

    /// <summary>Gera uma oferta e completa o pareamento (com a confirmação aprovada).</summary>
    public async Task<CompletePairingResponse> PairAsync(string deviceName = "Celular de teste")
    {
        var offer = await CreateOfferAsync();
        using var response = await CompletePairingAsync(offer.Code, deviceName);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CompletePairingResponse>(Json, TestContext.Current.CancellationToken))!;
    }

    /// <summary>Cliente HTTPS do listener público que aceita apenas o certificado com o pin esperado (como o app).</summary>
    public HttpClient CreatePublicClient(string? token = null)
    {
        var client = new HttpClient(CreatePinnedHandler()) { BaseAddress = new Uri($"https://127.0.0.1:{Runtime.Endpoints!.PublicPort}") };
        if (token is not null)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return Track(client);
    }

    /// <summary>Cliente HTTP sem TLS apontado para o listener público (deve falhar).</summary>
    public HttpClient CreatePlainHttpClientForPublicListener() =>
        Track(new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{Runtime.Endpoints!.PublicPort}") });

    public HttpClient CreateLoopbackClient() =>
        Track(new HttpClient { BaseAddress = new Uri($"http://{IPAddress.Loopback}:{Runtime.Endpoints!.LoopbackPort}") });

    /// <summary>Conexão real ao Hub por WebSocket, com o token na query (como o app) e validação do pin.</summary>
    public async Task<HubConnection> ConnectHubAsync(string token)
    {
        var expectedPin = Pin;
        var connection = new HubConnectionBuilder()
            .WithUrl($"https://127.0.0.1:{Runtime.Endpoints!.PublicPort}/hubs/remote", options =>
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>(token);
                options.HttpMessageHandlerFactory = _ => CreatePinnedHandler();
                options.WebSocketConfiguration = socket => socket.RemoteCertificateValidationCallback = (_, certificate, _, _) =>
                    certificate is not null && CertificatePin.Compute(X509CertificateLoader.LoadCertificate(certificate.GetRawCertData())) == expectedPin;
            })
            .AddJsonProtocol(options => options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)))
            .Build();
        _hubConnections.Add(connection);
        await connection.StartAsync(TestContext.Current.CancellationToken);
        return connection;
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var connection in _hubConnections)
        {
            await connection.DisposeAsync();
        }

        foreach (var client in _clients)
        {
            client.Dispose();
        }

        await Runtime.DisposeAsync();
        _certificate.Dispose();
        SqliteConnection.ClearAllPools();
        Directory.Delete(DataDirectory, recursive: true);
    }

    private HttpClientHandler CreatePinnedHandler()
    {
        var expectedPin = Pin;
        return new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, certificate, _, _) =>
                certificate is not null && CertificatePin.Compute(certificate) == expectedPin,
        };
    }

    private HttpClient Track(HttpClient client)
    {
        _clients.Add(client);
        return client;
    }

    private sealed class FixedCertificateProvider(X509Certificate2 certificate) : ICertificateProvider
    {
        public Task<CertificateResult> GetOrCreateAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new CertificateResult(certificate, Regenerated: false));
    }
}
