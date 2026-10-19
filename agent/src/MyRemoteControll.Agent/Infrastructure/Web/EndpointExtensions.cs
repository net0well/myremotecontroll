using System.Reflection;

namespace MyRemoteControll.Agent.Infrastructure.Web;

public static class EndpointExtensions
{
    /// <summary>Mapeia todos os <see cref="IEndpointGroup"/> do assembly; o bootstrap não muda quando surge uma feature.</summary>
    public static IEndpointRouteBuilder MapEndpoints(this IEndpointRouteBuilder app, Assembly assembly)
    {
        var groups = assembly
            .GetTypes()
            .Where(t => t.IsAssignableTo(typeof(IEndpointGroup)) && t is { IsInterface: false, IsAbstract: false })
            .Select(Activator.CreateInstance)
            .Cast<IEndpointGroup>();

        foreach (var group in groups)
        {
            group.Map(app);
        }

        return app;
    }
}
