using Serilog;
using Serilog.Events;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Options;
using ReservEase.Alumni.Mailtrap.Sdk.Extensions;
using ReservEase.Alumni.Sms.Sdk.Extensions;
using ReservEase.Alumni.Platform.Api.Extensions;
using ReservEase.Alumni.Platform.Api.Options;
using ReservEase.Alumni.Platform.Api.Services;
using ReservEase.Alumni.Platform.Api.Services.Implementations;
using ReservEase.Alumni.Platform.Api.Services.Interfaces;
using ReservEase.Alumni.Paystack.Sdk.Extensions;
using ReservEase.Alumni.PostgresDb.Sdk.Extensions;
using ReservEase.Alumni.Redis.Sdk.Extensions;
using ReservEase.Alumni.Reports.Sdk.Extensions;
using ReservEase.Alumni.Storage.Sdk.Extensions;
using ReservEase.Alumni.Temporal.Sdk;

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
    .WriteTo.File("logs/platform-api.log", rollingInterval: RollingInterval.Day, buffered: true, flushToDiskInterval: TimeSpan.FromSeconds(2))
    .ReadFrom.Configuration(ctx.Configuration));

// Token config
var tokenConfig = builder.Configuration
    .GetSection(nameof(BearerTokenConfig))
    .Get<BearerTokenConfig>()!;
builder.Services.Configure<BearerTokenConfig>(
    builder.Configuration.GetSection(nameof(BearerTokenConfig)));

// Data + cache
builder.Services.AddAlumniPostgresSdk(builder.Configuration, "AlumniConnection");
builder.Services.AddRedisDatabase<PlatformRedisConfig>(builder.Configuration);
builder.Services.AddStorageService(builder.Configuration);
builder.Services.AddPaystackService(builder.Configuration);
builder.Services.AddMailtrapEmailService(builder.Configuration);
builder.Services.AddArkeselSmsService(builder.Configuration);
builder.Services.AddTemporalClientProvider(builder.Configuration);
builder.Services.AddHttpContextAccessor();

// Auth + API
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddAlumniRateLimiting();
builder.Services.AddPlatformBearerAuth(tokenConfig);
builder.Services.AddGoogleAuth(builder.Configuration);
builder.Services.AddApiVersioning(1);
builder.Services.AddSwagger("Platform API");
builder.Services.AddPlatformControllers();

// Application services
builder.Services.AddScoped<IPlatformAuthService, PlatformAuthService>();
builder.Services.AddScoped<IInstitutionManagementService, InstitutionManagementService>();
builder.Services.AddScoped<IInstitutionIntegrationService, InstitutionIntegrationService>();
builder.Services.AddScoped<IPlatformMemberService, PlatformMemberService>();
builder.Services.AddScoped<IPlatformBroadcastService, PlatformBroadcastService>();
builder.Services.AddScoped<IMarketingCampaignService, MarketingCampaignService>();
builder.Services.AddScoped<IPayoutService, PayoutService>();
builder.Services.AddScoped<IInstitutionHealthService, InstitutionHealthService>();
builder.Services.AddScoped<IBatchPayoutService, BatchPayoutService>();
builder.Services.AddScoped<IGroupPayoutService, GroupPayoutService>();
builder.Services.AddScoped<IInstitutionPayoutService, InstitutionPayoutService>();
builder.Services.AddScoped<IStaffScope, StaffScope>();
builder.Services.AddScoped<ITeamService, TeamService>();
builder.Services.AddScoped<IIncentiveService, IncentiveService>();
builder.Services.AddScoped<IPlatformStaffService, PlatformStaffService>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddScoped<ISupportCaseService, SupportCaseService>();
builder.Services.AddScoped<IOnboardingLeadService, OnboardingLeadService>();
builder.Services.AddScoped<IActivationTrackingService, ActivationTrackingService>();
builder.Services.AddScoped<IActivationWorkService, ActivationWorkService>();
builder.Services.AddScoped<IAnnouncementService, AnnouncementService>();
builder.Services.AddScoped<IPlatformSettingsService, PlatformSettingsService>();
builder.Services.AddScoped<IUploadService, UploadService>();
builder.Services.AddScoped<IPlatformAnalyticsService, PlatformAnalyticsService>();
builder.Services.AddReportJobs();

builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 50 * 1024 * 1024);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHealthChecks();

var app = builder.Build();

// Establish (or attempt) the Temporal connection at startup so a failure is visible
// immediately in logs, rather than surfacing silently on the first request.
app.Services.GetRequiredService<ITemporalClientProvider>();

var enableSwagger = builder.Configuration.GetValue<bool>("ENABLE_SWAGGER", false);

if (app.Environment.IsDevelopment() || enableSwagger)
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Platform API v1"));
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
// No TenantResolutionMiddleware here — Platform.Api operates across every
// institution by design; its services explicitly IgnoreQueryFilters() where needed.
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

await PostgresExtensionService.ApplyMigrationsAsync(app.Services);
await DataSeeder.SeedAsync(app.Services);

await app.RunAsync();
