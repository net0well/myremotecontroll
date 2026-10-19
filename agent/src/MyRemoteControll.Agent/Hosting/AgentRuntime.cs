using System.Diagnostics;
using Microsoft.Extensions.Options;
using MyRemoteControll.Agent.Infrastructure.Logging;
using MyRemoteControll.Agent.Infrastructure.Options;
using MyRemoteControll.Agent.Infrastructure.Persistence;
using MyRemoteControll.Agent.Infrastructure.Web;
using Serilog.Core;

namespace MyRemoteControll.Agent.Hosting;

/// <summary>
/// Ciclo de vida do host, independente da bandeja: inicia fora da thread da UI, publica o estado e tenta de novo
/// quando a porta está ocupada (spec 001). Os eventos são disparados em threads de segundo plano.
/// </summary>
internal sealed class AgentRuntime : IAsyncDisposable
{
    private readonly AgentRuntimeOptions _options;
    private readonly Lock _sync = new();
    private WebApplication? _app;
    private Logger? _logger;
    private AgentStatus _status = AgentStatus.Starting;
    private string? _statusMessage;

    public AgentRuntime(AgentRuntimeOptions? options = null)
    {
        _options = options ?? new AgentRuntimeOptions();
        Gate.Changed += (_, _) => StatusChanged?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? StatusChanged;

    public event EventHandler<AgentNotice>? NoticeRaised;

    public RemoteAccessGate Gate { get; } = new();

    public AgentPaths? Paths { get; private set; }

    /// <summary>Conhecido assim que a configuração é lida, mesmo que a inicialização falhe depois.</summary>
    public string? LogsDirectory { get; private set; }

    public AgentEndpoints? Endpoints { get; private set; }

    public IServiceProvider? Services => _app?.Services;

    public AgentStatus Status
    {
        get
        {
            lock (_sync)
            {
                return _status == AgentStatus.Running && Gate.IsPaused ? AgentStatus.Paused : _status;
            }
        }
    }

    public string? StatusMessage
    {
        get
        {
            lock (_sync)
            {
                return _statusMessage;
            }
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SetStatus(AgentStatus.Starting, null);
            try
            {
                var app = await AgentApplicationFactory
                    .CreateAndStartAsync(_options, Gate, GetLogger, RaiseNotice, cancellationToken)
                    .ConfigureAwait(false);

                Paths = app.Services.GetRequiredService<AgentPaths>();
                Endpoints = app.Services.GetRequiredService<AgentEndpointsAccessor>().Current;
                _app = app;
                SetStatus(AgentStatus.Running, null);
                _logger?.Information(
                    "Agente iniciado em {ElapsedMs} ms (porta pública {PublicPort}, loopback {LoopbackPort})",
                    (long)Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds,
                    Endpoints.PublicPort,
                    Endpoints.LoopbackPort);
                return;
            }
            catch (PortInUseException ex)
            {
                var retrySeconds = (int)_options.PortRetryInterval.TotalSeconds;
                SetStatus(AgentStatus.Faulted, $"{ex.Message} Nova tentativa em {retrySeconds} s.");
                _logger?.Warning(ex, "Porta em uso ({Ports}); nova tentativa em {RetrySeconds} s", ex.Ports, retrySeconds);
                await Task.Delay(_options.PortRetryInterval, _options.TimeProvider, cancellationToken).ConfigureAwait(false);
            }
            catch (OptionsValidationException ex)
            {
                Fault($"Configuração inválida: {string.Join(" ", ex.Failures)}", ex);
                return;
            }
            catch (DatabaseInitializationException ex)
            {
                Fault("Falha ao preparar o banco de dados. Abra os logs para detalhes.", ex);
                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                SetStatus(AgentStatus.Stopped, null);
                throw;
            }
            catch (Exception ex)
            {
                // Fronteira da inicialização em segundo plano: qualquer falha vira estado Faulted visível na bandeja,
                // em vez de derrubar o processo sem explicação.
                Fault("Erro ao iniciar o agente. Abra os logs para detalhes.", ex);
                return;
            }
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        var app = Interlocked.Exchange(ref _app, null);
        if (app is not null)
        {
            await app.StopAsync(cancellationToken).ConfigureAwait(false);
            await app.DisposeAsync().ConfigureAwait(false);
        }

        SetStatus(AgentStatus.Stopped, null);
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None).ConfigureAwait(false);
        if (_logger is not null)
        {
            await _logger.DisposeAsync().ConfigureAwait(false);
        }
    }

    private Logger GetLogger(IConfiguration configuration, AgentPaths paths)
    {
        LogsDirectory = paths.LogsDirectory;
        return _logger ??= AgentLogging.Create(configuration, paths);
    }

    private void Fault(string message, Exception exception)
    {
        SetStatus(AgentStatus.Faulted, message);
        _logger?.Error(exception, "Falha ao iniciar o agente: {Message}", message);
    }

    private void SetStatus(AgentStatus status, string? message)
    {
        lock (_sync)
        {
            _status = status;
            _statusMessage = message;
        }

        StatusChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RaiseNotice(AgentNotice notice)
    {
        _logger?.Warning("{NoticeTitle}: {NoticeMessage}", notice.Title, notice.Message);
        NoticeRaised?.Invoke(this, notice);
    }
}
