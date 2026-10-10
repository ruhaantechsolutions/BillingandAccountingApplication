using Serilog;
using Serilog.Events;

namespace RBiz.WebAPI.Extensions;

/// <summary>
/// Serilog wiring. Note that only this layer references Serilog: the Application and
/// Infrastructure layers log through Microsoft.Extensions.Logging's ILogger&lt;T&gt;, so
/// swapping the logging backend later touches this file alone.
/// </summary>
internal static class SerilogExtensions
{
    /// <summary>
    /// A minimal logger installed before the host is built. Without it, an exception
    /// thrown during startup (bad connection string, invalid JWT key) is never logged.
    /// </summary>
    public static void AddBootstrapLogger()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console()
            .CreateBootstrapLogger();
    }

    /// <summary>
    /// Replaces the bootstrap logger with the real one, configured from the "Serilog"
    /// section of appsettings so levels and sinks change without a rebuild.
    /// </summary>
    public static IHostApplicationBuilder AddSerilogLogging(this IHostApplicationBuilder builder)
    {
        builder.Services.AddSerilog((services, configuration) => configuration
            .ReadFrom.Configuration(builder.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext());

        return builder;
    }

    /// <summary>
    /// One structured log line per request instead of the three or four ASP.NET Core emits
    /// by default, with the status code and elapsed time attached.
    /// </summary>
    public static IApplicationBuilder UseRequestLogging(this WebApplication app)
    {
        app.UseSerilogRequestLogging(options =>
        {
            options.MessageTemplate =
                "{RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";

            // A failed request should stand out in the log at its own level.
            options.GetLevel = static (httpContext, _, exception) =>
                exception is not null || httpContext.Response.StatusCode >= 500
                    ? LogEventLevel.Error
                    : httpContext.Response.StatusCode >= 400
                        ? LogEventLevel.Warning
                        : LogEventLevel.Information;

            options.EnrichDiagnosticContext = static (diagnosticContext, httpContext) =>
            {
                diagnosticContext.Set("TraceId", httpContext.TraceIdentifier);
                diagnosticContext.Set("ClientIp", httpContext.Connection.RemoteIpAddress?.ToString());
            };
        });

        return app;
    }
}
