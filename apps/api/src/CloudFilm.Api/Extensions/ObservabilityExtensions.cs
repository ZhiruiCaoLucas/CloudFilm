using Microsoft.IdentityModel.JsonWebTokens;
using Serilog;
using Serilog.Events;

namespace CloudFilm.Api.Extensions;

/// <summary>
/// Structured logging and request auditing.
/// </summary>
/// <remarks>
/// Every request is logged with the authenticated subject and a correlation id.
/// This is the substrate the clinical audit trail is built on, so it is configured
/// before any endpoint is mapped.
/// </remarks>
internal static class ObservabilityExtensions
{
    /// <summary>
    /// Replaces the default logging stack with Serilog, configured from application settings.
    /// </summary>
    /// <param name="builder">The application builder.</param>
    /// <returns>The same builder, to allow chaining.</returns>
    public static WebApplicationBuilder AddCloudFilmObservability(this WebApplicationBuilder builder)
    {
        builder.Host.UseSerilog((context, services, configuration) => configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext());

        return builder;
    }

    /// <summary>
    /// Enables per request audit logging.
    /// </summary>
    /// <param name="app">The application.</param>
    /// <returns>The same application, to allow chaining.</returns>
    public static WebApplication UseCloudFilmRequestLogging(this WebApplication app)
    {
        app.UseSerilogRequestLogging(options =>
        {
            options.GetLevel = static (httpContext, _, exception) =>
                exception is not null || httpContext.Response.StatusCode >= 500
                    ? LogEventLevel.Error
                    : httpContext.Request.Path.StartsWithSegments("/health")
                        ? LogEventLevel.Verbose
                        : LogEventLevel.Information;

            options.EnrichDiagnosticContext = static (diagnosticContext, httpContext) =>
            {
                diagnosticContext.Set("CorrelationId", httpContext.TraceIdentifier);
                // JwtRegisteredClaimNames.Sub, not ClaimTypes.NameIdentifier. The bearer
                // handler sets MapInboundClaims = false, so inbound claims keep their
                // registered short names and are never rewritten to the WS-Federation
                // URIs. Reading the URI form matched nothing, so every authenticated
                // request was logged as "anonymous".
                diagnosticContext.Set(
                    "Subject",
                    httpContext.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? "anonymous");
                diagnosticContext.Set("ClientIp", httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");
            };
        });

        return app;
    }
}
