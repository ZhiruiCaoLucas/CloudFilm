using System.Text.Json;
using CloudFilm.Api.Endpoints;
using CloudFilm.Api.Extensions;
using CloudFilm.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Scalar.AspNetCore;
using Serilog;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Composition root.
// Each concern is registered by one extension method so that Program.cs stays a
// readable table of contents rather than a wiring dump.
// ---------------------------------------------------------------------------
builder
    .AddCloudFilmObservability()
    .AddCloudFilmAuthentication()
    .AddCloudFilmCors()
    .AddCloudFilmHealthChecks();

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddOpenApi();

WebApplication app = builder.Build();

app.UseExceptionHandler();
app.UseCloudFilmSecurityHeaders();
app.UseCloudFilmRequestLogging();

if (!app.Environment.IsDevelopment())
{
    // Terminated at the ingress in production; enforced here as defence in depth.
    app.UseHsts();
}

app.UseCors(CorsExtensions.PortalPolicy);
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = static _ => false,
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = static check => check.Tags.Contains(HealthCheckExtensions.ReadinessTag),
    ResponseWriter = WriteHealthResponseAsync,
});

app.MapSystemEndpoints();
app.MapImagingEndpoints();

try
{
    await app.RunAsync().ConfigureAwait(false);
}
catch (Exception exception)
{
    Log.Fatal(exception, "CloudFilm API terminated unexpectedly.");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync().ConfigureAwait(false);
}

static Task WriteHealthResponseAsync(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "application/json";

    return context.Response.WriteAsync(JsonSerializer.Serialize(new
    {
        status = report.Status.ToString(),
        durationMs = report.TotalDuration.TotalMilliseconds,
        checks = report.Entries.Select(entry => new
        {
            name = entry.Key,
            status = entry.Value.Status.ToString(),
            durationMs = entry.Value.Duration.TotalMilliseconds,
        }),
    }));
}

/// <summary>
/// Entry point marker used by the integration test host.
/// </summary>
public partial class Program
{
    /// <summary>
    /// Prevents the compiler from generating a public parameterless constructor.
    /// </summary>
    protected Program()
    {
    }
}
