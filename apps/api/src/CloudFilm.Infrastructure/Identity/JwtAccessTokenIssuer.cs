using System.Security.Claims;
using System.Text;
using CloudFilm.Application.Abstractions;
using CloudFilm.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;


namespace CloudFilm.Infrastructure.Identity;

/// <summary>
/// Issues HMAC-SHA256 signed JSON Web Tokens.
/// </summary>
/// <remarks>
/// <para>
/// The signing key comes from configuration and nowhere else. There is no
/// fallback and no generated key: a process that starts with a random key would
/// silently invalidate every token it issued the moment it restarted, and would
/// hide a missing secret instead of failing. <see cref="JwtOptions"/> is
/// validated on start-up, so a missing or short key stops the host.
/// </para>
/// <para>
/// Claim names are the registered short forms - <c>sub</c>, <c>email</c>,
/// <c>name</c> - to match <c>MapInboundClaims = false</c> on the bearer handler.
/// With mapping off the validating side sees exactly these names rather than the
/// long WS-Federation URIs, so what is written here is what
/// <c>HttpContext.User</c> reads back.
/// </para>
/// <para>
/// Roles are the one exception, and deliberately so. They are written as
/// <see cref="ClaimTypes.Role"/> - the long URI - because that is the claim type
/// the bearer handler's <c>TokenValidationParameters</c> treats as a role, and
/// nothing in this deployment overrides it. Writing the short <c>role</c> instead
/// would produce a token that looks correct, validates correctly, and quietly
/// fails every <c>RequireRole</c> and <c>IsInRole</c> check, because with
/// inbound mapping off nothing would rename it. The verbose claim name is worth
/// an authorisation system that works by construction rather than by
/// configuration nobody remembers to set.
/// </para>
/// </remarks>
internal sealed class JwtAccessTokenIssuer(IOptions<JwtOptions> options, IClock clock) : IAccessTokenIssuer
{
    private readonly JwtOptions _options = options.Value;
    private readonly IClock _clock = clock;
    private readonly JsonWebTokenHandler _handler = new();

    /// <inheritdoc />
    public AccessToken Issue(
    Guid userId,
    string email,
    string displayName,
    IReadOnlyList<string> roles)
{
    ArgumentNullException.ThrowIfNull(roles);

    DateTimeOffset issuedAt = _clock.UtcNow;
    DateTimeOffset expiresAt = issuedAt.AddMinutes(_options.AccessTokenLifetimeMinutes);

    SigningCredentials credentials = new(
        new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey)),
        SecurityAlgorithms.HmacSha256);

    Dictionary<string, object> claims = new(StringComparer.Ordinal)
    {
        [JwtRegisteredClaimNames.Sub] = userId.ToString(),
        [JwtRegisteredClaimNames.Email] = email,
        [JwtRegisteredClaimNames.Name] = displayName,
        [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString(),
    };

    // An account with no roles gets no role claim at all.
    if (roles.Count > 0)
    {
        claims[ClaimTypes.Role] = roles.ToArray();
    }

    SecurityTokenDescriptor descriptor = new()
    {
        Issuer = _options.Issuer,
        Audience = _options.Audience,
        IssuedAt = issuedAt.UtcDateTime,
        NotBefore = issuedAt.UtcDateTime,
        Expires = expiresAt.UtcDateTime,
        SigningCredentials = credentials,
        Claims = claims,
    };

    return new AccessToken(_handler.CreateToken(descriptor), expiresAt);
}
}
