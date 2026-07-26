using System.ComponentModel.DataAnnotations;

namespace CloudFilm.Api.Configuration;

/// <summary>
/// Bearer token validation settings.
/// </summary>
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
}
