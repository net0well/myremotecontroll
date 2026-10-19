using Serilog.Context;

namespace MyRemoteControll.Agent.Infrastructure.Logging;

/// <summary>Um ID por requisição, presente nos logs, na auditoria e na resposta (ADR-0020); reaproveita o do cliente se for seguro.</summary>
internal sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";
    private const int MaxLength = 64;

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[HeaderName].ToString();
        if (!IsSafe(correlationId))
        {
            correlationId = CorrelationContext.NewId();
        }

        context.Items[HeaderName] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;
        CorrelationContext.Current = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context).ConfigureAwait(false);
        }
    }

    public static string? Get(HttpContext context) => context.Items[HeaderName] as string;

    private static bool IsSafe(string value) =>
        value.Length is > 0 and <= MaxLength && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
}
