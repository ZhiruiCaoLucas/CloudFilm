using CloudFilm.Application.Abstractions;
using CloudFilm.Application.Auditing;
using CloudFilm.Application.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace CloudFilm.Infrastructure.Identity;

/// <summary>
/// Verifies credentials with ASP.NET Identity and issues a token on success.
/// </summary>
/// <remarks>
/// <para>
/// Uses <see cref="SignInManager{TUser}.CheckPasswordSignInAsync"/> rather than
/// <c>UserManager.CheckPasswordAsync</c>. That is not a style preference: the
/// configured <c>Lockout.MaxFailedAccessAttempts</c> and
/// <c>SignIn.RequireConfirmedEmail</c> are enforced by the sign-in manager and
/// by nothing else. Comparing the hash directly would leave both settings
/// written down but never applied - a lockout policy that does not lock out.
/// </para>
/// <para>
/// Every failure returns <see langword="null"/>. The four distinguishable
/// outcomes - no such account, wrong password, unconfirmed e-mail, locked out -
/// are collapsed here rather than at the endpoint, so no future caller can
/// reintroduce the disclosure by handling them separately.
/// </para>
/// </remarks>
internal sealed class IdentityAuthenticationService(
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager,
    IAccessTokenIssuer tokenIssuer,
    IAuditLogService auditLog,
    ILogger<IdentityAuthenticationService> logger) : IAuthenticationService
{
    /// <summary>
    /// A user that does not exist, used only to give the password hasher
    /// something to chew on when the account is unknown.
    /// </summary>
    private static readonly ApplicationUser _timingDecoy = new() { UserName = "timing-decoy" };


    private readonly SignInManager<ApplicationUser> _signInManager = signInManager;
    private readonly UserManager<ApplicationUser> _userManager = userManager;
    private readonly IAccessTokenIssuer _tokenIssuer = tokenIssuer;
    private readonly IAuditLogService _auditLog = auditLog;
    private readonly ILogger<IdentityAuthenticationService> _logger = logger;

    /// <summary>
    /// A real hash of a value nobody knows, computed once per process.
    /// </summary>
    /// <remarks>
    /// Returning early for an unknown account answers in microseconds while a
    /// wrong password costs a full PBKDF2 verification, and that difference is
    /// measurable over the network - an account enumeration oracle that
    /// identical response bodies do not close. Verifying against this hash makes
    /// the unknown-account path do the same work.
    /// </remarks>
    private readonly string _timingDecoyHash =
        userManager.PasswordHasher.HashPassword(_timingDecoy, Guid.NewGuid().ToString());

    /// <inheritdoc />
    public async Task<LoginResult?> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string email = request.Email.Trim();

        ApplicationUser? user = await _userManager.FindByEmailAsync(email).ConfigureAwait(false);

        if (user is null)
        {
            _userManager.PasswordHasher.VerifyHashedPassword(
                _timingDecoy,
                _timingDecoyHash,
                request.Password);

            _logger.LogInformation("Sign-in rejected: no account matched the supplied address.");

            // No actor: nobody owns this address. The address itself is not
            // recorded - a row saying "failed sign-in, no actor, alice@x.test"
            // would tell any reader of the table that alice@x.test does not
            // exist, which is the disclosure this whole path avoids.
            await AuditAsync(null, AuditAction.LoginFailed, cancellationToken).ConfigureAwait(false);

            return null;
        }

        SignInResult result = await _signInManager
            .CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true)
            .ConfigureAwait(false);

        if (!result.Succeeded)
        {
            // The account identifier, never the address or the password. It is
            // enough to investigate a lockout without putting a credential or a
            // personal identifier in the log.
            _logger.LogInformation(
                "Sign-in rejected for {UserId}: lockedOut={LockedOut}, notAllowed={NotAllowed}.",
                user.Id,
                result.IsLockedOut,
                result.IsNotAllowed);

            // One action for all four rejection reasons. The account exists, so it
            // has an actor - but which of wrong password, lockout or unconfirmed
            // e-mail occurred is deliberately not written down.
            await AuditAsync(user.Id, AuditAction.LoginFailed, cancellationToken).ConfigureAwait(false);

            return null;
        }

        AccessToken token = _tokenIssuer.Issue(
            user.Id.ToString(),
            user.Email ?? email,
            user.DisplayName ?? email);

        _logger.LogInformation("Sign-in succeeded for {UserId}.", user.Id);

        await AuditAsync(user.Id, AuditAction.LoginSucceeded, cancellationToken).ConfigureAwait(false);

        return new LoginResult(token.Value, token.ExpiresAt, user.DisplayName ?? email);
    }

    /// <summary>
    /// Records a sign-in outcome without letting the audit trail change it.
    /// </summary>
    /// <remarks>
    /// Swallows write failures on purpose, and this is the one place in CloudFilm
    /// where that is correct. If an audit outage turned a valid sign-in into a
    /// rejection, the login endpoint would become a probe for the audit
    /// database's health, and a clinician with the right password would be told
    /// it was wrong. The failure is logged as an error so it is visible to
    /// operators, who can then decide whether to take the service out of use.
    ///
    /// PHI access takes the opposite decision - see ImagingEndpoints.
    /// </remarks>
    private async Task AuditAsync(Guid? actorUserId, string action, CancellationToken cancellationToken)
    {
        try
        {
            await _auditLog
                .RecordAsync(
                    new AuditEntry(actorUserId, action, AuditResourceType.Authentication, ResourceId: null),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "Could not record a {Action} audit event.", action);
        }
    }
}
