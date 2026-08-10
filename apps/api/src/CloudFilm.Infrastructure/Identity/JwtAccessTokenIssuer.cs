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
/// </remarks>
internal sealed class JwtAccessTokenIssuer(IOptions<JwtOptions> options, IClock clock) : IAccessTokenIssuer
{
    private readonly JwtOptions _options = options.Value;
    private readonly IClock _clock = clock;
    private readonly JsonWebTokenHandler _handler = new();

    /// <inheritdoc />
    public AccessToken Issue(string userId, string email, string displayName)
    {
        DateTimeOffset issuedAt = _clock.UtcNow;
        DateTimeOffset expiresAt = issuedAt.AddMinutes(_options.AccessTokenLifetimeMinutes);

        SigningCredentials credentials = new(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey)),
            SecurityAlgorithms.HmacSha256);

        SecurityTokenDescriptor descriptor = new()
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = issuedAt.UtcDateTime,
            NotBefore = issuedAt.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = credentials,
            Claims = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                [JwtRegisteredClaimNames.Sub] = userId,
                [JwtRegisteredClaimNames.Email] = email,
                [JwtRegisteredClaimNames.Name] = displayName,
                [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString(),
            },
        };

        return new AccessToken(_handler.CreateToken(descriptor), expiresAt);
    }
}
