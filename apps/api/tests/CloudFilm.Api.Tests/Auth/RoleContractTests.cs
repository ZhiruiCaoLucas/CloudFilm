using System.Reflection;
using CloudFilm.Application.Auth;
using FluentAssertions;

namespace CloudFilm.Api.Tests.Auth;

/// <summary>
/// Which roles CloudFilm has, and how they appear in the authentication
/// contracts.
/// </summary>
/// <remarks>
/// Contract tests only. Nothing here creates a role, issues a token or calls an
/// endpoint: what is under test is the set of names the rest of the system will
/// be built against, and the shape the answer takes when it reaches a client.
/// </remarks>
public sealed class RoleContractTests
{
    private static readonly string[] _approvedRoles = ["Administrator", "Clinician"];

    private static readonly string[] _policies = ["RequiresApplicationRole", "RequiresAdministrator"];

    // -----------------------------------------------------------------------
    // The role set.
    // -----------------------------------------------------------------------

    /// <summary>
    /// Two roles were approved. A third appearing here is a product decision
    /// that was made somewhere it should not have been.
    /// </summary>
    [Fact]
    public void ThereAreExactlyTwoApplicationRoles()
    {
        RoleNames.All.Should().HaveCount(2);
        RoleNames.All.Should().BeEquivalentTo(_approvedRoles);
    }

    [Fact]
    public void TheRolesAreAdministratorAndClinician()
    {
        RoleNames.Administrator.Should().Be("Administrator");
        RoleNames.Clinician.Should().Be("Clinician");

        RoleNames.All.Should().Contain(RoleNames.Administrator);
        RoleNames.All.Should().Contain(RoleNames.Clinician);
    }

    /// <summary>
    /// A patient signing in to see their own imaging needs an account bound to a
    /// medical record number and per-record filtering on every read. Naming the
    /// role without that machinery would produce an account that authenticates
    /// and then sees everybody.
    /// </summary>
    [Fact]
    public void ThereIsNoPatientRole()
    {
        RoleNames.All.Should().NotContain(role =>
            role.Contains("Patient", StringComparison.OrdinalIgnoreCase));

        PublicConstants(typeof(RoleNames)).Should().NotContain(value =>
            value.Contains("Patient", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Role names reach the database and the token as text, so two entries that
    /// differ only in case would be one role to a case-insensitive comparison
    /// and two rows in <c>AspNetRoles</c>.
    /// </summary>
    [Fact]
    public void NoRoleIsDefinedTwice()
    {
        RoleNames.All.Should().OnlyHaveUniqueItems();

        RoleNames.All.Distinct(StringComparer.OrdinalIgnoreCase)
            .Should().HaveCount(RoleNames.All.Count);
    }

    /// <summary>
    /// Every constant on the type is a role, and every role is in the list. A
    /// constant that never reaches <see cref="RoleNames.All"/> is a role the
    /// seeder never creates and authorisation never admits.
    /// </summary>
    [Fact]
    public void EveryRoleConstantIsInTheList() =>
        PublicConstants(typeof(RoleNames)).Should().BeEquivalentTo(RoleNames.All);

    [Fact]
    public void RoleNamesAreUsableAsStoredIdentifiers()
    {
        foreach (string role in RoleNames.All)
        {
            role.Should().NotBeNullOrWhiteSpace();
            role.Should().Be(role.Trim(), "a stored role name must not carry surrounding whitespace");
            role.Should().MatchRegex("^[A-Za-z]+$", "role names are compared and typed by people");
        }
    }

    // -----------------------------------------------------------------------
    // The policies.
    // -----------------------------------------------------------------------

    /// <summary>
    /// The policy every clinical endpoint carries. An authenticated account with
    /// no application role fails it and is refused - it is never promoted by
    /// default.
    /// </summary>
    [Fact]
    public void ThereIsAnApplicationRolePolicy() =>
        AuthorizationPolicies.RequiresApplicationRole.Should().NotBeNullOrWhiteSpace();

    /// <summary>
    /// The policy the acts that decide who else may see a study carry. It is not
    /// about reading patient data - both roles read the same studies - but about
    /// granting access, which a clinician doing clinical work has no reason to
    /// do.
    /// </summary>
    [Fact]
    public void ThereIsAnAdministratorOnlyPolicy() =>
        AuthorizationPolicies.RequiresAdministrator.Should().NotBeNullOrWhiteSpace();

    /// <summary>
    /// Two policies, and no third arriving unnoticed. Each policy name is a rule
    /// somebody has to be able to point at, so the set is pinned down here
    /// rather than left to grow.
    /// </summary>
    [Fact]
    public void ThereAreExactlyTwoPolicies() =>
        PublicConstants(typeof(AuthorizationPolicies)).Should().BeEquivalentTo(_policies);

    /// <summary>
    /// The two are different rules and must be different names. One value
    /// serving both would make an Administrator-only endpoint open to every
    /// role the day somebody tidied up the duplication.
    /// </summary>
    [Fact]
    public void TheTwoPoliciesAreDistinct() =>
        AuthorizationPolicies.RequiresAdministrator
            .Should().NotBe(AuthorizationPolicies.RequiresApplicationRole);

    // -----------------------------------------------------------------------
    // How roles appear in the authentication contracts.
    // -----------------------------------------------------------------------

    /// <summary>
    /// An account with no role is a real state - it signs in and is refused
    /// everything else - so it is an empty list rather than missing information
    /// a caller has to guess at.
    /// </summary>
    [Fact]
    public void ASignInWithoutRolesReportsNoneRatherThanNothing()
    {
        LoginResult result = new("a.b.c", DateTimeOffset.UtcNow.AddHours(1), "Administrator");

        result.Roles.Should().NotBeNull();
        result.Roles.Should().BeEmpty();
    }

    [Fact]
    public void ACurrentUserWithoutRolesReportsNoneRatherThanNothing()
    {
        CurrentUser user = new("11111111-2222-3333-4444-555555555555", "a@example.test", "A");

        user.Roles.Should().NotBeNull();
        user.Roles.Should().BeEmpty();
    }

    [Fact]
    public void ASignInCarriesTheRolesItWasGiven()
    {
        LoginResult result = new("a.b.c", DateTimeOffset.UtcNow.AddHours(1), "Administrator")
        {
            Roles = [RoleNames.Administrator],
        };

        result.Roles.Should().Equal(RoleNames.Administrator);
        result.AccessToken.Should().Be("a.b.c");
        result.DisplayName.Should().Be("Administrator");
    }

    [Fact]
    public void ACurrentUserCarriesTheRolesItWasGiven()
    {
        CurrentUser user = new("11111111-2222-3333-4444-555555555555", "a@example.test", "A")
        {
            Roles = [RoleNames.Clinician],
        };

        user.Roles.Should().Equal(RoleNames.Clinician);
        user.UserId.Should().Be("11111111-2222-3333-4444-555555555555");
    }

    /// <summary>
    /// The existing three-argument construction still compiles and still means
    /// what it meant, which is what lets the token-issuing code paths change in
    /// their own review rather than in this one.
    /// </summary>
    [Fact]
    public void TheExistingContractsKeepTheirPositionalShape()
    {
        PositionalNames(typeof(LoginResult)).Should().Equal("AccessToken", "ExpiresAt", "DisplayName");
        PositionalNames(typeof(CurrentUser)).Should().Equal("UserId", "Email", "DisplayName");
    }

    /// <summary>
    /// Neither contract may carry a password, a hash, a security stamp or
    /// lockout state. The shape is the enforcement.
    /// </summary>
    [Fact]
    public void NeitherContractCarriesAnythingSecret()
    {
        foreach (Type type in new[] { typeof(LoginResult), typeof(CurrentUser) })
        {
            IEnumerable<string> names = type
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(property => property.Name);

            names.Should().NotContain(name =>
                name.Contains("Password", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Hash", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Stamp", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Lockout", StringComparison.OrdinalIgnoreCase));
        }
    }

    private static IEnumerable<string> PublicConstants(Type type) =>
        type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(field => field is { IsLiteral: true, IsInitOnly: false }
                && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!);

    private static IEnumerable<string> PositionalNames(Type type) =>
        type.GetConstructors()
            .OrderByDescending(constructor => constructor.GetParameters().Length)
            .First()
            .GetParameters()
            .Select(parameter => parameter.Name!);
}
