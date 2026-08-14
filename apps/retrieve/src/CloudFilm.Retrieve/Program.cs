using System.Net.Http.Headers;
using System.Text;
using CloudFilm.Retrieve;
using CloudFilm.Retrieve.Endpoints;
using CloudFilm.Retrieve.Health;
using CloudFilm.Retrieve.Hospital;
using CloudFilm.Retrieve.Orthanc;
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

// The named client carries the DICOMweb base address. Orthanc is reached over
// the internal network only; nothing outside this service knows the address.
builder.Services.AddHttpClient(OrthancDicomWebAdapter.HttpClientName, client =>
{
    client.BaseAddress = new Uri($"{retrieve.OrthancUrl.TrimEnd('/')}/dicom-web/");
    client.Timeout = TimeSpan.FromSeconds(30);

    // Orthanc rejects anonymous DICOMweb requests with 401 whenever
    // AuthenticationEnabled is on. Credentials are attached to the client rather
    // than to individual calls, so every request the adapter makes carries them,
    // including its connectivity probe.
    //
    // Left unset when no user name is configured: local development runs Orthanc
    // with authentication disabled, and sending an empty credential there would
    // be noise rather than a fallback.
    if (retrieve.OrthancUsername.Length > 0)
    {
        string credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{retrieve.OrthancUsername}:{retrieve.OrthancPassword}"));

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", credentials);
    }
});

builder.Services.AddHospitalAdapter<OrthancDicomWebAdapter>();

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
app.MapImagingEndpoints();
app.MapSearchEndpoints();

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
