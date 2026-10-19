using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using MyRemoteControll.Agent.Infrastructure.Logging;
using MyRemoteControll.Agent.Infrastructure.Security;
using MyRemoteControll.Agent.Infrastructure.Web;
using MyRemoteControll.Contracts.Pairing;

namespace MyRemoteControll.Agent.Features.Pairing;

internal sealed class PairingEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/v1/pairing/complete", CompleteAsync)
            .AllowAnonymous()
            .AddEndpointFilter<ValidationFilter<CompletePairingRequest>>()
            .WithName("CompletePairing")
            .WithSummary("Troca o código do QR pelo token do aparelho, após confirmação no PC.");

    private static async Task<Results<Created<CompletePairingResponse>, ProblemHttpResult>> CompleteAsync(
        CompletePairingRequest request,
        PairingService pairing,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        var result = await pairing
            .CompleteAsync(request, http.Connection.RemoteIpAddress?.ToString(), CorrelationIdMiddleware.Get(http), cancellationToken)
            .ConfigureAwait(false);

        return result.Response is { } response
            ? TypedResults.Created((string?)null, response)
            : ApiProblems.Create(result.StatusCode, result.ErrorCode!, result.Title!);
    }
}

internal sealed class CompletePairingRequestValidator : AbstractValidator<CompletePairingRequest>
{
    public CompletePairingRequestValidator()
    {
        RuleFor(r => r.Code).Must(SecureTokens.HasValidFormat).WithMessage("Código de pareamento em formato inválido.");
        RuleFor(r => r.DeviceName).NotEmpty().MaximumLength(100);
        RuleFor(r => r.Platform).IsInEnum();
    }
}
