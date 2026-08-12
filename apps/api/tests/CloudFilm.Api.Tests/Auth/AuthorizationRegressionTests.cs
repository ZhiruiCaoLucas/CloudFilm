using System.Net;
using FluentAssertions;

namespace CloudFilm.Api.Tests.Auth;

/// <summary>
/// What the authorisation fallback policy does, and just as importantly what it
/// does not.
/// </summary>
/// <remarks>
/// <para>
/// The policy makes every endpoint authenticated unless it says otherwise. That
/// is the right default for a service returning patient demographics, but it
/// reaches endpoints nobody was thinking about when it was switched on - the
/// health probes above all. A probe answering 401 marks the container unhealthy,
/// and everything gated on <c>service_healthy</c> never starts. These tests
/// exist so that regression is caught here rather than by a stack that will not
/// come up.
/// </para>
/// <para>
/// Anonymous requests only. A signed-in request to the imaging endpoints would
/// reach the Retrieve Service and Orthanc, which this suite deliberately runs
/// without; that path is verified end to end against a live stack.
/// </para>
/// </remarks>
public sealed class AuthorizationRegressionTests(AuthApiFactory factory)
    : IClassFixture<AuthApiFactory>
{
    private readonly AuthApiFactory _factory = factory;

    [Theory]
    [InlineData("/api/v1/patients")]
    [InlineData("/api/v1/patients/1000192961")]
    [InlineData("/api/v1/studies/1.2.840.113564.145320946818674.1292.636566240007310666.706")]
    public async Task PhiEndpointsRejectAnonymousRequests(string path)
    {
        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(path);

        response.StatusCode.Should().Be(
            HttpStatusCode.Unauthorized,
            "patient data must never be served without a token");
    }

    [Fact]
    public async Task SystemInfoStaysAnonymous()
    {
        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/api/v1/system/info");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task LivenessProbeStaysAnonymous()
    {
        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/health/live");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Readiness must not be refused for want of a credential.
    /// </summary>
    /// <remarks>
    /// Asserts "not 401" rather than "200" on purpose. Readiness runs the
    /// PostgreSQL, Redis and Retrieve checks, and this host has none of them, so
    /// a healthy 200 is not something the test can honestly expect. What it can
    /// assert - and what the fallback policy could break - is that the probe is
    /// reachable at all.
    /// </remarks>
    [Fact]
    public async Task ReadinessProbeIsNotRefusedForWantOfAToken()
    {
        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/health/ready");

        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }
}
