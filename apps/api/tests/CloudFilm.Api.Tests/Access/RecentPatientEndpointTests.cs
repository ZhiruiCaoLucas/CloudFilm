using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using CloudFilm.Application.Abstractions;
using CloudFilm.Application.Access;
using CloudFilm.Application.Auth;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using NSubstitute.ClearExtensions;

namespace CloudFilm.Api.Tests.Access;

public sealed class AccessApiFactory : WebApplicationFactory<Program>
{
    public IRecentPatientAccess RecentPatients { get; } = Substitute.For<IRecentPatientAccess>();

    public void ResetSubstitutes()
    {
        RecentPatients.ClearSubstitute(ClearOptions.All);

        RecentPatients
            .GetRecentPatientsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<RecentPatient>>([]));
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureTestServices(services =>
        {
            foreach (ServiceDescriptor descriptor in services
                .Where(service => service.ServiceType == typeof(IHostedService)
                    && service.ImplementationType?.Namespace?.StartsWith("CloudFilm", StringComparison.Ordinal) == true)
                .ToList())
            {
                services.Remove(descriptor);
            }

            services.RemoveAll<IRecentPatientAccess>();
            services.AddScoped(_ => RecentPatients);
        });
    }
}

public sealed class RecentPatientEndpointTests : IClassFixture<AccessApiFactory>
{
    private const string _path = "/api/v1/access/recent";

    private static readonly Guid _actor = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid _otherActor = Guid.Parse("99999999-8888-7777-6666-555555555555");

    private readonly AccessApiFactory _factory;

    public RecentPatientEndpointTests(AccessApiFactory factory)
    {
        _factory = factory;

        _factory.ResetSubstitutes();
    }

    private HttpClient CreateSignedInClient(Guid actorUserId)
    {
        HttpClient client = _factory.CreateClient();

        using IServiceScope scope = _factory.Services.CreateScope();
        IAccessTokenIssuer issuer = scope.ServiceProvider.GetRequiredService<IAccessTokenIssuer>();

        AccessToken token = issuer.Issue(
            actorUserId,
            "clinician@example.test",
            "Dr. Example",
            [RoleNames.Clinician]);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token.Value);

        return client;
    }

    [Fact]
    public async Task RejectsAnonymousRequests()
    {
        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(_path);

        response.StatusCode.Should().Be(
            HttpStatusCode.Unauthorized,
            "a list of patients a clinician opened is itself patient-identifying");

        await _factory.RecentPatients.DidNotReceiveWithAnyArgs()
            .GetRecentPatientsAsync(default, default);
    }

    [Fact]
    public async Task ReadsTheHistoryOfTheAccountInTheToken()
    {
        using HttpClient client = CreateSignedInClient(_actor);

        using HttpResponseMessage response = await client.GetAsync(_path);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await _factory.RecentPatients.Received(1)
            .GetRecentPatientsAsync(_actor, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// There is no parameter for whose history to read - but a route that grew
    /// one silently would still answer 200, so absence is asserted rather than
    /// assumed.
    /// </summary>
    [Theory]
    [InlineData("?actorUserId=99999999-8888-7777-6666-555555555555")]
    [InlineData("?userId=99999999-8888-7777-6666-555555555555")]
    [InlineData("?action=LOGIN_SUCCEEDED&resourceType=Authentication")]
    [InlineData("?take=1000")]
    public async Task IgnoresEveryQueryParameterTheClientInvents(string query)
    {
        using HttpClient client = CreateSignedInClient(_actor);

        using HttpResponseMessage response = await client.GetAsync(_path + query);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await _factory.RecentPatients.Received(1)
            .GetRecentPatientsAsync(_actor, Arg.Any<CancellationToken>());
        await _factory.RecentPatients.DidNotReceive()
            .GetRecentPatientsAsync(_otherActor, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The response carries medical record numbers and nothing else - no actor,
    /// no correlation id, no timestamps, and no hint that an audit table is what
    /// answered the question.
    /// </summary>
    [Fact]
    public async Task ReturnsMedicalRecordNumbersAndNothingElse()
    {
        _factory.RecentPatients
            .GetRecentPatientsAsync(_actor, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<RecentPatient>>(
                [new RecentPatient("1000192961"), new RecentPatient("1000192962")]));

        using HttpClient client = CreateSignedInClient(_actor);

        using HttpResponseMessage response = await client.GetAsync(_path);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        string body = await response.Content.ReadAsStringAsync();

        using JsonDocument document = JsonDocument.Parse(body);

        document.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
        document.RootElement.GetArrayLength().Should().Be(2);

        foreach (JsonElement entry in document.RootElement.EnumerateArray())
        {
            entry.EnumerateObject().Select(property => property.Name)
                .Should().Equal("medicalRecordNumber");
        }

        document.RootElement[0].GetProperty("medicalRecordNumber").GetString()
            .Should().Be("1000192961");

        body.Should().NotContain(_actor.ToString());
        body.Should().NotContain("resourceId");
        body.Should().NotContain("correlationId");
        body.Should().NotContain("occurredAt");
    }

    [Fact]
    public async Task ReturnsAnEmptyListForAUserWithNoHistory()
    {
        using HttpClient client = CreateSignedInClient(_otherActor);

        using HttpResponseMessage response = await client.GetAsync(_path);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be("[]");
    }
}
