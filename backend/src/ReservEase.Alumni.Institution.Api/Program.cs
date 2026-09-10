using Serilog;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Services;
using ReservEase.Alumni.Institution.Api.Extensions;
using ReservEase.Alumni.Institution.Api.Options;
using ReservEase.Alumni.Institution.Api.Services;
using ReservEase.Alumni.Institution.Api.Services.Implementations;
using ReservEase.Alumni.Institution.Api.Services.Interfaces;
using ReservEase.Alumni.Common.Sdk.Options;
using ReservEase.Alumni.Mailtrap.Sdk.Extensions;
using ReservEase.Alumni.Paystack.Sdk.Extensions;
using ReservEase.Alumni.PostgresDb.Sdk.Extensions;
using ReservEase.Alumni.PostgresDb.Sdk.Middleware;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using ReservEase.Alumni.Redis.Sdk.Extensions;
using ReservEase.Alumni.Storage.Sdk.Extensions;
using ReservEase.Alumni.Sms.Sdk.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Configuration layering: base → environment-specific → environment variables
builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

// Serilog
builder.Host.UseSerilog((ctx, lc) => lc
    .WriteTo.Console()
    .WriteTo.File("logs/admin-api.log", rollingInterval: RollingInterval.Day)
    .ReadFrom.Configuration(ctx.Configuration));

// Token config
var tokenConfig = builder.Configuration
    .GetSection(nameof(BearerTokenConfig))
    .Get<BearerTokenConfig>()!;
builder.Services.Configure<BearerTokenConfig>(
    builder.Configuration.GetSection(nameof(BearerTokenConfig)));

// Data + cache + external services
builder.Services.AddAlumniPostgresSdk(builder.Configuration, "AlumniConnection");
builder.Services.AddRedisDatabase<InstitutionRedisConfig>(builder.Configuration);
builder.Services.AddRedisDatabase<PublicContentCacheConfig>(builder.Configuration);
builder.Services.AddStorageService(builder.Configuration);
builder.Services.AddMailtrapEmailService(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddPaystackService(builder.Configuration);
builder.Services.AddArkeselSmsService(builder.Configuration);

// Auth + API
// Custom-domain institutions (a real feature — see TenantResolutionMiddleware's
// byCustomDomain lookup) don't match the base-domain suffix check CORS uses
// otherwise, so a background refresher keeps this cache in sync with
// Institution.CustomDomain — see CustomDomainCacheRefresher and
// CorsExtensions.IsAllowedOrigin. Registered as the concrete instance (not
// just the interface) so both DI-resolved consumers and the CORS setup below
// share the exact same object.
var customDomainCache = new CustomDomainCache();
builder.Services.AddSingleton<ICustomDomainCache>(customDomainCache);
builder.Services.AddHostedService<CustomDomainCacheRefresher>();

// WithExposedHeaders: without it, browsers hide Content-Disposition from JS on
// a cross-origin response (frontend/backend are different origins in every
// real deployment) — the Reports page's CSV export reads it to name the
// downloaded file, and would otherwise silently fall back to a generic filename.
builder.Services.AddTenantAwareCors(builder.Configuration, customDomainCache, policy => policy.WithExposedHeaders("Content-Disposition"));
builder.Services.AddAlumniRateLimiting();
builder.Services.AddBearerAuth(tokenConfig);
builder.Services.AddGoogleAuth(builder.Configuration);
builder.Services.AddApiVersioning(1);
builder.Services.AddSwagger("Institution API");
builder.Services.AddAlumniControllers();
builder.Services.AddActorSystem();

// Application services
builder.Services.AddScoped<IInstitutionAuthService, InstitutionAuthService>();
builder.Services.AddScoped<IMemberManagementService, MemberManagementService>();
builder.Services.AddScoped<ICampaignService, CampaignService>();
builder.Services.AddScoped<IContributionService, ContributionService>();
builder.Services.AddScoped<IPayoutService, PayoutService>();
builder.Services.AddScoped<IJobService, JobService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IUploadService, UploadService>();
builder.Services.AddScoped<IEventService, EventService>();
builder.Services.AddScoped<INewsService, NewsService>();
builder.Services.AddScoped<IForumService, ForumService>();
builder.Services.AddScoped<IMentorshipService, MentorshipService>();
builder.Services.AddScoped<IResourceService, ResourceService>();
builder.Services.AddScoped<IInstitutionStaffService, InstitutionStaffService>();
builder.Services.AddScoped<IInstitutionSpotlightService, InstitutionSpotlightService>();
builder.Services.AddScoped<IBatchService, BatchService>();
builder.Services.AddScoped<ICommunityService, CommunityService>();
builder.Services.AddScoped<INotificationDispatcher, NotificationDispatcher>();
builder.Services.AddScoped<IBroadcastService, BroadcastService>();
builder.Services.AddScoped<ISupportTicketService, SupportTicketService>();
builder.Services.AddScoped<IStoreService, StoreService>();
builder.Services.AddScoped<IAlbumService, AlbumService>();
builder.Services.AddScoped<IBusinessDirectoryService, BusinessDirectoryService>();
builder.Services.AddScoped<INotificationPreferenceService, NotificationPreferenceService>();

// Request body size limit (50 MB)
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 50 * 1024 * 1024);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHealthChecks();

var app = builder.Build();

// Must be the very first middleware — everything downstream that reads
// Connection.RemoteIpAddress (rate limiting) or Request.Scheme depends on
// this having already run.
app.UseAlumniForwardedHeaders();
app.UseAlumniSecurityHeaders();

var enableSwagger = builder.Configuration.GetValue<bool>("ENABLE_SWAGGER", false);

// Swagger only in development, or when explicitly enabled in production.
if (app.Environment.IsDevelopment() || enableSwagger)
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Institution API v1"));
}

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();

    var useHttpsRedirect = builder.Configuration.GetValue<bool>("USE_HTTPS_REDIRECT", false);
    if (useHttpsRedirect)
    {
        app.UseHttpsRedirection();
    }
}

// Global exception handler — never expose server errors to the frontend.
// Gated on IsDevelopment(), not "!IsProduction()": a Staging/QA environment
// name would otherwise also leak stack traces to anyone who can reach it.
app.UseExceptionHandler(app.Environment.IsDevelopment());

app.UseSerilogRequestLogging();
app.UseRouting();
app.UseCors();
app.UseRateLimiter();
app.UseTenantResolution();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");
app.UseActorSystem();

await PostgresExtensionService.ApplyMigrationsAsync(app.Services);
if (!app.Environment.IsDevelopment())
{
    await DataSeeder.SeedAsync(app.Services);
}


await app.RunAsync();

