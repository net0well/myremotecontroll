using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Features;

namespace MyRemoteControll.Agent.Hosting;

/// <summary>Portas efetivamente abertas pelo Kestrel (diferem da configuração quando ela pede porta 0).</summary>
internal sealed class AgentEndpointsAccessor(IServer server)
{
    public AgentEndpoints Current
    {
        get
        {
            var addresses = server.Features.GetRequiredFeature<IServerAddressesFeature>().Addresses;
            int publicPort = 0, loopbackPort = 0;
            foreach (var address in addresses.Select(a => new Uri(a)))
            {
                if (address.Scheme == Uri.UriSchemeHttps)
                {
                    publicPort = address.Port;
                }
                else
                {
                    loopbackPort = address.Port;
                }
            }

            return new AgentEndpoints(publicPort, loopbackPort);
        }
    }
}
