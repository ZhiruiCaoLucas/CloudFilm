using System.Reflection;
using CloudFilm.Retrieve.Hospital;
using FluentAssertions;

namespace CloudFilm.Retrieve.Tests;

/// <summary>
/// The search contract as hospital adapters see it.
/// </summary>
/// <remarks>
/// Deliberately mirrors the API's contract of the same name. These tests exist
/// so the two cannot drift silently: they are separate types on either side of
/// an HTTP boundary, and a field added to one and forgotten on the other would
/// otherwise surface as a search condition that quietly stops being applied.
/// </remarks>
public sealed class StudySearchContractTests
{
    private static readonly string[] _queryFields =
        ["PatientName", "AccessionNumber", "StudyDateFrom", "StudyDateTo", "Page", "PageSize"];

    private static readonly string[] _resultFields = ["Items", "HasMore", "Page", "PageSize"];

    private static IEnumerable<string> PropertyNames(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(property => property.Name);

    [Fact]
    public void TheQueryCarriesExactlyTheAgreedFields() =>
        PropertyNames(typeof(StudySearchQuery)).Should().BeEquivalentTo(_queryFields);

    /// <summary>
    /// An adapter converts a page into whatever its archive understands. Putting
    /// the offset in the contract would push a DICOMweb detail onto adapters
    /// that have never heard of one.
    /// </summary>
    [Fact]
    public void TheQueryExposesNoOffsetOrLimit() =>
        PropertyNames(typeof(StudySearchQuery))
            .Should().NotContain(name =>
                name.Contains("Offset", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Limit", StringComparison.OrdinalIgnoreCase));

    [Fact]
    public void TheQueryExposesNoFuzzyMatching() =>
        PropertyNames(typeof(StudySearchQuery))
            .Should().NotContain(name => name.Contains("Fuzzy", StringComparison.OrdinalIgnoreCase));

    [Fact]
    public void TheQueryUsesRealDatesRatherThanArchiveDateStrings()
    {
        typeof(StudySearchQuery).GetProperty("StudyDateFrom")!.PropertyType.Should().Be<DateOnly?>();
        typeof(StudySearchQuery).GetProperty("StudyDateTo")!.PropertyType.Should().Be<DateOnly?>();
    }

    [Fact]
    public void TheResultReusesTheExistingContracts()
    {
        typeof(StudySearchResult).GetProperty("Items")!.PropertyType
            .Should().Be<IReadOnlyList<StudySearchItem>>();

        typeof(StudySearchItem).GetProperty("Patient")!.PropertyType.Should().Be<PatientSummary>();
        typeof(StudySearchItem).GetProperty("Study")!.PropertyType.Should().Be<StudySummary>();
    }

    [Fact]
    public void TheResultCarriesExactlyTheAgreedFields() =>
        PropertyNames(typeof(StudySearchResult)).Should().BeEquivalentTo(_resultFields);

    /// <summary>
    /// Searchability is carried by the type system rather than by a
    /// configuration flag: an archive that cannot be searched simply does not
    /// implement this interface.
    /// </summary>
    [Fact]
    public void SearchIsASeparateCapabilityFromTheBaseAdapter()
    {
        typeof(IStudySearchSource).IsAssignableFrom(typeof(IHospitalAdapter)).Should().BeFalse();
        typeof(IHospitalAdapter).IsAssignableFrom(typeof(IStudySearchSource)).Should().BeFalse();

        typeof(IStudySearchSource).GetMethods().Should().ContainSingle()
            .Which.Name.Should().Be("SearchStudiesAsync");
    }
}
