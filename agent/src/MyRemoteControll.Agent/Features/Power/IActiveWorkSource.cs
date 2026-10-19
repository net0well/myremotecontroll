using MyRemoteControll.Contracts.Power;

namespace MyRemoteControll.Agent.Features.Power;

/// <summary>
/// Trabalho em andamento que uma ação de energia interromperia: sessões do Claude trabalhando (spec 008) e builds
/// (spec 014). Até lá não há fontes registradas e a lista fica vazia.
/// </summary>
internal interface IActiveWorkSource
{
    ValueTask<IReadOnlyList<ActiveWorkItemDto>> GetActiveWorkAsync(CancellationToken cancellationToken);
}
