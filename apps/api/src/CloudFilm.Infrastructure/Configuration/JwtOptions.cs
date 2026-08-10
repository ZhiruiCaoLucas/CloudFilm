using System.ComponentModel.DataAnnotations;

namespace CloudFilm.Infrastructure.Configuration;

/// <summary>
/// Bearer token settings, used both to issue tokens and to validate them.
/// </summary>
/// <remarks>
/// Lives in Infrastructure rather than in the API project because the token
/// issuer needs it and Infrastructure cannot reference the API - the dependency
/// runs the other way. The section name and every environment variable
/// (<c>Jwt__Issuer</c>, <c>Jwt__Audience</c>, <c>Jwt__SigningKey</c>) are
/// unchanged by the move, so no deployment configuration changes.
/// </remarks>
public sealed class JwtOptions
{
    /// <summary>
    /// The configuration section name.
    /// </summary>
    public const string SectionName = "Jwt";

    /// <summary>
    /// Gets or sets the token issuer.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string Issuer { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the token audience.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the symmetric signing key.
    /// </summary>
    /// <remarks>
    /// Must be at least 32 bytes. Supplied through the environment or a secret store,
    /// never committed to the repository.
    /// </remarks>
    [Required(AllowEmptyStrings = false)]
    [MinLength(32)]
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the permitted clock skew, in seconds, when validating token lifetime.
    /// </summary>
    [Range(0, 300)]
    public int ClockSkewSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets how long an issued access token remains valid, in minutes.
    /// </summary>
    /// <remarks>
    /// Bounded rather than free-form: a token that never expires is a permanent
    /// credential, and CloudFilm issues no refresh token yet, so the upper bound
    /// is a working day rather than a week.
    /// </remarks>
    [Range(1, 480)]
    public int AccessTokenLifetimeMinutes { get; set; } = 60;
}
