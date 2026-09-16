using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ReservEase.Alumni.Mailtrap.Sdk.Extensions;
using ReservEase.Alumni.PaymentCallbacks.Sdk.Options;
using ReservEase.Alumni.PaymentCallbacks.Sdk.Workflows;
using ReservEase.Alumni.Notifications.Sdk.Workflows;
using ReservEase.Alumni.Operations.Worker.Workflows.Contributions;
using ReservEase.Alumni.Operations.Worker.Workflows.Notifications;
using ReservEase.Alumni.Operations.Worker.Workflows.ServiceRequests;
using ReservEase.Alumni.Operations.Worker.Workflows.StoreOrders;
using ReservEase.Alumni.Paystack.Sdk.Extensions;
using ReservEase.Alumni.PostgresDb.Sdk.Extensions;
using ReservEase.Alumni.Redis.Sdk.Extensions;
using ReservEase.Alumni.Sms.Sdk.Extensions;
using ReservEase.Alumni.Whatsapp.Sdk.Extensions;
using Temporalio.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

// Data + external services the granular activities call directly — no domain
// service classes registered here anymore (IStoreOrderService/IServiceRequestService/
// IContributionService and their large, mostly-unrelated method surfaces belong to
// Member.Api; this worker's activities talk to repositories/DbContext/Paystack/Redis
// directly). Sms/WhatsApp/Email are registered here now, not in each web API project —
// NotificationDispatchActivities is the only thing in the whole solution that still
// sends any of them, since every other notification call site now just signals the
// NotificationDispatch workflow instead.
builder.Services.AddAlumniPostgresSdk(builder.Configuration, "AlumniConnection");
builder.Services.AddRedisDatabase<MemberRedisConfig>(builder.Configuration);
builder.Services.AddPaystackService(builder.Configuration);
builder.Services.AddArkeselSmsService(builder.Configuration);
builder.Services.AddWaSenderWhatsAppService(builder.Configuration);
builder.Services.AddMailtrapEmailService(builder.Configuration);

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

// Its own queue, isolated from payment-callback processing — see
// NotificationTaskQueues' doc comment for why.
builder.Services
    .AddHostedTemporalWorker(NotificationTaskQueues.Dispatch)
    .AddWorkflow<NotificationDispatchWorkflow>()
    .AddScopedActivities<NotificationDispatchActivities>();

var host = builder.Build();

await PostgresExtensionService.ApplyMigrationsAsync(host.Services);

await host.RunAsync();
