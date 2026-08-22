namespace CloudFilm.Application.Sharing;

/// <summary>
/// The credential a browser presents to the imaging proxy.
/// </summary>
/// <remarks>
/// <para>
/// One mechanism for both kinds of viewer. A clinician opening the workspace and
/// a stranger opening a share link end up holding the same thing: an opaque
/// identifier, in a cookie, bound to exactly one study. The gateway in front of
/// DICOMweb therefore has one rule to enforce rather than two, and neither kind
/// of caller can reach a study their session does not name.
/// </para>
/// <para>
/// Short-lived on purpose. A viewer session outlives a page load and little
/// else; anything longer would turn a cookie left in a browser into standing
/// access to a patient's imaging.
/// </para>
/// </remarks>
public static class ViewerSessions
{
    /// <summary>
    /// The cookie the browser sends to the imaging gateway.
    /// </summary>
    /// <remarks>
    /// Named here so the endpoint that reads it and the portal that writes it
    /// agree. Cookies ignore ports, which is what lets one written by the portal
    /// reach the viewer on another port of the same host - and what makes a
    /// shared parent domain a deployment requirement when the two are on
    /// different hostnames.
    /// </remarks>
    public const string CookieName = "cloudfilm_viewer";

    /// <summary>
    /// How long a viewer session stands.
    /// </summary>
    /// <remarks>
    /// Fifteen minutes: long enough to open a study and read it, short enough
    /// that a forgotten browser is not a lasting grant. A share link's own expiry
    /// still applies on top - see <see cref="ViewerSessionState.ShareLinkId"/>.
    /// </remarks>
    public static TimeSpan Lifetime { get; } = TimeSpan.FromMinutes(15);
}

/// <summary>
/// A viewer session, as the caller that created it is told about it.
/// </summary>
/// <param name="SessionId">The opaque identifier to put in the cookie.</param>
/// <param name="StudyInstanceUid">The one study this session admits.</param>
/// <param name="Permission">What the session permits, from <c>GrantPermissions</c>.</param>
/// <param name="ExpiresAtUtc">When it stops working.</param>
public sealed record ViewerSession(
    string SessionId,
    string StudyInstanceUid,
    string Permission,
    DateTimeOffset ExpiresAtUtc);

/// <summary>
/// What a viewer session is bound to, as the gateway reads it back.
/// </summary>
/// <remarks>
/// Carries no patient, no name and no record number. The gateway compares a
/// study identifier and a permission and needs nothing else, so nothing else is
/// stored where a cache dump would find it.
/// </remarks>
/// <param name="StudyInstanceUid">The one study this session admits.</param>
/// <param name="Permission">What the session permits.</param>
/// <param name="ShareLinkId">
/// The share link the session came from, or <see langword="null"/> when it was
/// created for a signed-in clinician. Kept so the gateway can ask, on every
/// request, whether that link still stands: without it a revoked link would keep
/// working until the session expired, and revocation that takes up to a quarter
/// of an hour is not revocation.
/// </param>
public sealed record ViewerSessionState(
    string StudyInstanceUid,
    string Permission,
    Guid? ShareLinkId);
