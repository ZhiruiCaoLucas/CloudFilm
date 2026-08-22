namespace CloudFilm.Application.Sharing;

/// <summary>
/// The bounds a share link is stored and issued within.
/// </summary>
public static class ShareLinkLimits
{
    /// <summary>
    /// The longest Study Instance UID a share link will accept.
    /// </summary>
    /// <remarks>
    /// Matches <c>AuditLog.ResourceId</c>, <c>StudyReadState.StudyInstanceUid</c>
    /// and <c>AccessGrant.StudyInstanceUid</c>, which already store these exact
    /// strings. DICOM caps a UID at 64 characters; 128 leaves room without
    /// letting an unbounded string into a table that is indexed on it.
    /// </remarks>
    public const int MaxStudyInstanceUidLength = 128;

    /// <summary>The longest permission name the column holds.</summary>
    public const int MaxPermissionLength = 32;

    /// <summary>
    /// The length of the stored token digest, in bytes.
    /// </summary>
    /// <remarks>
    /// SHA-256, so always 32. PostgreSQL's <c>bytea</c> has no fixed-length
    /// form, so this is a fact about what is written rather than a constraint
    /// the database enforces - which is why it is stated here, once, for the
    /// code that writes and reads it.
    /// </remarks>
    public const int TokenHashLength = 32;

    /// <summary>
    /// The longest life a share link may be given.
    /// </summary>
    /// <remarks>
    /// A ceiling on how long a bearer credential for patient imaging can exist,
    /// not a default. It is stated here so that the endpoint issuing links and
    /// any test of that endpoint read the same number; enforcing it belongs to
    /// the endpoint, because refusing a request is an HTTP decision and this
    /// layer only stores what it is given.
    /// </remarks>
    public static TimeSpan MaximumLifetime { get; } = TimeSpan.FromDays(7);
}

/// <summary>
/// A share link, as the application reports it.
/// </summary>
/// <remarks>
/// Carries no token and no digest of one. The token exists in exactly two
/// places: the response that created it, once, and the recipient's URL. Nothing
/// that lists or inspects links can hand it back, because there is nowhere in
/// this shape to put it.
/// </remarks>
/// <param name="Id">The link's identifier, and what a revocation names.</param>
/// <param name="StudyInstanceUid">The study the link leads to.</param>
/// <param name="Permission">What the link permits, from <c>GrantPermissions</c>.</param>
/// <param name="CreatedAtUtc">When the link was issued.</param>
/// <param name="ExpiresAtUtc">When it stops working. Never absent.</param>
/// <param name="RevokedAtUtc">
/// When it was withdrawn, or <see langword="null"/> while it stands.
/// </param>
public sealed record ShareLinkSummary(
    Guid Id,
    string StudyInstanceUid,
    string Permission,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    DateTimeOffset? RevokedAtUtc);

/// <summary>
/// What a working share link leads to.
/// </summary>
/// <remarks>
/// Deliberately the smallest answer a redemption needs: which study, and what
/// may be done with it. A caller that receives this has already been told the
/// link is live - there is no state here to re-check, and therefore no way for a
/// caller to forget to.
/// </remarks>
/// <param name="ShareLinkId">The link that was redeemed, for the audit trail.</param>
/// <param name="StudyInstanceUid">The study the link leads to.</param>
/// <param name="Permission">What the link permits.</param>
public sealed record ShareLinkTarget(Guid ShareLinkId, string StudyInstanceUid, string Permission);

/// <summary>
/// A request to issue a share link.
/// </summary>
/// <remarks>
/// There is deliberately nowhere here to say who is issuing the link, and
/// nowhere to supply a token. The issuer comes from the bearer token on the
/// request and the credential is minted by the server, so a client can neither
/// attribute a link to somebody else nor choose a value it already knows. The
/// shape is the enforcement, not a check that could be forgotten.
/// </remarks>
/// <param name="StudyInstanceUid">The study the link should lead to.</param>
/// <param name="Permission">
/// What the link permits. One of the two stored permissions:
/// <c>GrantPermissions.View</c> issues a link that opens the study in the viewer
/// and is refused by the download route, and <c>GrantPermissions.Download</c>
/// issues one that does both - viewing is covered by the existing permission
/// ladder, in which Download satisfies View. Anything else is refused.
/// </param>
/// <param name="ExpiresAtUtc">
/// When the link should stop working. Required, and capped at
/// <see cref="ShareLinkLimits.MaximumLifetime"/>.
/// </param>
public sealed record CreateShareLinkRequest(
    string StudyInstanceUid,
    string Permission,
    DateTimeOffset ExpiresAtUtc);

/// <summary>
/// A newly issued share link, including its token.
/// </summary>
/// <remarks>
/// <para>
/// The only shape in CloudFilm that carries a share token, and it is returned
/// exactly once - from the request that created the link. The database holds a
/// SHA-256 digest, so nothing can reconstruct this value afterwards: a token
/// its holder loses is re-issued, not recovered.
/// </para>
/// <para>
/// No URL. The API has no configured public address of its own, and deriving one
/// from the request's Host header would produce an internal name behind a
/// reverse proxy and a forgeable one everywhere else. Whoever displays the link
/// knows where it lives; this only says what to put in it.
/// </para>
/// </remarks>
/// <param name="Id">The link's identifier, and what a revocation names.</param>
/// <param name="StudyInstanceUid">The study the link leads to.</param>
/// <param name="Permission">What the link permits.</param>
/// <param name="CreatedAtUtc">When the link was issued.</param>
/// <param name="ExpiresAtUtc">When it stops working.</param>
/// <param name="Token">
/// The token, in plaintext. Shown once; never stored, logged or audited.
/// </param>
public sealed record CreatedShareLink(
    Guid Id,
    string StudyInstanceUid,
    string Permission,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    string Token);
