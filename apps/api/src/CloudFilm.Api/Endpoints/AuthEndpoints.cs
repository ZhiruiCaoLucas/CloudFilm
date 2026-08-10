using System.Security.Claims;
using CloudFilm.Application.Abstractions;
using CloudFilm.Application.Auth;
using Microsoft.IdentityModel.JsonWebTokens;

namespace CloudFilm.Api.Endpoints;

/// <summary>
/// Sign-in and the identity of the signed-in user.
/// </summary>
/// <remarks>
/// The endpoints hold no authentication logic. Credential verification and token
/// issuance belong to <see cref="IAuthenticationService"/>; what happens here is
/// HTTP - bind a body, choose a status code, shape a response.
/// </remarks>
internal static class AuthEndpoints
{
    /// <summary>
    /// The single response returned for every rejected sign-in.
    /// </summary>
    /// <remarks>
    /// One instance, one message, one status code. An unknown account and a
    /// wrong password must be indistinguishable from outside, so there is
    /// deliberately nowhere to put a more specific reason.
    /// </remarks>
    private static readonly AuthError _invalidCredentials = new("Invalid credentials");

    /// <summary>
    /// Maps the authentication endpoints.
    /// </summary>
    /// <param name="app">The route builder.</param>
    /// <returns>The same route builder, to allow chaining.</returns>
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder auth = app
            .MapGroup("/api/v1/auth")
            .WithTags("Authentication");

        auth.MapPost("/login", async (
                    LoginRequest request,
                    IAuthenticationService authentication,
                    CancellationToken cancellationToken) =>
            {
                LoginResult? result = await authentication
                    .LoginAsync(request, cancellationToken)
                    .ConfigureAwait(false);

                return result is null
                    ? Results.Json(_invalidCredentials, statusCode: StatusCodes.Status401Unauthorized)
                    : Results.Ok(result);
            })
            .WithName("Login")
            .WithSummary("Exchanges credentials for an access token.")
            .AllowAnonymous()
            .Produces<LoginResult>()
            .Produces<AuthError>(StatusCodes.Status401Unauthorized);

        auth.MapGet("/me", (ClaimsPrincipal principal) =>
            {
                // Read straight from the validated token. No database round trip:
                // sub, email and name are in the token precisely so that
                // identifying the caller costs nothing on every request.
                CurrentUser user = new(
                    principal.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? string.Empty,
                    principal.FindFirstValue(JwtRegisteredClaimNames.Email) ?? string.Empty,
                    principal.FindFirstValue(JwtRegisteredClaimNames.Name) ?? string.Empty);

                return Results.Ok(user);
            })
            .WithName("CurrentUser")
            .WithSummary("Returns the signed-in user, as carried by the bearer token.")
            .RequireAuthorization()
            .Produces<CurrentUser>()
            .Produces(StatusCodes.Status401Unauthorized);

        return app;
    }
}

/// <summary>
/// The body returned when a sign-in is rejected.
/// </summary>
/// <param name="Error">A message that describes nothing an attacker could use.</param>
internal sealed record AuthError(string Error);
