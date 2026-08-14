using System.Reflection;
using CloudFilm.Application.Imaging;
using FluentAssertions;

namespace CloudFilm.Api.Tests.Imaging;

/// <summary>
/// The shape of a study search, and what it refuses to accept.
/// </summary>
/// <remarks>
/// Contract and validation only. Nothing here reaches an archive: what is under
/// test is the boundary that decides whether a query is fit to become one.
/// </remarks>
public sealed class StudySearchQueryTests
{
    private static readonly string[] _queryFields =
        ["PatientName", "AccessionNumber", "StudyDateFrom", "StudyDateTo", "Page", "PageSize"];

    private static readonly string[] _resultFields = ["Items", "HasMore", "Page", "PageSize"];

    private static readonly string[] _itemFields = ["Patient", "Study"];

    private static StudySearchQuery AQuery(int page = 0, int pageSize = 25) =>
        new(
            PatientName: "Doe",
            AccessionNumber: null,
            StudyDateFrom: null,
            StudyDateTo: null,
            Page: page,
            PageSize: pageSize);

    private static IEnumerable<string> PropertyNames(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(property => property.Name);

    // -----------------------------------------------------------------------
    // Paging bounds.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(0, 1)]
    [InlineData(0, 25)]
    [InlineData(0, 100)]
    [InlineData(7, 100)]
    public void AcceptsPagesWithinTheBounds(int page, int pageSize) =>
        StudySearchValidation.Validate(AQuery(page, pageSize)).Should().BeEmpty();

    [Theory]
    [InlineData(-1)]
    [InlineData(-100)]
    public void RejectsANegativePage(int page)
    {
        IReadOnlyList<string> problems = StudySearchValidation.Validate(AQuery(page: page));

        problems.Should().ContainSingle().Which.Should().Contain("Page");
    }

    /// <summary>
    /// The ceiling is a limit on how much patient data one request can disclose,
    /// so it is enforced here rather than trusted from the client.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    [InlineData(1000)]
    public void RejectsAPageSizeOutsideTheBounds(int pageSize)
    {
        IReadOnlyList<string> problems = StudySearchValidation.Validate(AQuery(pageSize: pageSize));

        problems.Should().ContainSingle().Which.Should().Contain("PageSize");
    }

    [Fact]
    public void TheLargestPageIsOneHundred()
    {
        StudySearchLimits.MaxPageSize.Should().Be(100);
        StudySearchLimits.MinPageSize.Should().Be(1);
    }

    /// <summary>
    /// A person who fixes one field and is then told about the next has been
    /// made to submit the form twice to learn what one response could have said.
    /// </summary>
    [Fact]
    public void ReportsEveryProblemAtOnce()
    {
        StudySearchQuery query = new(
            PatientName: null,
            AccessionNumber: null,
            StudyDateFrom: new DateOnly(2018, 12, 31),
            StudyDateTo: new DateOnly(2018, 1, 1),
            Page: -1,
            PageSize: 500);

        StudySearchValidation.Validate(query).Should().HaveCount(3);
    }

    // -----------------------------------------------------------------------
    // Dates.
    // -----------------------------------------------------------------------

    [Fact]
    public void AcceptsADateRangeInOrder()
    {
        StudySearchQuery query = AQuery() with
        {
            StudyDateFrom = new DateOnly(2018, 1, 1),
            StudyDateTo = new DateOnly(2018, 12, 31),
        };

        StudySearchValidation.Validate(query).Should().BeEmpty();
    }

    [Fact]
    public void AcceptsASingleDayAsAClosedRange()
    {
        DateOnly day = new(2018, 3, 14);

        StudySearchValidation.Validate(AQuery() with { StudyDateFrom = day, StudyDateTo = day })
            .Should().BeEmpty();
    }

    /// <summary>
    /// DICOM range syntax supports an open end in either direction, so one bound
    /// on its own is a legitimate search rather than half of one.
    /// </summary>
    [Fact]
    public void AcceptsAnOpenEndedRange()
    {
        StudySearchValidation.Validate(AQuery() with { StudyDateFrom = new DateOnly(2018, 1, 1) })
            .Should().BeEmpty();

        StudySearchValidation.Validate(AQuery() with { StudyDateTo = new DateOnly(2018, 12, 31) })
            .Should().BeEmpty();
    }

    [Fact]
    public void RejectsAnInvertedDateRange()
    {
        StudySearchQuery query = AQuery() with
        {
            StudyDateFrom = new DateOnly(2018, 12, 31),
            StudyDateTo = new DateOnly(2018, 1, 1),
        };

        StudySearchValidation.Validate(query).Should().ContainSingle()
            .Which.Should().Contain("StudyDateFrom");
    }

    [Theory]
    [InlineData("2018-03-14")]
    [InlineData("20180314")]
    [InlineData("  2018-03-14  ")]
    public void ReadsTheDateFormatsTheSystemActuallyUses(string value)
    {
        StudySearchValidation.TryParseStudyDate(value, out DateOnly? date).Should().BeTrue();

        date.Should().Be(new DateOnly(2018, 3, 14));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TreatsAnAbsentDateAsNoBoundRatherThanAnError(string? value)
    {
        StudySearchValidation.TryParseStudyDate(value, out DateOnly? date).Should().BeTrue();

        date.Should().BeNull();
    }

    /// <summary>
    /// A study search that silently guesses the month is worse than one that
    /// refuses: 03/04/2018 is March in one country and April in another.
    /// </summary>
    [Theory]
    [InlineData("03/04/2018")]
    [InlineData("14-03-2018")]
    [InlineData("2018-3-14")]
    [InlineData("2018-13-01")]
    [InlineData("20180230")]
    [InlineData("18-03-14")]
    [InlineData("yesterday")]
    [InlineData("2018")]
    [InlineData("201803")]
    [InlineData("20180314*")]
    public void RefusesADateItWouldHaveToGuessAt(string value)
    {
        StudySearchValidation.TryParseStudyDate(value, out DateOnly? date).Should().BeFalse();

        date.Should().BeNull();
    }

    // -----------------------------------------------------------------------
    // What the contract must not contain.
    // -----------------------------------------------------------------------

    /// <summary>
    /// Paging is stated as a page, not as a row offset. The translation to
    /// <c>offset</c> belongs to whichever adapter speaks a language that has
    /// one, and a caller that could set it directly could page past the ceiling.
    /// </summary>
    [Fact]
    public void TheQueryExposesNoOffset() =>
        PropertyNames(typeof(StudySearchQuery))
            .Should().NotContain(name => name.Contains("Offset", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Measured against the deployment and deliberately left out: fuzzy matching
    /// answered 200 but returned nothing for the sample data, so shipping it
    /// would be shipping a switch nobody has seen work.
    /// </summary>
    [Fact]
    public void TheQueryExposesNoFuzzyMatching() =>
        PropertyNames(typeof(StudySearchQuery))
            .Should().NotContain(name => name.Contains("Fuzzy", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// No DICOMweb vocabulary in a contract the whole application depends on.
    /// </summary>
    [Fact]
    public void TheQueryExposesNoArchiveQueryLanguage() =>
        PropertyNames(typeof(StudySearchQuery))
            .Should().NotContain(name =>
                name.Contains("Limit", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Wildcard", StringComparison.OrdinalIgnoreCase)
                || name.Contains("IncludeField", StringComparison.OrdinalIgnoreCase));

    [Fact]
    public void TheQueryCarriesExactlyTheAgreedFields() =>
        PropertyNames(typeof(StudySearchQuery)).Should().BeEquivalentTo(_queryFields);

    // -----------------------------------------------------------------------
    // The result.
    // -----------------------------------------------------------------------

    /// <summary>
    /// A search result is a pairing of contracts the system already has, not a
    /// new kind of study or a flattened copy of two records.
    /// </summary>
    [Fact]
    public void TheResultReusesTheExistingContracts()
    {
        typeof(StudySearchResult).GetProperty("Items")!.PropertyType
            .Should().Be<IReadOnlyList<StudySearchItem>>();

        typeof(StudySearchItem).GetProperty("Patient")!.PropertyType.Should().Be<PatientSummary>();
        typeof(StudySearchItem).GetProperty("Study")!.PropertyType.Should().Be<StudySummary>();
    }

    /// <summary>
    /// A search crosses patients, so a row that cannot say whose study it is
    /// cannot be acted on. Both halves are required.
    /// </summary>
    [Fact]
    public void EveryHitCarriesItsPatient() =>
        PropertyNames(typeof(StudySearchItem)).Should().BeEquivalentTo(_itemFields);

    /// <summary>
    /// The archive reports no total - no X-Total-Count, no Content-Range, no
    /// Link header - so a total could only be produced by fetching every match
    /// and counting, which is what paging exists to avoid.
    /// </summary>
    [Fact]
    public void TheResultPromisesNoTotalItCannotKnow() =>
        PropertyNames(typeof(StudySearchResult))
            .Should().NotContain(name =>
                name.Contains("Total", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Count", StringComparison.OrdinalIgnoreCase));

    [Fact]
    public void TheResultCarriesExactlyTheAgreedFields() =>
        PropertyNames(typeof(StudySearchResult)).Should().BeEquivalentTo(_resultFields);

    // -----------------------------------------------------------------------
    // Values are carried, not interpreted.
    // -----------------------------------------------------------------------

    /// <summary>
    /// The contract stores what the user typed. Trimming or case-folding a name
    /// here would change what DICOM matches, and escaping belongs to the
    /// translator that knows which query language it is escaping for.
    /// </summary>
    [Fact]
    public void CarriesSearchTermsExactlyAsSupplied()
    {
        StudySearchQuery query = new(
            PatientName: "  d'Arcy^Jean-Luc  ",
            AccessionNumber: "ACC*01",
            StudyDateFrom: null,
            StudyDateTo: null,
            Page: 0,
            PageSize: 25);

        query.PatientName.Should().Be("  d'Arcy^Jean-Luc  ");
        query.AccessionNumber.Should().Be("ACC*01");

        StudySearchValidation.Validate(query).Should().BeEmpty();
    }
}
