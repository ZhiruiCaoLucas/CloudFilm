using CloudFilm.Application.Auth;
using CloudFilm.Infrastructure.Configuration;
using CloudFilm.Infrastructure.Identity;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace CloudFilm.Api.Tests.Auth;

/// <summary>
/// What start-up does to roles and to the seeded account.
/// </summary>
/// <remarks>
/// <para>
/// The Identity managers are substituted rather than backed by a database. What
/// is under test is the seeder's decisions - create this, skip that, fail loudly
/// - and those are visible in the calls it makes. Whether PostgreSQL then stores
/// the row is EF Core's and Identity's business, verified against a live stack.
/// </para>
/// <para>
/// <see cref="UserManager{TUser}"/> and <see cref="RoleManager{TRole}"/> are
/// classes with virtual members, so they substitute through their public
/// constructors; the dependencies they never reach on these paths are left null
/// deliberately, because supplying real ones would imply the test needs them.
/// </para>
/// </remarks>
public sealed class IdentitySeederTests
{
    private const string _email = "admin@example.test";
    private const string _password = "correct horse battery staple";

    private static UserManager<ApplicationUser> AUserManager() =>
        Substitute.For<UserManager<ApplicationUser>>(
            Substitute.For<IUserStore<ApplicationUser>>(),
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null);

    private static RoleManager<ApplicationRole> ARoleManager() =>
        Substitute.For<RoleManager<ApplicationRole>>(
            Substitute.For<IRoleStore<ApplicationRole>>(),
            null,
            null,
            null,
            null);

    /// <summary>Builds a seeder over the two substituted managers.</summary>
    private static IdentitySeeder CreateSeeder(
        UserManager<ApplicationUser> users,
        RoleManager<ApplicationRole> roles,
        bool withSeedCredentials = true)
    {
        ServiceCollection services = new();
        services.AddSingleton(users);
        services.AddSingleton(roles);

        ServiceProvider provider = services.BuildServiceProvider();

        SeedOptions options = withSeedCredentials
            ? new SeedOptions { AdminEmail = _email, AdminPassword = _password }
            : new SeedOptions();

        return new IdentitySeeder(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(options),
            NullLogger<IdentitySeeder>.Instance);
    }

    /// <summary>Every role is missing, every write succeeds, no account exists yet.</summary>
    private static (UserManager<ApplicationUser> Users, RoleManager<ApplicationRole> Roles) AnEmptyDeployment()
    {
        RoleManager<ApplicationRole> roles = ARoleManager();
        roles.RoleExistsAsync(Arg.Any<string>()).Returns(false);
        roles.CreateAsync(Arg.Any<ApplicationRole>()).Returns(IdentityResult.Success);

        UserManager<ApplicationUser> users = AUserManager();
        users.FindByEmailAsync(Arg.Any<string>()).Returns((ApplicationUser?)null);
        users.CreateAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>()).Returns(IdentityResult.Success);
        users.IsInRoleAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>()).Returns(false);
        users.AddToRoleAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>()).Returns(IdentityResult.Success);

        return (users, roles);
    }

    // -----------------------------------------------------------------------
    // Roles.
    // -----------------------------------------------------------------------

    [Fact]
    public async Task CreatesEveryApplicationRoleWhenNoneExist()
    {
        (UserManager<ApplicationUser> users, RoleManager<ApplicationRole> roles) = AnEmptyDeployment();

        await CreateSeeder(users, roles).StartAsync(CancellationToken.None);

        await roles.Received(1).CreateAsync(
            Arg.Is<ApplicationRole>(role => role.Name == RoleNames.Administrator));
        await roles.Received(1).CreateAsync(
            Arg.Is<ApplicationRole>(role => role.Name == RoleNames.Clinician));
        await roles.Received(RoleNames.All.Count).CreateAsync(Arg.Any<ApplicationRole>());
    }

    /// <summary>Restarting the host is not a write.</summary>
    [Fact]
    public async Task CreatesNothingWhenEveryRoleAlreadyExists()
    {
        (UserManager<ApplicationUser> users, RoleManager<ApplicationRole> roles) = AnEmptyDeployment();
        roles.RoleExistsAsync(Arg.Any<string>()).Returns(true);

        await CreateSeeder(users, roles).StartAsync(CancellationToken.None);

        await roles.DidNotReceiveWithAnyArgs().CreateAsync(default!);
    }

    [Fact]
    public async Task CreatesOnlyTheMissingRole()
    {
        (UserManager<ApplicationUser> users, RoleManager<ApplicationRole> roles) = AnEmptyDeployment();
        roles.RoleExistsAsync(RoleNames.Administrator).Returns(true);
        roles.RoleExistsAsync(RoleNames.Clinician).Returns(false);

        await CreateSeeder(users, roles).StartAsync(CancellationToken.None);

        await roles.Received(1).CreateAsync(
            Arg.Is<ApplicationRole>(role => role.Name == RoleNames.Clinician));
        await roles.DidNotReceive().CreateAsync(
            Arg.Is<ApplicationRole>(role => role.Name == RoleNames.Administrator));
    }

    [Fact]
    public async Task CreatesNoRoleTheApplicationDoesNotDefine()
    {
        (UserManager<ApplicationUser> users, RoleManager<ApplicationRole> roles) = AnEmptyDeployment();

        await CreateSeeder(users, roles).StartAsync(CancellationToken.None);

        await roles.DidNotReceive().CreateAsync(
            Arg.Is<ApplicationRole>(role => !RoleNames.All.Contains(role.Name!, StringComparer.Ordinal)));
    }

    /// <summary>
    /// Roles are what authorisation is defined against, so they exist whether or
    /// not this deployment seeds an account.
    /// </summary>
    [Fact]
    public async Task CreatesTheRolesEvenWhenNoSeedAccountIsConfigured()
    {
        (UserManager<ApplicationUser> users, RoleManager<ApplicationRole> roles) = AnEmptyDeployment();

        await CreateSeeder(users, roles, withSeedCredentials: false).StartAsync(CancellationToken.None);

        await roles.Received(RoleNames.All.Count).CreateAsync(Arg.Any<ApplicationRole>());
        await users.DidNotReceiveWithAnyArgs().FindByEmailAsync(default!);
        await users.DidNotReceiveWithAnyArgs().CreateAsync(default!, default!);
    }

    // -----------------------------------------------------------------------
    // The seeded account.
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GivesTheNewSeedAccountTheAdministratorRole()
    {
        (UserManager<ApplicationUser> users, RoleManager<ApplicationRole> roles) = AnEmptyDeployment();

        await CreateSeeder(users, roles).StartAsync(CancellationToken.None);

        await users.Received(1).AddToRoleAsync(
            Arg.Is<ApplicationUser>(user => user.Email == _email),
            RoleNames.Administrator);
    }

    /// <summary>
    /// An account seeded before roles existed holds none. After authorisation is
    /// enforced that is an administrator who cannot use the deployment, so the
    /// role is attached even though the account is otherwise left alone.
    /// </summary>
    [Fact]
    public async Task GivesAnExistingSeedAccountTheAdministratorRole()
    {
        (UserManager<ApplicationUser> users, RoleManager<ApplicationRole> roles) = AnEmptyDeployment();

        ApplicationUser existing = new() { Email = _email, UserName = _email };
        users.FindByEmailAsync(_email).Returns(existing);

        await CreateSeeder(users, roles).StartAsync(CancellationToken.None);

        await users.Received(1).AddToRoleAsync(existing, RoleNames.Administrator);
    }

    /// <summary>
    /// The account is left exactly as it is: re-running start-up must not reset
    /// a password an administrator has since changed.
    /// </summary>
    [Fact]
    public async Task LeavesAnExistingSeedAccountsCredentialsAlone()
    {
        (UserManager<ApplicationUser> users, RoleManager<ApplicationRole> roles) = AnEmptyDeployment();
        users.FindByEmailAsync(_email).Returns(new ApplicationUser { Email = _email, UserName = _email });

        await CreateSeeder(users, roles).StartAsync(CancellationToken.None);

        await users.DidNotReceiveWithAnyArgs().CreateAsync(default!, default!);
    }

    [Fact]
    public async Task NeverGivesTheSeedAccountTheClinicianRole()
    {
        (UserManager<ApplicationUser> users, RoleManager<ApplicationRole> roles) = AnEmptyDeployment();

        await CreateSeeder(users, roles).StartAsync(CancellationToken.None);

        await users.DidNotReceive().AddToRoleAsync(Arg.Any<ApplicationUser>(), RoleNames.Clinician);
        await users.Received(1).AddToRoleAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>());
    }

    [Fact]
    public async Task DoesNotRepeatAnAssignmentTheAccountAlreadyHas()
    {
        (UserManager<ApplicationUser> users, RoleManager<ApplicationRole> roles) = AnEmptyDeployment();

        ApplicationUser existing = new() { Email = _email, UserName = _email };
        users.FindByEmailAsync(_email).Returns(existing);
        users.IsInRoleAsync(existing, RoleNames.Administrator).Returns(true);

        await CreateSeeder(users, roles).StartAsync(CancellationToken.None);

        await users.DidNotReceiveWithAnyArgs().AddToRoleAsync(default!, default!);
    }

    // -----------------------------------------------------------------------
    // Failure is loud.
    // -----------------------------------------------------------------------

    /// <summary>
    /// A host that starts with a missing role answers every authorised request
    /// with a refusal while looking perfectly healthy. Failing here turns that
    /// into an obvious deployment error.
    /// </summary>
    [Fact]
    public async Task FailsStartUpWhenARoleCannotBeCreated()
    {
        (UserManager<ApplicationUser> users, RoleManager<ApplicationRole> roles) = AnEmptyDeployment();
        roles.CreateAsync(Arg.Any<ApplicationRole>())
            .Returns(IdentityResult.Failed(new IdentityError { Code = "X", Description = "no" }));

        Func<Task> start = () => CreateSeeder(users, roles).StartAsync(CancellationToken.None);

        await start.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task FailsStartUpWhenTheAdministratorRoleCannotBeAssigned()
    {
        (UserManager<ApplicationUser> users, RoleManager<ApplicationRole> roles) = AnEmptyDeployment();
        users.AddToRoleAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>())
            .Returns(IdentityResult.Failed(new IdentityError { Code = "X", Description = "no" }));

        Func<Task> start = () => CreateSeeder(users, roles).StartAsync(CancellationToken.None);

        await start.Should().ThrowAsync<InvalidOperationException>();
    }
}
