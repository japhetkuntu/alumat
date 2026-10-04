using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using ReservEase.Alumni.Mailtrap.Sdk.Options;
using ReservEase.Alumni.Operations.Worker.Workflows.ScheduledJobs;
using ReservEase.Alumni.Paystack.Sdk.Options;
using ReservEase.Alumni.Paystack.Sdk.Services;
using ReservEase.Alumni.PostgresDb.Sdk.DbContexts;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using ReservEase.Alumni.Temporal.Sdk;
using ReservEase.Alumni.TestKit;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.Operations.Worker.Tests;

/// <summary>Builds <see cref="ScheduledJobsActivities"/> over an in-memory database with every collaborator mocked.</summary>
public sealed class ScheduledJobsRig
{
    public string DbName { get; } = TestDb.NewName();
    public Mock<IPaystackService> Paystack { get; } = new();
    public Mock<ITemporalClientProvider> Temporal { get; } = new();
    public CurrentTenantService Tenant { get; } = new();
    public PaystackConfig PaystackConfig { get; } = new() { GatewayFeePercentage = 1.95m, GatewayFeeSafetyBufferSubunit = 2 };
    public Dictionary<string, string?> Config { get; } = new() { ["MemberPortalBaseDomain"] = "members.test", ["AdminPortalBaseDomain"] = "admin.test" };
    public CapturingLogger<ScheduledJobsActivities> Log { get; } = new();
    public ScheduledJobsActivities Activities { get; }

    public ScheduledJobsRig()
    {
        Temporal.SetupGet(t => t.IsAvailable).Returns(false);   // enqueued notifications are then logged as dropped
        var db = TestDb.Create(DbName, tenant: Tenant);
        Activities = new ScheduledJobsActivities(
            R<Institution>(db), R<MemberEntity>(db), R<NotificationPreference>(db), R<Job>(db), R<AlumniEvent>(db), R<EventRsvp>(db), R<Pledge>(db),
            R<ActivationTask>(db), R<PlatformStaff>(db), R<PlatformNotification>(db), R<Campaign>(db), R<Spotlight>(db), R<RecurringContribution>(db),
            R<Contribution>(db), R<Batch>(db), R<Notification>(db), R<ForumCategory>(db), R<ForumThread>(db), R<ForumPost>(db),
            Tenant, Paystack.Object, PaystackConfig, Temporal.Object,
            Options.Create(new MailtrapConfig()), new ConfigurationBuilder().AddInMemoryCollection(Config).Build(), Log);
    }

    private static AlumniPgRepository<T> R<T>(AlumniDbContext db) where T : BaseEntity => new(db);

    public AlumniDbContext Db() => TestDb.Create(DbName);

    public async Task Seed(params object[] entities)
    {
        using var db = Db();
        foreach (var e in entities) db.Add(e);
        await db.SaveChangesAsync();
    }
}
