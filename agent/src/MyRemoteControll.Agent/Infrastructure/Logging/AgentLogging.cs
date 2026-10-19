using MyRemoteControll.Agent.Infrastructure.Options;
using Serilog;
using Serilog.Core;

namespace MyRemoteControll.Agent.Infrastructure.Logging;

internal static class AgentLogging
{
    private const string OutputTemplate =
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} {CorrelationId} {Message:lj}{NewLine}{Exception}";

    /// <summary>
    /// Logger único do processo, em arquivo rotativo (ADR-0020). Os níveis vêm da seção <c>Serilog</c>: o
    /// appsettings.json mantém <c>Microsoft.AspNetCore</c> em Warning para que URLs com query string (que podem
    /// trazer <c>access_token</c>) nunca sejam gravadas.
    /// </summary>
    public static Logger Create(IConfiguration configuration, AgentPaths paths)
    {
        Directory.CreateDirectory(paths.LogsDirectory);

        return new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .Enrich.FromLogContext()
            .WriteTo.File(
                Path.Combine(paths.LogsDirectory, "agent-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                fileSizeLimitBytes: 10 * 1024 * 1024,
                rollOnFileSizeLimit: true,
                outputTemplate: OutputTemplate,
                formatProvider: System.Globalization.CultureInfo.InvariantCulture)
            .CreateLogger();
    }
}
