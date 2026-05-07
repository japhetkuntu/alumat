using Serilog;
using Umat.Alumni.Admin.Api.Extensions;
using Umat.Alumni.Admin.Api.Options;
using Umat.Alumni.Mailtrap.Sdk.Options;
using Umat.Alumni.Admin.Api.Services;
using Umat.Alumni.Admin.Api.Services.Implementations;
using Umat.Alumni.Admin.Api.Services.Interfaces;
using Umat.Alumni.Common.Sdk.Options;
using Umat.Alumni.Mailhog.Sdk.Extensions;
using Umat.Alumni.Mailtrap.Sdk.Extensions;
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
    .WriteTo.File("logs/admin-api.log", rollingInterval: RollingInterval.Day)
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
builder.Services.AddRedisDatabase<AdminRedisConfig>(builder.Configuration);
builder.Services.AddStorageService(builder.Configuration);
builder.Services.AddMailhogEmailService(builder.Configuration);
// builder.Services.AddMailtrapEmailService(builder.Configuration);
builder.Services.AddPaystackService(builder.Configuration);

// Auth + API
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddBearerAuth(tokenConfig);
builder.Services.AddApiVersioning(1);
builder.Services.AddSwagger("UMaT Alumni Admin API");
builder.Services.AddAlumniControllers();
builder.Services.AddActorSystem();

// Application services
builder.Services.AddScoped<IAdminAuthService, AdminAuthService>();
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
builder.Services.AddScoped<IAdminManagementService, AdminManagementService>();
builder.Services.AddScoped<IAdminSpotlightService, AdminSpotlightService>();

// Request body size limit (10 MB)
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 10 * 1024 * 1024);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHealthChecks();

var app = builder.Build();

// Swagger only in development
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "UMaT Alumni Admin API v1"));
}

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}
app.UseHttpsRedirection();

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
await DataSeeder.SeedAsync(app.Services);

await app.RunAsync();

