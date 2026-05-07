using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Akka.Actor;
using Akka.Actor.Setup;
using Akka.DependencyInjection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Versioning;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Umat.Alumni.Common.Sdk.Models;
using Umat.Alumni.Common.Sdk.Options;

namespace Umat.Alumni.Admin.Api.Extensions;

public static class ServiceRegistrationExtensions
{
    public static IServiceCollection AddBearerAuth(
        this IServiceCollection services, BearerTokenConfig config)
    {
        services.AddAuthentication(x =>
            {
                x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(opts =>
            {
                opts.SaveToken = true;
                opts.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    RequireExpirationTime = true,
                    ValidIssuer = config.Issuer,
                    ValidAudience = config.Audience,
                    IssuerSigningKeys = new[]
                    {
                        new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config.AdminSigningKey)),
                    },
                    RoleClaimType = ClaimTypes.Role,
                };
            });

        services.AddAuthorization();
        return services;
    }

    public static IServiceCollection AddApiVersioning(
        this IServiceCollection services, int majorVersion = 1)
    {
        services.AddApiVersioning(opts =>
        {
            opts.DefaultApiVersion = new ApiVersion(majorVersion, 0);
            opts.AssumeDefaultVersionWhenUnspecified = true;
            opts.ReportApiVersions = true;
            opts.ApiVersionReader = ApiVersionReader.Combine(
                new UrlSegmentApiVersionReader(),
                new HeaderApiVersionReader("x-api-version"),
                new MediaTypeApiVersionReader("x-api-version"));
        });

        services.AddVersionedApiExplorer(opts =>
        {
            opts.GroupNameFormat = "'v'VVV";
            opts.SubstituteApiVersionInUrl = true;
        });

        return services;
    }

    public static IServiceCollection AddSwagger(this IServiceCollection services, string title)
    {
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = title,
                Version = "v1",
                Description = "API for managing the UMaT Alumni platform — members, campaigns, contributions, events, jobs, news, forum, mentorship, and resources.",
                Contact = new OpenApiContact { Name = "UMaT Alumni Team", Email = "admin@umat.edu.gh" },
            });
            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Description = "Enter your JWT token in the format: Bearer {token}",
                Type = SecuritySchemeType.Http,
                Scheme = "Bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
            });
            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                    },
                    Array.Empty<string>()
                }
            });
            c.EnableAnnotations();
            c.OrderActionsBy(d => d.RelativePath);
        });

        return services;
    }

    public static IServiceCollection AddActorSystem(
        this IServiceCollection services, Action<ActorSystemSetup>? configure = null)
    {
        services.AddSingleton(provider =>
        {
            var setup = BootstrapSetup.Create()
                .And(DependencyResolverSetup.Create(provider));
            configure?.Invoke(setup);
            return ActorSystem.Create("alumni-admin", setup);
        });
        return services;
    }

    public static WebApplication UseActorSystem(this WebApplication app)
    {
        var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
        lifetime.ApplicationStopping.Register(() =>
        {
            var actorSystem = app.Services.GetService<ActorSystem>();
            actorSystem?.Terminate().Wait(TimeSpan.FromSeconds(5));
        });
        return app;
    }

    public static IServiceCollection AddAlumniControllers(this IServiceCollection services)
    {
        services.Configure<RouteOptions>(opts => opts.LowercaseUrls = true);

        services.AddControllers(opts =>
            {
                opts.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
            })
            .AddNewtonsoftJson(opts =>
            {
                opts.SerializerSettings.ContractResolver =
                    new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver();
                opts.SerializerSettings.NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore;
                opts.SerializerSettings.DateTimeZoneHandling = Newtonsoft.Json.DateTimeZoneHandling.Utc;
            })
            .ConfigureApiBehaviorOptions(opts =>
            {
                opts.InvalidModelStateResponseFactory = context =>
                {
                    var errors = context.ModelState
                        .Where(e => e.Value?.Errors?.Count > 0)
                        .Select(e => new { Field = e.Key, ErrorMessage = e.Value?.Errors?.FirstOrDefault()?.ErrorMessage ?? "Invalid" })
                        .ToList();

                    var response = new ApiResponse<object>
                    {
                        Message = "Validation errors",
                        Code = 400,
                        Errors = errors,
                    };
                    return new BadRequestObjectResult(response);
                };
            });

        return services;
    }
}
