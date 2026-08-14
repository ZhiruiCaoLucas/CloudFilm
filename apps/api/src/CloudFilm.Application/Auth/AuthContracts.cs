namespace CloudFilm.Application.Auth;

/// <summary>
/// A sign-in attempt.
/// </summary>
/// <param name="Email">The e-mail address the account was registered with.</param>
/// <param name="Password">The password, in clear text over TLS. Never logged, never stored.</param>
public sealed record LoginRequest(string Email, string Password);

/// <summary>
/// A successful sign-in.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately narrow. The ASP.NET Identity user carries a password hash, a
/// security stamp, a concurrency stamp and lockout state; none of it is any of
/// the client's business, so no mapping from <c>ApplicationUser</c> exists and
/// the type cannot accidentally grow one.
/// </para>
/// <para>
/// <see cref="Roles"/> is an initialised property rather than a fourth
/// positional field, so every existing construction of this record still
/// compiles and still means what it meant. That matters more than symmetry: a
/// contract change that forces edits to the code paths issuing tokens is a
/// contract change that cannot be reviewed on its own.
/// </para>
/// </remarks>
/// <param name="AccessToken">The signed JWT to send as a bearer token.</param>
/// <param name="ExpiresAt">When the token stops being accepted, in UTC.</param>
/// <param name="DisplayName">The name to show in the portal.</param>
public sealed record LoginResult(string AccessToken, DateTimeOffset ExpiresAt, string DisplayName)
{
    /// <summary>
    /// The application roles the account holds.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Empty when the account holds none, never absent and never null. An
    /// account with no role is a real and meaningful state - it authenticates
    /// and is refused everything else - so it is represented as an empty list
    /// rather than as missing information a caller has to guess at.
    /// </para>
    /// <para>
    /// Values come from <see cref="RoleNames"/>. A client may use them to decide
    /// what to show; nothing may use them to decide what is allowed. That
    /// decision is the API's, on every request, from the token it validated.
    /// </para>
    /// </remarks>
    public IReadOnlyList<string> Roles { get; init; } = [];
}

/// <summary>
/// The signed-in user, as resolved from a bearer token.
/// </summary>
/// <remarks>
/// Built from token claims alone, so reading it costs no database round trip.
/// That is the point of putting these three values in the token.
/// </remarks>
/// <param name="UserId">The account identifier, from the <c>sub</c> claim.</param>
/// <param name="Email">The e-mail address, from the <c>email</c> claim.</param>
/// <param name="DisplayName">The display name, from the <c>name</c> claim.</param>
public sealed record CurrentUser(string UserId, string Email, string DisplayName)
{
    /// <summary>
    /// The application roles carried by the bearer token.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Read from the token like the other three values, so resolving the current
    /// user still costs no database round trip. The price of that is staleness:
    /// a role granted or withdrawn after a token was issued is not reflected
    /// until the token expires, which is the accepted trade for not querying the
    /// database on every request.
    /// </para>
    /// <para>
    /// For display only. A portal may hide a control the roles do not cover, but
    /// hiding a control is not access control - the endpoint behind it enforces
    /// the same rule again, and it is the one that counts.
    /// </para>
    /// </remarks>
    public IReadOnlyList<string> Roles { get; init; } = [];
}
