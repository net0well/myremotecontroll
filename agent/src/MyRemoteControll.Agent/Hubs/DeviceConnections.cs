using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;

namespace MyRemoteControll.Agent.Hubs;

/// <summary>
/// Conexões abertas no Hub por aparelho: para derrubá-las na revogação (spec 002, AC-4) e para atualizar os grupos de
/// permissão quando o papel muda (spec 003, AC-4).
/// </summary>
internal sealed class DeviceConnections
{
    private readonly ConcurrentDictionary<string, (Guid DeviceId, HubCallerContext Context)> _connections = new();

    public static string GroupFor(Guid deviceId) => $"device:{deviceId:N}";

    public void Add(Guid deviceId, HubCallerContext context) => _connections[context.ConnectionId] = (deviceId, context);

    public void Remove(string connectionId) => _connections.TryRemove(connectionId, out _);

    public IReadOnlySet<Guid> ConnectedDevices => _connections.Values.Select(c => c.DeviceId).ToHashSet();

    public IReadOnlyList<string> ConnectionsOf(Guid deviceId) =>
        [.. _connections.Where(c => c.Value.DeviceId == deviceId).Select(c => c.Key)];

    public int Abort(Guid deviceId)
    {
        var aborted = 0;
        foreach (var (connectionId, connection) in _connections)
        {
            if (connection.DeviceId == deviceId)
            {
                connection.Context.Abort();
                _connections.TryRemove(connectionId, out _);
                aborted++;
            }
        }

        return aborted;
    }
}
