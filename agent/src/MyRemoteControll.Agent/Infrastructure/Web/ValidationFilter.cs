using FluentValidation;
using MyRemoteControll.Contracts.Errors;

namespace MyRemoteControll.Agent.Infrastructure.Web;

/// <summary>Valida o corpo da requisição na borda e responde ProblemDetails com <c>validation.failed</c>.</summary>
internal sealed class ValidationFilter<TRequest>(IValidator<TRequest> validator) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var request = context.Arguments.OfType<TRequest>().FirstOrDefault();
        if (request is null)
        {
            return await next(context).ConfigureAwait(false);
        }

        var result = await validator.ValidateAsync(request, context.HttpContext.RequestAborted).ConfigureAwait(false);
        if (result.IsValid)
        {
            return await next(context).ConfigureAwait(false);
        }

        return TypedResults.ValidationProblem(
            result.ToDictionary(),
            title: "Dados inválidos.",
            extensions: new Dictionary<string, object?> { [ApiProblems.CodeExtension] = ErrorCodes.ValidationFailed });
    }
}
