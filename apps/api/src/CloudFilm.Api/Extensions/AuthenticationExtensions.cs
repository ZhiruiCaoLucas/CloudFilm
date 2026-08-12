using System.Text;
using CloudFilm.Infrastructure.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;

namespace CloudFilm.Api.Extensions;

/// <summary>
/// Authentication and authorisation wiring.
/// </summary>
internal static class AuthenticationExtensions
{
    /// <summary>
    /// Registers JWT bearer authentication and the authorisation services.
    /// </summary>
    /// <param name="builder">The application builder.</param>
    /// <returns>The same builder, to allow chaining.</returns>
    public static WebApplicationBuilder AddCloudFilmAuthentication(this WebApplicationBuilder builder)
    {
        // JwtOptions is bound and validated once, by AddInfrastructure, because
        // the token issuer lives there and Infrastructure cannot reference this
        // project. Binding it a second time here would register a duplicate
        // validation and invite the two copies to disagree. This reads the same
        // section directly, because the bearer handler is configured before the
        // service provider exists.
        JwtOptions jwt = builder.Configuration
            .GetSection(JwtOptions.SectionName)
            .Get<JwtOptions>() ?? new JwtOptions();

        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                    ClockSkew = TimeSpan.FromSeconds(jwt.ClockSkewSeconds),
                };
            });

        // Authenticated by default. The fallback policy applies to every endpoint
        // that declares no authorisation metadata of its own, which inverts the
        // failure mode: an endpoint added without thinking about authorisation is
        // now closed rather than open. For a service that returns patient
        // demographics, "forgot to protect it" must not be a silent disclosure.
        //
        // The cost is that genuinely public endpoints have to say so out loud -
        // health probes, system info and sign-in all carry AllowAnonymous, and a
        // missing one shows up immediately as a 401 rather than years later as a
        // breach. Feature modules still add their own role based policies on top.
        builder.Services.AddAuthorization(options =>
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build());

        return builder;
    }
}
