using System.ComponentModel.DataAnnotations;

namespace CloudFilm.Api.Configuration;

/// <summary>
/// Settings describing the browser facing surface the API must trust.
/// </summary>
public sealed class PortalOptions
{
    /// <summary>
    /// The configuration section name.
    /// </summary>
    public const string SectionName = "Portal";

    /// <summary>
    /// Gets or sets the origins permitted to call the API from a browser.
    /// </summary>
    [Required]
    [MinLength(1)]
    public string[] AllowedOrigins { get; set; } = [];
}
