using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.PaymentCallbacks.Sdk.Models;
using ReservEase.Alumni.PaymentCallbacks.Sdk.Services.Implementations;
using ReservEase.Alumni.Paystack.Sdk.Models;
using ReservEase.Alumni.Paystack.Sdk.Options;
using ReservEase.Alumni.Paystack.Sdk.Services;
using ReservEase.Alumni.PostgresDb.Sdk.DbContexts;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.Storage.Sdk.Services;
using ReservEase.Alumni.Temporal.Sdk;
using ReservEase.Alumni.TestKit;
using Institution = ReservEase.Alumni.PostgresDb.Sdk.Entities.Institution;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.PaymentCallbacks.Tests;

public class ServiceRequestServiceTests
{
    private const string Tenant = "inst-1";

    private sealed class Rig
    {
        public string DbName { get; } = TestDb.NewName();
        public Mock<IPaystackService> Paystack { get; } = new();
        public Mock<IStorageService> Storage { get; } = new();
        public Mock<ITemporalClientProvider> Temporal { get; } = new();
        public List<InitializePaymentRequest> Initialized { get; } = new();
        public AuthData Member { get; } = new() { Id = "m1", FirstName = "Ama", LastName = "Mensah", Email = "ama@x.com" };
        public ServiceRequestService Service { get; private set; } = null!;

        public Rig(Action<Institution>? institution = null, bool paystackAccepts = true)
        {
            Paystack.Setup(p => p.InitializePaymentAsync(It.IsAny<InitializePaymentRequest>()))
                .Callback<InitializePaymentRequest>(r => Initialized.Add(r))
                .ReturnsAsync(() => new InitializePaymentResponse
                {
                    Status = paystackAccepts, Message = paystackAccepts ? "ok" : "Declined",
                    Data = new InitializePaymentData { AuthorizationUrl = "https://paystack/pay" },
                });
            Storage.Setup(s => s.UploadFileAsync(It.IsAny<IFormFile>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync("https://cdn/att.pdf");
            Temporal.SetupGet(t => t.IsAvailable).Returns(false);

            var inst = new Institution { Id = Tenant, Slug = "umat", Name = "UMaT" };
            institution?.Invoke(inst);
            using (var seed = TestDb.Create(DbName, Tenant)) { seed.Institutions.Add(inst); seed.SaveChanges(); }
            Fresh();
        }

        public ServiceRequestService Fresh()
        {
            var db = TestDb.Create(DbName, Tenant);
            return Service = new ServiceRequestService(
                new AlumniPgRepository<ServiceRequest>(db), new AlumniPgRepository<ServiceType>(db), new AlumniPgRepository<Institution>(db),
                new AlumniPgRepository<MemberEntity>(db), TestDb.Tenant(Tenant, "umat"), Paystack.Object,
                new PaystackConfig { GatewayFeePercentage = 1.95m, GatewayFeeSafetyBufferSubunit = 2 }, Storage.Object, Temporal.Object,
                new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["PaystackConfig:CallbackUrl"] = "https://app.test/pay" }).Build(),
                NullLogger<ServiceRequestService>.Instance);
        }

        public AlumniDbContext Db() => TestDb.Create(DbName, Tenant);

        public async Task AddService(ServiceType type)
        {
            using var db = Db();
            type.InstitutionId = Tenant;
            db.ServiceTypes.Add(type);
            await db.SaveChangesAsync();
        }

        public Task<ServiceRequest> OnlyRequest() => Db().ServiceRequests.SingleAsync();
    }

    private static ServiceType Service(decimal price = 50, params ServiceFieldDefinition[] fields) =>
        new() { Id = "s1", Name = "Transcript", Price = price, Fields = fields.ToList(), Stages = ["Received", "Sent"] };

    private static ServiceFieldDefinition Field(string key, string type = "Text", bool required = false) =>
        new() { Key = key, Label = key.ToUpperInvariant(), Type = type, Required = required };

    private static CreateServiceRequestRequest Req(object? answers = null, string serviceId = "s1") =>
        new() { ServiceTypeId = serviceId, AnswersJson = JsonSerializer.Serialize(answers ?? new { }) };

    private static IFormFile File(string name = "id.pdf", int length = 10) => new FormFile(new MemoryStream(new byte[length]), 0, length, "f", name);

    [Fact]
    public async Task A_valid_request_is_saved_pending_and_paystack_is_initialised_for_the_price()
    {
        var rig = new Rig();
        await rig.AddService(Service(75.25m, Field("reason", required: true)));

        var response = await rig.Service.CreateRequestAsync(Req(new { reason = "Job application" }), new(), rig.Member);

        Assert.Equal(200, response.Code);
        var request = await rig.OnlyRequest();
        Assert.Equal(("Pending", "", 75.25m, "Transcript", "m1"), (request.PaymentStatus, request.CurrentStage, request.Amount, request.ServiceTypeName, request.MemberId));
        Assert.StartsWith("SR_", request.TransactionRef);
        Assert.Equal(request.TransactionRef, response.Data!.Reference);
        Assert.Equal(request.Id, response.Data.RequestId);
        Assert.Equal("Job application", request.FieldAnswers["reason"]);
        Assert.Equal(8, request.RequestNumber.Length);

        var init = Assert.Single(rig.Initialized);
        Assert.Equal(7525, init.Amount);
        Assert.Equal("true", init.Metadata!["serviceRequest"]);
        Assert.Equal("https://app.test/pay/callback", init.CallbackUrl);
        Assert.Null(init.Subaccount);
    }

    [Fact]
    public async Task The_request_callback_url_wins_over_the_configured_one()
    {
        var rig = new Rig();
        await rig.AddService(Service());
        var req = Req(); req.CallbackUrl = "https://mine/cb";
        await rig.Service.CreateRequestAsync(req, new(), rig.Member);
        Assert.Equal("https://mine/cb", rig.Initialized.Single().CallbackUrl);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("Draft")]
    [InlineData("Archived")]
    public async Task A_service_that_is_missing_or_not_active_is_unavailable(string state)
    {
        var rig = new Rig();
        if (state != "missing") { var s = Service(); s.Status = state; await rig.AddService(s); }

        var response = await rig.Service.CreateRequestAsync(Req(), new(), rig.Member);

        Assert.Equal(400, response.Code);
        Assert.Contains("no longer available", response.Message);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[1,2,3]")]
    [InlineData("{\"a\": 1}")]   // values must be strings
    public async Task Unreadable_answers_are_rejected(string json)
    {
        var rig = new Rig();
        await rig.AddService(Service());
        var response = await rig.Service.CreateRequestAsync(new CreateServiceRequestRequest { ServiceTypeId = "s1", AnswersJson = json }, new(), rig.Member);
        Assert.Equal(400, response.Code);
        Assert.Contains("Could not read", response.Message);
    }

    [Fact]
    public async Task A_json_null_is_treated_as_no_answers()
    {
        var rig = new Rig();
        await rig.AddService(Service());
        var response = await rig.Service.CreateRequestAsync(new CreateServiceRequestRequest { ServiceTypeId = "s1", AnswersJson = "null" }, new(), rig.Member);
        Assert.Equal(200, response.Code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_required_text_answer_must_not_be_blank(string? value)
    {
        var rig = new Rig();
        await rig.AddService(Service(50, Field("reason", required: true)));
        var answers = value is null ? new Dictionary<string, string>() : new Dictionary<string, string> { ["reason"] = value };

        var response = await rig.Service.CreateRequestAsync(Req(answers), new(), rig.Member);

        Assert.Equal(400, response.Code);
        Assert.Contains("\"REASON\" is required", response.Message);
        Assert.Equal(0, await rig.Db().ServiceRequests.CountAsync());
        rig.Paystack.Verify(p => p.InitializePaymentAsync(It.IsAny<InitializePaymentRequest>()), Times.Never);
    }

    [Fact]
    public async Task An_optional_answer_may_be_left_out()
    {
        var rig = new Rig();
        await rig.AddService(Service(50, Field("note")));
        Assert.Equal(200, (await rig.Service.CreateRequestAsync(Req(), new(), rig.Member)).Code);
    }

    [Fact]
    public async Task A_required_file_must_be_attached_and_non_empty()
    {
        var rig = new Rig();
        await rig.AddService(Service(50, Field("id", "File", required: true)));

        Assert.Equal(400, (await rig.Service.CreateRequestAsync(Req(), new(), rig.Member)).Code);
        Assert.Equal(400, (await rig.Service.CreateRequestAsync(Req(), new() { ["id"] = File(length: 0) }, rig.Member)).Code);
    }

    [Fact]
    public async Task An_attached_file_is_uploaded_under_the_slug_and_recorded_by_key()
    {
        var rig = new Rig();
        await rig.AddService(Service(50, Field("id", "File", required: true)));

        var response = await rig.Service.CreateRequestAsync(Req(), new() { ["id"] = File("passport.PNG") }, rig.Member);

        Assert.Equal(200, response.Code);
        rig.Storage.Verify(s => s.UploadFileAsync(It.IsAny<IFormFile>(), It.Is<string>(n => n.EndsWith(".PNG")), "alumni", "umat"), Times.Once);
        Assert.Equal("https://cdn/att.pdf", (await rig.OnlyRequest()).Attachments["id"]);
    }

    [Fact]
    public async Task An_optional_file_that_is_not_sent_is_skipped()
    {
        var rig = new Rig();
        await rig.AddService(Service(50, Field("id", "File")));
        Assert.Equal(200, (await rig.Service.CreateRequestAsync(Req(), new(), rig.Member)).Code);
        Assert.Empty((await rig.OnlyRequest()).Attachments);
        rig.Storage.Verify(s => s.UploadFileAsync(It.IsAny<IFormFile>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task A_rejected_form_never_leaves_stray_uploads_behind()
    {
        var rig = new Rig();
        await rig.AddService(Service(50, Field("id", "File"), Field("reason", required: true)));

        var response = await rig.Service.CreateRequestAsync(Req(), new() { ["id"] = File() }, rig.Member);   // reason missing

        Assert.Equal(400, response.Code);
        rig.Storage.Verify(s => s.UploadFileAsync(It.IsAny<IFormFile>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task A_service_that_is_not_priced_is_refused_because_services_are_never_free(double price)
    {
        var rig = new Rig();
        await rig.AddService(Service((decimal)price));
        var response = await rig.Service.CreateRequestAsync(Req(), new(), rig.Member);
        Assert.Equal(400, response.Code);
        Assert.Contains("properly configured", response.Message);
        Assert.Equal(0, await rig.Db().ServiceRequests.CountAsync());
    }

    [Fact]
    public async Task With_a_subaccount_the_payer_covers_the_fees_and_the_institution_nets_the_full_price()
    {
        var rig = new Rig(i => { i.PaystackSubaccountCode = "ACCT_9"; i.PlatformFeePercentage = 3m; });
        await rig.AddService(Service(200));

        await rig.Service.CreateRequestAsync(Req(), new(), rig.Member);

        var request = await rig.OnlyRequest();
        var expected = PaystackFeeCalculator.CalculateZeroDeductionCharge(20000, 3m, 1.95m, 0, null, 2);
        var init = rig.Initialized.Single();
        Assert.Equal(expected.ChargeAmountSubunit, init.Amount);
        Assert.Equal(expected.TransactionChargeSubunit, init.TransactionCharge);
        Assert.Equal(("account", "ACCT_9"), (init.Bearer, init.Subaccount));
        Assert.Equal(200m, request.Amount);
        Assert.Equal(request.Amount + request.PlatformFeeAmount + request.GatewayFeeAmount, request.GrossChargeAmount);
        Assert.Equal(expected.ChargeAmountSubunit / 100m, request.GrossChargeAmount);
    }

    [Fact]
    public async Task Without_a_subaccount_no_fees_are_added()
    {
        var rig = new Rig();
        await rig.AddService(Service(80));
        await rig.Service.CreateRequestAsync(Req(), new(), rig.Member);

        var request = await rig.OnlyRequest();
        Assert.Equal((0m, 0m, 0m, 80m), (request.PlatformFeeAmount, request.GatewayFeeAmount, request.TransactionChargeAmount, request.GrossChargeAmount));
        Assert.Null(rig.Initialized.Single().TransactionCharge);
    }

    [Fact]
    public async Task When_paystack_declines_the_request_is_marked_failed()
    {
        var rig = new Rig(paystackAccepts: false);
        await rig.AddService(Service());

        var response = await rig.Service.CreateRequestAsync(Req(), new(), rig.Member);

        Assert.Equal(400, response.Code);
        var request = await rig.OnlyRequest();
        Assert.Equal(("Failed", "Declined"), (request.PaymentStatus, request.FailureMessage));
    }

    [Fact]
    public async Task The_pending_request_is_saved_before_paystack_hears_about_it()
    {
        var rig = new Rig();
        await rig.AddService(Service());
        var existed = false;
        rig.Paystack.Setup(p => p.InitializePaymentAsync(It.IsAny<InitializePaymentRequest>()))
            .Returns<InitializePaymentRequest>(async r =>
            {
                existed = await rig.Db().ServiceRequests.AnyAsync(x => x.TransactionRef == r.Reference);
                return new InitializePaymentResponse { Status = true, Data = new InitializePaymentData() };
            });

        await rig.Service.CreateRequestAsync(Req(), new(), rig.Member);

        Assert.True(existed);
    }

    [Fact]
    public async Task An_unexpected_error_is_a_500()
    {
        var rig = new Rig();
        await rig.AddService(Service());
        rig.Paystack.Setup(p => p.InitializePaymentAsync(It.IsAny<InitializePaymentRequest>())).ThrowsAsync(new TimeoutException());
        Assert.Equal(500, (await rig.Service.CreateRequestAsync(Req(), new(), rig.Member)).Code);
    }

    // ── Catalogue ───────────────────────────────────────────────────────

    [Fact]
    public async Task The_catalogue_lists_only_active_services_and_supports_word_search()
    {
        var rig = new Rig();
        await rig.AddService(new ServiceType { Id = "a", Name = "Official Transcript", Price = 10 });
        await rig.AddService(new ServiceType { Id = "b", Name = "Certificate Copy", Price = 10 });
        await rig.AddService(new ServiceType { Id = "c", Name = "Hidden Transcript", Price = 10, Status = "Draft" });

        var all = await rig.Service.GetServiceTypesAsync(new ServiceTypeFilter());
        Assert.Equal(2, all.Data!.TotalCount);

        var found = await rig.Service.GetServiceTypesAsync(new ServiceTypeFilter { Search = "transcript OFFICIAL" });
        Assert.Equal(new[] { "a" }, found.Data!.Results.Select(s => s.Id));
    }

    [Fact]
    public async Task A_single_service_is_returned_only_when_active()
    {
        var rig = new Rig();
        await rig.AddService(Service());
        await rig.AddService(new ServiceType { Id = "d", Name = "D", Price = 1, Status = "Draft" });

        Assert.Equal(200, (await rig.Service.GetServiceTypeByIdAsync("s1")).Code);
        Assert.Equal(404, (await rig.Service.GetServiceTypeByIdAsync("d")).Code);
        Assert.Equal(404, (await rig.Service.GetServiceTypeByIdAsync("nope")).Code);
    }

    // ── Status / ownership / history ────────────────────────────────────

    private static async Task<(Rig rig, string reference)> Place(Action<ServiceRequest>? tweak = null)
    {
        var rig = new Rig();
        await rig.AddService(Service());
        var reference = (await rig.Service.CreateRequestAsync(Req(), new(), rig.Member)).Data!.Reference!;
        if (tweak is not null)
        {
            using var db = rig.Db();
            var r = await db.ServiceRequests.SingleAsync();
            tweak(r);
            await db.SaveChangesAsync();
        }
        rig.Fresh();
        return (rig, reference);
    }

    [Fact]
    public async Task OwnsReference_matches_only_service_request_references()
    {
        var (rig, reference) = await Place();
        Assert.True(await rig.Service.OwnsReferenceAsync(reference));
        Assert.False(await rig.Service.OwnsReferenceAsync("SO_other"));
    }

    [Theory]
    [InlineData("Successful", "Payment confirmed")]
    [InlineData("Pending", "Payment has been initiated but not yet completed.")]
    public async Task Status_reports_state_with_a_friendly_message(string status, string message)
    {
        var (rig, reference) = await Place(r => r.PaymentStatus = status);
        var response = await rig.Service.GetRequestStatusAsync(reference, rig.Member);
        Assert.Equal((status, message, 50m), (response.Data!.PaymentStatus, response.Data.Message, response.Data.Amount));
    }

    [Fact]
    public async Task A_failed_request_reports_its_reason_or_a_default()
    {
        var (rig, reference) = await Place(r => { r.PaymentStatus = "Failed"; r.FailureMessage = "Insufficient funds"; });
        Assert.Equal("Insufficient funds", (await rig.Service.GetRequestStatusAsync(reference, rig.Member)).Data!.Message);

        var (rig2, reference2) = await Place(r => { r.PaymentStatus = "Failed"; r.FailureMessage = null; });
        Assert.Equal("Payment failed", (await rig2.Service.GetRequestStatusAsync(reference2, rig2.Member)).Data!.Message);
    }

    [Fact]
    public async Task Status_404s_for_an_unknown_reference_and_400s_for_someone_elses()
    {
        var (rig, reference) = await Place();
        Assert.Equal(404, (await rig.Service.GetRequestStatusAsync("SR_none", rig.Member)).Code);
        Assert.Equal(400, (await rig.Service.GetRequestStatusAsync(reference, new AuthData { Id = "intruder" })).Code);
    }

    [Fact]
    public async Task My_requests_are_scoped_to_the_member_and_can_be_filtered_by_stage()
    {
        var rig = new Rig();
        using (var db = rig.Db())
        {
            db.ServiceRequests.AddRange(
                new ServiceRequest { Id = "r1", MemberId = "m1", RequestNumber = "A", CurrentStage = "Received" },
                new ServiceRequest { Id = "r2", MemberId = "m1", RequestNumber = "B", CurrentStage = "Sent" },
                new ServiceRequest { Id = "r3", MemberId = "m2", RequestNumber = "C", CurrentStage = "Received" });
            await db.SaveChangesAsync();
        }

        var all = await rig.Service.GetMyRequestsAsync(new ServiceRequestFilter(), "m1");
        Assert.Equal(2, all.Data!.TotalCount);

        var staged = await rig.Service.GetMyRequestsAsync(new ServiceRequestFilter { Stage = "Sent" }, "m1");
        Assert.Equal(new[] { "B" }, staged.Data!.Results.Select(r => r.RequestNumber));

        Assert.Empty((await rig.Service.GetMyRequestsAsync(new ServiceRequestFilter(), "nobody")).Data!.Results);
    }
}
