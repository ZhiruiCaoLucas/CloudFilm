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
/// Deliberately three fields. The ASP.NET Identity user carries a password hash,
/// a security stamp, a concurrency stamp and lockout state; none of it is any of
/// the client's business, so no mapping from <c>ApplicationUser</c> exists and
/// the type cannot accidentally grow one.
/// </remarks>
/// <param name="AccessToken">The signed JWT to send as a bearer token.</param>
/// <param name="ExpiresAt">When the token stops being accepted, in UTC.</param>
/// <param name="DisplayName">The name to show in the portal.</param>
public sealed record LoginResult(string AccessToken, DateTimeOffset ExpiresAt, string DisplayName);

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
public sealed record CurrentUser(string UserId, string Email, string DisplayName);
