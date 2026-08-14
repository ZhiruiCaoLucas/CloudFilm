using CloudFilm.Retrieve.Endpoints;
using CloudFilm.Retrieve.Hospital;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;

namespace CloudFilm.Retrieve.Tests;

/// <summary>
/// What the Retrieve search endpoint accepts, refuses, and passes on.
/// </summary>
/// <remarks>
/// The handler is called directly rather than through a web host. What is under
/// test is the decision it makes - parse, validate, refuse or delegate - and a
/// host would add a server, a socket and a JSON round trip without adding an
/// assertion. No archive is involved either: the adapter is a substitute, so
/// these tests never need Orthanc.
/// </remarks>
public sealed class RetrieveSearchEndpointTests
{
    /// <summary>An archive that can be searched, which is both capabilities at once.</summary>
    public interface ISearchableAdapter : IHospitalAdapter, IStudySearchSource
    {
    }

    private static readonly StudySearchResult _aPage = new(
        [
            new StudySearchItem(
                new PatientSummary("1000192961", "DOE JOHN", "Male", "1971-07-24"),
                new StudySummary("1.2.840.113564.1", "CT", "Chest", "ACC-1", "2018-03-14", "Completed", 2, 57)),
        ],
        HasMore: true,
        Page: 1,
        PageSize: 10);

    private static (IHospitalAdapterRegistry Registry, ISearchableAdapter Adapter) GivenASearchableSource()
    {
        ISearchableAdapter adapter = Substitute.For<ISearchableAdapter>();
        adapter
            .SearchStudiesAsync(Arg.Any<StudySearchQuery>(), Arg.Any<CancellationToken>())
            .Returns(_aPage);

        IHospitalAdapterRegistry registry = Substitute.For<IHospitalAdapterRegistry>();
        registry.Adapters.Returns([adapter]);

        return (registry, adapter);
    }

    private static Task<IResult> SearchAsync(
        IHospitalAdapterRegistry registry,
        string? patientName = null,
        string? accessionNumber = null,
        string? studyDateFrom = null,
        string? studyDateTo = null,
        int? page = null,
        int? pageSize = null) =>
        SearchEndpoints.SearchAsync(
            patientName,
            accessionNumber,
            studyDateFrom,
            studyDateTo,
            page,
            pageSize,
            registry,
            CancellationToken.None);

    // -----------------------------------------------------------------------
    // The contract is accepted.
    // -----------------------------------------------------------------------

    [Fact]
    public async Task AcceptsTheSearchContractAndReachesTheAdapter()
    {
        (IHospitalAdapterRegistry registry, ISearchableAdapter adapter) = GivenASearchableSource();

        IResult result = await SearchAsync(
            registry,
            patientName: "DOE",
            accessionNumber: "ACC",
            studyDateFrom: "2018-01-01",
            studyDateTo: "20181231",
            page: 1,
            pageSize: 10);

        result.Should().BeOfType<Ok<StudySearchResult>>();

        await adapter.Received(1).SearchStudiesAsync(
            Arg.Is<StudySearchQuery>(query =>
                query.PatientName == "DOE"
                && query.AccessionNumber == "ACC"
                && query.StudyDateFrom == new DateOnly(2018, 1, 1)
                && query.StudyDateTo == new DateOnly(2018, 12, 31)
                && query.Page == 1
                && query.PageSize == 10),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The page the adapter produced is the page the caller gets, unchanged.
    /// </summary>
    [Fact]
    public async Task PreservesTheResultTheAdapterReturned()
    {
        (IHospitalAdapterRegistry registry, _) = GivenASearchableSource();

        IResult result = await SearchAsync(registry, patientName: "DOE");

        StudySearchResult body = result.Should().BeOfType<Ok<StudySearchResult>>().Subject.Value!;

        body.Should().BeSameAs(_aPage);
        body.Items.Should().ContainSingle();
        body.HasMore.Should().BeTrue();
        body.Page.Should().Be(1);
        body.PageSize.Should().Be(10);
    }

    [Fact]
    public async Task DefaultsToTheFirstPageAndAScreenful()
    {
        (IHospitalAdapterRegistry registry, ISearchableAdapter adapter) = GivenASearchableSource();

        await SearchAsync(registry, patientName: "DOE");

        await adapter.Received(1).SearchStudiesAsync(
            Arg.Is<StudySearchQuery>(query =>
                query.Page == 0 && query.PageSize == StudySearchLimits.DefaultPageSize),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Whether an unfiltered search is allowed is a policy about who may see how
    /// much patient data, and it belongs to the authenticated, audited endpoint
    /// in front of this one.
    /// </summary>
    [Fact]
    public async Task DoesNotEnforceTheEmptySearchPolicyItself()
    {
        (IHospitalAdapterRegistry registry, ISearchableAdapter adapter) = GivenASearchableSource();

        IResult result = await SearchAsync(registry);

        result.Should().BeOfType<Ok<StudySearchResult>>();

        await adapter.Received(1).SearchStudiesAsync(
            Arg.Any<StudySearchQuery>(),
            Arg.Any<CancellationToken>());
    }

    // -----------------------------------------------------------------------
    // Invalid queries never reach an archive.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("03/04/2018", null)]
    [InlineData("2018-3-4", null)]
    [InlineData(null, "yesterday")]
    [InlineData(null, "20181332")]
    public async Task RefusesADateItWouldHaveToGuessAt(string? from, string? to)
    {
        (IHospitalAdapterRegistry registry, ISearchableAdapter adapter) = GivenASearchableSource();

        IResult result = await SearchAsync(registry, studyDateFrom: from, studyDateTo: to);

        result.Should().BeOfType<ProblemHttpResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status400BadRequest);

        await adapter.DidNotReceiveWithAnyArgs().SearchStudiesAsync(default!, default);
    }

    [Theory]
    [InlineData(-1, 25)]
    [InlineData(0, 0)]
    [InlineData(0, 101)]
    public async Task RefusesAPageOutsideTheBounds(int page, int pageSize)
    {
        (IHospitalAdapterRegistry registry, ISearchableAdapter adapter) = GivenASearchableSource();

        IResult result = await SearchAsync(registry, patientName: "DOE", page: page, pageSize: pageSize);

        result.Should().BeOfType<ProblemHttpResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status400BadRequest);

        await adapter.DidNotReceiveWithAnyArgs().SearchStudiesAsync(default!, default);
    }

    [Fact]
    public async Task RefusesAnInvertedDateRange()
    {
        (IHospitalAdapterRegistry registry, ISearchableAdapter adapter) = GivenASearchableSource();

        IResult result = await SearchAsync(
            registry,
            studyDateFrom: "2018-12-31",
            studyDateTo: "2018-01-01");

        result.Should().BeOfType<ProblemHttpResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status400BadRequest);

        await adapter.DidNotReceiveWithAnyArgs().SearchStudiesAsync(default!, default);
    }

    // -----------------------------------------------------------------------
    // Sources that cannot answer.
    // -----------------------------------------------------------------------

    [Fact]
    public async Task NoConfiguredSourceIsAnEmptyArchiveRatherThanAnError()
    {
        IHospitalAdapterRegistry registry = Substitute.For<IHospitalAdapterRegistry>();
        registry.Adapters.Returns([]);

        IResult result = await SearchAsync(registry, patientName: "DOE", page: 3, pageSize: 10);

        StudySearchResult body = result.Should().BeOfType<Ok<StudySearchResult>>().Subject.Value!;

        body.Items.Should().BeEmpty();
        body.HasMore.Should().BeFalse();
        body.Page.Should().Be(3);
        body.PageSize.Should().Be(10);
    }

    /// <summary>
    /// A source that cannot be searched is a real gap. Saying so is more useful
    /// than an empty page that reads as "nothing matched".
    /// </summary>
    [Fact]
    public async Task ASourceThatCannotSearchSaysSo()
    {
        IHospitalAdapter adapter = Substitute.For<IHospitalAdapter>();

        IHospitalAdapterRegistry registry = Substitute.For<IHospitalAdapterRegistry>();
        registry.Adapters.Returns([adapter]);

        IResult result = await SearchAsync(registry, patientName: "DOE");

        result.Should().BeOfType<ProblemHttpResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status501NotImplemented);
    }
}
