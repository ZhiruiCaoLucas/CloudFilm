using System.ComponentModel.DataAnnotations;

namespace CloudFilm.Retrieve;

/// <summary>
/// Retrieve Service configuration.
/// </summary>
public sealed class RetrieveOptions
{
    /// <summary>
    /// The configuration section name.
    /// </summary>
    public const string SectionName = "Retrieve";

    /// <summary>
    /// Gets or sets the base address of the Orthanc instance that retrieved studies are staged into.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    [Url]
    public string OrthancUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user name Orthanc's HTTP API is authenticated with.
    /// </summary>
    /// <remarks>
    /// Optional. Local development runs Orthanc with authentication disabled, and
    /// leaving this empty sends anonymous requests. Any deployment with
    /// authentication enabled must set it, or Orthanc answers 401.
    /// </remarks>
    public string OrthancUsername { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the password paired with <see cref="OrthancUsername"/>.
    /// </summary>
    /// <remarks>
    /// Supplied through the environment or a secret store, never committed. Only
    /// the Retrieve Service holds it: the credential must never reach the API,
    /// and must never reach a browser.
    /// </remarks>
    public string OrthancPassword { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets how long a staged study remains in Orthanc before it is eligible for eviction.
    /// </summary>
    /// <remarks>
    /// CloudFilm is a transient cache in front of the hospital archive, never a
    /// system of record. Bounding the staging lifetime is a security control, not a
    /// storage optimisation.
    /// </remarks>
    [Range(typeof(TimeSpan), "00:05:00", "7.00:00:00")]
    public TimeSpan StagingLifetime { get; set; } = TimeSpan.FromHours(12);
}
