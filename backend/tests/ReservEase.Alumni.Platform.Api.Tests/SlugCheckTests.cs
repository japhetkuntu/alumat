using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.EntityFrameworkCore;
using Moq;
using ReservEase.Alumni.Mailtrap.Sdk.Options;
using ReservEase.Alumni.Platform.Api.Models;
using ReservEase.Alumni.Platform.Api.Options;
using MsOptions = Microsoft.Extensions.Options.Options;
using ReservEase.Alumni.Paystack.Sdk.Services;
using ReservEase.Alumni.Platform.Api.Services.Implementations;
using ReservEase.Alumni.Platform.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.DbContexts;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.Redis.Sdk.Services;
using ReservEase.Alumni.Temporal.Sdk;
using ReservEase.Alumni.TestKit;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;
namespace ReservEase.Alumni.Platform.Api.Tests;

/// <summary>Checking a subdomain: a missing value is a bad request, and a real one says whether it is free.</summary>
public class SlugCheckTests
{
    private static InstitutionManagementService Create(params object[] seed)
    {
        var name = TestDb.NewName();
        using (var db = TestDb.Create(name)) { foreach (var e in seed) db.Add(e); db.SaveChanges(); }
        var ctx = TestDb.Create(name);
        return new InstitutionManagementService(ctx,
            new AlumniPgRepository<Institution>(ctx), new AlumniPgRepository<InstitutionStaff>(ctx), new AlumniPgRepository<MemberEntity>(ctx),
            new AlumniPgRepository<Contribution>(ctx), new AlumniPgRepository<StoreOrder>(ctx), new AlumniPgRepository<ServiceRequest>(ctx),
            new AlumniPgRepository<EventRsvp>(ctx), new AlumniPgRepository<AlumniEvent>(ctx),
            new AlumniPgRepository<PaymentTransaction>(ctx), new AlumniPgRepository<Campaign>(ctx), new AlumniPgRepository<Batch>(ctx),
            new AlumniPgRepository<ForumCategory>(ctx), new AlumniPgRepository<PlatformStaff>(ctx), Mock.Of<IAuditLogService>(), Mock.Of<IPaystackService>(),
            new ConfigurationBuilder().Build(), Mock.Of<ITemporalClientProvider>(t => t.IsAvailable == false),
            MsOptions.Create(new MailtrapConfig()), Mock.Of<IRedisService<PlatformRedisConfig>>(), new FixedScope(), NullLogger<InstitutionManagementService>.Instance);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ABlankSlug_IsABadRequest_NotAServerError(string? slug)
        => Assert.Equal(400, (await Create().CheckSlugAsync(slug!)).Code);

    [Fact]
    public async Task ASlugInUse_IsReportedTaken_AndAFreeOneAvailable()
    {
        var s = Create(new Institution { Id = "i1", Slug = "mentora", Name = "Mentora" });
        var taken = await s.CheckSlugAsync(" Mentora ");
        var free = await s.CheckSlugAsync("brand-new");
        Assert.Equal(200, taken.Code); Assert.False(taken.Data!.Available);
        Assert.Equal(200, free.Code); Assert.True(free.Data!.Available);
    }
}
