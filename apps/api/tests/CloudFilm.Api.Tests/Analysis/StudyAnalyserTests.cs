using System.Globalization;
using System.Net;
using System.Text;
using CloudFilm.Application.Analysis;
using CloudFilm.Application.Imaging;
using CloudFilm.Infrastructure.Analysis;
using FluentAssertions;
namespace CloudFilm.Api.Tests.Analysis;


/// <summary>
/// What the adapter sends to the analysis service, and what it accepts back.
/// </summary>
public sealed class StudyAnalyserTests
{
    private const string _study = "1.2.840.113564.1";
    private const string _otherStudy = "1.2.840.113564.999";
    private const string _boundary = "wado-boundary-123";

    /// <summary>
    /// A well-formed answer from the analysis service, as it arrives on the wire.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Field names are camelCase because the adapter reads them with
    /// <c>JsonSerializerDefaults.Web</c>, and they mirror the Python service's own
    /// response: <c>studyInstanceUid</c>, <c>modelName</c>, <c>modelVersion</c>,
    /// <c>completedAtUtc</c> and <c>findings</c>. <c>completedAtUtc</c>, not
    /// <c>analysedAtUtc</c> - the Application record spells it the first way and
    /// both sides have a test pinning that.
    /// </para>
    /// <para>
    /// The values are not arbitrary. Every one of them is asserted somewhere in
    /// this file, and two tests reach into this string and edit it: one swaps the
    /// study identifier for another study's, and one substitutes the score. So the
    /// study UID must appear exactly once, and <c>"score": 0.5</c> must appear
    /// exactly once - which is why the first finding scores 1.0. A finding scored
    /// 1 means "observed" and one scored otherwise carries a measurement, which is
    /// the convention the service and the portal both follow.
    /// </para>
    /// </remarks>
    private const string _result = """
        {
          "studyInstanceUid": "1.2.840.113564.1",
          "modelName": "dicom-characterization",
          "modelVersion": "0.1.0",
          "completedAtUtc": "2026-08-16T09:00:00Z",
          "findings": [
            { "label": "instances=2", "score": 1.0 },
            { "label": "mean-pixel-intensity-normalised", "score": 0.5 }
          ]
        }
        """;

    // 其他测试代码……

    private sealed class RecordingHandler(HttpStatusCode status, string body)
        : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        public string RequestBody { get; private set; } = string.Empty;

        public string? RequestContentType { get; private set; }

        public long? ObservedContentLength { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Request = request;
            RequestContentType = request.Content?.Headers.ContentType?.ToString();

            // 必须在 SendAsync 中、Content 被 dispose 之前读取
            ObservedContentLength = request.Content?.Headers.ContentLength;

            if (request.Content is not null)
            {
                RequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            return new HttpResponseMessage(status)
            {
                Content = new StringContent(
                    body,
                    Encoding.UTF8,
                    "application/json"),
            };
        }
    }

    // 下面继续是其他测试……

    /// <summary>A WADO-RS study response, as the archive would send it.</summary>
    private static StudyDownload AStudyDownload(
        int instances = 2,
        string boundary = _boundary,
        string? contentType = null)
    {
        StringBuilder body = new();

        for (int instance = 1; instance <= instances; instance++)
        {
            body.Append(CultureInfo.InvariantCulture, $"--{boundary}\r\n");
            body.Append("Content-Type: application/dicom\r\n\r\n");
            body.Append(CultureInfo.InvariantCulture, $"DICM-instance-{instance}");
            body.Append("\r\n");
        }

        body.Append(CultureInfo.InvariantCulture, $"--{boundary}--\r\n");

        MemoryStream stream = new(Encoding.ASCII.GetBytes(body.ToString()));

        return new StudyDownload(
            stream,
            contentType ?? $"multipart/related; type=\"application/dicom\"; boundary={boundary}");
    }

    private static (StudyAnalyser Analyser, RecordingHandler Handler) Given(
        HttpStatusCode status = HttpStatusCode.OK,
        string body = _result)
    {
        RecordingHandler handler = new(status, body);

        HttpClient client = new(handler, disposeHandler: false)
        {
            BaseAddress = new Uri("http://analysis.test/"),
        };

        return (new StudyAnalyser(client), handler);
    }

    // -----------------------------------------------------------------------
    // What is sent.
    // -----------------------------------------------------------------------

    /// <summary>
    /// The two multiparts are not the same shape. WADO-RS answers
    /// `multipart/related` with unnamed parts; the analyser accepts a form upload.
    /// Forwarding the body unchanged would be rejected by the service, and this is
    /// the assertion that says the rewrite happened.
    /// </summary>
    [Fact]
    public async Task TheStudyIsRewrittenAsAFormUpload()
    {
        (StudyAnalyser analyser, RecordingHandler handler) = Given();

        await using StudyDownload study = AStudyDownload();

        await analyser.AnalyseAsync(_study, study);

        handler.RequestContentType.Should().StartWith("multipart/form-data");
        handler.RequestContentType.Should().NotContain("related");
        handler.RequestBody.Should().Contain("Content-Disposition: form-data; name=\"files\"");
    }

    /// <summary>
    /// One part per instance, under the field name the service reads them from.
    /// </summary>
    [Fact]
    public async Task EveryInstanceBecomesItsOwnFilePart()
    {
        (StudyAnalyser analyser, RecordingHandler handler) = Given();

        await using StudyDownload study = AStudyDownload(instances: 3);

        await analyser.AnalyseAsync(_study, study);

        CountOf(handler.RequestBody, "name=\"files\"").Should().Be(3);

        handler.RequestBody.Should().Contain("DICM-instance-1");
        handler.RequestBody.Should().Contain("DICM-instance-2");
        handler.RequestBody.Should().Contain("DICM-instance-3");
    }

    /// <summary>
    /// The instance bytes cross unchanged. This layer moves DICOM; it does not
    /// re-encode it, and a study that arrived intact must leave intact.
    /// </summary>
    [Fact]
    public async Task TheInstanceBytesAreNotAltered()
    {
        (StudyAnalyser analyser, RecordingHandler handler) = Given();

        await using StudyDownload study = AStudyDownload(instances: 1);

        await analyser.AnalyseAsync(_study, study);

        handler.RequestBody.Should().Contain("Content-Type: application/dicom");
        handler.RequestBody.Should().Contain("DICM-instance-1\r\n");
    }

    /// <summary>
    /// The correlation values the service accepts, and no others. There is no
    /// field here that could tell it where to fetch anything.
    /// </summary>
    [Fact]
    public async Task TheStudyIdentifierAndARequestIdentifierTravelAsFormFields()
    {
        (StudyAnalyser analyser, RecordingHandler handler) = Given();

        await using StudyDownload study = AStudyDownload();

        await analyser.AnalyseAsync(_study, study);

        handler.RequestBody.Should().Contain("name=\"studyInstanceUid\"");
        handler.RequestBody.Should().Contain(_study);
        handler.RequestBody.Should().Contain("name=\"requestId\"");
    }

    /// <summary>
    /// A correlation value must be able to identify a call and nothing else. A
    /// request id drawn from the study, the patient or a token would put those in
    /// the analyser's logs - the one place this design keeps them out of.
    /// </summary>
    [Fact]
    public async Task TheRequestIdentifierIsFreshAndCarriesNothingAboutTheStudy()
    {
        (StudyAnalyser analyser, RecordingHandler handler) = Given();

        await using StudyDownload first = AStudyDownload();
        await analyser.AnalyseAsync(_study, first);
        string firstId = RequestIdIn(handler.RequestBody);

        await using StudyDownload second = AStudyDownload();
        await analyser.AnalyseAsync(_study, second);
        string secondId = RequestIdIn(handler.RequestBody);

        firstId.Should().NotBe(secondId);
        firstId.Should().NotContain(_study);
        firstId.Should().MatchRegex("^[0-9a-f]{32}$");
    }

    [Fact]
    public async Task TheServiceIsAskedOnItsOwnRoute()
    {
        (StudyAnalyser analyser, RecordingHandler handler) = Given();

        await using StudyDownload study = AStudyDownload();

        await analyser.AnalyseAsync(_study, study);

        handler.Request!.Method.Should().Be(HttpMethod.Post);
        handler.Request.RequestUri!.ToString().Should().Be("http://analysis.test/analyze");
    }

    /// <summary>
    /// A study is large. The length of the rewritten upload is not known until it
    /// has been written, and refusing to state one is what keeps this streaming
    /// instead of buffering a study to measure it.
    /// </summary>
    [Fact]
    public async Task TheUploadIsStreamedRatherThanMeasuredFirst()
    {
        (StudyAnalyser analyser, RecordingHandler handler) = Given();

        await using StudyDownload study = AStudyDownload();

        await analyser.AnalyseAsync(_study, study);

        handler.ObservedContentLength.Should().BeNull();
    }

    /// <summary>
    /// Anything the archive sends that is not an instance is dropped rather than
    /// forwarded. The analyser is given DICOM and nothing else.
    /// </summary>
    [Fact]
    public async Task APartThatIsNotDicomIsNotForwarded()
    {
        (StudyAnalyser analyser, RecordingHandler handler) = Given();

        string body =
            $"--{_boundary}\r\nContent-Type: application/dicom\r\n\r\nDICM-instance-1\r\n"
            + $"--{_boundary}\r\nContent-Type: application/json\r\n\r\n{{\"warning\":\"partial\"}}\r\n"
            + $"--{_boundary}--\r\n";

        await using StudyDownload study = new(
            new MemoryStream(Encoding.ASCII.GetBytes(body)),
            $"multipart/related; boundary={_boundary}");

        await analyser.AnalyseAsync(_study, study);

        CountOf(handler.RequestBody, "name=\"files\"").Should().Be(1);
        handler.RequestBody.Should().NotContain("partial");
    }

    // -----------------------------------------------------------------------
    // The boundary.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("multipart/related; type=\"application/dicom\"; boundary=abc123", "abc123")]
    [InlineData("multipart/related; boundary=\"quoted-boundary\"", "quoted-boundary")]
    [InlineData("MULTIPART/RELATED; BOUNDARY=upper", "upper")]
    public void TheBoundaryIsReadFromTheArchivesContentType(string contentType, string expected) =>
        DicomFormDataContent.BoundaryOf(contentType).Should().Be(expected);

    /// <summary>
    /// Without a boundary the response cannot be split, and guessing one would
    /// mean sending something arbitrary to the analyser.
    /// </summary>
    [Theory]
    [InlineData("multipart/related")]
    [InlineData("application/dicom")]
    [InlineData("")]
    public void AResponseThatCannotBeSplitIsRefused(string contentType) =>
        FluentActions.Invoking(() => DicomFormDataContent.BoundaryOf(contentType))
            .Should().Throw<InvalidOperationException>();

    /// <summary>
    /// The boundary this adapter generates must not appear inside a DICOM
    /// instance, or the upload would be split in the wrong place. Random is the
    /// only way to be sure of that against binary content.
    /// </summary>
    [Fact]
    public async Task EachRequestUsesItsOwnBoundary()
    {
        (StudyAnalyser analyser, RecordingHandler handler) = Given();

        await using StudyDownload first = AStudyDownload();
        await analyser.AnalyseAsync(_study, first);
        string firstBoundary = handler.RequestContentType!;

        await using StudyDownload second = AStudyDownload();
        await analyser.AnalyseAsync(_study, second);

        handler.RequestContentType.Should().NotBe(firstBoundary);
    }

    // -----------------------------------------------------------------------
    // What comes back.
    // -----------------------------------------------------------------------

    [Fact]
    public async Task TheServicesAnswerBecomesTheApplicationContract()
    {
        (StudyAnalyser analyser, _) = Given();

        await using StudyDownload study = AStudyDownload();

        StudyAnalysis? analysis = await analyser.AnalyseAsync(_study, study);

        analysis.Should().NotBeNull();
        analysis!.StudyInstanceUid.Should().Be(_study);
        analysis.ModelName.Should().Be("dicom-characterization");
        analysis.ModelVersion.Should().Be("0.1.0");
        analysis.CompletedAtUtc.Should().Be(new DateTimeOffset(2026, 8, 16, 9, 0, 0, TimeSpan.Zero));
        analysis.Findings.Should().HaveCount(2);
        analysis.Findings[0].Label.Should().Be("instances=2");
        analysis.Findings[1].Score.Should().Be(0.5d);
    }

    /// <summary>
    /// The identifier CloudFilm authorised is the one the result carries. The
    /// service's own answer is checked against it and never substituted for it.
    /// </summary>
    [Fact]
    public async Task AnAnswerAboutADifferentStudyIsRefused()
    {
        (StudyAnalyser analyser, _) = Given(body: _result.Replace(_study, _otherStudy, StringComparison.Ordinal));

        await using StudyDownload study = AStudyDownload();

        await FluentActions.Invoking(() => analyser.AnalyseAsync(_study, study))
            .Should().ThrowAsync<AnalysisFailedException>();
    }

    /// <summary>
    /// A score outside the contract's range would reach a clinician as a number
    /// nobody can interpret. This is the last place it can be stopped.
    /// </summary>
    [Theory]
    [InlineData("1.5")]
    [InlineData("-0.1")]
    public async Task AScoreOutsideTheContractsRangeIsRefused(string score)
    {
        (StudyAnalyser analyser, _) = Given(
            body: _result.Replace("\"score\": 0.5", $"\"score\": {score}", StringComparison.Ordinal));

        await using StudyDownload study = AStudyDownload();

        await FluentActions.Invoking(() => analyser.AnalyseAsync(_study, study))
            .Should().ThrowAsync<AnalysisFailedException>();
    }

    [Fact]
    public async Task AResultWithNoModelIsRefused()
    {
        (StudyAnalyser analyser, _) = Given(
            body: """{"studyInstanceUid":"1.2.840.113564.1","completedAtUtc":"2026-08-16T09:00:00Z","findings":[]}""");

        await using StudyDownload study = AStudyDownload();

        await FluentActions.Invoking(() => analyser.AnalyseAsync(_study, study))
            .Should().ThrowAsync<AnalysisFailedException>();
    }

    /// <summary>
    /// A failure is not an empty result. The caller must be able to tell "the
    /// analyser is not answering" from "there was nothing to say", because one is
    /// worth retrying and the other is not.
    /// </summary>
    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task AFailingServiceThrowsRatherThanReturningNothing(HttpStatusCode status)
    {
        (StudyAnalyser analyser, _) = Given(status, body: """{"detail":"nope"}""");

        await using StudyDownload study = AStudyDownload();

        (await FluentActions.Invoking(() => analyser.AnalyseAsync(_study, study))
            .Should().ThrowAsync<AnalysisFailedException>())
            .Which.StatusCode.Should().Be(status);
    }

    /// <summary>
    /// A failure message is read by an operator and written to a log. The service's
    /// own response body has been through nothing that checked it, and it came from
    /// a process holding a patient's imaging.
    /// </summary>
    [Fact]
    public async Task AFailureNeverQuotesTheServicesResponse()
    {
        (StudyAnalyser analyser, _) = Given(
            HttpStatusCode.InternalServerError,
            body: """{"detail":"ZZTESTPATIENT^UNMISTAKABLE"}""");

        await using StudyDownload study = AStudyDownload();

        Exception thrown = (await FluentActions.Invoking(() => analyser.AnalyseAsync(_study, study))
            .Should().ThrowAsync<AnalysisFailedException>()).Which;

        thrown.Message.Should().NotContain("ZZTESTPATIENT");
        thrown.Message.Should().Contain("500");
    }

    /// <summary>
    /// A study the archive returned with no instances in it. Nothing was analysed
    /// because there was nothing to analyse, which is an answer rather than a
    /// fault - and the one case this adapter reports as null.
    /// </summary>
    [Fact]
    public async Task AStudyWithNoInstancesYieldsNoAnalysis()
    {
        (StudyAnalyser analyser, _) = Given(HttpStatusCode.BadRequest, body: """{"detail":"empty"}""");

        await using StudyDownload study = new(
            new MemoryStream(Encoding.ASCII.GetBytes($"--{_boundary}--\r\n")),
            $"multipart/related; boundary={_boundary}");

        StudyAnalysis? analysis = await analyser.AnalyseAsync(_study, study);

        analysis.Should().BeNull();
    }

    // -----------------------------------------------------------------------
    // What is refused before anything is sent.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AStudyWithoutAnIdentifierIsRefused(string uid)
    {
        (StudyAnalyser analyser, RecordingHandler handler) = Given();

        await using StudyDownload study = AStudyDownload();

        await FluentActions.Invoking(() => analyser.AnalyseAsync(uid, study))
            .Should().ThrowAsync<ArgumentException>();

        handler.Request.Should().BeNull("nothing should reach the analyser");
    }

    [Fact]
    public async Task AnIdentifierLongerThanTheContractAllowsIsRefused()
    {
        (StudyAnalyser analyser, RecordingHandler handler) = Given();

        await using StudyDownload study = AStudyDownload();

        string tooLong = new('1', AnalysisLimits.MaxStudyInstanceUidLength + 1);

        await FluentActions.Invoking(() => analyser.AnalyseAsync(tooLong, study))
            .Should().ThrowAsync<ArgumentException>();

        handler.Request.Should().BeNull();
    }

    private static int CountOf(string haystack, string needle)
    {
        int count = 0;
        int index = haystack.IndexOf(needle, StringComparison.Ordinal);

        while (index >= 0)
        {
            count++;
            index = haystack.IndexOf(needle, index + needle.Length, StringComparison.Ordinal);
        }

        return count;
    }

    /// <summary>The value of the requestId field in a captured multipart body.</summary>
    private static string RequestIdIn(string body)
    {
        const string marker = "name=\"requestId\"\r\n\r\n";

        int start = body.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
        int end = body.IndexOf("\r\n", start, StringComparison.Ordinal);

        return body[start..end];
    }
}
