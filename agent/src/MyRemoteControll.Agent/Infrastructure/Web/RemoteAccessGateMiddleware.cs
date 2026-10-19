using MyRemoteControll.Contracts.Errors;

namespace MyRemoteControll.Agent.Infrastructure.Web;

/// <summary>Com o acesso remoto pausado, o listener público responde 503; o loopback continua atendendo.</summary>
internal sealed class RemoteAccessGateMiddleware(RequestDelegate next, RemoteAccessGate gate)
{
    public Task InvokeAsync(HttpContext context)
    {
        if (gate.IsPaused && context.GetListenerKind() == ListenerKind.Public)
        {
            return ApiProblems
                .Create(StatusCodes.Status503ServiceUnavailable, ErrorCodes.AgentPaused, "O acesso remoto está pausado no PC.")
                .ExecuteAsync(context);
        }

        return next(context);
    }
}
