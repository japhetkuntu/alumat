using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Common.Sdk.Options;
using ReservEase.Alumni.Common.Sdk.Services;
using ReservEase.Alumni.Mailtrap.Sdk.Options;
using ReservEase.Alumni.Member.Api.Models;
using ReservEase.Alumni.Member.Api.Services.Implementations;
using ReservEase.Alumni.PaymentCallbacks.Sdk.Options;
using ReservEase.Alumni.PostgresDb.Sdk.DbContexts;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.Storage.Sdk.Services;
using ReservEase.Alumni.Temporal.Sdk;
using ReservEase.Alumni.TestKit;
using Institution = ReservEase.Alumni.PostgresDb.Sdk.Entities.Institution;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.Member.Api.Tests;

/// <summary>Changing a member's sign-in email: a code to the current address, a code to the new one, and an undo from the old one.</summary>
public class EmailChangeTests : IDisposable
{
    private const string Tenant = "inst-1";
    private readonly Microsoft.Data.Sqlite.SqliteConnection connection;
    private readonly InMemoryRedisService<MemberRedisConfig> redis = new();
    private readonly BearerTokenConfig tokens = new()
    {
        MemberSigningKey = "test-signing-key-at-least-32-chars-long!!", Issuer = "iss", Audience = "aud", AccessTokenLifetime = 2, RefreshTokenLifetime = 7,
    };
    private EmailChangeService service = null!;
    private readonly AuthData ama = new() { Id = "m1", Email = "ama@x.com", FirstName = "Ama", LastName = "Mensah", Role = "Member" };

    public EmailChangeTests()
    {
        var (first, conn) = TestDb.CreateRelational(Tenant);
        connection = conn;
        first.Institutions.Add(new Institution { Id = Tenant, Slug = "umat", Name = "UMaT", PortalName = "UMaT Portal" });
        first.SaveChanges();
        Seed(Person("m1", "ama@x.com"), Person("m2", "kofi@x.com"));
    }

    public void Dispose() => connection.Dispose();

    private static MemberEntity Person(string id, string email, string status = "Active") => new()
    {
        Id = id, FirstName = "Ama", LastName = "Mensah", Email = email, Status = status, GraduationYear = 2015, Password = BCrypt.Net.BCrypt.HashPassword("Secret#123"), InstitutionId = Tenant,
    };

    private void Seed(params MemberEntity[] people)
    {
        using var db = TestDb.OpenRelational(connection, Tenant);
        db.AddRange(people);
        db.SaveChanges();
        Fresh();
    }

    private EmailChangeService Fresh()
    {
        var tenant = TestDb.Tenant(Tenant, "umat");
        var db = TestDb.OpenRelational(connection, Tenant);
        var http = new DefaultHttpContext();
        http.Request.Scheme = "https";
        http.Request.Host = new HostString("umat.members.test");
        var provider = new Mock<ITemporalClientProvider>();
        provider.SetupGet(p => p.IsAvailable).Returns(false);
        var accessor = Mock.Of<IHttpContextAccessor>(a => a.HttpContext == http);
        var google = new Mock<IGoogleTokenVerifier>();
        var storage = new Mock<IStorageService>();
        var integrations = new ReservEase.Alumni.PostgresDb.Sdk.Integrations.IntegrationService(
            new AlumniPgRepository<IntegrationApiKey>(db), new AlumniPgRepository<InstitutionIntegration>(db),
            new AlumniPgRepository<Institution>(db), new AlumniPgRepository<OnboardingLink>(db), new AlumniPgRepository<MemberEntity>(db));
        var auth = new MemberAuthService(
            new AlumniPgRepository<MemberEntity>(db), new AlumniPgRepository<Referral>(db), new AlumniPgRepository<Community>(db), new AlumniPgRepository<CommunityMembership>(db), new AlumniPgRepository<Institution>(db), tenant,
            accessor, redis, Options.Create(tokens), Options.Create(new MailtrapConfig()),
            provider.Object, storage.Object, google.Object, integrations, NullLogger<MemberAuthService>.Instance);
        return service = new EmailChangeService(
            new AlumniPgRepository<MemberEntity>(db), new AlumniPgRepository<Institution>(db), redis, auth, provider.Object, tenant, accessor, NullLogger<EmailChangeService>.Instance);
    }

    private string EmailOf(string id)
    {
        using var db = TestDb.OpenRelational(connection, Tenant);
        return db.Members.AsNoTracking().Single(m => m.Id == id).Email;
    }

    private PendingEmailChange? Pending(string id = "m1") => redis.GetAsync<PendingEmailChange>($"email:change:{id}").GetAwaiter().GetResult();

    private async Task<PendingEmailChange> StartTo(string newEmail)
    {
        var started = await service.StartAsync(new StartEmailChangeRequest(newEmail), ama);
        Assert.Equal(200, started.Code);
        return Pending()!;
    }

    private async Task<string> FinishAsync(string newEmail)
    {
        await StartTo(newEmail);
        Assert.Equal(200, (await service.ConfirmCurrentAsync(new EmailChangeCodeRequest(Pending()!.Otp), ama)).Code);
        var done = await service.ConfirmNewAsync(new EmailChangeCodeRequest(Pending()!.Otp), ama);
        Assert.Equal(200, done.Code);
        return done.Data!.Email;
    }

    [Fact]
    public async Task Both_codes_right_changes_the_email_and_signs_the_member_in_again()
    {
        var email = await FinishAsync("  Ama.New@Example.COM ");
        Assert.Equal("ama.new@example.com", email);
        Assert.Equal("ama.new@example.com", EmailOf("m1"));
        Assert.Null(Pending());
    }

    [Fact]
    public async Task The_new_session_carries_the_new_email_and_other_devices_lose_their_refresh_token()
    {
        await redis.SetAsync("member:refresh:m1", "old-device-token");
        await StartTo("ama.new@example.com");
        await service.ConfirmCurrentAsync(new EmailChangeCodeRequest(Pending()!.Otp), ama);
        var done = await service.ConfirmNewAsync(new EmailChangeCodeRequest(Pending()!.Otp), ama);

        Assert.Equal("ama.new@example.com", done.Data!.Session.User.Email);
        var stored = await redis.GetAsync<string>("member:refresh:m1");
        Assert.NotEqual("old-device-token", stored);
        Assert.Equal(done.Data.Session.Tokens.RefreshToken, stored);
    }

    [Fact]
    public async Task Nothing_changes_until_the_second_code_is_right()
    {
        await StartTo("ama.new@example.com");
        await service.ConfirmCurrentAsync(new EmailChangeCodeRequest(Pending()!.Otp), ama);
        Assert.Equal("ama@x.com", EmailOf("m1"));

        var wrong = await service.ConfirmNewAsync(new EmailChangeCodeRequest("000000"), ama);
        Assert.Equal(400, wrong.Code);
        Assert.Equal("ama@x.com", EmailOf("m1"));
    }

    [Fact]
    public async Task The_code_for_the_current_address_cannot_be_used_for_the_new_one()
    {
        var pending = await StartTo("ama.new@example.com");
        // Still on the first step, so the final step refuses even a correct code.
        var skipped = await service.ConfirmNewAsync(new EmailChangeCodeRequest(pending.Otp), ama);
        Assert.Equal(400, skipped.Code);
        Assert.Equal("ama@x.com", EmailOf("m1"));
    }

    [Fact]
    public async Task A_new_code_is_sent_to_the_new_address_after_the_first_is_confirmed()
    {
        var first = await StartTo("ama.new@example.com");
        Assert.Equal("Current", first.Step);
        await service.ConfirmCurrentAsync(new EmailChangeCodeRequest(first.Otp), ama);
        var second = Pending()!;
        Assert.Equal("New", second.Step);
        Assert.NotEqual(first.Otp, second.Otp);
    }

    [Fact]
    public async Task Five_wrong_guesses_end_the_change()
    {
        await StartTo("ama.new@example.com");
        for (var i = 0; i < EmailChangeService.MaxWrongGuesses; i++)
            Assert.Equal(400, (await service.ConfirmCurrentAsync(new EmailChangeCodeRequest("000000"), ama)).Code);

        Assert.Null(Pending());
        Assert.Equal("ama@x.com", EmailOf("m1"));
    }

    [Fact]
    public async Task An_address_another_member_has_cannot_be_used()
    {
        var response = await service.StartAsync(new StartEmailChangeRequest("KOFI@x.com"), ama);
        Assert.Equal(400, response.Code);
        Assert.Null(Pending());
    }

    [Fact]
    public async Task The_address_is_checked_again_at_the_end_in_case_someone_took_it_meanwhile()
    {
        var pending = await StartTo("ama.new@example.com");
        Seed(Person("m3", "ama.new@example.com"));

        var response = await service.ConfirmCurrentAsync(new EmailChangeCodeRequest(pending.Otp), ama);
        Assert.Equal(400, response.Code);
        Assert.Equal("ama@x.com", EmailOf("m1"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    [InlineData("a@b")]
    [InlineData("Ama <a@b.com>")]
    public async Task A_bad_address_is_refused(string value)
    {
        Assert.Equal(400, (await service.StartAsync(new StartEmailChangeRequest(value), ama)).Code);
    }

    [Fact]
    public async Task The_same_address_is_refused()
    {
        Assert.Equal(400, (await service.StartAsync(new StartEmailChangeRequest("AMA@x.com"), ama)).Code);
    }

    [Fact]
    public async Task Only_an_active_account_can_change_its_email()
    {
        Seed(Person("m4", "pending@x.com", "Pending"));
        var response = await service.StartAsync(new StartEmailChangeRequest("p2@x.com"), new AuthData { Id = "m4", Email = "pending@x.com", Role = "Member" });
        Assert.Equal(403, response.Code);
    }

    [Fact]
    public async Task Starting_is_limited_per_hour()
    {
        for (var i = 0; i < EmailChangeService.MaxStartsPerHour; i++)
            Assert.Equal(200, (await service.StartAsync(new StartEmailChangeRequest($"try{i}@example.com"), ama)).Code);
        Assert.Equal(400, (await service.StartAsync(new StartEmailChangeRequest("one-too-many@example.com"), ama)).Code);
    }

    [Fact]
    public async Task Resending_is_limited_and_gives_a_fresh_code()
    {
        var first = await StartTo("ama.new@example.com");
        var sent = 1;
        string last = first.Otp;
        while (sent < EmailChangeService.MaxSendsPerChange)
        {
            Assert.Equal(200, (await service.ResendAsync(ama)).Code);
            sent++;
            Assert.NotEqual(last, Pending()!.Otp);
            last = Pending()!.Otp;
        }
        Assert.Equal(400, (await service.ResendAsync(ama)).Code);
    }

    [Fact]
    public async Task A_cancelled_change_cannot_be_finished()
    {
        var pending = await StartTo("ama.new@example.com");
        await service.CancelAsync(ama);
        Assert.Equal(400, (await service.ConfirmCurrentAsync(new EmailChangeCodeRequest(pending.Otp), ama)).Code);
    }

    [Fact]
    public async Task The_status_never_shows_the_full_address()
    {
        await StartTo("ama.new@example.com");
        var status = (await service.GetStatusAsync(ama)).Data!;
        Assert.Equal("Current", status.Step);
        Assert.Equal("am•••@x.com", status.CurrentEmailHint);
        Assert.Equal("am•••@example.com", status.NewEmailHint);
    }

    // ── Undo ────────────────────────────────────────────────────────────

    private async Task<string> UndoTokenAfterChange()
    {
        await FinishAsync("ama.new@example.com");
        var key = redis.Store.Keys.Single(k => k.StartsWith("email:change:undo:"));
        return key["email:change:undo:".Length..];
    }

    [Fact]
    public async Task The_link_to_the_old_address_puts_the_old_email_back_and_signs_everyone_out()
    {
        var token = await UndoTokenAfterChange();
        var response = await service.UndoAsync(new UndoEmailChangeRequest(token));

        Assert.Equal(200, response.Code);
        Assert.Equal("ama@x.com", EmailOf("m1"));
        Assert.False(await redis.ExistsAsync("member:refresh:m1"));
    }

    [Fact]
    public async Task An_undo_link_works_only_once()
    {
        var token = await UndoTokenAfterChange();
        Assert.Equal(200, (await service.UndoAsync(new UndoEmailChangeRequest(token))).Code);
        Assert.Equal(400, (await service.UndoAsync(new UndoEmailChangeRequest(token))).Code);
    }

    [Fact]
    public async Task An_undo_link_is_dead_once_the_email_has_changed_again()
    {
        var token = await UndoTokenAfterChange();
        await FinishAsync("third@example.com");

        var response = await service.UndoAsync(new UndoEmailChangeRequest(token));
        Assert.Equal(400, response.Code);
        Assert.Equal("third@example.com", EmailOf("m1"));
    }

    [Fact]
    public async Task Undo_does_not_take_an_address_that_someone_else_now_holds()
    {
        var token = await UndoTokenAfterChange();
        Seed(Person("m5", "ama@x.com"));

        var response = await service.UndoAsync(new UndoEmailChangeRequest(token));
        Assert.Equal(409, response.Code);
        Assert.Equal("ama.new@example.com", EmailOf("m1"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("0000000000000000000000000000")]
    public async Task A_guessed_undo_token_does_nothing(string token)
    {
        Assert.Equal(400, (await service.UndoAsync(new UndoEmailChangeRequest(token))).Code);
    }
}
