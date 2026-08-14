using System.Security.Claims;
using CloudFilm.Application.Abstractions;
using CloudFilm.Application.Access;
using CloudFilm.Application.Auth;
using Microsoft.IdentityModel.JsonWebTokens;

namespace CloudFilm.Api.Endpoints;

/// <summary>
/// What the signed-in user has looked at.
/// </summary>
/// <remarks>
/// <para>
/// Not an audit API. There is one route, it answers one question, and the answer
/// is always scoped to the caller: nothing here accepts an account, an action or
/// a resource type from the client, so there is no request that returns another
/// user's history and none that returns a sign-in event.
/// </para>
/// <para>
/// Carries no authorisation metadata, like the imaging endpoints. The
/// authorisation fallback policy closes every endpoint that does not opt out, so
/// this is authenticated by default rather than by remembering to say so.
/// </para>
/// </remarks>
internal static class AccessEndpoints
{
    /// <summary>
    /// Maps the access-history endpoints.
    /// </summary>
    /// <param name="app">The route builder.</param>
    /// <returns>The same route builder, to allow chaining.</returns>
    public static IEndpointRouteBuilder MapAccessEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder access = app
            .MapGroup("/api/v1/access")
            .WithTags("Access")

            // Group level, because every route added under this prefix reads
            // which patients somebody opened. A rule declared once here cannot
            // be missed off the next route the way a per-route one can.
            .RequireAuthorization(AuthorizationPolicies.RequiresApplicationRole);

        access.MapGet("/recent", async (
                    ClaimsPrincipal principal,
                    IRecentPatientAccess recentPatients,
                    CancellationToken cancellationToken) =>
            {
                Guid? actorUserId = ActorUserId(principal);

                // A validated token with no parseable subject cannot be scoped to
                // an account, and an unscoped read of this table would be every
                // user's history. Empty is the only safe answer.
                if (actorUserId is null)
                {
                    return Results.Ok(Array.Empty<RecentPatient>());
                }

                return Results.Ok(
                    await recentPatients
                        .GetRecentPatientsAsync(actorUserId.Value, cancellationToken)
                        .ConfigureAwait(false));
            })
            .WithName("RecentPatients")
            .WithSummary("Lists the patients the signed-in user opened most recently.")
            .Produces<IReadOnlyList<RecentPatient>>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        return app;
    }

    /// <summary>
    /// The account behind the bearer token.
    /// </summary>
    /// <param name="principal">The validated caller.</param>
    /// <returns>The account identifier, or <see langword="null"/> if the token carries no usable subject.</returns>
    /// <remarks>
    /// Reads the registered <c>sub</c> claim rather than
    /// <c>ClaimTypes.NameIdentifier</c>: the bearer handler sets
    /// <c>MapInboundClaims = false</c>, so claims keep the names they were issued
    /// with and the WS-Federation URI form matches nothing.
    ///
    /// Duplicated from ImagingEndpoints rather than shared, because that file is
    /// frozen for this milestone. Three lines in two places is the smaller cost.
    /// </remarks>
    private static Guid? ActorUserId(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out Guid id)
            ? id
            : null;
}
