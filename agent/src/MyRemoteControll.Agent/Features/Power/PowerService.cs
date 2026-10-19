using System.ComponentModel;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using MyRemoteControll.Agent.Domain.Audit;
using MyRemoteControll.Agent.Hubs;
using MyRemoteControll.Agent.Infrastructure.Audit;
using MyRemoteControll.Agent.Infrastructure.Results;
using MyRemoteControll.Contracts.Errors;
using MyRemoteControll.Contracts.Hub;
using MyRemoteControll.Contracts.Power;
using MyRemoteControll.Contracts.Security;
using MyRemoteControll.Windows.Power;

namespace MyRemoteControll.Agent.Features.Power;

/// <summary>Quem pediu a ação de energia (para a auditoria e para mostrar no PC).</summary>
internal sealed record PowerRequester(Guid? DeviceId, string? DeviceName, bool LocalConfirmation);

/// <summary>
/// Ações de energia (spec 005): uma pendente por vez, contagem sempre no agente e execução no prazo pelo
/// <see cref="IPowerService"/>. Avisa todos os aparelhos (<c>PowerCountdown</c>/<c>PowerCancelled</c>) e o PC (broker).
/// Se o agente encerrar durante a contagem, a ação é descartada e registrada.
/// </summary>
internal sealed partial class PowerService(
    IPowerService windows,
    IEnumerable<IActiveWorkSource> activeWorkSources,
    IHubContext<RemoteHub, IRemoteClient> hubContext,
    IAuditLog auditLog,
    PowerCountdownBroker broker,
    TimeProvider timeProvider,
    ILogger<PowerService> logger) : IHostedLifecycleService, IDisposable
{
    public const string CancelAction = "power.cancel";
    public const string ExecuteAction = "power.execute";
    public const string DiscardAction = "power.discard";

    /// <summary>Atraso mínimo: a resposta e o <c>PowerCountdown</c> chegam aos aparelhos antes de o PC desligar.</summary>
    public static readonly TimeSpan MinimumDelay = TimeSpan.FromSeconds(1);

    private static readonly OperationError AlreadyScheduled = new(
        ErrorCodes.PowerAlreadyScheduled,
        "Já existe uma ação de energia agendada. Cancele-a ou substitua.",
        StatusCodes.Status409Conflict);

    private static readonly OperationError HibernateUnavailable = new(
        ErrorCodes.PowerHibernateUnavailable,
        "A hibernação está desativada neste PC.",
        StatusCodes.Status409Conflict);

    private static readonly OperationError SleepUnavailable = new(
        ErrorCodes.PowerSleepUnavailable,
        "Este PC não oferece suspensão.",
        StatusCodes.Status409Conflict);

    private static readonly OperationError NothingToCancel = new(
        ErrorCodes.PowerNothingToCancel,
        "Não há ação de energia agendada.",
        StatusCodes.Status404NotFound);

    // Serializa pedido, cancelamento, execução e descarte, inclusive os avisos, para os aparelhos verem a ordem certa.
    private readonly SemaphoreSlim _operations = new(1, 1);
    private Pending? _pending;

    /// <summary>A contagem mudou (agendada ou terminada), para o painel.</summary>
    public event EventHandler? Changed;

    public PowerCountdownDto? Current => Volatile.Read(ref _pending)?.Countdown;

    public PowerCapabilities Capabilities => windows.GetCapabilities();

    /// <summary>O Windows Update espera reinício (desligar ou reiniciar pode demorar).</summary>
    public bool UpdatePending => windows.IsUpdateRebootPending();

    /// <summary>Ação de auditoria do pedido, ex.: <c>power.shutdown</c>.</summary>
    public static string RequestAuditAction(PowerActionKind action) =>
        $"power.{JsonNamingPolicy.CamelCase.ConvertName(action.ToString())}";

    /// <summary>Desligar, reiniciar e hibernar exigem <c>power.shutdown</c>; bloquear e suspender, <c>power.basic</c>.</summary>
    public static string RequiredPermission(PowerActionKind action) =>
        action is PowerActionKind.Shutdown or PowerActionKind.Restart or PowerActionKind.Hibernate
            ? PermissionKeys.PowerShutdown
            : PermissionKeys.PowerBasic;

    public async Task<OperationResult<PowerCountdownDto>> RequestAsync(
        PowerActionRequest request,
        PowerRequester requester,
        CancellationToken cancellationToken)
    {
        var refusal = Unavailable(request.Action) ?? await ActiveWorkAsync(request, cancellationToken).ConfigureAwait(false);
        if (refusal is not null)
        {
            await AuditRequestAsync(request, requester, refusal.Code, executesAt: null, cancellationToken).ConfigureAwait(false);
            return refusal;
        }

        var delay = TimeSpan.FromSeconds(request.DelaySeconds);
        if (delay < MinimumDelay)
        {
            delay = MinimumDelay;
        }

        var updatePending = request.Action is PowerActionKind.Shutdown or PowerActionKind.Restart && windows.IsUpdateRebootPending();
        Pending pending;
        await _operations.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_pending is not null)
            {
                await AuditRequestAsync(request, requester, AlreadyScheduled.Code, executesAt: null, cancellationToken).ConfigureAwait(false);
                return AlreadyScheduled;
            }

            var countdown = new PowerCountdownDto(
                request.Action,
                timeProvider.GetUtcNow() + delay,
                request.Force,
                requester.DeviceId,
                requester.DeviceName,
                updatePending);

            // O timer só é ligado depois que a pendente existe, para o callback sempre encontrá-la.
            Pending? created = null;
            var timer = timeProvider.CreateTimer(_ => _ = ExecuteDueAsync(created!), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            var notice = new PowerCountdownNotice(countdown, () => CancelFromPcAsync());
            created = pending = new Pending(countdown, timer, notice);
            Volatile.Write(ref _pending, pending);
            timer.Change(delay, Timeout.InfiniteTimeSpan);

            await AuditRequestAsync(request, requester, errorCode: null, countdown.ExecutesAt, cancellationToken).ConfigureAwait(false);
            await hubContext.Clients.All.PowerCountdown(countdown).ConfigureAwait(false);
        }
        finally
        {
            _operations.Release();
        }

        LogScheduled(request.Action, pending.Countdown.ExecutesAt);
        broker.Publish(pending.Notice);
        Changed?.Invoke(this, EventArgs.Empty);
        return OperationResult<PowerCountdownDto>.Ok(pending.Countdown);
    }

    public async Task<OperationResult<PowerCancelledDto>> CancelAsync(
        AuditActor actor,
        PowerCancelSource source,
        CancellationToken cancellationToken)
    {
        Pending cancelled;
        PowerCancelledDto result;
        await _operations.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_pending is null)
            {
                await auditLog.WriteAsync(
                    actor.Record(CancelAction, AuditResult.Failed) with { ErrorCode = NothingToCancel.Code },
                    cancellationToken).ConfigureAwait(false);
                return NothingToCancel;
            }

            cancelled = _pending;
            Volatile.Write(ref _pending, null);
            await cancelled.Timer.DisposeAsync().ConfigureAwait(false);

            result = new PowerCancelledDto(cancelled.Countdown.Action, actor.DeviceId, source);
            await auditLog.WriteAsync(
                actor.Record(CancelAction, AuditResult.Success) with { Target = new { action = cancelled.Countdown.Action, source } },
                cancellationToken).ConfigureAwait(false);
            await hubContext.Clients.All.PowerCancelled(result).ConfigureAwait(false);
        }
        finally
        {
            _operations.Release();
        }

        LogCancelled(cancelled.Countdown.Action, source);
        cancelled.Notice.End();
        Changed?.Invoke(this, EventArgs.Empty);
        return OperationResult<PowerCancelledDto>.Ok(result);
    }

    /// <summary>Encerramento do agente com contagem ativa: a ação é descartada (ninguém mais vai executá-la).</summary>
    public async Task StoppingAsync(CancellationToken cancellationToken)
    {
        Pending? discarded;
        await _operations.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            discarded = _pending;
            if (discarded is null)
            {
                return;
            }

            Volatile.Write(ref _pending, null);
            await discarded.Timer.DisposeAsync().ConfigureAwait(false);
            await auditLog.WriteAsync(
                new AuditRecord(DiscardAction, AuditResult.Success)
                {
                    Source = AuditSource.System,
                    Target = new { action = discarded.Countdown.Action, reason = "agentShutdown" },
                },
                cancellationToken).ConfigureAwait(false);
            await hubContext.Clients.All
                .PowerCancelled(new PowerCancelledDto(discarded.Countdown.Action, null, PowerCancelSource.AgentShutdown))
                .ConfigureAwait(false);
        }
        finally
        {
            _operations.Release();
        }

        LogDiscarded(discarded.Countdown.Action);
        discarded.Notice.End();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public Task StartingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public void Dispose()
    {
        _pending?.Timer.Dispose();
        _operations.Dispose();
    }

    private static PowerOperation ToOperation(PowerActionKind action) => action switch
    {
        PowerActionKind.Shutdown => PowerOperation.Shutdown,
        PowerActionKind.Restart => PowerOperation.Restart,
        PowerActionKind.Sleep => PowerOperation.Sleep,
        PowerActionKind.Hibernate => PowerOperation.Hibernate,
        PowerActionKind.Lock => PowerOperation.Lock,
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, null),
    };

    private OperationError? Unavailable(PowerActionKind action)
    {
        if (action is not (PowerActionKind.Sleep or PowerActionKind.Hibernate))
        {
            return null;
        }

        var capabilities = windows.GetCapabilities();
        return action switch
        {
            PowerActionKind.Sleep when !capabilities.SleepAvailable => SleepUnavailable,
            PowerActionKind.Hibernate when !capabilities.HibernateAvailable => HibernateUnavailable,
            _ => null,
        };
    }

    /// <summary>Bloquear não interrompe trabalho; as outras ações pedem confirmação extra se houver trabalho ativo.</summary>
    private async Task<OperationError?> ActiveWorkAsync(PowerActionRequest request, CancellationToken cancellationToken)
    {
        if (request.Action == PowerActionKind.Lock || request.AcknowledgeActiveWork)
        {
            return null;
        }

        List<ActiveWorkItemDto> items = [];
        foreach (var source in activeWorkSources)
        {
            items.AddRange(await source.GetActiveWorkAsync(cancellationToken).ConfigureAwait(false));
        }

        return items.Count == 0
            ? null
            : new OperationError(
                ErrorCodes.PowerActiveWork,
                "Há trabalho em andamento que seria interrompido. Confirme para continuar.",
                StatusCodes.Status409Conflict,
                items);
    }

    private ValueTask AuditRequestAsync(
        PowerActionRequest request,
        PowerRequester requester,
        string? errorCode,
        DateTimeOffset? executesAt,
        CancellationToken cancellationToken) =>
        auditLog.WriteAsync(
            AuditActor.Device(requester.DeviceId, requester.LocalConfirmation).Record(
                RequestAuditAction(request.Action),
                errorCode is null ? AuditResult.Success : AuditResult.Failed) with
            {
                ErrorCode = errorCode,
                Target = new { delaySeconds = request.DelaySeconds, force = request.Force, executesAt },
            },
            cancellationToken);

    private async Task<bool> CancelFromPcAsync() =>
        (await CancelAsync(AuditActor.Tray, PowerCancelSource.Tray, CancellationToken.None).ConfigureAwait(false)).IsSuccess;

    private async Task ExecuteDueAsync(Pending due)
    {
        await _operations.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            // Cancelada ou descartada enquanto o timer disparava: quem tirou a pendente primeiro vence.
            if (!ReferenceEquals(_pending, due))
            {
                return;
            }

            Volatile.Write(ref _pending, null);
            await due.Timer.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            _operations.Release();
        }

        due.Notice.End();
        Changed?.Invoke(this, EventArgs.Empty);

        var countdown = due.Countdown;
        var actor = AuditActor.Device(countdown.RequestedByDeviceId);
        var target = new { action = countdown.Action, force = countdown.Force };

        // Registrada antes: desligar e suspender podem não dar tempo de registrar depois.
        await auditLog.WriteAsync(actor.Record(ExecuteAction, AuditResult.Success) with { Target = target }).ConfigureAwait(false);
        LogExecuting(countdown.Action);
        try
        {
            await windows.ExecuteAsync(ToOperation(countdown.Action), countdown.Force, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is PowerException or Win32Exception or InvalidOperationException)
        {
            LogExecutionFailed(ex, countdown.Action);
            await auditLog.WriteAsync(
                actor.Record(ExecuteAction, AuditResult.Failed) with { Target = target, ErrorCode = ErrorCodes.InternalError }).ConfigureAwait(false);
            await hubContext.Clients.All
                .PowerCancelled(new PowerCancelledDto(countdown.Action, null, PowerCancelSource.Failed))
                .ConfigureAwait(false);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Ação de energia {Action} agendada para {ExecutesAt}")]
    private partial void LogScheduled(PowerActionKind action, DateTimeOffset executesAt);

    [LoggerMessage(Level = LogLevel.Information, Message = "Ação de energia {Action} cancelada ({Source})")]
    private partial void LogCancelled(PowerActionKind action, PowerCancelSource source);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Ação de energia {Action} descartada: o agente está encerrando")]
    private partial void LogDiscarded(PowerActionKind action);

    [LoggerMessage(Level = LogLevel.Information, Message = "Executando a ação de energia {Action}")]
    private partial void LogExecuting(PowerActionKind action);

    [LoggerMessage(Level = LogLevel.Error, Message = "O Windows recusou a ação de energia {Action}")]
    private partial void LogExecutionFailed(Exception exception, PowerActionKind action);

    private sealed record Pending(PowerCountdownDto Countdown, ITimer Timer, PowerCountdownNotice Notice);
}
