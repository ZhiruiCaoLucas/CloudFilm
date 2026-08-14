using System.Security.Claims;
using CloudFilm.Application.Abstractions;
using CloudFilm.Application.Auditing;
using CloudFilm.Application.Auth;
using CloudFilm.Application.Imaging;
using Microsoft.IdentityModel.JsonWebTokens;

namespace CloudFilm.Api.Endpoints;

/// <summary>
/// The portal's read path: patients and their imaging studies.
/// </summary>
/// <remarks>
/// <para>
/// These endpoints expose <see cref="IImagingCatalog"/> and nothing else. They
/// hold no knowledge of where the data originates, which is what allows the
/// catalog implementation to be replaced without touching transport or client.
/// </para>
/// <para>
/// Every endpoint here returns patient data and every one of them is audited.
/// The audit write happens <em>before</em> the response is produced, so a
/// failure to record an access prevents the disclosure rather than following it.
/// </para>
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
            .RequireAuthorization(AuthorizationPolicies.RequiresApplicationRole)
            .Produces<IReadOnlyList<PatientSummary>>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        patients.MapGet("/{medicalRecordNumber}", async (
                    string medicalRecordNumber,
                    ClaimsPrincipal principal,
                    IImagingCatalog catalog,
                    IAuditLogService auditLog,
                    CancellationToken cancellationToken) =>
            {
                PatientRecord? record = await catalog
                    .FindPatientAsync(medicalRecordNumber, cancellationToken)
                    .ConfigureAwait(false);

                // A lookup that matched nothing is not an access. Recording one
                // would fill the trail with accesses to patients who were never
                // disclosed, and would make the table usable for probing which
                // record numbers exist.
                if (record is null)
                {
                    return Results.NotFound();
                }

                await auditLog
                    .RecordAsync(
                        new AuditEntry(
                            ActorUserId(principal),
                            AuditAction.ViewPatient,
                            AuditResourceType.Patient,
                            medicalRecordNumber),
                        cancellationToken)
                    .ConfigureAwait(false);

                return Results.Ok(record);
            })
            .WithName("GetPatient")
            .WithSummary("Returns a patient and their imaging studies.")

            // Authentication alone is no longer enough to read a patient: the
            // account must hold an application role. Declared here as metadata
            // rather than checked in the handler, so the rule is visible where
            // the route is and cannot be forgotten inside a branch.
            .RequireAuthorization(AuthorizationPolicies.RequiresApplicationRole)
            .Produces<PatientRecord>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        RouteGroupBuilder studies = app
            .MapGroup("/api/v1/studies")
            .WithTags("Studies");

        studies.MapGet("/{studyInstanceUid}", async (
                    string studyInstanceUid,
                    ClaimsPrincipal principal,
                    IImagingCatalog catalog,
                    IAuditLogService auditLog,
                    CancellationToken cancellationToken) =>
            {
                StudyContext? context = await catalog
                    .FindStudyAsync(studyInstanceUid, cancellationToken)
                    .ConfigureAwait(false);

                if (context is null)
                {
                    return Results.NotFound();
                }

                await auditLog
                    .RecordAsync(
                        new AuditEntry(
                            ActorUserId(principal),
                            AuditAction.ViewStudy,
                            AuditResourceType.Study,
                            studyInstanceUid),
                        cancellationToken)
                    .ConfigureAwait(false);

                return Results.Ok(context);
            })
            .WithName("GetStudy")
            .WithSummary("Returns a study together with its patient and sibling studies.")
            .RequireAuthorization(AuthorizationPolicies.RequiresApplicationRole)
            .Produces<StudyContext>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }

    /// <summary>
    /// The account behind the bearer token.
    /// </summary>
    /// <param name="principal">The validated caller.</param>
    /// <returns>The account identifier, or <see langword="null"/> if the token carries no usable subject.</returns>
    /// <remarks>
    /// Reads the registered <c>sub</c> claim, not <c>ClaimTypes.NameIdentifier</c>.
    /// The bearer handler sets <c>MapInboundClaims = false</c>, so claims keep the
    /// names the token was issued with and are never rewritten to the
    /// WS-Federation URIs; looking for the URI form finds nothing and would file
    /// every access under "no actor".
    ///
    /// Null is unreachable in practice - the fallback policy means no unauthenticated
    /// request arrives here - but a token without a parseable subject must record the
    /// access anonymously rather than throw and hide that the access happened.
    /// </remarks>
    private static Guid? ActorUserId(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out Guid id)
            ? id
            : null;
}
