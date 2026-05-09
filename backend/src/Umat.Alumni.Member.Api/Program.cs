using Serilog;
using Umat.Alumni.Common.Sdk.Options;
using Umat.Alumni.Mailhog.Sdk.Extensions;
using Umat.Alumni.Mailtrap.Sdk.Extensions;
using Umat.Alumni.Mailtrap.Sdk.Options;
using Umat.Alumni.Member.Api.Extensions;
using Umat.Alumni.Member.Api.Options;
using Umat.Alumni.Member.Api.Services.Implementations;
using Umat.Alumni.Member.Api.Services.Interfaces;
using Umat.Alumni.Paystack.Sdk.Extensions;
using Umat.Alumni.PostgresDb.Sdk.Extensions;
using Umat.Alumni.Redis.Sdk.Extensions;
using Umat.Alumni.Storage.Sdk.Extensions;

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

// Email template config (legacy Mailtrap template names used by services)
builder.Services.Configure<MailtrapConfig>(
    builder.Configuration.GetSection(nameof(MailtrapConfig)));

// Data + cache + external services
builder.Services.AddAlumniPostgresSdk(builder.Configuration, "AlumniConnection");
builder.Services.AddRedisDatabase<MemberRedisConfig>(builder.Configuration);
builder.Services.AddMailhogEmailService(builder.Configuration);
// builder.Services.AddMailtrapEmailService(builder.Configuration);
builder.Services.AddPaystackService(builder.Configuration);
builder.Services.AddStorageService(builder.Configuration);

// Auth + API
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddBearerAuth(tokenConfig);
builder.Services.AddApiVersioning(1);
builder.Services.AddSwagger("UMaT Alumni Member API");
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

// Request body size limit (10 MB)
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 10 * 1024 * 1024);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHealthChecks();

var app = builder.Build();

var enableSwagger = builder.Configuration.GetValue<bool>("ENABLE_SWAGGER", false);

// Swagger only in development, or when explicitly enabled in production.
if (app.Environment.IsDevelopment() || enableSwagger)
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "UMaT Alumni Member API v1"));
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
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");
app.UseActorSystem();

await PostgresExtensionService.ApplyMigrationsAsync(app.Services);

await app.RunAsync();
