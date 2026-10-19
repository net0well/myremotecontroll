using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Connections.Features;

namespace MyRemoteControll.Agent.Infrastructure.Web;

public enum ListenerKind
{
    /// <summary>LAN/Tailscale, HTTPS, autenticado.</summary>
    Public,

    /// <summary>127.0.0.1, só para o MCP e os hooks do Claude Code.</summary>
    Loopback,
}

/// <summary>
/// Marca cada conexão com o listener que a aceitou. A decisão "público ou loopback" vem da conexão real, nunca do
/// header Host, que o cliente controla.
/// </summary>
public static class Listeners
{
    private static readonly object ListenerKey = new();

    public static Func<ConnectionDelegate, ConnectionDelegate> Tag(ListenerKind kind) =>
        next => connection =>
        {
            connection.Items[ListenerKey] = kind;
            return next(connection);
        };

    public static ListenerKind GetListenerKind(this HttpContext context) =>
        context.Features.Get<IConnectionItemsFeature>()?.Items.TryGetValue(ListenerKey, out var value) == true
            && value is ListenerKind kind
                ? kind
                : ListenerKind.Public;

    /// <summary>Restringe o endpoint ao listener loopback; no público ele responde 404.</summary>
    public static TBuilder LoopbackOnly<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder =>
        builder.WithMetadata(LoopbackOnlyMetadata.Instance);

    internal sealed class LoopbackOnlyMetadata
    {
        public static readonly LoopbackOnlyMetadata Instance = new();
    }
}
