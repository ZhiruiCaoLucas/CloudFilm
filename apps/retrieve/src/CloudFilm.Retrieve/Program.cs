using CloudFilm.Retrieve;
using CloudFilm.Retrieve.Endpoints;
using CloudFilm.Retrieve.Health;
using CloudFilm.Retrieve.Hospital;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Scalar.AspNetCore;
using Serilog;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

builder.Services.AddOptions<RetrieveOptions>()
    .Bind(builder.Configuration.GetSection(RetrieveOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

RetrieveOptions retrieve = builder.Configuration
    .GetSection(RetrieveOptions.SectionName)
    .Get<RetrieveOptions>() ?? new RetrieveOptions();

// Hospital integrations are additive: a deployment registers the adapters it needs
// with AddHospitalAdapter<T>() and nothing else in this file changes.
builder.Services.AddHospitalAdapters();

builder.Services.AddHealthChecks()
    .AddUrlGroup(new Uri($"{retrieve.OrthancUrl.TrimEnd('/')}/system"), name: "orthanc", tags: ["ready"])
    .AddCheck<HospitalAdapterHealthCheck>("hospital-adapters", tags: ["ready"]);

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

WebApplication app = builder.Build();

app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = static _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = static check => check.Tags.Contains("ready"),
});

app.MapHospitalEndpoints();

try
{
    await app.RunAsync().ConfigureAwait(false);
}
catch (Exception exception)
{
    Log.Fatal(exception, "CloudFilm Retrieve Service terminated unexpectedly.");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync().ConfigureAwait(false);
}

/// <summary>
/// Entry point marker used by the test host.
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
