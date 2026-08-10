using CloudFilm.Application.Auth;

namespace CloudFilm.Application.Abstractions;

/// <summary>
/// Verifies credentials and issues an access token.
/// </summary>
/// <remarks>
/// The port the sign-in endpoint depends on. Its implementation owns ASP.NET
/// Identity; nothing above this interface knows that Identity exists, which is
/// what keeps <c>ApplicationUser</c> out of the transport contract.
/// </remarks>
public interface IAuthenticationService
{
    /// <summary>
    /// Authenticates a set of credentials.
    /// </summary>
    /// <param name="request">The credentials to verify.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// The issued token, or <see langword="null"/> when the credentials are not
    /// accepted.
    /// </returns>
    /// <remarks>
    /// One <see langword="null"/> for every failure, on purpose. An unknown
    /// account, a wrong password, an unconfirmed e-mail address and a locked-out
    /// account are indistinguishable to the caller, so no endpoint built on this
    /// port can leak which of them occurred. The reason is recorded in the log,
    /// where only an operator can see it.
    /// </remarks>
    public Task<LoginResult?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
}
