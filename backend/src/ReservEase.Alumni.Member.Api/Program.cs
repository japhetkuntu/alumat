using Serilog;
using Serilog.Events;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Options;
using ReservEase.Alumni.Mailtrap.Sdk.Extensions;
using ReservEase.Alumni.Member.Api.Extensions;
using ReservEase.Alumni.Member.Api.Services.Implementations;
using ReservEase.Alumni.Member.Api.Services.Interfaces;
using ReservEase.Alumni.PaymentCallbacks.Sdk.Options;
using ReservEase.Alumni.PaymentCallbacks.Sdk.Services.Implementations;
using ReservEase.Alumni.PaymentCallbacks.Sdk.Services.Interfaces;
using ReservEase.Alumni.Paystack.Sdk.Extensions;
using ReservEase.Alumni.PostgresDb.Sdk.Extensions;
using ReservEase.Alumni.PostgresDb.Sdk.Middleware;
using ReservEase.Alumni.Redis.Sdk.Extensions;
using ReservEase.Alumni.Storage.Sdk.Extensions;
using ReservEase.Alumni.Temporal.Sdk;
using ReservEase.Alumni.WebPush.Sdk.Options;

var builder = WebApplication.CreateBuilder(args);

// Configuration layering: base → environment-specific → environment variables
builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

// Serilog
// The framework and the database layer narrate every request at Information level, several lines each, and every line is written
// to the console and the file while the request waits. That was costing as much as the work itself under load (the same traffic
// served twice as fast with it quiet), so only their warnings and errors are kept. Our own messages stay, and the request line
// below says which requests are slow. appsettings can still raise any of this.
builder.Host.UseSerilog((ctx, lc) => lc
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("System", LogEventLevel.Warning)
    .WriteTo.Console()
    .WriteTo.File("logs/member-api.log", rollingInterval: RollingInterval.Day, buffered: true, flushToDiskInterval: TimeSpan.FromSeconds(2))
    .ReadFrom.Configuration(ctx.Configuration));

// Token config
var tokenConfig = builder.Configuration
    .GetSection(nameof(BearerTokenConfig))
    .Get<BearerTokenConfig>()!;
builder.Services.Configure<BearerTokenConfig>(
    builder.Configuration.GetSection(nameof(BearerTokenConfig)));
builder.Services.Configure<WebPushConfig>(
    builder.Configuration.GetSection(nameof(WebPushConfig)));

builder.Services.AddHttpContextAccessor();

// Data + cache + external services
builder.Services.AddAlumniPostgresSdk(builder.Configuration, "AlumniConnection");
builder.Services.AddRedisDatabase<MemberRedisConfig>(builder.Configuration);
builder.Services.AddRedisDatabase<PublicContentCacheConfig>(builder.Configuration);
builder.Services.AddRedisDatabase<TenantResolutionCacheConfig>(builder.Configuration);
builder.Services.AddMailtrapEmailService(builder.Configuration);
builder.Services.AddPaystackService(builder.Configuration);
builder.Services.AddStorageService(builder.Configuration);
builder.Services.AddTemporalClientProvider(builder.Configuration);

// Auth + API
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddAlumniRateLimiting();
builder.Services.AddBearerAuth(tokenConfig);
builder.Services.AddGoogleAuth(builder.Configuration);
builder.Services.AddApiVersioning(1);
builder.Services.AddSwagger("Member API");
builder.Services.AddMemberControllers();

// Application services
builder.Services.AddScoped<IMemberAuthService, MemberAuthService>();
builder.Services.AddScoped<ReservEase.Alumni.Member.Api.Services.Implementations.IInstitutionSwitchService, ReservEase.Alumni.Member.Api.Services.Implementations.InstitutionSwitchService>();
builder.Services.AddScoped<IMemberAccountDeletionService, MemberAccountDeletionService>();
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
builder.Services.AddScoped<ICommunityService, CommunityService>();
builder.Services.AddScoped<IStoreOrderService, StoreOrderService>();
builder.Services.AddScoped<IEventTicketService, EventTicketService>();
builder.Services.AddScoped<IMemberEventUpdateService, MemberEventUpdateService>();
builder.Services.AddScoped<IServiceRequestService, ServiceRequestService>();
builder.Services.AddScoped<IAlbumService, AlbumService>();
builder.Services.AddScoped<IBusinessDirectoryService, BusinessDirectoryService>();
builder.Services.AddScoped<IHomeService, HomeService>();

// Request body size limit (50 MB)
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 50 * 1024 * 1024);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHealthChecks();

var app = builder.Build();

// Establish (or attempt) the Temporal connection at startup so a failure is visible
// immediately in logs, rather than surfacing silently on the first webhook request.
app.Services.GetRequiredService<ITemporalClientProvider>();

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

// Global exception handler — never expose server errors to the frontend
app.UseExceptionHandler(!app.Environment.IsProduction());

// One line per request, but only the ones worth reading: failures, and anything slower than the half second we aim for.
app.UseSerilogRequestLogging(o => o.GetLevel = (http, elapsedMs, ex) =>
    ex is not null || http.Response.StatusCode >= 500 ? LogEventLevel.Error
    : elapsedMs > 500 ? LogEventLevel.Warning
    : http.Response.StatusCode >= 400 ? LogEventLevel.Information
    : LogEventLevel.Debug);
app.UseRouting();
app.UseCors();
app.UseRateLimiter();
app.UseTenantResolution();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

await PostgresExtensionService.ApplyMigrationsAsync(app.Services);

await app.RunAsync();
