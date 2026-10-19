namespace MyRemoteControll.Agent.Infrastructure.Web;

/// <summary>
/// Endpoints marcados com <see cref="Listeners.LoopbackOnly{TBuilder}"/> só existem no loopback; os demais só no
/// listener público. Do lado errado, a rota simplesmente não existe (404).
/// </summary>
internal sealed class ListenerRoutingMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        var endpoint = context.GetEndpoint();
        if (endpoint is not null)
        {
            var loopbackOnly = endpoint.Metadata.GetMetadata<Listeners.LoopbackOnlyMetadata>() is not null;
            var onLoopback = context.GetListenerKind() == ListenerKind.Loopback;
            if (loopbackOnly != onLoopback)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return Task.CompletedTask;
            }
        }

        return next(context);
    }
}
