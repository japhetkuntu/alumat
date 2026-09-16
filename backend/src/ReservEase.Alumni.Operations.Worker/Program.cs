using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ReservEase.Alumni.PaymentCallbacks.Sdk.Services.Implementations;
using ReservEase.Alumni.PaymentCallbacks.Sdk.Services.Interfaces;
using ReservEase.Alumni.PaymentCallbacks.Sdk.Options;
using ReservEase.Alumni.PaymentCallbacks.Sdk.Workflows;
using ReservEase.Alumni.Operations.Worker.Workflows.Contributions;
using ReservEase.Alumni.Operations.Worker.Workflows.ServiceRequests;
using ReservEase.Alumni.Operations.Worker.Workflows.StoreOrders;
using ReservEase.Alumni.Paystack.Sdk.Extensions;
using ReservEase.Alumni.PostgresDb.Sdk.Extensions;
using ReservEase.Alumni.Redis.Sdk.Extensions;
using ReservEase.Alumni.Sms.Sdk.Extensions;
using ReservEase.Alumni.Storage.Sdk.Extensions;
using ReservEase.Alumni.Whatsapp.Sdk.Extensions;
using Temporalio.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

// Data + external services — same registrations Member.Api makes for the services this
// worker now hosts (payment-callback processing needs the same DB/gateway/notification
// access the old in-process actor had).
builder.Services.AddAlumniPostgresSdk(builder.Configuration, "AlumniConnection");
builder.Services.AddRedisDatabase<MemberRedisConfig>(builder.Configuration);
builder.Services.AddPaystackService(builder.Configuration);
builder.Services.AddArkeselSmsService(builder.Configuration);
builder.Services.AddWaSenderWhatsAppService(builder.Configuration);
builder.Services.AddStorageService(builder.Configuration);

builder.Services.AddScoped<IStoreOrderService, StoreOrderService>();
builder.Services.AddScoped<IServiceRequestService, ServiceRequestService>();
builder.Services.AddScoped<IContributionService, ContributionService>();
builder.Services.AddScoped<INotificationDispatcher, NotificationDispatcher>();

builder.Services
    .AddTemporalClient(opts =>
    {
        opts.TargetHost = builder.Configuration["TemporalConfig:Address"] ?? "localhost:7233";
        opts.Namespace = builder.Configuration["TemporalConfig:Namespace"] ?? "default";
        opts.ApiKey = builder.Configuration["TemporalConfig:ApiKey"];
    })
    .AddHostedTemporalWorker(OperationsTaskQueues.PaymentCallbackProcessing)
    .AddWorkflow<ProcessContributionCallbackWorkflow>()
    .AddWorkflow<ProcessServiceRequestCallbackWorkflow>()
    .AddWorkflow<ProcessStoreOrderCallbackWorkflow>()
    .AddScopedActivities<ContributionCallbackActivities>()
    .AddScopedActivities<ServiceRequestCallbackActivities>()
    .AddScopedActivities<StoreOrderCallbackActivities>();

var host = builder.Build();

await PostgresExtensionService.ApplyMigrationsAsync(host.Services);

await host.RunAsync();
