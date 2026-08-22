using System.ComponentModel.DataAnnotations;
using CloudFilm.Api.Configuration;
using FluentAssertions;

namespace CloudFilm.Api.Tests.Analysis;

/// <summary>
/// What the API insists on knowing before it will start.
/// </summary>
/// <remarks>
/// These options are validated with <c>ValidateDataAnnotations().ValidateOnStart()</c>,
/// so a mistake here is not a failed request - it is a stack that does not come
/// up. That makes the boundary between "required" and "optional" worth a test:
/// adding a required setting silently breaks every deployment that has not been
/// updated to supply it.
/// </remarks>
public sealed class DownstreamOptionsTests
{
    private const string _retrieve = "http://retrieve:8080";
    private const string _orthanc = "http://orthanc:8042";

    private static DownstreamOptions AnOptions(string? analysisServiceUrl = null) =>
        new()
        {
            RetrieveServiceUrl = _retrieve,
            OrthancUrl = _orthanc,
            AnalysisServiceUrl = analysisServiceUrl,
        };

    /// <summary>
    /// Validates the way the host does: every property, and
    /// <see cref="IValidatableObject"/> along with them.
    /// </summary>
    private static List<ValidationResult> Validate(DownstreamOptions options)
    {
        List<ValidationResult> results = [];

        Validator.TryValidateObject(options, new ValidationContext(options), results, validateAllProperties: true);

        return results;
    }

    /// <summary>
    /// The state every existing deployment is in: no analysis service, nothing
    /// in the environment about one. It must start.
    /// </summary>
    [Fact]
    public void AStackWithNoAnalysisServiceIsValid()
    {
        DownstreamOptions options = AnOptions();

        Validate(options).Should().BeEmpty();
        options.IsAnalysisConfigured.Should().BeFalse();
    }

    /// <summary>
    /// Compose passes optional settings as <c>${VAR:-}</c>, which arrives as an
    /// empty string rather than an absent key. Blank must mean "not deployed", or
    /// the safe way to write that line would be the one that breaks start-up.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ABlankAnalysisAddressMeansNotDeployed(string configured)
    {
        DownstreamOptions options = AnOptions(configured);

        Validate(options).Should().BeEmpty();
        options.IsAnalysisConfigured.Should().BeFalse();
    }

    [Theory]
    [InlineData("http://analysis:8000")]
    [InlineData("https://analysis.internal")]
    public void AnAbsoluteHttpAddressIsAccepted(string configured)
    {
        DownstreamOptions options = AnOptions(configured);

        Validate(options).Should().BeEmpty();
        options.IsAnalysisConfigured.Should().BeTrue();
    }

    /// <summary>
    /// A mistyped address fails at start-up rather than at the first request,
    /// which is what <c>ValidateOnStart</c> is for. The <c>file:</c> case is not
    /// pedantry: a configuration value is not a place to accept a scheme that
    /// would make a client read the local disk.
    /// </summary>
    [Theory]
    [InlineData("analysis:8000")]
    [InlineData("/analysis")]
    [InlineData("not an address")]
    [InlineData("file:///etc/passwd")]
    [InlineData("ftp://analysis")]
    public void AMalformedAnalysisAddressIsRefused(string configured)
    {
        List<ValidationResult> results = Validate(AnOptions(configured));

        results.Should().ContainSingle();
        results[0].MemberNames.Should().Contain(nameof(DownstreamOptions.AnalysisServiceUrl));
    }

    /// <summary>
    /// The two addresses the API cannot work without are still required. This is
    /// the regression guard for the change that made a third one optional.
    /// </summary>
    [Fact]
    public void TheAddressesTheApiCannotWorkWithoutAreStillRequired()
    {
        DownstreamOptions empty = new();

        List<ValidationResult> results = Validate(empty);

        string[] expected =
        [
            nameof(DownstreamOptions.RetrieveServiceUrl),
            nameof(DownstreamOptions.OrthancUrl),
        ];

        results.Should().HaveCount(2);
        results.SelectMany(result => result.MemberNames).Should().BeEquivalentTo(expected);
    }
}
