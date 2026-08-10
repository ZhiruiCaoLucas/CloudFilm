namespace CloudFilm.Infrastructure.Configuration;

/// <summary>
/// The first administrator account, created at start-up when configured.
/// </summary>
/// <remarks>
/// Solves the bootstrap problem: authentication is required to reach the API,
/// but no account exists to authenticate with, and CloudFilm has no
/// self-service registration. Both values are optional - a deployment that
/// provisions accounts another way simply leaves them unset, and the seeder
/// does nothing.
/// </remarks>
public sealed class SeedOptions
{
    /// <summary>
    /// The configuration section name.
    /// </summary>
    public const string SectionName = "Seed";

    /// <summary>
    /// Gets or sets the e-mail address, which is also the user name.
    /// </summary>
    public string? AdminEmail { get; set; }

    /// <summary>
    /// Gets or sets the initial password.
    /// </summary>
    /// <remarks>
    /// Never logged, never returned by any endpoint, and hashed by ASP.NET
    /// Identity before it reaches the database.
    /// </remarks>
    public string? AdminPassword { get; set; }
}
