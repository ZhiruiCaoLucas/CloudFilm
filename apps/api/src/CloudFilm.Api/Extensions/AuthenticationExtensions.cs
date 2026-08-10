using System.Text;
using CloudFilm.Infrastructure.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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

        // Feature modules declare their own role based policies as they are introduced.
        builder.Services.AddAuthorization();

        return builder;
    }
}
