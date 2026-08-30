using Serilog;
using ReservEase.Alumni.Common.Sdk.Extensions;
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
builder.Services.AddStorageService(builder.Configuration);
builder.Services.AddMailtrapEmailService(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddPaystackService(builder.Configuration);
builder.Services.AddArkeselSmsService(builder.Configuration);

// Auth + API
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddAlumniRateLimiting();
builder.Services.AddBearerAuth(tokenConfig);
builder.Services.AddApiVersioning(1);
builder.Services.AddSwagger("Institution API");
builder.Services.AddAlumniControllers();
builder.Services.AddActorSystem();

// Application services
builder.Services.AddScoped<IInstitutionAuthService, InstitutionAuthService>();
builder.Services.AddScoped<IMemberManagementService, MemberManagementService>();
builder.Services.AddScoped<ICampaignService, CampaignService>();
builder.Services.AddScoped<IContributionService, ContributionService>();
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

// Global exception handler — never expose server errors to the frontend
app.UseExceptionHandler(!app.Environment.IsProduction());

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
await DataSeeder.SeedAsync(app.Services);

await app.RunAsync();

