namespace MyRemoteControll.Agent.Infrastructure.Web;

/// <summary>Grupo de endpoints de uma feature, descoberto automaticamente por <see cref="EndpointExtensions.MapEndpoints"/>.</summary>
public interface IEndpointGroup
{
    void Map(IEndpointRouteBuilder app);
}
