using Microsoft.AspNetCore.Http.HttpResults;

namespace MyRemoteControll.Agent.Infrastructure.Web;

/// <summary>ProblemDetails com a extensão <c>code</c> estável (hub-contract.md, seção 1).</summary>
internal static class ApiProblems
{
    public const string CodeExtension = "code";

    public static ProblemHttpResult Create(int statusCode, string code, string title) =>
        TypedResults.Problem(
            statusCode: statusCode,
            title: title,
            extensions: new Dictionary<string, object?> { [CodeExtension] = code });
}
