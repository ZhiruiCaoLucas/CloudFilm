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
    ConfigureOrthancClient(client);
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Shared by both Orthanc clients so the address and the credential are written
// once. Only the timeout differs between them.
//
// Orthanc rejects anonymous DICOMweb requests with 401 whenever
// AuthenticationEnabled is on. Credentials are attached to the client rather
// than to individual calls, so every request the adapter makes carries them,
// including its connectivity probe.
//
// Left unset when no user name is configured: local development runs Orthanc
// with authentication disabled, and sending an empty credential there would be
// noise rather than a fallback.
void ConfigureOrthancClient(HttpClient client)
{
    client.BaseAddress = new Uri($"{retrieve.OrthancUrl.TrimEnd('/')}/dicom-web/");

    if (retrieve.OrthancUsername.Length > 0)
    {
        string credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{retrieve.OrthancUsername}:{retrieve.OrthancPassword}"));

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", credentials);
    }
}

// The same address and credential, without the thirty second bound.
// HttpClient.Timeout covers the body as well as the headers, so a study large
// enough to be worth downloading would be cut off mid-transfer. This client is
// bounded by the request's cancellation instead.
builder.Services.AddHttpClient(OrthancDicomWebAdapter.DownloadHttpClientName, client =>
{
    ConfigureOrthancClient(client);
    client.Timeout = Timeout.InfiniteTimeSpan;
});

builder.Services.AddHospitalAdapter<OrthancDicomWebAdapter>();

// One Orthanc check, and it is the adapter's own probe.
//
// There used to be a second: an anonymous GET of http://orthanc:8042/system.
// Orthanc answers that with 401 whenever AuthenticationEnabled is on, which is
// every deployment except one running the development overlay - so readiness was
// structurally unreachable in production and only looked healthy locally because
// the overlay turns Orthanc's authentication off.
//
// Removing it loses no coverage. HospitalAdapterHealthCheck calls
// OrthancDicomWebAdapter.ProbeAsync, which issues dicom-web/studies?limit=1 on
// the client ConfigureOrthancClient built - so it carries the credential, and it
// exercises the DICOMweb plugin this service actually depends on rather than
// merely proving that a socket accepts.
builder.Services.AddHealthChecks()
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
app.MapDownloadEndpoints();

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
