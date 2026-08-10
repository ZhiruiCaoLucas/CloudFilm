using CloudFilm.Infrastructure.Configuration;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CloudFilm.Infrastructure.Identity;

/// <summary>
/// Creates the first administrator account when one is configured.
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
        string? email = _options.AdminEmail?.Trim();
        string? password = _options.AdminPassword;

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            _logger.LogInformation(
                "Identity seed credentials are not configured; skipping user seed.");

            return;
        }

        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();

        UserManager<ApplicationUser> users =
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        ApplicationUser? existing = await users.FindByEmailAsync(email).ConfigureAwait(false);

        if (existing is not null)
        {
            // Never touch an account that already exists. Re-running start-up
            // must not reset a password an administrator has since changed, and
            // must not silently hand back an account whose password rotated.
            _logger.LogInformation("Identity seed account already exists; leaving it unchanged.");

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
            // Identity error descriptions name the rule that failed - password
            // too short, duplicate address - and never echo the password itself.
            string errors = string.Join("; ", result.Errors.Select(error => $"{error.Code}: {error.Description}"));

            throw new InvalidOperationException($"Could not create the seed account. {errors}");
        }

        _logger.LogInformation("Identity seed account created.");
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
