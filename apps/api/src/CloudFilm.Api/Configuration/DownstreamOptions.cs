using System.ComponentModel.DataAnnotations;

namespace CloudFilm.Api.Configuration;

/// <summary>
/// Base addresses of the services the API depends on at runtime.
/// </summary>
/// <remarks>
/// These are internal, network isolated addresses. They are never returned to a browser.
/// </remarks>
public sealed class DownstreamOptions : IValidatableObject
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

    /// <summary>
    /// Gets or sets the base address of the analysis service, when one is deployed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Optional, and the only address here that is. The other two are what the
    /// API is for - without them it cannot answer a single question about a
    /// patient - while a deployment with no analysis service is a complete,
    /// working CloudFilm. Marking this <c>[Required]</c> would stop every
    /// existing stack from starting for want of a service nobody has asked for.
    /// </para>
    /// <para>
    /// Blank means "not deployed", not "misconfigured". That matters because the
    /// Compose files already pass optional settings as <c>${VAR:-}</c>, which
    /// arrives here as an empty string rather than as an absent key; treating it
    /// as an error would make the safe way of writing that line the one that
    /// breaks start-up.
    /// </para>
    /// <para>
    /// Internal, like its siblings. The analysis service authenticates nobody and
    /// is reachable only from the compose network, so this is a container address
    /// and never one a browser could resolve.
    /// </para>
    /// </remarks>
    public string? AnalysisServiceUrl { get; set; }

    /// <summary>
    /// Gets a value indicating whether an analysis service is configured.
    /// </summary>
    /// <remarks>
    /// One place decides what "configured" means, so that the composition root
    /// and every future caller agree. Absent, empty and whitespace are all the
    /// same answer.
    /// </remarks>
    public bool IsAnalysisConfigured => !string.IsNullOrWhiteSpace(AnalysisServiceUrl);

    /// <summary>
    /// Validates the settings this type's attributes cannot express.
    /// </summary>
    /// <param name="validationContext">The context the options are validated in.</param>
    /// <returns>One result per problem found, or nothing when the options are usable.</returns>
    /// <remarks>
    /// <c>[Url]</c> would reject the empty string, which is how a deployment says
    /// it has no analysis service. The address is therefore checked here, and only
    /// when there is one to check - so a stack without the service starts, and a
    /// stack with a mistyped address fails at start-up rather than at the first
    /// request, which is the point of <c>ValidateOnStart</c>.
    /// </remarks>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!IsAnalysisConfigured)
        {
            yield break;
        }

        bool absolute = Uri.TryCreate(AnalysisServiceUrl, UriKind.Absolute, out Uri? address);

        // http and https only. A configuration value is not a place to accept
        // file: or any other scheme that would make the client fetch something
        // nobody meant it to.
        if (!absolute || (address!.Scheme != Uri.UriSchemeHttp && address.Scheme != Uri.UriSchemeHttps))
        {
            yield return new ValidationResult(
                $"{SectionName}:{nameof(AnalysisServiceUrl)} must be an absolute http or https address.",
                [nameof(AnalysisServiceUrl)]);
        }
    }
}
