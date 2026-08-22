using System.Security.Claims;

namespace CloudFilm.Application.Abstractions;

/// <summary>
/// How much of a study a caller may be shown.
/// </summary>
/// <remarks>
/// Three outcomes, and the middle one is why this type exists: a caller may be
/// entitled to one study without being entitled to the catalogue it sits in.
/// </remarks>
public enum StudyAccessScope
{
    /// <summary>
    /// The caller may not be told anything about this study, including whether
    /// it exists.
    /// </summary>
    Denied = 0,

    /// <summary>
    /// The caller may see the study and the context it normally comes with -
    /// the patient and their other studies.
    /// </summary>
    /// <remarks>
    /// What every account holding an application role gets today, and what they
    /// have always got. Resource-level grants add to this model; they do not
    /// replace it.
    /// </remarks>
    FullCatalog = 1,

    /// <summary>
    /// The caller may see this study and nothing else about the patient's
    /// imaging history.
    /// </summary>
    /// <remarks>
    /// Reached by holding an access grant rather than a clinical role. The
    /// study's siblings are not part of what was granted, so their identifiers
    /// must not travel in the response - a Study Instance UID is enough to open
    /// a study in the viewer.
    /// </remarks>
    GrantedStudyOnly = 2,
}

/// <summary>
/// Decides how much of a study a caller may be shown, before anything is read.
/// </summary>
/// <remarks>
/// <para>
/// Sits between the authorisation policies, which ask whether a caller may use
/// this part of the application at all, and the imaging catalogue, which knows
/// nothing about who is asking. Its answer is reached without contacting the
/// archive, so a caller who may not see a study never causes a query about one.
/// </para>
/// <para>
/// It only ever adds. An account holding an application role is admitted
/// exactly as before, without a database round trip and without consulting a
/// grant; grants are what let somebody in who would otherwise be refused.
/// Nothing here can take access away from a caller who had it.
/// </para>
/// </remarks>
public interface IStudyAccessPolicy
{
    /// <summary>
    /// Decides what a caller may be shown of one study.
    /// </summary>
    /// <param name="principal">The authenticated caller.</param>
    /// <param name="studyInstanceUid">The study being asked for.</param>
    /// <param name="permission">
    /// The permission the request needs, from <c>GrantPermissions</c>.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>How much of the study the caller may be shown.</returns>
    /// <remarks>
    /// A refusal is <see cref="StudyAccessScope.Denied"/> rather than an
    /// exception, because the caller has to answer it the same way it answers a
    /// study that does not exist. Anything else would let the difference between
    /// the two be measured.
    /// </remarks>
    public Task<StudyAccessScope> AuthoriseAsync(
        ClaimsPrincipal principal,
        string studyInstanceUid,
        string permission,
        CancellationToken cancellationToken = default);
}
