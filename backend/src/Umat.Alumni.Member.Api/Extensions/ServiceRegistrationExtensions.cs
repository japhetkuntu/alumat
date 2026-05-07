using System.Security.Claims;
using System.Text;
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
using Umat.Alumni.Member.Api.Actors;

namespace Umat.Alumni.Member.Api.Extensions;

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
                        new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config.MemberSigningKey)),
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
                Description = "API for alumni members — profile, contributions, events, jobs, news, forum, mentorship, resources.",
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
            return ActorSystem.Create("alumni-member", setup);
        });

        // Create a single shared actor to process Paystack callback messages.
        services.AddSingleton(provider =>
        {
            var system = provider.GetRequiredService<ActorSystem>();
            return system.ActorOf(DependencyResolver.For(system).Props<PaystackCallbackActor>(), "paystackCallbackProcessor");
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

    public static IServiceCollection AddMemberControllers(this IServiceCollection services)
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
