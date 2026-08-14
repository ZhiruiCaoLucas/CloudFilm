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
    /// <param name="roles">
    /// The application roles the account holds, written as role claims. Empty
    /// when it holds none.
    /// </param>
    /// <returns>The signed token and the instant it expires.</returns>
    /// <remarks>
    /// <para>
    /// The subject is a <see cref="Guid"/> rather than a string. Every token
    /// CloudFilm issues is for an account, and an account is identified by a
    /// Guid; taking the string form here would let a caller mint a token whose
    /// subject nothing downstream can resolve to an account, which is precisely
    /// the state every endpoint has to defend against.
    /// </para>
    /// <para>
    /// Roles are a required argument with no default and no overload that omits
    /// them. A caller that has not decided what a token may do has not finished
    /// deciding whether to issue one, and a defaulted empty list would make
    /// "this account has no roles" indistinguishable from "nobody looked".
    /// </para>
    /// </remarks>
    public AccessToken Issue(
        Guid userId,
        string email,
        string displayName,
        IReadOnlyList<string> roles);
}

/// <summary>
/// A signed access token and its expiry.
/// </summary>
/// <param name="Value">The compact serialised JWT.</param>
/// <param name="ExpiresAt">When the token stops being accepted, in UTC.</param>
public sealed record AccessToken(string Value, DateTimeOffset ExpiresAt);
