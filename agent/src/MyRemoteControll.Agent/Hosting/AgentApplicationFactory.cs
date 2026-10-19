using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentValidation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MyRemoteControll.Agent.Features.Audit;
using MyRemoteControll.Agent.Features.Devices;
using MyRemoteControll.Agent.Features.Health;
using MyRemoteControll.Agent.Features.Pairing;
using MyRemoteControll.Agent.Features.Power;
using MyRemoteControll.Agent.Features.Status;
using MyRemoteControll.Agent.Hubs;
using MyRemoteControll.Agent.Infrastructure.Audit;
using MyRemoteControll.Agent.Infrastructure.Auth;
using MyRemoteControll.Agent.Infrastructure.Authorization;
using MyRemoteControll.Agent.Infrastructure.Dashboard;
using MyRemoteControll.Agent.Infrastructure.Logging;
using MyRemoteControll.Agent.Infrastructure.Options;
using MyRemoteControll.Agent.Infrastructure.Persistence;
using MyRemoteControll.Agent.Infrastructure.Security;
using MyRemoteControll.Agent.Infrastructure.Settings;
using MyRemoteControll.Agent.Infrastructure.Tls;
using MyRemoteControll.Agent.Infrastructure.Web;
using MyRemoteControll.Contracts.Errors;
using MyRemoteControll.Windows.Monitoring;
using MyRemoteControll.Windows.Power;
using MyRemoteControll.Windows.Processes;
using Serilog;
using Serilog.Events;
using Serilog.Extensions.Logging;

namespace MyRemoteControll.Agent.Hosting;

/// <summary>
/// Monta e inicia o host do agente: configuração, banco, segredos, certificado, serviços, os dois listeners do Kestrel
/// e o pipeline HTTP. Cada tentativa de inicialização cria um host novo.
/// </summary>
internal static class AgentApplicationFactory
{
    public static async Task<WebApplication> CreateAndStartAsync(
        AgentRuntimeOptions runtimeOptions,
        RemoteAccessGate gate,
        Func<IConfiguration, AgentPaths, Serilog.ILogger> getLogger,
        Action<AgentNotice> notify,
        CancellationToken cancellationToken)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = runtimeOptions.Args,
            ContentRootPath = AppContext.BaseDirectory,
        });
        builder.Configuration.AddInMemoryCollection(runtimeOptions.ConfigurationOverrides);

        var agentOptions = builder.Configuration.GetSection(AgentOptions.SectionName).Get<AgentOptions>() ?? new AgentOptions();

        // O logger nasce antes da validação para que até uma configuração inválida fique registrada; com um diretório
        // de dados inválido, os logs vão para o diretório padrão.
        var logger = getLogger(builder.Configuration, PathsForLogging(agentOptions));
        AgentOptionsValidator.ThrowIfInvalid(agentOptions);
        var paths = AgentPaths.From(agentOptions);

        using var bootstrapLoggerFactory = new SerilogLoggerFactory(logger);
        var timeProvider = runtimeOptions.TimeProvider;

        var database = await new DatabaseInitializer(timeProvider, bootstrapLoggerFactory.CreateLogger<DatabaseInitializer>())
            .InitializeAsync(paths.DatabasePath, cancellationToken)
            .ConfigureAwait(false);
        if (database.PreservedCorruptFile is not null)
        {
            notify(new AgentNotice(
                "Banco de dados recriado",
                $"O banco estava corrompido e foi preservado como {Path.GetFileName(database.PreservedCorruptFile)}."));
        }

        var pepper = PepperStore.LoadOrCreate(paths.DataDirectory, out var pepperRegenerated);
        if (pepperRegenerated)
        {
            notify(new AgentNotice(
                "Chave de segurança recriada",
                "A chave que protege os tokens não pôde ser lida e foi recriada. Os aparelhos pareados precisam parear de novo."));
        }

        var connectionString = SqliteDatabase.ConnectionString(paths.DatabasePath);
        ((IConfigurationBuilder)builder.Configuration).Add(new AgentSettingsConfigurationSource(connectionString));
        var settingsProvider = ((IConfigurationRoot)builder.Configuration).Providers.OfType<AgentSettingsConfigurationProvider>().Last();
        var settingsStore = new AgentSettingsStore(connectionString, settingsProvider, timeProvider);

        var certificateContext = new CertificateProviderContext(builder.Configuration, settingsStore, bootstrapLoggerFactory);
        var certificateProvider = runtimeOptions.CertificateProviderFactory?.Invoke(certificateContext)
            ?? new StoreCertificateProvider(
                builder.Configuration, settingsStore, timeProvider, bootstrapLoggerFactory.CreateLogger<StoreCertificateProvider>());
        var certificate = await certificateProvider.GetOrCreateAsync(cancellationToken).ConfigureAwait(false);
        if (certificate.Regenerated)
        {
            notify(new AgentNotice(
                "Certificado recriado",
                "O certificado do agente foi gerado de novo. Os aparelhos pareados precisam parear outra vez."));
        }

        var services = builder.Services;
        ConfigurePlatformServices(builder, logger, connectionString);
        services.AddSingleton(timeProvider);
        services.AddSingleton(gate);
        services.AddSingleton(paths);
        services.AddSingleton(settingsStore);
        services.AddSingleton(new TokenHasher(pepper));
        services.AddSingleton(new AgentCertificate(certificate.Certificate));
        services.AddSingleton(new AgentNotifications(notify));
        services.AddSingleton(runtimeOptions.PairingConfirmation);
        services.AddSingleton(runtimeOptions.PowerCountdowns);
        ConfigureFeatureServices(services);
        runtimeOptions.ConfigureServices?.Invoke(services);
        ConfigureKestrel(builder, agentOptions, certificate);

        var app = builder.Build();
        ConfigurePipeline(app);

        var started = false;
        try
        {
            await app.StartAsync(cancellationToken).ConfigureAwait(false);
            started = true;
            return app;
        }
        catch (Exception ex) when (IsAddressInUse(ex))
        {
            throw new PortInUseException(FindBusyPorts(agentOptions), ex);
        }
        finally
        {
            if (!started)
            {
                await app.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private static void ConfigurePlatformServices(WebApplicationBuilder builder, Serilog.ILogger logger, string connectionString)
    {
        var services = builder.Services;

        builder.Logging.ClearProviders();
        services.AddSerilog(logger, dispose: false);

        services.AddSingleton<IValidateOptions<AgentOptions>, AgentOptionsValidator>();
        services.AddOptions<AgentOptions>().BindConfiguration(AgentOptions.SectionName).ValidateOnStart();

        // A fábrica também registra o DbContext como serviço com escopo de requisição.
        services.AddDbContextFactory<AgentDbContext>(options => options.UseSqlite(connectionString));

        // Enums trafegam como texto camelCase ("administrator", "ios"), tanto no REST quanto no Hub.
        var enumConverter = new JsonStringEnumConverter(JsonNamingPolicy.CamelCase);
        services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(enumConverter));
        services
            .AddSignalR(options => options.AddFilter<PermissionHubFilter>())
            .AddJsonProtocol(options => options.PayloadSerializerOptions.Converters.Add(enumConverter));

        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
        {
            if (CorrelationIdMiddleware.Get(context.HttpContext) is { } correlationId)
            {
                context.ProblemDetails.Extensions["correlationId"] = correlationId;
            }

            var fallbackCode = context.ProblemDetails.Status switch
            {
                StatusCodes.Status400BadRequest => ErrorCodes.ValidationFailed,
                StatusCodes.Status500InternalServerError => ErrorCodes.InternalError,
                _ => null,
            };
            if (fallbackCode is not null)
            {
                context.ProblemDetails.Extensions.TryAdd(ApiProblems.CodeExtension, fallbackCode);
            }
        });

        services
            .AddAuthentication(DeviceTokenDefaults.AuthenticationScheme)
            .AddScheme<AuthenticationSchemeOptions, DeviceTokenAuthenticationHandler>(DeviceTokenDefaults.AuthenticationScheme, null);

        // Tudo exige aparelho autenticado, exceto o que for marcado explicitamente com AllowAnonymous; permissões viram
        // policies "perm:<chave>" criadas sob demanda, e negações são auditadas (ADR-0009).
        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
        services.AddHttpContextAccessor();
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, AuditingAuthorizationResultHandler>();

        services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");
        services.AddValidatorsFromAssemblyContaining<PairingEndpoints>(ServiceLifetime.Singleton, includeInternalTypes: true);
    }

    private static void ConfigureFeatureServices(IServiceCollection services)
    {
        services.AddSingleton<IProcessRunner, ProcessRunner>();
        services.AddSingleton<AgentEndpointsAccessor>();
        services.AddScoped<DeviceTokenValidator>();

        services.AddSingleton<AuditWriter>();
        services.AddSingleton<IAuditLog>(sp => sp.GetRequiredService<AuditWriter>());
        services.AddHostedService(sp => sp.GetRequiredService<AuditWriter>());

        services.AddOptions<AuditOptions>().BindConfiguration(AuditOptions.SectionName);
        services.AddSingleton<AuditQuery>();
        services.AddHostedService<AuditRetention>();

        services.AddSingleton<DevicePermissionResolver>();
        services.AddSingleton<PermissionHubFilter>();
        services.AddSingleton<DeviceConnections>();
        services.AddSingleton<DeviceRevocation>();
        services.AddSingleton<DeviceManagement>();
        services.AddHostedService<ShutdownNotifier>();

        services.AddSingleton<IHostAddressProvider, HostAddressProvider>();
        services.AddSingleton<PairingLockout>();
        services.AddSingleton<PairingService>();

        services.AddSingleton<IPowerService, WindowsPowerService>();
        services.AddSingleton<PowerService>();
        services.AddHostedService(sp => sp.GetRequiredService<PowerService>());

        // A fonte de métricas é cara de montar (~1 s): só é criada quando alguém pede, ou no aquecimento depois do início.
        services.AddSingleton<IMetricsSource>(sp => new WindowsMetricsSource(sp.GetRequiredService<TimeProvider>()));
        services.AddSingleton(sp => new Lazy<IMetricsSource>(sp.GetRequiredService<IMetricsSource>));
        services.AddSingleton<ISystemStatusSource, WindowsSystemStatusSource>();
        services.AddSingleton<DashboardAggregator>();
        services.AddHostedService(sp => sp.GetRequiredService<DashboardAggregator>());
        services.AddSingleton<MetricsSampler>();
        services.AddHostedService(sp => sp.GetRequiredService<MetricsSampler>());
        services.AddSingleton<StatusPublisher>();
        services.AddHostedService(sp => sp.GetRequiredService<StatusPublisher>());
        services.AddSingleton<PowerCardPublisher>();
        services.AddHostedService(sp => sp.GetRequiredService<PowerCardPublisher>());
    }

    private static void ConfigureKestrel(WebApplicationBuilder builder, AgentOptions options, CertificateResult certificate) =>
        builder.WebHost.ConfigureKestrel(kestrel =>
        {
            kestrel.AddServerHeader = false;
            kestrel.ListenAnyIP(options.PublicPort, listen =>
            {
                listen.Use(Listeners.Tag(ListenerKind.Public));
                listen.UseHttps(certificate.Certificate);
            });
            kestrel.Listen(IPAddress.Loopback, options.LoopbackPort, listen => listen.Use(Listeners.Tag(ListenerKind.Loopback)));
        });

    private static void ConfigurePipeline(WebApplication app)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseSerilogRequestLogging(options => options.GetLevel = (context, _, exception) =>
            exception is not null || context.Response.StatusCode >= StatusCodes.Status500InternalServerError
                ? LogEventLevel.Error
                : context.Request.Path.StartsWithSegments("/health")
                    ? LogEventLevel.Verbose
                    : LogEventLevel.Information);
        app.UseExceptionHandler();
        app.UseMiddleware<RemoteAccessGateMiddleware>();
        app.UseRouting();
        app.UseMiddleware<ListenerRoutingMiddleware>();
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapEndpoints(typeof(AgentApplicationFactory).Assembly);
        app.MapHub<RemoteHub>(RemoteHub.Path);
    }

    private static AgentPaths PathsForLogging(AgentOptions options) =>
        !string.IsNullOrWhiteSpace(options.DataDirectory) && Path.IsPathFullyQualified(options.DataDirectory)
            ? AgentPaths.From(options)
            : new AgentPaths(AgentPaths.DefaultDataDirectory);

    private static bool IsAddressInUse(Exception exception) =>
        exception is AddressInUseException || exception.InnerException is AddressInUseException;

    private static int[] FindBusyPorts(AgentOptions options)
    {
        List<int> busy = [];
        if (PortProbe.IsInUse(IPAddress.IPv6Any, options.PublicPort))
        {
            busy.Add(options.PublicPort);
        }

        if (PortProbe.IsInUse(IPAddress.Loopback, options.LoopbackPort))
        {
            busy.Add(options.LoopbackPort);
        }

        return [.. busy];
    }
}
