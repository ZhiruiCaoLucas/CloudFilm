using CloudFilm.Retrieve.Hospital;

namespace CloudFilm.Retrieve.Endpoints;

/// <summary>
/// Imaging queries, answered by the registered hospital adapters.
/// </summary>
/// <remarks>
/// The Retrieve Service is the only component permitted to reach an imaging
/// source, so these endpoints are how the API obtains patients and studies.
/// They contain no source-specific knowledge: every query is delegated to an
/// <see cref="IHospitalAdapter"/>.
/// </remarks>
internal static class ImagingEndpoints
{
    /// <summary>
    /// Maps the imaging query endpoints.
    /// </summary>
    /// <param name="app">The route builder.</param>
    /// <returns>The same route builder, to allow chaining.</returns>
    public static IEndpointRouteBuilder MapImagingEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder patients = app
            .MapGroup("/api/v1/imaging/patients")
            .WithTags("Imaging");

        patients.MapGet("/", async (
                    IHospitalAdapterRegistry registry,
                    CancellationToken cancellationToken) =>
            {
                IHospitalAdapter? adapter = registry.Adapters.FirstOrDefault();
                if (adapter is null)
                {
                    return Results.Ok(Array.Empty<PatientSummary>());
                }

                return Results.Ok(await adapter.ListPatientsAsync(cancellationToken)
                    .ConfigureAwait(false));
            })
            .WithName("RetrieveListPatients")
            .WithSummary("Lists the patients the configured imaging sources hold studies for.")
            .Produces<IReadOnlyList<PatientSummary>>();

        patients.MapGet("/{medicalRecordNumber}", async (
                    string medicalRecordNumber,
                    IHospitalAdapterRegistry registry,
                    CancellationToken cancellationToken) =>
            {
                IHospitalAdapter? adapter = registry.Adapters.FirstOrDefault();
                if (adapter is null)
                {
                    return Results.NotFound();
                }

                PatientRecord? record = await adapter
                    .FindPatientAsync(medicalRecordNumber, cancellationToken)
                    .ConfigureAwait(false);

                return record is null ? Results.NotFound() : Results.Ok(record);
            })
            .WithName("RetrieveGetPatient")
            .WithSummary("Returns a patient and their imaging studies.")
            .Produces<PatientRecord>()
            .Produces(StatusCodes.Status404NotFound);

        RouteGroupBuilder studies = app
            .MapGroup("/api/v1/imaging/studies")
            .WithTags("Imaging");

        studies.MapGet("/{studyInstanceUid}", async (
                    string studyInstanceUid,
                    IHospitalAdapterRegistry registry,
                    CancellationToken cancellationToken) =>
            {
                IHospitalAdapter? adapter = registry.Adapters.FirstOrDefault();
                if (adapter is null)
                {
                    return Results.NotFound();
                }

                StudyContext? context = await adapter
                    .FindStudyAsync(studyInstanceUid, cancellationToken)
                    .ConfigureAwait(false);

                return context is null ? Results.NotFound() : Results.Ok(context);
            })
            .WithName("RetrieveGetStudy")
            .WithSummary("Returns a study together with its patient and sibling studies.")
            .Produces<StudyContext>()
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }
}
