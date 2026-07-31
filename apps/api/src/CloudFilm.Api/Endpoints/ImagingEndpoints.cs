using CloudFilm.Application.Abstractions;
using CloudFilm.Application.Imaging;

namespace CloudFilm.Api.Endpoints;

/// <summary>
/// The portal's read path: patients and their imaging studies.
/// </summary>
/// <remarks>
/// These endpoints expose <see cref="IImagingCatalog"/> and nothing else. They
/// hold no knowledge of where the data originates, which is what allows the
/// catalog implementation to be replaced without touching transport or client.
/// </remarks>
internal static class ImagingEndpoints
{
    /// <summary>
    /// Maps the patient and study endpoints.
    /// </summary>
    /// <param name="app">The route builder.</param>
    /// <returns>The same route builder, to allow chaining.</returns>
    public static IEndpointRouteBuilder MapImagingEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder patients = app
            .MapGroup("/api/v1/patients")
            .WithTags("Patients");

        patients.MapGet("/", async (IImagingCatalog catalog, CancellationToken cancellationToken) =>
                Results.Ok(await catalog.ListPatientsAsync(cancellationToken).ConfigureAwait(false)))
            .WithName("ListPatients")
            .WithSummary("Lists the patients this deployment can serve.")
            .AllowAnonymous()
            .Produces<IReadOnlyList<PatientSummary>>();

        patients.MapGet("/{medicalRecordNumber}", async (
                    string medicalRecordNumber,
                    IImagingCatalog catalog,
                    CancellationToken cancellationToken) =>
            {
                PatientRecord? record = await catalog
                    .FindPatientAsync(medicalRecordNumber, cancellationToken)
                    .ConfigureAwait(false);

                return record is null ? Results.NotFound() : Results.Ok(record);
            })
            .WithName("GetPatient")
            .WithSummary("Returns a patient and their imaging studies.")
            .AllowAnonymous()
            .Produces<PatientRecord>()
            .Produces(StatusCodes.Status404NotFound);

        RouteGroupBuilder studies = app
            .MapGroup("/api/v1/studies")
            .WithTags("Studies");

        studies.MapGet("/{studyInstanceUid}", async (
                    string studyInstanceUid,
                    IImagingCatalog catalog,
                    CancellationToken cancellationToken) =>
            {
                StudyContext? context = await catalog
                    .FindStudyAsync(studyInstanceUid, cancellationToken)
                    .ConfigureAwait(false);

                return context is null ? Results.NotFound() : Results.Ok(context);
            })
            .WithName("GetStudy")
            .WithSummary("Returns a study together with its patient and sibling studies.")
            .AllowAnonymous()
            .Produces<StudyContext>()
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }
}
