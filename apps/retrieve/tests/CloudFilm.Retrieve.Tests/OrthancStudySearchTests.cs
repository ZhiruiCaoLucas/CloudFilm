using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using CloudFilm.Retrieve.Hospital;
using CloudFilm.Retrieve.Orthanc;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace CloudFilm.Retrieve.Tests;

/// <summary>
/// Records every request and answers with a canned QIDO-RS body.
/// </summary>
/// <remarks>
/// The seam is the HTTP message handler rather than the adapter's own methods,
/// so what is asserted is the request that would reach Orthanc - the query
/// string, and how many requests there were. A test that stubbed the adapter's
/// internals could not tell a correct query from a plausible one.
/// </remarks>
internal sealed class RecordingHandler(string responseJson) : HttpMessageHandler
{
    private readonly List<Uri> _requests = [];

    public IReadOnlyList<Uri> Requests => _requests;

    public Uri LastRequest => _requests[^1];

    /// <summary>The last request's query string, with percent-encoding undone.</summary>
    public string LastQuery => Uri.UnescapeDataString(LastRequest.Query);

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        _requests.Add(request.RequestUri!);

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, Encoding.UTF8, "application/dicom+json"),
        });
    }
}

/// <summary>
/// What the Orthanc adapter actually asks DICOMweb when a clinician searches.
/// </summary>
public sealed class OrthancStudySearchTests
{
    private const string _mrn = "1000192961";

    private static string StudyJson(int index) => $$"""
        {
          "00100020": { "vr": "LO", "Value": ["{{_mrn}}{{index}}"] },
          "00100010": { "vr": "PN", "Value": [{ "Alphabetic": "DOE^JOHN" }] },
          "00100030": { "vr": "DA", "Value": ["19710724"] },
          "00100040": { "vr": "CS", "Value": ["M"] },
          "0020000D": { "vr": "UI", "Value": ["1.2.840.113564.{{index}}"] },
          "00080020": { "vr": "DA", "Value": ["20180314"] },
          "00080050": { "vr": "SH", "Value": ["ACC{{index}}"] },
          "00080061": { "vr": "CS", "Value": ["CT"] },
          "00081030": { "vr": "LO", "Value": ["Chest"] },
          "00201206": { "vr": "IS", "Value": [2] },
          "00201208": { "vr": "IS", "Value": [57] }
        }
        """;

    private static string ArchiveHolding(int studies) =>
        "[" + string.Join(',', Enumerable.Range(0, studies).Select(StudyJson)) + "]";

    private static (OrthancDicomWebAdapter Adapter, RecordingHandler Handler) Given(int studies)
    {
        RecordingHandler handler = new(ArchiveHolding(studies));

        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(OrthancDicomWebAdapter.HttpClientName).Returns(_ =>

            // disposeHandler: false because the adapter disposes the client it is
            // handed - correctly, since it creates one per call - and the handler
            // has to outlive it to record the requests.
            new HttpClient(handler, disposeHandler: false)
            {
                BaseAddress = new Uri("http://orthanc.test/dicom-web/"),
            });

        return (new OrthancDicomWebAdapter(factory, NullLogger<OrthancDicomWebAdapter>.Instance), handler);
    }

    private static StudySearchQuery AQuery(
        string? patientName = null,
        string? accessionNumber = null,
        DateOnly? from = null,
        DateOnly? to = null,
        int page = 0,
        int pageSize = 25) =>
        new(patientName, accessionNumber, from, to, page, pageSize);

    // -----------------------------------------------------------------------
    // 1. PatientName.
    // -----------------------------------------------------------------------

    [Fact]
    public async Task SearchesPatientNameAsAPrefix()
    {
        (OrthancDicomWebAdapter adapter, RecordingHandler handler) = Given(1);

        await adapter.SearchStudiesAsync(AQuery(patientName: "DOE"));

        handler.LastQuery.Should().Contain("PatientName=DOE*");
    }

    /// <summary>
    /// DICOM matching has no escape sequence, so a wildcard a person typed
    /// cannot be made literal. Removing it keeps a search term a search term
    /// instead of letting a stray character widen the query to everything.
    /// </summary>
    [Theory]
    [InlineData("DOE*", "PatientName=DOE*")]
    [InlineData("D*O?E", "PatientName=DOE*")]
    [InlineData("  DOE  ", "PatientName=DOE*")]
    [InlineData("DOE^JOHN", "PatientName=DOE^JOHN*")]
    public async Task StripsWildcardsTheUserTypedBeforeAddingItsOwn(string typed, string expected)
    {
        (OrthancDicomWebAdapter adapter, RecordingHandler handler) = Given(1);

        await adapter.SearchStudiesAsync(AQuery(patientName: typed));

        handler.LastQuery.Should().Contain(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("*")]
    [InlineData("?*")]
    public async Task OmitsANameConditionThatWouldMatchEverything(string? typed)
    {
        (OrthancDicomWebAdapter adapter, RecordingHandler handler) = Given(1);

        await adapter.SearchStudiesAsync(AQuery(patientName: typed));

        handler.LastQuery.Should().NotContain("PatientName");
    }

    // -----------------------------------------------------------------------
    // 2. AccessionNumber.
    // -----------------------------------------------------------------------

    [Fact]
    public async Task SearchesAccessionNumberAsAPrefix()
    {
        (OrthancDicomWebAdapter adapter, RecordingHandler handler) = Given(1);

        await adapter.SearchStudiesAsync(AQuery(accessionNumber: "ACC12"));

        handler.LastQuery.Should().Contain("AccessionNumber=ACC12*");
    }

    // -----------------------------------------------------------------------
    // 3/4. StudyDate ranges.
    // -----------------------------------------------------------------------

    [Fact]
    public async Task RendersAClosedDateRange()
    {
        (OrthancDicomWebAdapter adapter, RecordingHandler handler) = Given(1);

        await adapter.SearchStudiesAsync(
            AQuery(from: new DateOnly(2018, 1, 1), to: new DateOnly(2018, 12, 31)));

        handler.LastQuery.Should().Contain("StudyDate=20180101-20181231");
    }

    [Fact]
    public async Task RendersAnOpenStartRange()
    {
        (OrthancDicomWebAdapter adapter, RecordingHandler handler) = Given(1);

        await adapter.SearchStudiesAsync(AQuery(from: new DateOnly(2018, 1, 1)));

        handler.LastQuery.Should().Contain("StudyDate=20180101-");
        handler.LastQuery.Should().NotContain("StudyDate=20180101-2");
    }

    [Fact]
    public async Task RendersAnOpenEndRange()
    {
        (OrthancDicomWebAdapter adapter, RecordingHandler handler) = Given(1);

        await adapter.SearchStudiesAsync(AQuery(to: new DateOnly(2018, 12, 31)));

        handler.LastQuery.Should().Contain("StudyDate=-20181231");
    }

    [Fact]
    public async Task OmitsTheDateConditionWhenNeitherBoundIsGiven()
    {
        (OrthancDicomWebAdapter adapter, RecordingHandler handler) = Given(1);

        await adapter.SearchStudiesAsync(AQuery(patientName: "DOE"));

        handler.LastQuery.Should().NotContain("StudyDate");
    }

    // -----------------------------------------------------------------------
    // 5. Combination.
    // -----------------------------------------------------------------------

    /// <summary>
    /// Multiple parameters are AND in QIDO-RS, which was confirmed against the
    /// deployment. One request carries all of them.
    /// </summary>
    [Fact]
    public async Task CombinesEveryConditionIntoOneRequest()
    {
        (OrthancDicomWebAdapter adapter, RecordingHandler handler) = Given(1);

        await adapter.SearchStudiesAsync(AQuery(
            patientName: "DOE",
            accessionNumber: "ACC",
            from: new DateOnly(2018, 1, 1),
            to: new DateOnly(2018, 12, 31)));

        handler.Requests.Should().ContainSingle();
        handler.LastQuery.Should().Contain("PatientName=DOE*")
            .And.Contain("AccessionNumber=ACC*")
            .And.Contain("StudyDate=20180101-20181231");
    }

    [Fact]
    public async Task NeverAsksForFuzzyMatching()
    {
        (OrthancDicomWebAdapter adapter, RecordingHandler handler) = Given(1);

        await adapter.SearchStudiesAsync(AQuery(patientName: "DOE"));

        handler.LastQuery.Should().NotContain("fuzzy");
    }

    // -----------------------------------------------------------------------
    // 6/7. Paging.
    // -----------------------------------------------------------------------

    /// <summary>
    /// One more than the page holds: the archive reports no total, so the extra
    /// result is the only available evidence that another page exists.
    /// </summary>
    [Theory]
    [InlineData(1, 2)]
    [InlineData(25, 26)]
    [InlineData(100, 101)]
    public async Task AsksForOneResultBeyondThePage(int pageSize, int expectedLimit)
    {
        (OrthancDicomWebAdapter adapter, RecordingHandler handler) = Given(1);

        await adapter.SearchStudiesAsync(AQuery(pageSize: pageSize));

        handler.LastQuery.Should().Contain("limit=" + expectedLimit.ToString(CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData(0, 25, 0)]
    [InlineData(1, 25, 25)]
    [InlineData(4, 10, 40)]
    public async Task TranslatesThePageIntoAnOffset(int page, int pageSize, int expectedOffset)
    {
        (OrthancDicomWebAdapter adapter, RecordingHandler handler) = Given(1);

        await adapter.SearchStudiesAsync(AQuery(page: page, pageSize: pageSize));

        handler.LastQuery.Should().Contain("offset=" + expectedOffset.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// The ceiling is enforced here as well as at the API, because this service
    /// has its own HTTP surface.
    /// </summary>
    [Theory]
    [InlineData(101)]
    [InlineData(5000)]
    public async Task RefusesToAskForMoreThanAHundred(int requested)
    {
        (OrthancDicomWebAdapter adapter, RecordingHandler handler) = Given(1);

        StudySearchResult result = await adapter.SearchStudiesAsync(AQuery(pageSize: requested));

        handler.LastQuery.Should().Contain("limit=101");
        result.PageSize.Should().Be(100);
    }

    [Fact]
    public async Task ClampsAPageSizeBelowOne()
    {
        (OrthancDicomWebAdapter adapter, RecordingHandler handler) = Given(1);

        StudySearchResult result = await adapter.SearchStudiesAsync(AQuery(pageSize: 0));

        handler.LastQuery.Should().Contain("limit=2");
        result.PageSize.Should().Be(1);
    }

    [Fact]
    public async Task ClampsANegativePageToTheFirstOne()
    {
        (OrthancDicomWebAdapter adapter, RecordingHandler handler) = Given(1);

        StudySearchResult result = await adapter.SearchStudiesAsync(AQuery(page: -3));

        handler.LastQuery.Should().Contain("offset=0");
        result.Page.Should().Be(0);
    }

    // -----------------------------------------------------------------------
    // 8. HasMore.
    // -----------------------------------------------------------------------

    /// <summary>
    /// The extra result is evidence, not a result: it is counted and dropped.
    /// </summary>
    [Fact]
    public async Task ReportsMoreAndDropsTheExtraResultWhenThePageOverflows()
    {
        (OrthancDicomWebAdapter adapter, _) = Given(11);

        StudySearchResult result = await adapter.SearchStudiesAsync(AQuery(pageSize: 10));

        result.HasMore.Should().BeTrue();
        result.Items.Should().HaveCount(10);
    }

    [Fact]
    public async Task ReportsNoMoreWhenThePageIsNotFull()
    {
        (OrthancDicomWebAdapter adapter, _) = Given(4);

        StudySearchResult result = await adapter.SearchStudiesAsync(AQuery(pageSize: 10));

        result.HasMore.Should().BeFalse();
        result.Items.Should().HaveCount(4);
    }

    [Fact]
    public async Task ReportsNoMoreWhenThePageIsExactlyFull()
    {
        (OrthancDicomWebAdapter adapter, _) = Given(10);

        StudySearchResult result = await adapter.SearchStudiesAsync(AQuery(pageSize: 10));

        result.HasMore.Should().BeFalse();
        result.Items.Should().HaveCount(10);
    }

    [Fact]
    public async Task MatchingNothingIsAnEmptyPageRatherThanAFailure()
    {
        (OrthancDicomWebAdapter adapter, _) = Given(0);

        StudySearchResult result = await adapter.SearchStudiesAsync(AQuery(patientName: "NOBODY"));

        result.Items.Should().BeEmpty();
        result.HasMore.Should().BeFalse();
    }

    // -----------------------------------------------------------------------
    // 9. No N+1.
    // -----------------------------------------------------------------------

    /// <summary>
    /// A hundred hits must cost one request. Resolving each hit's patient
    /// separately would be a hundred and one, against an archive shared with the
    /// viewer.
    /// </summary>
    [Fact]
    public async Task ResolvesAFullPageWithASingleRequest()
    {
        (OrthancDicomWebAdapter adapter, RecordingHandler handler) = Given(101);

        StudySearchResult result = await adapter.SearchStudiesAsync(AQuery(pageSize: 100));

        handler.Requests.Should().ContainSingle("one search is one HTTP request, whatever the page holds");
        result.Items.Should().HaveCount(100);
        result.HasMore.Should().BeTrue();
    }

    // -----------------------------------------------------------------------
    // 10. Mapping.
    // -----------------------------------------------------------------------

    /// <summary>
    /// Every patient attribute a result row needs is already on the study
    /// result, which is why no second lookup is required.
    /// </summary>
    [Fact]
    public async Task BuildsThePatientFromTheSameResponseAsTheStudy()
    {
        (OrthancDicomWebAdapter adapter, _) = Given(1);

        StudySearchResult result = await adapter.SearchStudiesAsync(AQuery(patientName: "DOE"));

        StudySearchItem hit = result.Items.Should().ContainSingle().Subject;

        hit.Patient.MedicalRecordNumber.Should().Be(_mrn + "0");
        hit.Patient.FullName.Should().Be("DOE JOHN");
        hit.Patient.BirthDate.Should().Be("1971-07-24");
        hit.Patient.Gender.Should().Be("Male");

        hit.Study.StudyInstanceUid.Should().Be("1.2.840.113564.0");
        hit.Study.AccessionNumber.Should().Be("ACC0");
        hit.Study.PerformedOn.Should().Be("2018-03-14");
        hit.Study.Modality.Should().Be("CT");
        hit.Study.SeriesCount.Should().Be(2);
        hit.Study.InstanceCount.Should().Be(57);
    }

    [Fact]
    public async Task TheRequestGoesToTheStudiesResource()
    {
        (OrthancDicomWebAdapter adapter, RecordingHandler handler) = Given(1);

        await adapter.SearchStudiesAsync(AQuery(patientName: "DOE"));

        handler.LastRequest.AbsolutePath.Should().Be("/dicom-web/studies");
    }

    /// <summary>Guards the fixture itself: a malformed body would make every mapping test meaningless.</summary>
    [Fact]
    public void TheFixtureIsValidDicomJson()
    {
        using JsonDocument document = JsonDocument.Parse(ArchiveHolding(2));

        document.RootElement.GetArrayLength().Should().Be(2);
    }
}
