using Microsoft.AspNetCore.Http.HttpResults;
using MyRemoteControll.Agent.Infrastructure.Web;
using MyRemoteControll.Contracts.Hub;

namespace MyRemoteControll.Agent.Infrastructure.Results;

/// <summary>
/// Falha esperada de uma operação: código estável do contrato, mensagem para o usuário, status HTTP e, quando o código
/// prevê, dados do erro (ex.: a lista de <c>power.activeWork</c>).
/// </summary>
internal sealed record OperationError(string Code, string Message, int StatusCode, object? Details = null);

/// <summary>Result pattern (padrão do kit): falhas esperadas não viram exceção.</summary>
internal readonly record struct OperationResult<T>(T? Value, OperationError? Error)
{
    public bool IsSuccess => Error is null;

    public static OperationResult<T> Ok(T value) => new(value, null);

    public static implicit operator OperationResult<T>(OperationError error) => new(default, error);

    public HubResponse<T> ToHubResponse() =>
        Error is null ? HubResponse.Success(Value!) : HubResponse.Failure<T>(Error.Code, Error.Message, Error.Details);
}

internal static class OperationErrorExtensions
{
    public static ProblemHttpResult ToProblem(this OperationError error) =>
        ApiProblems.Create(error.StatusCode, error.Code, error.Message);
}
