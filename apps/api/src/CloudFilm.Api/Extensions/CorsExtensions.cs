using CloudFilm.Api.Configuration;

namespace CloudFilm.Api.Extensions;

/// <summary>
/// Cross origin policy for the browser portal.
/// </summary>
internal static class CorsExtensions
{
    /// <summary>
    /// The name of the portal CORS policy.
    /// </summary>
    public const string PortalPolicy = "cloudfilm-portal";

    /// <summary>
    /// Registers a CORS policy limited to the configured portal origins.
    /// </summary>
    /// <param name="builder">The application builder.</param>
    /// <returns>The same builder, to allow chaining.</returns>
    public static WebApplicationBuilder AddCloudFilmCors(this WebApplicationBuilder builder)
    {
        builder.Services.AddOptions<PortalOptions>()
            .Bind(builder.Configuration.GetSection(PortalOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        PortalOptions portal = builder.Configuration
            .GetSection(PortalOptions.SectionName)
            .Get<PortalOptions>() ?? new PortalOptions();

        builder.Services.AddCors(cors =>
            cors.AddPolicy(PortalPolicy, policy => policy
                .WithOrigins(portal.AllowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials()));

        return builder;
    }
}
