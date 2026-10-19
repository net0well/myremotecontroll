using System.Net;
using System.Net.Sockets;

namespace MyRemoteControll.Agent.Hosting;

/// <summary>Descobre qual porta configurada está ocupada, para a mensagem da bandeja ser precisa.</summary>
internal static class PortProbe
{
    public static bool IsInUse(IPAddress address, int port)
    {
        if (port == 0)
        {
            return false;
        }

        var listener = new TcpListener(address, port);
        try
        {
            if (address.Equals(IPAddress.IPv6Any))
            {
                listener.Server.DualMode = true;
            }

            listener.Start();
            return false;
        }
        catch (SocketException)
        {
            return true;
        }
        finally
        {
            listener.Dispose();
        }
    }
}
