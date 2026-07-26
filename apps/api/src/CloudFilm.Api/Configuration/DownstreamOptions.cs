using System.ComponentModel.DataAnnotations;

namespace CloudFilm.Api.Configuration;

/// <summary>
/// Base addresses of the services the API depends on at runtime.
/// </summary>
/// <remarks>
/// These are internal, network isolated addresses. They are never returned to a browser.
/// </remarks>
public sealed class DownstreamOptions
{
    /// <summary>
    /// The configuration section name.
    /// </summary>
    public const string SectionName = "Downstream";

    /// <summary>
    /// Gets or sets the base address of the Retrieve Service.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    [Url]
    public string RetrieveServiceUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the base address of the Orthanc DICOM server.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    [Url]
    public string OrthancUrl { get; set; } = string.Empty;
}
