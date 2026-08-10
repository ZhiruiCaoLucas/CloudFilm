namespace CloudFilm.Application.Abstractions;

/// <summary>
/// Mints signed access tokens.
/// </summary>
/// <remarks>
/// Separate from <see cref="IAuthenticationService"/> because the two answer
/// different questions - "are these credentials good?" and "what does a token
/// for this subject look like?" - and because token format is the part most
/// likely to change. Moving from a symmetric key to asymmetric signing, or
/// adding claims, should not touch credential verification.
/// </remarks>
public interface IAccessTokenIssuer
{
    /// <summary>
    /// Issues an access token for a subject.
    /// </summary>
    /// <param name="userId">The account identifier, written to the <c>sub</c> claim.</param>
    /// <param name="email">The e-mail address, written to the <c>email</c> claim.</param>
    /// <param name="displayName">The display name, written to the <c>name</c> claim.</param>
    /// <returns>The signed token and the instant it expires.</returns>
    public AccessToken Issue(string userId, string email, string displayName);
}

/// <summary>
/// A signed access token and its expiry.
/// </summary>
/// <param name="Value">The compact serialised JWT.</param>
/// <param name="ExpiresAt">When the token stops being accepted, in UTC.</param>
public sealed record AccessToken(string Value, DateTimeOffset ExpiresAt);
