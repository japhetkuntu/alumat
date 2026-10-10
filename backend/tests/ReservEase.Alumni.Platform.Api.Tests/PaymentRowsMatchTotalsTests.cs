using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ReservEase.Alumni.Mailtrap.Sdk.Options;
using ReservEase.Alumni.Platform.Api.Options;
using MsOptions = Microsoft.Extensions.Options.Options;
using ReservEase.Alumni.Paystack.Sdk.Services;
using ReservEase.Alumni.Platform.Api.Services.Implementations;
using ReservEase.Alumni.Platform.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.Redis.Sdk.Services;
using ReservEase.Alumni.Temporal.Sdk;
using ReservEase.Alumni.TestKit;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.Platform.Api.Tests;

/// <summary>
/// The institution page lists payments row by row and also shows totals; the dashboard shows the grand total. They must be the
/// same measure. The fee quoted to a payer is not what the platform earns (the gateway's real fee is taken from the charge), so
/// a list of quoted fees never added up to the totals.
/// </summary>
public class PaymentRowsMatchTotalsTests
{
    private static (InstitutionManagementService s, string db) Create(params object[] seed)
    {
        var name = TestDb.NewName();
        using (var db = TestDb.Create(name)) { foreach (var e in seed) db.Add(e); db.SaveChanges(); }
        var ctx = TestDb.Create(name);
        var cache = new Mock<IRedisService<PlatformRedisConfig>>();
        var service = new InstitutionManagementService(ctx,
            new AlumniPgRepository<Institution>(ctx), new AlumniPgRepository<InstitutionStaff>(ctx), new AlumniPgRepository<MemberEntity>(ctx),
            new AlumniPgRepository<Contribution>(ctx), new AlumniPgRepository<StoreOrder>(ctx), new AlumniPgRepository<ServiceRequest>(ctx),
            new AlumniPgRepository<PaymentTransaction>(ctx), new AlumniPgRepository<Campaign>(ctx), new AlumniPgRepository<Batch>(ctx),
            new AlumniPgRepository<ForumCategory>(ctx), Mock.Of<IAuditLogService>(), Mock.Of<IPaystackService>(),
            new ConfigurationBuilder().Build(), Mock.Of<ITemporalClientProvider>(t => t.IsAvailable == false),
            MsOptions.Create(new MailtrapConfig()), cache.Object, NullLogger<InstitutionManagementService>.Instance);
        return (service, name);
    }

    [Fact]
    public async Task The_earned_column_of_the_payments_list_adds_up_to_the_institutions_revenue_total()
    {
        var (s, _) = Create(
            new Institution { Id = "i1", Name = "UMaT", Slug = "umat", Status = "Active" },
            // Quoted fee 2.00, real earnings 2.10 (the gateway took less than the estimate).
            new Contribution { Id = "c1", InstitutionId = "i1", CampaignId = "x", MemberId = "m", Status = "Successful", Amount = 100, PlatformFeeAmount = 2.00m, PlatformRevenueAmount = 2.10m, ConfirmedAt = DateTime.UtcNow },
            new Contribution { Id = "c2", InstitutionId = "i1", CampaignId = "x", MemberId = "m", Status = "Successful", Amount = 50, PlatformFeeAmount = 1.00m, PlatformRevenueAmount = 1.05m, ConfirmedAt = DateTime.UtcNow },
            // Not paid: earns nothing, and must not appear to.
            new Contribution { Id = "c3", InstitutionId = "i1", CampaignId = "x", MemberId = "m", Status = "Pending", Amount = 80, PlatformFeeAmount = 1.60m },
            new StoreOrder { Id = "o1", InstitutionId = "i1", Status = "Successful", TotalAmount = 40, PlatformFeeAmount = 0.80m, ConfirmedAt = DateTime.UtcNow },
            new StoreOrder { Id = "o2", InstitutionId = "i1", Status = "Pending", TotalAmount = 40, PlatformFeeAmount = 0.80m });

        var rows = (await s.GetPaymentsAsync("i1", 1, 50, null, null)).Data!.Results;
        var revenue = (await s.GetRevenueAsync("i1")).Data!;

        Assert.Equal(revenue.PlatformFeeTotal, rows.Sum(r => r.PlatformEarnedAmount));
        Assert.Equal(3.95m, revenue.PlatformFeeTotal);
        Assert.Equal(0m, rows.Single(r => r.Id == "c3").PlatformEarnedAmount);
        Assert.Equal(0m, rows.Single(r => r.Id == "o2").PlatformEarnedAmount);
    }

    [Fact]
    public async Task The_payment_detail_reports_the_same_earned_figure_as_its_row()
    {
        var (s, _) = Create(new Institution { Id = "i1", Name = "UMaT", Slug = "umat", Status = "Active" },
            new Contribution { Id = "c1", InstitutionId = "i1", CampaignId = "x", MemberId = "m", Status = "Successful", Amount = 100, PlatformFeeAmount = 2.00m, PlatformRevenueAmount = 2.10m, ConfirmedAt = DateTime.UtcNow });

        var detail = (await s.GetPaymentDetailAsync("i1", "c1", "Contribution")).Data!;

        Assert.Equal((2.00m, 2.10m), (detail.PlatformFeeAmount, detail.PlatformEarnedAmount));
    }
}
