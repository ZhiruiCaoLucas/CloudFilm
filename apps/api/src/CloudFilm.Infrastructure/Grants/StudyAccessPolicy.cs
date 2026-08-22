using System.Security.Claims;
using CloudFilm.Application.Abstractions;
using CloudFilm.Application.Auth;
using Microsoft.IdentityModel.JsonWebTokens;

namespace CloudFilm.Infrastructure.Grants;

/// <summary>
/// Admits clinical roles as before, and access grants in addition.
/// </summary>
/// <remarks>
/// <para>
/// The order of the two checks is the whole design. A caller holding an
/// application role is admitted first, from claims alone: no database is
/// consulted, no grant is looked for, and the answer is
/// <see cref="StudyAccessScope.FullCatalog"/> - byte for byte the access
/// Administrators and Clinicians have had since Sprint 6. That is what makes
/// grants additive rather than a new model quietly replacing the old one, and
/// it is why enabling this cannot cost anybody access they already had.
/// </para>
/// <para>
/// Only a caller who fails that first check is asked about grants. Today no such
/// caller reaches an endpoint - the application-role policy turns them away with
/// 403 first - so this branch is unreachable in production and is built for the
/// callers a later sprint introduces, which hold a grant and no clinical role.
/// It is wired, tested and inert rather than absent, because retrofitting a
/// resource check onto endpoints later is how the check ends up in the wrong
/// place.
/// </para>
/// <para>
/// Nothing here reaches the archive. Whether a study exists is not part of
/// whether somebody may be told about it, and asking would hand an unauthorised
/// caller a timing signal for free.
/// </para>
/// </remarks>
internal sealed class StudyAccessPolicy(IAccessGrantQuery grants) : IStudyAccessPolicy
{
    private readonly IAccessGrantQuery _grants = grants;

    /// <inheritdoc />
    public async Task<StudyAccessScope> AuthoriseAsync(
        ClaimsPrincipal principal,
        string studyInstanceUid,
        string permission,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(principal);

        // Existing behaviour, unchanged and unconditional. Both roles reach the
        // same studies they always have, and neither pays for a query.
        if (RoleNames.All.Any(principal.IsInRole))
        {
            return StudyAccessScope.FullCatalog;
        }

        if (string.IsNullOrWhiteSpace(studyInstanceUid))
        {
            return StudyAccessScope.Denied;
        }

        Guid? actorUserId = ActorUserId(principal);

        // A grant belongs to an account. A caller whose token names no account
        // cannot hold one, and must not be admitted by default.
        if (actorUserId is null)
        {
            return StudyAccessScope.Denied;
        }

        // Expiry and revocation are conditions of this query, not checks
        // performed on its result.
        bool granted = await _grants
            .HasAccessAsync(actorUserId.Value, studyInstanceUid.Trim(), permission, cancellationToken)
            .ConfigureAwait(false);

        return granted ? StudyAccessScope.GrantedStudyOnly : StudyAccessScope.Denied;
    }

    /// <summary>
    /// The account behind the bearer token.
    /// </summary>
    /// <remarks>
    /// Reads the registered <c>sub</c> claim, because the bearer handler sets
    /// <c>MapInboundClaims = false</c> and claims keep the names they were issued
    /// with.
    /// </remarks>
    private static Guid? ActorUserId(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out Guid id)
            ? id
            : null;
}
