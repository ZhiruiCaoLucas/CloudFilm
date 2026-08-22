using System.Security.Claims;
using CloudFilm.Application.Abstractions;
using CloudFilm.Application.Auth;
using CloudFilm.Application.Grants;
using CloudFilm.Infrastructure.Grants;
using FluentAssertions;
using Microsoft.IdentityModel.JsonWebTokens;
using NSubstitute;

namespace CloudFilm.Api.Tests.Grants;

/// <summary>
/// How much of a study a caller may be shown, decided before anything is read.
/// </summary>
/// <remarks>
/// The guarantee this suite exists for is the first one: a clinical role is
/// admitted exactly as it was before grants existed, without a database query
/// and without a grant. Everything else here is the additional path grants open
/// up, which today no caller reaches - the application-role policy turns a
/// roleless account away first - and which a later sprint will.
/// </remarks>
public sealed class StudyAccessPolicyTests
{
    private const string _study = "1.2.840.113564.1";

    private static readonly Guid _actor = Guid.Parse("11111111-2222-3333-4444-555555555555");

    private readonly IAccessGrantQuery _grants = Substitute.For<IAccessGrantQuery>();

    private StudyAccessPolicy CreatePolicy() => new(_grants);

    /// <summary>Builds a principal shaped like a validated CloudFilm token.</summary>
    /// <remarks>
    /// <c>ClaimTypes.Role</c> and the registered <c>sub</c>, because that is what
    /// the issuer writes and what the bearer handler reads back with inbound
    /// mapping off.
    /// </remarks>
    private static ClaimsPrincipal APrincipal(Guid? subject = null, params string[] roles)
    {
        List<Claim> claims = [];

        if (subject is not null)
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Sub, subject.Value.ToString()));
        }

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test", ClaimTypes.Name, ClaimTypes.Role));
    }

    // -----------------------------------------------------------------------
    // Existing access is untouched.
    // -----------------------------------------------------------------------

    /// <summary>
    /// The whole point of an additive model: enabling grants cost nobody the
    /// access they already had, and cost them no database round trip either.
    /// </summary>
    [Theory]
    [InlineData(RoleNames.Administrator)]
    [InlineData(RoleNames.Clinician)]
    public async Task AClinicalRoleKeepsTheAccessItAlwaysHad(string role)
    {
        StudyAccessScope scope = await CreatePolicy()
            .AuthoriseAsync(APrincipal(_actor, role), _study, GrantPermissions.View);

        scope.Should().Be(StudyAccessScope.FullCatalog);

        await _grants.DidNotReceiveWithAnyArgs().HasAccessAsync(default, default!, default!, default);
    }

    [Fact]
    public async Task HoldingBothRolesIsStillFullAccess()
    {
        StudyAccessScope scope = await CreatePolicy()
            .AuthoriseAsync(
                APrincipal(_actor, RoleNames.Administrator, RoleNames.Clinician),
                _study,
                GrantPermissions.View);

        scope.Should().Be(StudyAccessScope.FullCatalog);
    }

    /// <summary>
    /// A role is admitted whatever the grant table says, so a revoked or expired
    /// grant cannot take away access that came from somewhere else.
    /// </summary>
    [Fact]
    public async Task ARoleIsAdmittedEvenWhenNoGrantWouldBe()
    {
        _grants.HasAccessAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        StudyAccessScope scope = await CreatePolicy()
            .AuthoriseAsync(APrincipal(_actor, RoleNames.Clinician), _study, GrantPermissions.View);

        scope.Should().Be(StudyAccessScope.FullCatalog);
    }

    // -----------------------------------------------------------------------
    // The additional path.
    // -----------------------------------------------------------------------

    [Fact]
    public async Task AGrantAdmitsAnAccountToThatStudyAlone()
    {
        _grants.HasAccessAsync(_actor, _study, GrantPermissions.View, Arg.Any<CancellationToken>())
            .Returns(true);

        StudyAccessScope scope = await CreatePolicy()
            .AuthoriseAsync(APrincipal(_actor), _study, GrantPermissions.View);

        scope.Should().Be(StudyAccessScope.GrantedStudyOnly);
    }

    [Fact]
    public async Task AsksTheGrantTableAboutTheCallerTheStudyAndThePermission()
    {
        await CreatePolicy().AuthoriseAsync(APrincipal(_actor), _study, GrantPermissions.View);

        await _grants.Received(1)
            .HasAccessAsync(_actor, _study, GrantPermissions.View, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Expiry and revocation are conditions of the grant query, so a policy that
    /// asks it correctly inherits both. This pins down that the answer is taken
    /// as given rather than second-guessed.
    /// </summary>
    [Fact]
    public async Task NoLiveGrantMeansDenied()
    {
        _grants.HasAccessAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        StudyAccessScope scope = await CreatePolicy()
            .AuthoriseAsync(APrincipal(_actor), _study, GrantPermissions.View);

        scope.Should().Be(StudyAccessScope.Denied);
    }

    /// <summary>
    /// A grant belongs to an account. A token naming none cannot hold one, and
    /// must not be admitted by default.
    /// </summary>
    [Fact]
    public async Task ATokenWithNoAccountIsDenied()
    {
        StudyAccessScope scope = await CreatePolicy()
            .AuthoriseAsync(APrincipal(), _study, GrantPermissions.View);

        scope.Should().Be(StudyAccessScope.Denied);

        await _grants.DidNotReceiveWithAnyArgs().HasAccessAsync(default, default!, default!, default);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AnEmptyStudyIdentifierIsDenied(string study)
    {
        StudyAccessScope scope = await CreatePolicy()
            .AuthoriseAsync(APrincipal(_actor), study, GrantPermissions.View);

        scope.Should().Be(StudyAccessScope.Denied);

        await _grants.DidNotReceiveWithAnyArgs().HasAccessAsync(default, default!, default!, default);
    }

    /// <summary>
    /// The permission travels through unchanged, so the ladder stays in
    /// <c>GrantPermissions</c> rather than being restated here.
    /// </summary>
    [Fact]
    public async Task PassesTheRequestedPermissionThroughUntouched()
    {
        await CreatePolicy().AuthoriseAsync(APrincipal(_actor), _study, GrantPermissions.Download);

        await _grants.Received(1)
            .HasAccessAsync(_actor, _study, GrantPermissions.Download, Arg.Any<CancellationToken>());
    }
}
