using System.Text;
using CloudFilm.Application.Abstractions;
using CloudFilm.Infrastructure.Configuration;
using CloudFilm.Infrastructure.Identity;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace CloudFilm.Api.Tests.Auth;

/// <summary>
/// The token issuer, exercised against the same validation parameters the API
/// configures. A token that this suite accepts is a token the bearer handler
/// accepts, because both use one <see cref="JsonWebTokenHandler"/>.
/// </summary>
public sealed class JwtAccessTokenIssuerTests
{
    private const string _signingKey = "unit-test-signing-key-of-at-least-32-bytes";
    private const string _issuer = "https://cloudfilm.test";
    private const string _audience = "cloudfilm-test-audience";

    private static readonly DateTimeOffset _now = new(2026, 8, 9, 12, 0, 0, TimeSpan.Zero);

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = _now;
    }

    private static JwtAccessTokenIssuer CreateIssuer(IClock clock, int lifetimeMinutes = 60) =>
        new(
            Options.Create(new JwtOptions
            {
                Issuer = _issuer,
                Audience = _audience,
                SigningKey = _signingKey,
                AccessTokenLifetimeMinutes = lifetimeMinutes,
            }),
            clock);

    private static TokenValidationParameters ValidationParameters() => new()
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = _issuer,
        ValidAudience = _audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_signingKey)),
        ClockSkew = TimeSpan.Zero,
    };

    [Fact]
    public void IssueWritesTheSubjectEmailAndNameClaims()
    {
        AccessToken token = CreateIssuer(new FixedClock())
            .Issue("11111111-2222-3333-4444-555555555555", "clinician@example.test", "Administrator");

        JsonWebToken parsed = new(token.Value);

        parsed.GetClaim(JwtRegisteredClaimNames.Sub).Value
            .Should().Be("11111111-2222-3333-4444-555555555555");
        parsed.GetClaim(JwtRegisteredClaimNames.Email).Value
            .Should().Be("clinician@example.test");
        parsed.GetClaim(JwtRegisteredClaimNames.Name).Value
            .Should().Be("Administrator");
    }

    [Fact]
    public void IssueSetsTheConfiguredIssuerAndAudience()
    {
        AccessToken token = CreateIssuer(new FixedClock()).Issue("id", "a@b.test", "Name");

        JsonWebToken parsed = new(token.Value);

        parsed.Issuer.Should().Be(_issuer);
        parsed.Audiences.Should().ContainSingle().Which.Should().Be(_audience);
    }

    [Fact]
    public void IssueSignsWithHmacSha256AndNeverNone()
    {
        AccessToken token = CreateIssuer(new FixedClock()).Issue("id", "a@b.test", "Name");

        new JsonWebToken(token.Value).Alg.Should().Be(SecurityAlgorithms.HmacSha256);
    }

    [Fact]
    public void IssueExpiresAfterTheConfiguredLifetime()
    {
        AccessToken token = CreateIssuer(new FixedClock(), lifetimeMinutes: 60)
            .Issue("id", "a@b.test", "Name");

        JsonWebToken parsed = new(token.Value);

        // iat and exp are whole seconds in a JWT, so compare at that resolution.
        parsed.IssuedAt.Should().BeCloseTo(_now.UtcDateTime, TimeSpan.FromSeconds(1));
        parsed.ValidTo.Should().BeCloseTo(_now.AddMinutes(60).UtcDateTime, TimeSpan.FromSeconds(1));
        token.ExpiresAt.Should().Be(_now.AddMinutes(60));
    }

    [Fact]
    public async Task AFreshlyIssuedTokenValidates()
    {
        AccessToken token = CreateIssuer(new FixedClock { UtcNow = DateTimeOffset.UtcNow })
            .Issue("id", "a@b.test", "Name");

        TokenValidationResult result = await new JsonWebTokenHandler()
            .ValidateTokenAsync(token.Value, ValidationParameters());

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task AnExpiredTokenIsRejected()
    {
        // Issued far enough in the past that the lifetime has elapsed.
        FixedClock clock = new() { UtcNow = DateTimeOffset.UtcNow.AddHours(-3) };

        AccessToken token = CreateIssuer(clock, lifetimeMinutes: 60).Issue("id", "a@b.test", "Name");

        TokenValidationResult result = await new JsonWebTokenHandler()
            .ValidateTokenAsync(token.Value, ValidationParameters());

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task ATokenSignedWithADifferentKeyIsRejected()
    {
        AccessToken token = CreateIssuer(new FixedClock { UtcNow = DateTimeOffset.UtcNow })
            .Issue("id", "a@b.test", "Name");

        TokenValidationParameters parameters = ValidationParameters();
        parameters.IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes("a-completely-different-key-32-bytes-long"));

        TokenValidationResult result = await new JsonWebTokenHandler()
            .ValidateTokenAsync(token.Value, parameters);

        result.IsValid.Should().BeFalse();
    }
}
