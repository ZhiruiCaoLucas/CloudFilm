using System.Net;
using System.Text;
using CloudFilm.Retrieve.Hospital;
using CloudFilm.Retrieve.Orthanc;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace CloudFilm.Retrieve.Tests;

/// <summary>
/// What the adapter asks Orthanc for when a study is downloaded.
/// </summary>
/// <remarks>
/// The seam is the HTTP message handler, so what is asserted is the request that
/// would reach the archive - its path, its Accept header, and the fact that only
/// the Study Instance UID it was given ever appears in it.
/// </remarks>
public sealed class OrthancStudyDownloadTests
{
    private const string _study = "1.2.840.113564.145320946818674.1292.636566240007310666.706";

    private sealed class RecordingHandler(HttpStatusCode status, string contentType) : HttpMessageHandler
    {
        private readonly List<HttpRequestMessage> _requests = [];

        public IReadOnlyList<HttpRequestMessage> Requests => _requests;

        public HttpRequestMessage LastRequest => _requests[^1];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            _requests.Add(request);

            HttpResponseMessage response = new(status)
            {
                Content = new ByteArrayContent(Encoding.ASCII.GetBytes("DICM-not-a-real-study")),
            };

            response.Content.Headers.Remove("Content-Type");
            response.Content.Headers.TryAddWithoutValidation("Content-Type", contentType);

            return Task.FromResult(response);
        }
    }

    private static (OrthancDicomWebAdapter Adapter, RecordingHandler Handler) Given(
        HttpStatusCode status = HttpStatusCode.OK,
        string contentType = "multipart/related; type=\"application/dicom\"; boundary=abc123")
    {
        RecordingHandler handler = new(status, contentType);

        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(OrthancDicomWebAdapter.DownloadHttpClientName).Returns(_ =>
            new HttpClient(handler, disposeHandler: false)
            {
                BaseAddress = new Uri("http://orthanc.test/dicom-web/"),
            });

        return (new OrthancDicomWebAdapter(factory, NullLogger<OrthancDicomWebAdapter>.Instance), handler);
    }

    /// <summary>
    /// One WADO-RS request, for the study it was given, on the DICOMweb root.
    /// </summary>
    [Fact]
    public async Task AsksForTheStudyItWasGiven()
    {
        (OrthancDicomWebAdapter adapter, RecordingHandler handler) = Given();

        await using StudyDownload? download = await adapter.OpenStudyAsync(_study);

        download.Should().NotBeNull();

        handler.Requests.Should().ContainSingle();
        handler.LastRequest.RequestUri!.AbsolutePath
            .Should().Be($"/dicom-web/studies/{Uri.EscapeDataString(_study)}");
    }

    /// <summary>
    /// The DICOM representation, not the rendered one. Without this header
    /// Orthanc would answer with something a viewer wants rather than something
    /// a recipient can keep.
    /// </summary>
    [Fact]
    public async Task AsksForTheDicomRepresentation()
    {
        (OrthancDicomWebAdapter adapter, RecordingHandler handler) = Given();

        await using StudyDownload? download = await adapter.OpenStudyAsync(_study);

        download.Should().NotBeNull();

        handler.LastRequest.Headers.Accept.ToString()
            .Should().Contain("application/dicom");
    }

    /// <summary>
    /// A multipart body's content type carries the boundary that makes it
    /// parseable, so it is passed on exactly as the archive wrote it.
    /// </summary>
    [Fact]
    public async Task PassesTheArchivesMediaTypeThrough()
    {
        (OrthancDicomWebAdapter adapter, _) = Given();

        await using StudyDownload? download = await adapter.OpenStudyAsync(_study);

        download!.ContentType.Should().Contain("boundary=abc123");
        download.ContentType.Should().Contain("application/dicom");
    }

    [Fact]
    public async Task AStudyTheArchiveDoesNotHoldIsNothingRatherThanAFailure()
    {
        (OrthancDicomWebAdapter adapter, _) = Given(HttpStatusCode.NotFound);

        StudyDownload? download = await adapter.OpenStudyAsync(_study);

        download.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AnEmptyIdentifierNeverReachesTheArchive(string studyInstanceUid)
    {
        (OrthancDicomWebAdapter adapter, RecordingHandler handler) = Given();

        StudyDownload? download = await adapter.OpenStudyAsync(studyInstanceUid);

        download.Should().BeNull();
        handler.Requests.Should().BeEmpty();
    }

    /// <summary>
    /// The stream is live when it is handed over: the bytes are read from it
    /// afterwards, not copied into the download beforehand.
    /// </summary>
    [Fact]
    public async Task TheStudyArrivesOnTheStreamRatherThanInMemory()
    {
        (OrthancDicomWebAdapter adapter, _) = Given();

        await using StudyDownload? download = await adapter.OpenStudyAsync(_study);

        using StreamReader reader = new(download!.Content);

        (await reader.ReadToEndAsync()).Should().Be("DICM-not-a-real-study");
    }
}
