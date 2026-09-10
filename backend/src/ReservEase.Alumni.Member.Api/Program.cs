using Serilog;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Options;
using ReservEase.Alumni.Common.Sdk.Services;
using ReservEase.Alumni.Mailtrap.Sdk.Extensions;
using ReservEase.Alumni.Member.Api.Extensions;
using ReservEase.Alumni.Member.Api.Options;
using ReservEase.Alumni.Member.Api.Services.Implementations;
using ReservEase.Alumni.Member.Api.Services.Interfaces;
using ReservEase.Alumni.Paystack.Sdk.Extensions;
using ReservEase.Alumni.PostgresDb.Sdk.Extensions;
using ReservEase.Alumni.PostgresDb.Sdk.Middleware;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using ReservEase.Alumni.Redis.Sdk.Extensions;
using ReservEase.Alumni.Sms.Sdk.Extensions;
using ReservEase.Alumni.Storage.Sdk.Extensions;
using ReservEase.Alumni.Whatsapp.Sdk.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Configuration layering: base → environment-specific → environment variables
builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

// Serilog
builder.Host.UseSerilog((ctx, lc) => lc
    .WriteTo.Console()
    .WriteTo.File("logs/member-api.log", rollingInterval: RollingInterval.Day)
    .ReadFrom.Configuration(ctx.Configuration));

// Token config
var tokenConfig = builder.Configuration
    .GetSection(nameof(BearerTokenConfig))
    .Get<BearerTokenConfig>()!;
builder.Services.Configure<BearerTokenConfig>(
    builder.Configuration.GetSection(nameof(BearerTokenConfig)));

builder.Services.AddHttpContextAccessor();

// Data + cache + external services
builder.Services.AddAlumniPostgresSdk(builder.Configuration, "AlumniConnection");
builder.Services.AddRedisDatabase<MemberRedisConfig>(builder.Configuration);
builder.Services.AddRedisDatabase<PublicContentCacheConfig>(builder.Configuration);
builder.Services.AddMailtrapEmailService(builder.Configuration);
builder.Services.AddPaystackService(builder.Configuration);
builder.Services.AddStorageService(builder.Configuration);
builder.Services.AddArkeselSmsService(builder.Configuration);
builder.Services.AddWaSenderWhatsAppService(builder.Configuration);

// Auth + API
// See Institution.Api's Program.cs for why this cache exists and how it's shared.
var customDomainCache = new CustomDomainCache();
builder.Services.AddSingleton<ICustomDomainCache>(customDomainCache);
builder.Services.AddHostedService<CustomDomainCacheRefresher>();
builder.Services.AddTenantAwareCors(builder.Configuration, customDomainCache);
builder.Services.AddAlumniRateLimiting();
builder.Services.AddBearerAuth(tokenConfig);
builder.Services.AddGoogleAuth(builder.Configuration);
builder.Services.AddApiVersioning(1);
builder.Services.AddSwagger("Member API");
builder.Services.AddMemberControllers();
builder.Services.AddActorSystem();

// Application services
builder.Services.AddScoped<IMemberAuthService, MemberAuthService>();
builder.Services.AddScoped<IContributionService, ContributionService>();
builder.Services.AddScoped<ICampaignService, CampaignService>();
builder.Services.AddScoped<IMemberEventService, MemberEventService>();
builder.Services.AddScoped<IMemberJobService, MemberJobService>();
builder.Services.AddScoped<IMemberNewsService, MemberNewsService>();
builder.Services.AddScoped<IMemberForumService, MemberForumService>();
builder.Services.AddScoped<IMemberMentorshipService, MemberMentorshipService>();
builder.Services.AddScoped<IDirectoryService, DirectoryService>();
builder.Services.AddScoped<IMemberResourceService, MemberResourceService>();
builder.Services.AddScoped<ILeaderboardService, LeaderboardService>();
builder.Services.AddScoped<IBadgeService, BadgeService>();
builder.Services.AddScoped<ISpotlightService, SpotlightService>();
builder.Services.AddScoped<IReferralService, ReferralService>();
builder.Services.AddScoped<IClassNoteService, ClassNoteService>();
builder.Services.AddScoped<INotificationPreferenceService, NotificationPreferenceService>();
builder.Services.AddScoped<INotificationDispatcher, NotificationDispatcher>();
builder.Services.AddScoped<ICommunityService, CommunityService>();
builder.Services.AddScoped<IStoreOrderService, StoreOrderService>();
builder.Services.AddScoped<IAlbumService, AlbumService>();
builder.Services.AddScoped<IBusinessDirectoryService, BusinessDirectoryService>();
builder.Services.AddScoped<IDigestService, DigestService>();
builder.Services.AddHostedService<DigestSchedulerService>();
builder.Services.AddScoped<IRecurringGivingProcessor, RecurringGivingProcessor>();
builder.Services.AddHostedService<RecurringGivingSchedulerService>();
builder.Services.AddHostedService<BirthdaySpotlightSchedulerService>();

// Defense in depth, on top of each scheduler already catching its own
// exceptions internally: the default (StopHost) kills the ENTIRE API
// process — not just the offending scheduler — the moment any
// BackgroundService lets an exception escape. A background job going wrong
// should degrade (that one cycle skipped, logged, retried next tick), never
// take the whole API down with it.
builder.Services.Configure<HostOptions>(o => o.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore);

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
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Member API v1"));
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

await app.RunAsync();
