using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CloudFilm.Application.Abstractions;
using CloudFilm.Application.Auth;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using NSubstitute;

namespace CloudFilm.Api.Tests.Auth;

/// <summary>
/// A host that boots without PostgreSQL, Redis or the Retrieve Service.
/// </summary>
/// <remarks>
/// <para>
/// Only CloudFilm's own hosted services are removed - the migrator and the
/// seeder, the two components that touch a database at start-up. Removing every
/// <see cref="IHostedService"/> would also remove the web host's own, and
/// nothing would serve.
/// </para>
/// <para>
/// Nothing else needs a live dependency: the DbContext is resolved per request,
/// the Redis cache connects lazily, and no test here reaches a route that uses
/// either.
/// </para>
/// </remarks>
public sealed class AuthApiFactory : WebApplicationFactory<Program>
{
    /// <summary>
    /// Gets the credential verifier the host will use. Substituted, because what
    /// is under test is the HTTP contract rather than ASP.NET Identity.
    /// </summary>
    public IAuthenticationService Authentication { get; } = Substitute.For<IAuthenticationService>();

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Stated explicitly rather than inherited. Under Production the host
        // would load appsettings.json alone, where Jwt:SigningKey is empty, and
        // ValidateOnStart would refuse to start - a confusing failure that has
        // nothing to do with the test.
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

            services.RemoveAll<IAuthenticationService>();
            services.AddScoped(_ => Authentication);
        });
    }
}

/// <summary>
/// The HTTP surface of sign-in: status codes, response shapes, and the
/// indistinguishability of failures.
/// </summary>
public sealed class AuthEndpointsTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    private readonly AuthApiFactory _factory = factory;

    private void RejectEverything() =>
        _factory.Authentication
            .LoginAsync(Arg.Any<LoginRequest>(), Arg.Any<CancellationToken>())
            .Returns((LoginResult?)null);

    [Fact]
    public async Task LoginReturnsTheTokenWhenTheCredentialsAreAccepted()
    {
        DateTimeOffset expiry = DateTimeOffset.UtcNow.AddHours(1);

        _factory.Authentication
            .LoginAsync(Arg.Any<LoginRequest>(), Arg.Any<CancellationToken>())
            .Returns(new LoginResult("a.b.c", expiry, "Administrator"));

        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest("admin@example.test", "correct horse battery staple"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        LoginResult? body = await response.Content.ReadFromJsonAsync<LoginResult>();

        body.Should().NotBeNull();
        body!.AccessToken.Should().Be("a.b.c");
        body.DisplayName.Should().Be("Administrator");
    }

    [Fact]
    public async Task LoginReturns401WhenTheCredentialsAreRejected()
    {
        RejectEverything();

        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest("admin@example.test", "wrong"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Invalid credentials");
    }

    /// <summary>
    /// An unknown account and a wrong password must be indistinguishable.
    /// </summary>
    /// <remarks>
    /// Compares status code and body exactly. If a future change adds "no such
    /// user" to one branch, this fails.
    /// </remarks>
    [Fact]
    public async Task AnUnknownAccountAndAWrongPasswordAreIndistinguishable()
    {
        RejectEverything();

        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage unknownUser = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest("nobody-at-all@example.test", "any password"));

        using HttpResponseMessage wrongPassword = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest("admin@example.test", "the wrong password"));

        unknownUser.StatusCode.Should().Be(wrongPassword.StatusCode);

        string first = await unknownUser.Content.ReadAsStringAsync();
        string second = await wrongPassword.Content.ReadAsStringAsync();

        first.Should().Be(second);
    }

    [Fact]
    public async Task MeReturns401WithoutAToken()
    {
        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/api/v1/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task MeReturns401WhenTheTokenIsNotAToken()
    {
        using HttpClient client = _factory.CreateClient();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not.a.jwt");

        using HttpResponseMessage response = await client.GetAsync("/api/v1/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task MeReturnsTheCallerIdentityForAValidToken()
    {
        using HttpClient client = _factory.CreateClient();

        // Issued by the application's own registered issuer, so the token is
        // signed with the very key the bearer handler was configured with. This
        // is the end-to-end proof that issuance and validation agree.
        using IServiceScope scope = _factory.Services.CreateScope();
        IAccessTokenIssuer issuer = scope.ServiceProvider.GetRequiredService<IAccessTokenIssuer>();

        AccessToken token = issuer.Issue(
            "11111111-2222-3333-4444-555555555555",
            "admin@example.test",
            "Administrator");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Value);

        using HttpResponseMessage response = await client.GetAsync("/api/v1/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        CurrentUser? user = await response.Content.ReadFromJsonAsync<CurrentUser>();

        user.Should().NotBeNull();
        user!.UserId.Should().Be("11111111-2222-3333-4444-555555555555");
        user.Email.Should().Be("admin@example.test");
        user.DisplayName.Should().Be("Administrator");
    }
}
