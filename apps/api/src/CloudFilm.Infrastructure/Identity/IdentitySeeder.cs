using CloudFilm.Application.Auth;
using CloudFilm.Infrastructure.Configuration;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CloudFilm.Infrastructure.Identity;

/// <summary>
/// Creates the application roles, and the first administrator account when one
/// is configured.
/// </summary>
/// <remarks>
/// <para>
/// Authentication is a closed loop without this: the API will require an
/// account, and CloudFilm has no self-service registration to create one with.
/// </para>
/// <para>
/// Registered after <see cref="Persistence.DatabaseMigrator"/> so that it runs
/// once the Identity tables exist. Hosted services start in registration order.
/// </para>
/// </remarks>
internal sealed class IdentitySeeder(
    IServiceScopeFactory scopeFactory,
    IOptions<SeedOptions> options,
    ILogger<IdentitySeeder> logger) : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly SeedOptions _options = options.Value;
    private readonly ILogger<IdentitySeeder> _logger = logger;

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();

        // Roles first, and unconditionally. They are what authorisation is
        // defined against, so they must exist whether or not this deployment
        // seeds an account - a database with no roles cannot have anybody in one.
        await EnsureRolesAsync(scope.ServiceProvider).ConfigureAwait(false);

        string? email = _options.AdminEmail?.Trim();
        string? password = _options.AdminPassword;

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            _logger.LogInformation(
                "Identity seed credentials are not configured; skipping user seed.");

            return;
        }

        UserManager<ApplicationUser> users =
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        ApplicationUser? existing = await users.FindByEmailAsync(email).ConfigureAwait(false);

        if (existing is not null)
        {
            // The account is left exactly as it is - re-running start-up must
            // not reset a password an administrator has since changed. Its role
            // is still checked, because an account seeded before roles existed
            // holds none, and after authorisation is enforced an administrator
            // with no role is an administrator who cannot sign in to anything.
            _logger.LogInformation("Identity seed account already exists; leaving it unchanged.");

            await EnsureAdministratorAsync(users, existing).ConfigureAwait(false);

            return;
        }

        ApplicationUser user = new()
        {
            UserName = email,
            Email = email,

            // The configured policy sets SignIn.RequireConfirmedEmail. A seeded
            // account with an unconfirmed address would be created successfully
            // and then be unable to sign in, ever - correct password included.
            EmailConfirmed = true,
            DisplayName = "Administrator",
        };

        // CreateAsync hashes the password with the configured IPasswordHasher.
        // Nothing here hashes anything by hand.
        IdentityResult result = await users.CreateAsync(user, password).ConfigureAwait(false);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Could not create the seed account. {Describe(result)}");
        }

        _logger.LogInformation("Identity seed account created.");

        await EnsureAdministratorAsync(users, user).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates every application role that does not exist yet.
    /// </summary>
    /// <param name="services">The scoped provider to resolve the role manager from.</param>
    /// <remarks>
    /// <para>
    /// Idempotent: a role that is already there is left alone, so restarting the
    /// host is not a write. Only the roles in <see cref="RoleNames.All"/> are
    /// created, and none is ever deleted - removing a row an operator added by
    /// hand is not this application's business, and it would not grant anything
    /// in any case.
    /// </para>
    /// <para>
    /// A failure throws, which stops the host. A service that starts with a
    /// missing role would answer every authorised request with a refusal, and it
    /// would do so looking perfectly healthy; failing at start-up turns that into
    /// an obvious deployment error instead of a mystery in production.
    /// </para>
    /// </remarks>
    private async Task EnsureRolesAsync(IServiceProvider services)
    {
        RoleManager<ApplicationRole> roles =
            services.GetRequiredService<RoleManager<ApplicationRole>>();

        foreach (string name in RoleNames.All)
        {
            if (await roles.RoleExistsAsync(name).ConfigureAwait(false))
            {
                continue;
            }

            IdentityResult result = await roles
                .CreateAsync(new ApplicationRole(name))
                .ConfigureAwait(false);

            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Could not create the {name} role. {Describe(result)}");
            }

            _logger.LogInformation("Created the {Role} role.", name);
        }
    }

    /// <summary>
    /// Gives the seeded account the administrator role if it does not have it.
    /// </summary>
    /// <param name="users">The user manager.</param>
    /// <param name="user">The seeded account.</param>
    /// <remarks>
    /// <para>
    /// Administrator only. The seeded account operates the deployment; giving it
    /// a second role as well would grant rights nobody asked for and would make
    /// the account a poor example of what a role means.
    /// </para>
    /// <para>
    /// Checked before it is granted, so a restart neither duplicates the
    /// assignment nor writes at all. A failure throws for the same reason a
    /// failed role creation does: the alternative is a deployment whose only
    /// account cannot use it.
    /// </para>
    /// <para>
    /// This is the only place CloudFilm grants a role. Nothing in the sign-in
    /// path, and no endpoint, may promote an account - which is what makes
    /// "an authenticated account with no role is refused" a rule rather than a
    /// race.
    /// </para>
    /// </remarks>
    private async Task EnsureAdministratorAsync(UserManager<ApplicationUser> users, ApplicationUser user)
    {
        if (await users.IsInRoleAsync(user, RoleNames.Administrator).ConfigureAwait(false))
        {
            return;
        }

        IdentityResult result = await users
            .AddToRoleAsync(user, RoleNames.Administrator)
            .ConfigureAwait(false);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Could not assign the {RoleNames.Administrator} role to the seed account. {Describe(result)}");
        }

        _logger.LogInformation("Assigned the {Role} role to the seed account.", RoleNames.Administrator);
    }

    /// <summary>
    /// Renders an Identity failure without echoing anything secret.
    /// </summary>
    /// <param name="result">The failed result.</param>
    /// <returns>The error codes and descriptions, joined.</returns>
    /// <remarks>
    /// Identity error descriptions name the rule that failed - password too
    /// short, duplicate address - and never the value that failed it.
    /// </remarks>
    private static string Describe(IdentityResult result) =>
        string.Join("; ", result.Errors.Select(error => $"{error.Code}: {error.Description}"));

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
