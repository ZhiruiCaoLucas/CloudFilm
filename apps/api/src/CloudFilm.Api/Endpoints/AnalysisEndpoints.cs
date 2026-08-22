using System.Security.Claims;
using CloudFilm.Application.Abstractions;
using CloudFilm.Application.Analysis;
using CloudFilm.Application.Auditing;
using CloudFilm.Application.Auth;
using CloudFilm.Application.Grants;
using CloudFilm.Application.Imaging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;

namespace CloudFilm.Api.Endpoints;

/// <summary>
/// Sending one authorised study to the analysis service, and reading back the
/// result CloudFilm stored when it did.
/// </summary>
/// <remarks>
/// <para>
/// The study comes from the route and from nowhere else. There is no request
/// body, so there is no field a caller could use to name a different study, an
/// archive address or a file - and the analyser is handed bytes this endpoint
/// fetched rather than an identifier it could resolve for itself.
/// </para>
/// <para>
/// The order below is the security property, not a style: the access policy
/// answers before <see cref="IStudyDownloadSource"/> is asked for anything. A
/// caller who may not see a study must not be able to make the archive produce
/// it, however briefly - the round trip is measurable, and it would spend a
/// hospital's imaging tier on a request that was never going to be answered.
/// </para>
/// <para>
/// Analysis requires <see cref="GrantPermissions.Download"/> rather than
/// <see cref="GrantPermissions.View"/>. The study file leaves CloudFilm for
/// another process, which is what a download is; reading a record in the
/// workspace is not. The wider permission also fails safe, because it can be
/// relaxed later without taking access away from anyone who had it.
/// </para>
/// </remarks>
internal static class AnalysisEndpoints
{
    /// <summary>
    /// The answer when this deployment has no analysis service.
    /// </summary>
    /// <remarks>
    /// The same shape the Retrieve Service uses when its configured imaging
    /// source cannot do something: the request was well formed and the caller was
    /// entitled to make it, and this installation simply does not implement it.
    /// </remarks>
    private const string _notConfigured =
        "This deployment has no analysis service configured.";

    /// <summary>
    /// Maps the analysis endpoint.
    /// </summary>
    /// <param name="app">The route builder.</param>
    /// <returns>The same route builder, to allow chaining.</returns>
    public static IEndpointRouteBuilder MapAnalysisEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/imaging/studies/{studyInstanceUid}/analysis", async Task<IResult> (
                    string studyInstanceUid,
                    ClaimsPrincipal principal,

                    // Optional, and the only service in the API resolved this
                    // way. The port is registered only when an analysis service
                    // is configured, so without the attribute a deployment
                    // without one would have this parameter treated as a request
                    // body - and this endpoint has no body by design.
                    [FromServices] IStudyAnalyser? analyser,
                    IStudyAccessPolicy accessPolicy,
                    IStudyDownloadSource downloads,
                    IAuditLogService auditLog,
                    IStudyAnalysisResultStore analysisResults,
                    IUnitOfWork unitOfWork,
                    CancellationToken cancellationToken) =>
            {
                string trimmed = studyInstanceUid?.Trim() ?? string.Empty;

                if (trimmed.Length == 0)
                {
                    return Results.Problem(
                        title: "A Study Instance UID is required.",
                        statusCode: StatusCodes.Status400BadRequest);
                }

                if (trimmed.Length > AnalysisLimits.MaxStudyInstanceUidLength)
                {
                    // Refused rather than truncated, like every other route that
                    // accepts one: a truncated UID is a different study.
                    return Results.Problem(
                        title: "The Study Instance UID is too long.",
                        statusCode: StatusCodes.Status400BadRequest);
                }

                // Before anything is fetched, and before the analyser is even
                // looked at.
                StudyAccessScope scope = await accessPolicy
                    .AuthoriseAsync(principal, trimmed, GrantPermissions.Download, cancellationToken)
                    .ConfigureAwait(false);

                // The same answer a study that does not exist gets. A distinct
                // status here would say "this study exists, but not for you".
                if (scope == StudyAccessScope.Denied)
                {
                    return Results.NotFound();
                }

                if (analyser is null)
                {
                    // Answered after authorisation, so that the absence of an
                    // analysis service cannot be used to probe which studies
                    // exist: a caller who may not see this study is refused
                    // first, and learns nothing about the deployment.
                    return Results.Problem(
                        title: _notConfigured,
                        statusCode: StatusCodes.Status501NotImplemented);
                }

                // This endpoint owns the download and disposes it exactly once,
                // unlike the share route - which hands the stream to the response
                // and cannot release it until the body has been written. Here the
                // response is JSON produced after the analyser has finished, so
                // the study is released as soon as it has been read.
                await using StudyDownload? download = await downloads
                    .OpenStudyAsync(trimmed, cancellationToken)
                    .ConfigureAwait(false);

                if (download is null)
                {
                    return Results.NotFound();
                }

                StudyAnalysis? analysis = await analyser
                    .AnalyseAsync(trimmed, download, cancellationToken)
                    .ConfigureAwait(false);

                if (analysis is null)
                {
                    // The archive produced the study and it held no instances.
                    // Nothing was analysed because there was nothing to analyse,
                    // which for this route is the same as having nothing to
                    // return.
                    return Results.NotFound();
                }

                Guid? actorUserId = ActorUserId(principal);

                // After the analysis and before the caller is told of it. A study
                // that reached a processor and was not recorded as having done so
                // is exactly the access the audit trail exists to hold, so a
                // failure here fails the request: the caller gets the API's usual
                // 500 and no result.
                //
                // The result is stored in the same transaction as that record,
                // and deliberately: a stored analysis with no audit entry says a
                // study was processed and hides who did it, and an audit entry
                // with no stored analysis sends the reader back to the archive
                // for something CloudFilm already has. Either alone is worse
                // than neither, so both commit or neither does.
                await unitOfWork
                    .ExecuteAsync(
                        async operationToken =>
                        {
                            await auditLog
                                .RecordAsync(
                                    new AuditEntry(
                                        actorUserId,
                                        AuditAction.AnalyseStudy,
                                        AuditResourceType.Study,

                                        // The route's identifier, which is what
                                        // was authorised - never one recovered
                                        // from the DICOM.
                                        trimmed),
                                    operationToken)
                                .ConfigureAwait(false);

                            // Replaces whatever this study's previous result
                            // was. A rerun is how a clinician corrects a result
                            // produced by a model that has since been fixed, and
                            // leaving the old one to be found later would be the
                            // failure this whole route exists to avoid. The run
                            // itself is not lost: the audit trail above records
                            // every one of them, append-only.
                            await analysisResults
                                .SaveAsync(analysis, actorUserId, operationToken)
                                .ConfigureAwait(false);

                            return true;
                        },
                        cancellationToken)
                    .ConfigureAwait(false);

                // The value that was stored, rather than a re-read of the row.
                // The row is written from this object, so a second query would
                // cost a round trip to return what is already in hand.
                return Results.Ok(analysis);
            })
            .WithName("AnalyseStudy")
            .WithTags("Analysis")
            .WithSummary("Sends one study to the analysis service and returns what it reports.")
            .RequireAuthorization(AuthorizationPolicies.RequiresApplicationRole)
            .Produces<StudyAnalysis>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status501NotImplemented);

        // The same path, read instead of written. Mapped separately rather than
        // through a route group so that the working POST above is untouched, and
        // each route states its own authorisation rule rather than inheriting
        // one - which is the more explicit half of the trade the read-state group
        // makes in the other direction.
        //
        // No IStudyAnalyser here. Reading a stored result must keep working in a
        // deployment that has since removed its analysis service, and it must
        // never be a path that could start one.
        app.MapGet("/api/v1/imaging/studies/{studyInstanceUid}/analysis", async Task<IResult> (
                    string studyInstanceUid,
                    ClaimsPrincipal principal,
                    IStudyAccessPolicy accessPolicy,
                    IStudyAnalysisResultStore analysisResults,
                    CancellationToken cancellationToken) =>
            {
                string trimmed = studyInstanceUid?.Trim() ?? string.Empty;

                if (trimmed.Length == 0)
                {
                    return Results.Problem(
                        title: "A Study Instance UID is required.",
                        statusCode: StatusCodes.Status400BadRequest);
                }

                if (trimmed.Length > AnalysisLimits.MaxStudyInstanceUidLength)
                {
                    return Results.Problem(
                        title: "The Study Instance UID is too long.",
                        statusCode: StatusCodes.Status400BadRequest);
                }

                // View, not Download. Running an analysis requires the wider
                // permission because the study file leaves CloudFilm for another
                // process; reading a record CloudFilm already holds moves no
                // imaging anywhere, so it is the same permission as reading the
                // study in the workspace.
                //
                // Asked before the store, so a caller who may not see this study
                // cannot use the presence of a result to learn that it exists.
                if (await accessPolicy
                        .AuthoriseAsync(principal, trimmed, GrantPermissions.View, cancellationToken)
                        .ConfigureAwait(false) == StudyAccessScope.Denied)
                {
                    return Results.NotFound();
                }

                StudyAnalysis? stored = await analysisResults
                    .FindAsync(trimmed, cancellationToken)
                    .ConfigureAwait(false);

                // A study that has never been analysed is 404, the same answer a
                // caller who may not see it receives. That is the anti-
                // enumeration rule this API applies everywhere, and it costs
                // nothing here: the portal has already loaded the study before it
                // asks, so "no analysis yet" and "not your study" are never
                // ambiguous to a client that is entitled to be asking.
                //
                // Not audited. The findings are geometry and modality rather than
                // patient data, and the page that reads them has already recorded
                // VIEW_STUDY for the study they describe; a second entry per
                // workspace load would describe HTTP traffic rather than clinical
                // activity.
                return stored is null ? Results.NotFound() : Results.Ok(stored);
            })
            .WithName("GetStudyAnalysis")
            .WithTags("Analysis")
            .WithSummary("Returns the stored analysis of one study, if it has been analysed.")
            .RequireAuthorization(AuthorizationPolicies.RequiresApplicationRole)
            .Produces<StudyAnalysis>()
            .Produces(StatusCodes.Status400BadRequest)
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
    /// Reads the registered <c>sub</c> claim rather than
    /// <c>ClaimTypes.NameIdentifier</c>, because the bearer handler sets
    /// <c>MapInboundClaims = false</c> and claims keep the names they were issued
    /// with. Looking for the URI form would find nothing and file every analysis
    /// under "no actor".
    /// </remarks>
    private static Guid? ActorUserId(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out Guid id)
            ? id
            : null;
}
