using Microsoft.AspNetCore.Identity;

namespace CloudFilm.Infrastructure.Identity;

/// <summary>
/// The CloudFilm account record.
/// </summary>
/// <remarks>
/// Identity is deliberately an infrastructure concern. Domain code must never
/// reference this type; it refers to accounts by their <see cref="IdentityUser{TKey}.Id"/>.
/// </remarks>
public sealed class ApplicationUser : IdentityUser<Guid>
{
    /// <summary>
    /// Gets or sets the display name shown in the portal.
    /// </summary>
    public string? DisplayName { get; set; }
}
