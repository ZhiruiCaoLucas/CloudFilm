using System.Security.Claims;
using System.Text;
using CloudFilm.Application.Abstractions;
using CloudFilm.Application.Auth;
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

    private static readonly Guid _subject = Guid.Parse("11111111-2222-3333-4444-555555555555");

    /// <summary>No roles at all, which is what an unassigned account holds.</summary>
    private static readonly string[] _noRoles = [];

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
            .Issue(_subject, "clinician@example.test", "Seed Administrator", _noRoles);

        JsonWebToken parsed = new(token.Value);

        parsed.GetClaim(JwtRegisteredClaimNames.Sub).Value
            .Should().Be(_subject.ToString());
        parsed.GetClaim(JwtRegisteredClaimNames.Email).Value
            .Should().Be("clinician@example.test");
        parsed.GetClaim(JwtRegisteredClaimNames.Name).Value
            .Should().Be("Seed Administrator");
    }

    [Fact]
    public void IssueSetsTheConfiguredIssuerAndAudience()
    {
        AccessToken token = CreateIssuer(new FixedClock()).Issue(_subject, "a@b.test", "Name", _noRoles);

        JsonWebToken parsed = new(token.Value);

        parsed.Issuer.Should().Be(_issuer);
        parsed.Audiences.Should().ContainSingle().Which.Should().Be(_audience);
    }

    [Fact]
    public void IssueSignsWithHmacSha256AndNeverNone()
    {
        AccessToken token = CreateIssuer(new FixedClock()).Issue(_subject, "a@b.test", "Name", _noRoles);

        new JsonWebToken(token.Value).Alg.Should().Be(SecurityAlgorithms.HmacSha256);
    }

    [Fact]
    public void IssueExpiresAfterTheConfiguredLifetime()
    {
        AccessToken token = CreateIssuer(new FixedClock(), lifetimeMinutes: 60)
            .Issue(_subject, "a@b.test", "Name", _noRoles);

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
            .Issue(_subject, "a@b.test", "Name", _noRoles);

        TokenValidationResult result = await new JsonWebTokenHandler()
            .ValidateTokenAsync(token.Value, ValidationParameters());

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task AnExpiredTokenIsRejected()
    {
        // Issued far enough in the past that the lifetime has elapsed.
        FixedClock clock = new() { UtcNow = DateTimeOffset.UtcNow.AddHours(-3) };

        AccessToken token = CreateIssuer(clock, lifetimeMinutes: 60).Issue(_subject, "a@b.test", "Name", _noRoles);

        TokenValidationResult result = await new JsonWebTokenHandler()
            .ValidateTokenAsync(token.Value, ValidationParameters());

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task ATokenSignedWithADifferentKeyIsRejected()
    {
        AccessToken token = CreateIssuer(new FixedClock { UtcNow = DateTimeOffset.UtcNow })
            .Issue(_subject, "a@b.test", "Name", _noRoles);

        TokenValidationParameters parameters = ValidationParameters();
        parameters.IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes("a-completely-different-key-32-bytes-long"));

        TokenValidationResult result = await new JsonWebTokenHandler()
            .ValidateTokenAsync(token.Value, parameters);

        result.IsValid.Should().BeFalse();
    }

    // -----------------------------------------------------------------------
    // Role claims.
    // -----------------------------------------------------------------------

    /// <summary>
    /// The claim type is the long URI rather than a short <c>role</c>, because
    /// that is what the bearer handler treats as a role and nothing in this
    /// deployment overrides it. A short name would validate and then fail every
    /// authorisation check.
    /// </summary>
    [Theory]
    [InlineData(RoleNames.Administrator)]
    [InlineData(RoleNames.Clinician)]
    public void IssueWritesOneRoleAsARoleClaim(string role)
    {
        AccessToken token = CreateIssuer(new FixedClock())
            .Issue(_subject, "a@b.test", "Name", [role]);

        RolesOf(token).Should().Equal(role);
    }

    [Fact]
    public void IssueWritesEveryRoleTheAccountHolds()
    {
        AccessToken token = CreateIssuer(new FixedClock())
            .Issue(_subject, "a@b.test", "Name", [RoleNames.Administrator, RoleNames.Clinician]);

        RolesOf(token).Should().BeEquivalentTo(RoleNames.All);
    }

    /// <summary>
    /// No roles means no claim, not an empty one. "No role claim" is exactly
    /// what the authorisation policy refuses.
    /// </summary>
    [Fact]
    public void IssueWritesNoRoleClaimForAnAccountWithNoRoles()
    {
        AccessToken token = CreateIssuer(new FixedClock())
            .Issue(_subject, "a@b.test", "Name", _noRoles);

        RolesOf(token).Should().BeEmpty();
    }

    /// <summary>
    /// Roles are their own claim and must never be smuggled into an identity
    /// claim that carries one value.
    /// </summary>
    [Fact]
    public void IssueKeepsTheExistingClaimsUnchangedWhenRolesArePresent()
    {
        AccessToken token = CreateIssuer(new FixedClock())
            .Issue(_subject, "clinician@example.test", "Dr. Example", [RoleNames.Clinician]);

        JsonWebToken parsed = new(token.Value);

        parsed.GetClaim(JwtRegisteredClaimNames.Sub).Value.Should().Be(_subject.ToString());
        parsed.GetClaim(JwtRegisteredClaimNames.Email).Value.Should().Be("clinician@example.test");
        parsed.GetClaim(JwtRegisteredClaimNames.Name).Value.Should().Be("Dr. Example");
        parsed.GetClaim(JwtRegisteredClaimNames.Jti).Value.Should().NotBeNullOrWhiteSpace();

        parsed.GetClaim(JwtRegisteredClaimNames.Email).Value.Should().NotContain(RoleNames.Clinician);
        parsed.GetClaim(JwtRegisteredClaimNames.Name).Value.Should().NotContain(RoleNames.Clinician);
        parsed.GetClaim(JwtRegisteredClaimNames.Sub).Value.Should().NotContain(RoleNames.Clinician);
    }

    /// <summary>
    /// The claims the API will authorise from are the ones a validated token
    /// produces, not the ones written into the payload - so this reads them back
    /// through validation, with inbound mapping off exactly as the API has it.
    /// </summary>
    [Fact]
    public async Task AValidatedTokenCarriesTheRolesAsRoleClaims()
    {
        AccessToken token = CreateIssuer(new FixedClock { UtcNow = DateTimeOffset.UtcNow })
            .Issue(_subject, "a@b.test", "Name", [RoleNames.Administrator]);

        JsonWebTokenHandler handler = new() { MapInboundClaims = false };

        TokenValidationResult result = await handler
            .ValidateTokenAsync(token.Value, ValidationParameters());

        result.IsValid.Should().BeTrue();
        result.ClaimsIdentity.FindAll(ClaimTypes.Role).Select(claim => claim.Value)
            .Should().Equal(RoleNames.Administrator);
        result.ClaimsIdentity.RoleClaimType.Should().Be(ClaimTypes.Role);
    }

    /// <summary>Reads the role claims out of an unvalidated token payload.</summary>
    private static List<string> RolesOf(AccessToken token) =>
        new JsonWebToken(token.Value)
            .Claims
            .Where(claim => claim.Type == ClaimTypes.Role)
            .Select(claim => claim.Value)
            .ToList();
}
