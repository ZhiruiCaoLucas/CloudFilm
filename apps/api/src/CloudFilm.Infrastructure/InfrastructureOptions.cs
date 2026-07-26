using System.ComponentModel.DataAnnotations;

namespace CloudFilm.Infrastructure;

/// <summary>
/// Strongly typed infrastructure configuration, bound from the <c>Infrastructure</c>
/// configuration section and validated at start up.
/// </summary>
/// <remarks>
/// Validation runs on start so that a misconfigured hospital deployment fails
/// immediately and visibly rather than at the first request.
/// </remarks>
public sealed class InfrastructureOptions
{
    /// <summary>
    /// The configuration section name.
    /// </summary>
    public const string SectionName = "Infrastructure";

    /// <summary>
    /// Gets or sets the PostgreSQL connection string.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string PostgresConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Redis connection string.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string RedisConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the key prefix applied to every Redis entry, which allows a single
    /// Redis instance to be shared by more than one CloudFilm environment.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string RedisInstanceName { get; set; } = "cloudfilm:";

    /// <summary>
    /// Gets or sets a value indicating whether outstanding EF Core migrations are
    /// applied automatically at start up.
    /// </summary>
    public bool AutoMigrate { get; set; }
}
