namespace CloudFilm.Api.Extensions;

/// <summary>
/// Applies the response headers required of a browser facing clinical API.
/// </summary>
internal static class SecurityHeadersMiddleware
{
    /// <summary>
    /// Adds conservative security headers to every response.
    /// </summary>
    /// <param name="app">The application.</param>
    /// <returns>The same application, to allow chaining.</returns>
    public static WebApplication UseCloudFilmSecurityHeaders(this WebApplication app)
    {
        app.Use(static async (context, next) =>
        {
            IHeaderDictionary headers = context.Response.Headers;
            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            headers["Cross-Origin-Opener-Policy"] = "same-origin";
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
            await next(context).ConfigureAwait(false);
        });

        return app;
    }
}
