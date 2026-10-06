using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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

/// <summary>Registration, OTP, sign-in, refresh, reset and profile flows of <see cref="MemberAuthService"/> end to end over a real (SQLite) database.</summary>
public class MemberAuthFlowTests : IDisposable
{
    private const string Tenant = "inst-1";
    private readonly Microsoft.Data.Sqlite.SqliteConnection connection;
    private readonly InMemoryRedisService<MemberRedisConfig> redis = new();
    private readonly CapturingLogger<MemberAuthService> log = new();
    private readonly Mock<IGoogleTokenVerifier> google = new();
    private readonly Mock<IStorageService> storage = new();
    private readonly BearerTokenConfig tokens = new()
    {
        MemberSigningKey = "test-signing-key-at-least-32-chars-long!!", Issuer = "iss", Audience = "aud", AccessTokenLifetime = 2, RefreshTokenLifetime = 7,
    };
    private MemberAuthService service = null!;

    public MemberAuthFlowTests()
    {
        var (first, conn) = TestDb.CreateRelational(Tenant);
        connection = conn;
        first.Institutions.Add(new Institution { Id = Tenant, Slug = "umat", Name = "UMaT", PortalName = "UMaT Portal", PrimaryColorHex = "#112233" });
        first.SaveChanges();
        storage.Setup(s => s.UploadFileAsync(It.IsAny<IFormFile>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync("https://cdn/pic.png");
        Fresh();
    }

    public void Dispose() => connection.Dispose();

    private MemberAuthService Fresh()
    {
        var tenant = TestDb.Tenant(Tenant, "umat");
        var db = TestDb.OpenRelational(connection, Tenant);
        var http = new DefaultHttpContext();
        http.Request.Scheme = "https";
        http.Request.Host = new HostString("umat.members.test");
        var provider = new Mock<ITemporalClientProvider>();
        provider.SetupGet(p => p.IsAvailable).Returns(false);
        return service = new MemberAuthService(
            new AlumniPgRepository<MemberEntity>(db), new AlumniPgRepository<Referral>(db), new AlumniPgRepository<Community>(db), new AlumniPgRepository<CommunityMembership>(db), new AlumniPgRepository<Institution>(db), tenant,
            Mock.Of<IHttpContextAccessor>(a => a.HttpContext == http), redis, Options.Create(tokens), Options.Create(new MailtrapConfig()),
            provider.Object, storage.Object, google.Object, log);
    }

    private AlumniDbContext Db() => TestDb.OpenRelational(connection, Tenant);

    private async Task Seed(params object[] entities)
    {
        using var db = Db();
        foreach (var e in entities) { if (e is ITenantScoped t) t.InstitutionId = Tenant; db.Add(e); }
        await db.SaveChangesAsync();
        Fresh();
    }

    private async Task SetInstitution(Action<Institution> tweak)
    {
        using var db = Db();
        var inst = await db.Institutions.IgnoreQueryFilters().SingleAsync();
        tweak(inst);
        db.Update(inst);
        await db.SaveChangesAsync();
        Fresh();
    }

    private static MemberEntity Person(string status = "Active", string password = "Secret#123", string email = "ama@x.com", string id = "m1") => new()
    {
        Id = id, FirstName = "Ama", LastName = "Mensah", Email = email, Status = status, GraduationYear = 2015, Password = BCrypt.Net.BCrypt.HashPassword(password),
    };

    private static RegisterRequest Register(string email = "  New.User@Example.COM ", bool terms = true, string? referral = null, string? community = null, string? channel = null) =>
        new("  Kofi ", " Boateng ", email, "Secret#123", "0241", "S1", 2018, "dept1", referral, " Mining ", terms, community, channel);

    private CachedRegistration? Staged(string email = "new.user@example.com") => redis.GetAsync<CachedRegistration>($"reg:otp:{email}").GetAwaiter().GetResult();

    // ── Register + OTP ──────────────────────────────────────────────────

    [Fact]
    public async Task Registration_requires_accepting_the_terms()
    {
        var response = await service.RegisterAsync(Register(terms: false));
        Assert.Equal(400, response.Code);
        Assert.Contains("Terms", response.Message);
        Assert.Empty(redis.Store);
    }

    [Fact]
    public async Task Registering_stages_a_normalised_hashed_record_in_redis_and_sends_a_six_digit_code()
    {
        var response = await service.RegisterAsync(Register());

        Assert.Equal(200, response.Code);
        var staged = Staged()!;
        Assert.Equal(("Kofi", "Boateng", "new.user@example.com", "Mining"), (staged.FirstName, staged.LastName, staged.Email, staged.Program));
        Assert.Matches("^[0-9]{6}$", staged.Otp);
        Assert.NotEqual("Secret#123", staged.PasswordHash);
        Assert.True(BCrypt.Net.BCrypt.Verify("Secret#123", staged.PasswordHash));
        Assert.NotNull(staged.TermsAcceptedAt);
        Assert.Equal(0, staged.ResendCount);
        Assert.Contains(log.Entries, e => e.Message.Contains("Email") && e.Message.Contains("dropped"));   // handed to the notification pipeline
        Assert.Equal(0, await Db().Members.IgnoreQueryFilters().CountAsync());   // nothing is saved until the code is confirmed
    }

    [Theory]
    [InlineData("Active")]
    [InlineData("Pending")]
    [InlineData("Banned")]
    public async Task An_email_that_is_already_registered_is_a_conflict(string status)
    {
        await Seed(Person(status));
        Assert.Equal(409, (await service.RegisterAsync(Register("AMA@x.com"))).Code);
        Assert.Empty(redis.Store);
    }

    [Fact]
    public async Task A_rejected_registration_may_be_resubmitted_and_the_old_record_is_removed()
    {
        await Seed(Person("Suspended"));
        var response = await service.RegisterAsync(Register("ama@x.com"));
        Assert.Equal(200, response.Code);
        Assert.Equal(0, await Db().Members.IgnoreQueryFilters().CountAsync());
        Assert.NotNull(Staged("ama@x.com"));
    }

    [Fact]
    public async Task The_correct_code_creates_a_pending_member_awaiting_approval_and_clears_the_staged_record()
    {
        await service.RegisterAsync(Register());
        var otp = Staged()!.Otp;

        var response = await service.VerifyOtpAsync(new VerifyOtpRequest(" NEW.user@example.com", otp));

        Assert.Equal(201, response.Code);
        Assert.False(((RegistrationResultResponse)response.Data!).Approved);
        var m = await Db().Members.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(("Kofi", "Boateng", "new.user@example.com", "Pending", true, "self", 2018), (m.FirstName, m.LastName, m.Email, m.Status, m.IsEmailVerified, m.CreatedBy, m.GraduationYear));
        Assert.True(BCrypt.Net.BCrypt.Verify("Secret#123", m.Password));
        Assert.Null(m.MemberNumber);
        Assert.Null(Staged());
    }

    [Fact]
    public async Task A_wrong_code_is_rejected_and_the_staged_record_stays_so_the_right_code_still_works()
    {
        await service.RegisterAsync(Register());
        var otp = Staged()!.Otp;
        var wrong = otp == "123456" ? "654321" : "123456";

        var bad = await service.VerifyOtpAsync(new VerifyOtpRequest("new.user@example.com", wrong));

        Assert.Equal(400, bad.Code);
        Assert.Contains("Invalid verification code", bad.Message);
        Assert.NotNull(Staged());
        Assert.Equal(0, await Db().Members.IgnoreQueryFilters().CountAsync());
        Assert.Equal(201, (await service.VerifyOtpAsync(new VerifyOtpRequest("new.user@example.com", otp))).Code);
    }

    [Fact]
    public async Task A_code_of_a_different_length_is_just_invalid_not_an_error()
    {
        await service.RegisterAsync(Register());
        Assert.Equal(400, (await service.VerifyOtpAsync(new VerifyOtpRequest("new.user@example.com", "12"))).Code);
        Assert.Equal(400, (await service.VerifyOtpAsync(new VerifyOtpRequest("new.user@example.com", ""))).Code);
    }

    [Fact]
    public async Task Verifying_with_nothing_staged_asks_the_person_to_register_again()
    {
        var response = await service.VerifyOtpAsync(new VerifyOtpRequest("nobody@x.com", "123456"));
        Assert.Equal(400, response.Code);
        Assert.Contains("No pending registration", response.Message);
    }

    [Fact]
    public async Task With_auto_approval_on_the_member_is_active_immediately_with_the_next_member_number_for_their_year()
    {
        await SetInstitution(i => i.AutoApproveMembers = true);
        var existing = Person("Active", email: "o@x.com", id: "other"); existing.MemberNumber = "UMAT-2018-0007"; existing.GraduationYear = 2018;
        await Seed(existing);
        await service.RegisterAsync(Register());

        var response = await service.VerifyOtpAsync(new VerifyOtpRequest("new.user@example.com", Staged()!.Otp));

        Assert.True(((RegistrationResultResponse)response.Data!).Approved);
        var m = await Db().Members.IgnoreQueryFilters().SingleAsync(x => x.Email == "new.user@example.com");
        Assert.Equal(("Active", "UMAT-2018-0008"), (m.Status, m.MemberNumber));
    }

    [Fact]
    public async Task A_community_organisation_numbers_members_without_a_graduation_year()
    {
        await SetInstitution(i => { i.AutoApproveMembers = true; i.OrganizationType = OrganizationTypes.Community; });
        await service.RegisterAsync(Register());

        await service.VerifyOtpAsync(new VerifyOtpRequest("new.user@example.com", Staged()!.Otp));

        Assert.Equal("UMAT-0001", (await Db().Members.IgnoreQueryFilters().SingleAsync()).MemberNumber);
    }

    [Fact]
    public async Task A_referral_code_links_the_new_member_to_the_referrer_and_records_a_registered_referral()
    {
        var referrer = Person("Active", email: "ref@x.com", id: "ref1"); referrer.ReferralCode = "REF-ABC";
        await Seed(referrer);
        await service.RegisterAsync(Register(referral: "REF-ABC"));

        await service.VerifyOtpAsync(new VerifyOtpRequest("new.user@example.com", Staged()!.Otp));

        var m = await Db().Members.IgnoreQueryFilters().SingleAsync(x => x.Email == "new.user@example.com");
        Assert.Equal("ref1", m.ReferredById);
        var referral = await Db().Referrals.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(("ref1", "Registered", m.Id, "new.user@example.com"), (referral.ReferrerId, referral.Status, referral.ReferredMemberId, referral.ReferredEmail));
    }

    [Fact]
    public async Task A_referral_that_was_already_sent_as_an_invitation_is_upgraded_rather_than_duplicated()
    {
        var referrer = Person("Active", email: "ref@x.com", id: "ref1"); referrer.ReferralCode = "REF-ABC";
        await Seed(referrer, new Referral { Id = "inv", ReferrerId = "ref1", ReferredEmail = "new.user@example.com", Status = "Pending" });
        await service.RegisterAsync(Register(referral: "REF-ABC"));

        await service.VerifyOtpAsync(new VerifyOtpRequest("new.user@example.com", Staged()!.Otp));

        var referral = await Db().Referrals.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(("inv", "Registered"), (referral.Id, referral.Status));
    }

    // ── Community invitations ───────────────────────────────────────────

    private static Community Group(string id = "c1", bool active = true) => new() { Id = id, Name = "Mining 2018", IsActive = active };

    private async Task<(CommunityMembership? Membership, Referral Referral)> RegisterViaInvite(string? community, string? channel = "whatsapp")
    {
        await service.RegisterAsync(Register(referral: "REF-ABC", community: community, channel: channel));
        await service.VerifyOtpAsync(new VerifyOtpRequest("new.user@example.com", Staged()!.Otp));
        var newcomer = await Db().Members.IgnoreQueryFilters().SingleAsync(x => x.Email == "new.user@example.com");
        return (await Db().CommunityMemberships.IgnoreQueryFilters().SingleOrDefaultAsync(m => m.MemberId == newcomer.Id),
                await Db().Referrals.IgnoreQueryFilters().SingleAsync());
    }

    [Fact]
    public async Task An_invitation_from_a_community_leader_approves_the_newcomer_into_that_community()
    {
        var leader = Person("Active", email: "ref@x.com", id: "ref1"); leader.ReferralCode = "REF-ABC";
        await Seed(leader, Group(), new CommunityMembership { CommunityId = "c1", MemberId = "ref1", Status = "Approved", Role = "Leader" });

        var (membership, referral) = await RegisterViaInvite("c1");

        Assert.Equal(("Approved", "Member", "ref1"), (membership!.Status, membership.Role, membership.DecidedBy));
        Assert.Equal(("c1", "whatsapp"), (referral.CommunityId, referral.Channel));
    }

    [Fact]
    public async Task An_invitation_from_an_ordinary_member_only_asks_to_join_so_a_leader_decides()
    {
        var member = Person("Active", email: "ref@x.com", id: "ref1"); member.ReferralCode = "REF-ABC";
        await Seed(member, Group(), new CommunityMembership { CommunityId = "c1", MemberId = "ref1", Status = "Approved", Role = "Member" });

        var (membership, _) = await RegisterViaInvite("c1");

        Assert.Equal("Pending", membership!.Status);
    }

    [Fact]
    public async Task A_demoted_or_unapproved_leader_can_no_longer_vouch()
    {
        var leader = Person("Active", email: "ref@x.com", id: "ref1"); leader.ReferralCode = "REF-ABC";
        await Seed(leader, Group(), new CommunityMembership { CommunityId = "c1", MemberId = "ref1", Status = "Pending", Role = "Leader" });

        var (membership, _) = await RegisterViaInvite("c1");

        Assert.Equal("Pending", membership!.Status);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("inactive")]
    public async Task An_unknown_or_inactive_community_is_ignored_and_registration_still_works(string community)
    {
        var leader = Person("Active", email: "ref@x.com", id: "ref1"); leader.ReferralCode = "REF-ABC";
        await Seed(leader, Group("inactive", active: false));

        var (membership, referral) = await RegisterViaInvite(community);

        Assert.Null(membership);
        Assert.Null(referral.CommunityId);
        Assert.Equal("Registered", referral.Status);
    }

    [Fact]
    public async Task A_community_in_the_link_without_a_referral_code_changes_nothing()
    {
        await Seed(Group());
        await service.RegisterAsync(Register(community: "c1", channel: "whatsapp"));
        await service.VerifyOtpAsync(new VerifyOtpRequest("new.user@example.com", Staged()!.Otp));

        Assert.Equal(0, await Db().CommunityMemberships.IgnoreQueryFilters().CountAsync());
        Assert.Equal(0, await Db().Referrals.IgnoreQueryFilters().CountAsync());
    }

    [Theory]
    [InlineData("WhatsApp", "whatsapp")]
    [InlineData("qr", "qr")]
    [InlineData("<script>", null)]
    public async Task The_sharing_channel_is_kept_only_when_it_is_a_known_one(string sent, string? stored)
    {
        var leader = Person("Active", email: "ref@x.com", id: "ref1"); leader.ReferralCode = "REF-ABC";
        await Seed(leader, Group());

        var (_, referral) = await RegisterViaInvite("c1", sent);

        Assert.Equal(stored, referral.Channel);
    }

    [Fact]
    public async Task An_unknown_referral_code_is_ignored()
    {
        await service.RegisterAsync(Register(referral: "NOPE"));
        await service.VerifyOtpAsync(new VerifyOtpRequest("new.user@example.com", Staged()!.Otp));
        Assert.Null((await Db().Members.IgnoreQueryFilters().SingleAsync()).ReferredById);
        Assert.Equal(0, await Db().Referrals.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task The_code_can_be_resent_three_times_with_a_fresh_code_each_time_then_no_more()
    {
        await service.RegisterAsync(Register());
        var seen = new HashSet<string> { Staged()!.Otp };

        for (var i = 1; i <= 3; i++)
        {
            var ok = await service.ResendOtpAsync(new ResendOtpRequest("new.user@example.com"));
            Assert.Equal(200, ok.Code);
            Assert.Contains($"{3 - i} resend attempt(s) remaining", ok.Message);
            Assert.Equal(i, Staged()!.ResendCount);
            seen.Add(Staged()!.Otp);
        }

        var blocked = await service.ResendOtpAsync(new ResendOtpRequest("new.user@example.com"));
        Assert.Equal(400, blocked.Code);
        Assert.Contains("Maximum resend", blocked.Message);
        Assert.True(seen.Count >= 2);   // codes are regenerated, not reused
    }

    [Fact]
    public async Task Resending_with_nothing_staged_is_rejected()
        => Assert.Equal(400, (await service.ResendOtpAsync(new ResendOtpRequest("nobody@x.com"))).Code);

    // ── Google ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Google_registration_creates_a_pending_verified_member_with_an_unusable_password()
    {
        google.Setup(g => g.VerifyAsync("tok")).ReturnsAsync(new GoogleIdentity("g@x.com", "Gina", "Owusu", null));

        var response = await service.GoogleRegisterAsync(new GoogleRegisterRequest("tok", "0241", "S9", 2016, "d1", AcceptedTerms: true));

        Assert.Equal(201, response.Code);
        var m = await Db().Members.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(("Gina", "Pending", true, "google", 2016), (m.FirstName, m.Status, m.IsEmailVerified, m.CreatedBy, m.GraduationYear));
        Assert.False(BCrypt.Net.BCrypt.Verify("", m.Password));
    }

    [Fact]
    public async Task Google_registration_needs_a_valid_token_and_accepted_terms_and_refuses_existing_accounts()
    {
        google.Setup(g => g.VerifyAsync("bad")).ReturnsAsync((GoogleIdentity?)null);
        google.Setup(g => g.VerifyAsync("tok")).ReturnsAsync(new GoogleIdentity("ama@x.com", "Ama", "M", null));

        Assert.Equal(401, (await service.GoogleRegisterAsync(new GoogleRegisterRequest("bad", "0", null, 2016, null, AcceptedTerms: true))).Code);
        Assert.Equal(400, (await service.GoogleRegisterAsync(new GoogleRegisterRequest("tok", "0", null, 2016, null, AcceptedTerms: false))).Code);

        await Seed(Person("Pending"));
        var pending = await service.GoogleRegisterAsync(new GoogleRegisterRequest("tok", "0", null, 2016, null, AcceptedTerms: true));
        Assert.Equal(409, pending.Code);
        Assert.Contains("pending approval", pending.Message);
    }

    [Fact]
    public async Task Google_registration_with_auto_approval_is_active_with_a_number()
    {
        await SetInstitution(i => i.AutoApproveMembers = true);
        google.Setup(g => g.VerifyAsync("tok")).ReturnsAsync(new GoogleIdentity("g@x.com", "Gina", "O", null));

        var response = await service.GoogleRegisterAsync(new GoogleRegisterRequest("tok", "0", null, 2016, null, AcceptedTerms: true));

        Assert.True(((RegistrationResultResponse)response.Data!).Approved);
        var m = await Db().Members.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(("Active", "UMAT-2016-0001"), (m.Status, m.MemberNumber));
    }

    // ── Sign in ─────────────────────────────────────────────────────────

    [Fact]
    public async Task A_correct_login_returns_a_signed_token_with_the_members_claims_and_stores_the_refresh_token()
    {
        await Seed(Person());

        var response = await service.LoginAsync(new LoginRequest(" AMA@x.com ", "Secret#123"));

        Assert.Equal(200, response.Code);
        var data = response.Data!;
        Assert.Equal(("m1", "Member", 2015), (data.User.Id, data.User.Role, data.User.GraduationYear));
        Assert.Equal(2 * 3600, data.Tokens.ExpiresIn);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(data.Tokens.AccessToken);
        Assert.Equal(("iss", "aud"), (jwt.Issuer, jwt.Audiences.Single()));
        Assert.Equal("m1", jwt.Claims.First(c => c.Type is ClaimTypes.NameIdentifier or "nameid").Value);
        Assert.Contains(jwt.Claims, c => c.Value == "Member");
        Assert.Contains(jwt.Claims, c => c.Type == "institution_id" && c.Value == Tenant);
        Assert.True(jwt.ValidTo > DateTime.UtcNow.AddHours(1.9) && jwt.ValidTo < DateTime.UtcNow.AddHours(2.1));

        Assert.Equal(data.Tokens.RefreshToken, await redis.GetAsync<string>("member:refresh:m1"));
        Assert.NotNull((await Db().Members.IgnoreQueryFilters().SingleAsync()).LastLoginAt);
    }

    [Theory]
    [InlineData("ama@x.com", "wrong")]
    [InlineData("nobody@x.com", "Secret#123")]
    public async Task A_wrong_password_or_unknown_email_gives_the_same_vague_answer(string email, string password)
    {
        await Seed(Person());
        var response = await service.LoginAsync(new LoginRequest(email, password));
        Assert.Equal(400, response.Code);
        Assert.Equal("Invalid email or password", response.Message);
        Assert.Empty(redis.Store);
    }

    [Theory]
    [InlineData("Pending", "pending admin approval")]
    [InlineData("Banned", "banned")]
    [InlineData("Blocked", "permanently blocked")]
    [InlineData("Suspended", "registration was rejected")]
    public async Task Members_who_are_not_active_cannot_sign_in_and_are_told_why(string status, string fragment)
    {
        await Seed(Person(status));
        var response = await service.LoginAsync(new LoginRequest("ama@x.com", "Secret#123"));
        Assert.Equal(400, response.Code);
        Assert.Contains(fragment, response.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(redis.Store);
    }

    [Fact]
    public async Task Google_login_matches_by_email_and_applies_the_same_status_rules()
    {
        google.Setup(g => g.VerifyAsync("tok")).ReturnsAsync(new GoogleIdentity("ama@x.com", "Ama", "M", null));

        Assert.Equal(400, (await service.GoogleLoginAsync(new GoogleLoginRequest("tok"))).Code);   // not registered

        await Seed(Person("Banned"));
        Assert.Contains("banned", (await service.GoogleLoginAsync(new GoogleLoginRequest("tok"))).Message);
    }

    [Fact]
    public async Task Google_login_signs_in_an_active_member_and_stores_a_refresh_token()
    {
        await Seed(Person());
        google.Setup(g => g.VerifyAsync("tok")).ReturnsAsync(new GoogleIdentity("ama@x.com", "Ama", "M", null));

        var response = await service.GoogleLoginAsync(new GoogleLoginRequest("tok"));

        Assert.Equal(200, response.Code);
        Assert.NotNull(await redis.GetAsync<string>("member:refresh:m1"));
    }

    // ── Refresh ─────────────────────────────────────────────────────────

    private async Task<AuthTokensResponse> LoggedIn(string status = "Active")
    {
        await Seed(Person(status == "Active" ? "Active" : "Active"));
        var login = await service.LoginAsync(new LoginRequest("ama@x.com", "Secret#123"));
        if (status != "Active")
        {
            using var db = Db();
            var m = await db.Members.IgnoreQueryFilters().SingleAsync();
            m.Status = status;
            db.Update(m);
            await db.SaveChangesAsync();
            Fresh();
        }
        return login.Data!.Tokens;
    }

    [Fact]
    public async Task A_valid_pair_refreshes_to_a_new_pair_and_the_old_refresh_token_stops_working()
    {
        var old = await LoggedIn();

        var response = await service.RefreshTokenAsync(new RefreshTokenRequest(old.RefreshToken, old.AccessToken));

        Assert.Equal(200, response.Code);
        Assert.NotEqual(old.RefreshToken, response.Data!.Tokens.RefreshToken);
        var replay = await service.RefreshTokenAsync(new RefreshTokenRequest(old.RefreshToken, old.AccessToken));
        Assert.Equal(401, replay.Code);
    }

    [Fact]
    public async Task A_refresh_token_that_does_not_match_is_refused()
    {
        var pair = await LoggedIn();
        Assert.Equal(401, (await service.RefreshTokenAsync(new RefreshTokenRequest("not-the-token", pair.AccessToken))).Code);
    }

    [Theory]
    [InlineData("garbage")]
    [InlineData("")]
    public async Task A_malformed_access_token_is_refused(string access)
    {
        var pair = await LoggedIn();
        Assert.Equal(401, (await service.RefreshTokenAsync(new RefreshTokenRequest(pair.RefreshToken, access))).Code);
    }

    [Fact]
    public async Task An_access_token_signed_with_another_key_cannot_be_used_to_refresh()
    {
        var pair = await LoggedIn();
        var forged = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken("iss", "aud",
            [new Claim(ClaimTypes.NameIdentifier, "m1")], expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: new Microsoft.IdentityModel.Tokens.SigningCredentials(
                new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes("a-completely-different-signing-key-32chars!")),
                Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256)));
        Assert.Equal(401, (await service.RefreshTokenAsync(new RefreshTokenRequest(pair.RefreshToken, forged))).Code);
    }

    [Fact]
    public async Task An_expired_access_token_is_still_good_for_refreshing()
    {
        var pair = await LoggedIn();
        var expired = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken("iss", "aud",
            [new Claim(ClaimTypes.NameIdentifier, "m1")], notBefore: DateTime.UtcNow.AddHours(-5), expires: DateTime.UtcNow.AddHours(-3),
            signingCredentials: new Microsoft.IdentityModel.Tokens.SigningCredentials(
                new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(tokens.MemberSigningKey)),
                Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256)));
        Assert.Equal(200, (await service.RefreshTokenAsync(new RefreshTokenRequest(pair.RefreshToken, expired))).Code);
    }

    [Theory]
    [InlineData("Banned")]
    [InlineData("Blocked")]
    [InlineData("Suspended")]
    [InlineData("Pending")]
    [InlineData("Deleted")]
    public async Task A_member_who_is_no_longer_active_cannot_keep_refreshing_their_session(string status)
    {
        var pair = await LoggedIn(status);

        var response = await service.RefreshTokenAsync(new RefreshTokenRequest(pair.RefreshToken, pair.AccessToken));

        Assert.Equal(401, response.Code);
        Assert.Null(await redis.GetAsync<string>("member:refresh:m1"));   // and the stored session is revoked
    }

    // ── Passwords ───────────────────────────────────────────────────────

    [Fact]
    public async Task Forgot_password_gives_the_same_answer_for_unknown_emails_and_sends_nothing()
    {
        var unknown = await service.ForgotPasswordAsync(new ForgotPasswordRequest("nobody@x.com"));
        Assert.Equal(200, unknown.Code);
        Assert.Contains("if the account exists", unknown.Message);
        Assert.DoesNotContain(log.Entries, e => e.Message.Contains("dropped"));
    }

    [Fact]
    public async Task Forgot_password_issues_a_reset_token_and_queues_the_email()
    {
        await Seed(Person());
        var response = await service.ForgotPasswordAsync(new ForgotPasswordRequest(" AMA@x.com"));

        Assert.Equal(200, response.Code);
        var m = await Db().Members.IgnoreQueryFilters().SingleAsync();
        Assert.StartsWith("reset_", m.EmailVerificationToken);
        Assert.NotNull(m.EmailVerificationSentAt);
        Assert.Contains(log.Entries, e => e.Message.Contains("Email") && e.Message.Contains("dropped"));
    }

    private async Task<string> ResetTokenFor(string email = "ama@x.com")
    {
        await service.ForgotPasswordAsync(new ForgotPasswordRequest(email));
        Fresh();   // the next request gets its own context, as in production
        return (await Db().Members.IgnoreQueryFilters().SingleAsync()).EmailVerificationToken!;
    }

    [Fact]
    public async Task The_right_token_resets_the_password_and_can_be_used_only_once()
    {
        await Seed(Person());
        var token = await ResetTokenFor();

        var ok = await service.ResetPasswordAsync(new ResetPasswordRequest(token, "ama@x.com", "NewSecret#1"));

        Assert.Equal(200, ok.Code);
        var m = await Db().Members.IgnoreQueryFilters().SingleAsync();
        Assert.True(BCrypt.Net.BCrypt.Verify("NewSecret#1", m.Password));
        Assert.Null(m.EmailVerificationToken);
        Fresh();
        Assert.Equal(400, (await service.ResetPasswordAsync(new ResetPasswordRequest(token, "ama@x.com", "Another#2"))).Code);
        Fresh();
        Assert.Equal(200, (await service.LoginAsync(new LoginRequest("ama@x.com", "NewSecret#1"))).Code);
        Fresh();
        Assert.Equal(400, (await service.LoginAsync(new LoginRequest("ama@x.com", "Secret#123"))).Code);
    }

    [Theory]
    [InlineData("wrong-token", "ama@x.com")]
    [InlineData("reset_x", "nobody@x.com")]
    public async Task A_wrong_token_or_unknown_email_is_refused_with_one_message(string token, string email)
    {
        await Seed(Person());
        await ResetTokenFor();
        var response = await service.ResetPasswordAsync(new ResetPasswordRequest(token, email, "NewSecret#1"));
        Assert.Equal(400, response.Code);
        Assert.Equal("Invalid reset token or email.", response.Message);
        Assert.True(BCrypt.Net.BCrypt.Verify("Secret#123", (await Db().Members.IgnoreQueryFilters().SingleAsync()).Password));
    }

    [Fact]
    public async Task A_reset_token_expires_after_24_hours()
    {
        await Seed(Person());
        var token = await ResetTokenFor();
        using (var db = Db())
        {
            var m = await db.Members.IgnoreQueryFilters().SingleAsync();
            m.EmailVerificationSentAt = DateTime.UtcNow.AddHours(-25);
            db.Update(m);
            await db.SaveChangesAsync();
        }
        Fresh();

        var response = await service.ResetPasswordAsync(new ResetPasswordRequest(token, "ama@x.com", "NewSecret#1"));

        Assert.Equal(400, response.Code);
        Assert.Contains("expired", response.Message);
    }

    [Fact]
    public async Task An_email_verification_token_cannot_be_used_to_reset_a_password()
    {
        var m = Person(); m.EmailVerificationToken = "verify_abc"; m.EmailVerificationSentAt = DateTime.UtcNow;
        await Seed(m);
        Assert.Equal(400, (await service.ResetPasswordAsync(new ResetPasswordRequest("verify_abc", "ama@x.com", "NewSecret#1"))).Code);
    }

    [Fact]
    public async Task Changing_the_password_needs_the_current_one_and_signs_other_sessions_out()
    {
        await Seed(Person());
        await service.LoginAsync(new LoginRequest("ama@x.com", "Secret#123"));
        Fresh();
        var me = new AuthData { Id = "m1" };

        var wrong = await service.ChangePasswordAsync(new ChangePasswordRequest("nope", "NewSecret#1"), me);
        Assert.Equal(400, wrong.Code);
        Assert.NotNull(await redis.GetAsync<string>("member:refresh:m1"));
        Fresh();

        var ok = await service.ChangePasswordAsync(new ChangePasswordRequest("Secret#123", "NewSecret#1"), me);
        Assert.Equal(200, ok.Code);
        Assert.Null(await redis.GetAsync<string>("member:refresh:m1"));
        Fresh();
        Assert.Equal(200, (await service.LoginAsync(new LoginRequest("ama@x.com", "NewSecret#1"))).Code);
        Fresh();
        Assert.Equal(404, (await service.ChangePasswordAsync(new ChangePasswordRequest("a", "b"), new AuthData { Id = "ghost" })).Code);
    }

    // ── Email verification links ────────────────────────────────────────

    [Fact]
    public async Task A_verification_link_is_sent_only_to_unverified_accounts_and_never_reveals_whether_an_email_exists()
    {
        await Seed(Person());
        var unknown = await service.SendEmailVerificationLinkAsync(new SendEmailVerificationRequest("nobody@x.com"));
        Assert.Equal(200, unknown.Code);

        await service.SendEmailVerificationLinkAsync(new SendEmailVerificationRequest("ama@x.com"));
        Assert.StartsWith("verify_", (await Db().Members.IgnoreQueryFilters().SingleAsync()).EmailVerificationToken);
    }

    [Fact]
    public async Task An_already_verified_email_is_not_sent_another_link()
    {
        var m = Person(); m.IsEmailVerified = true;
        await Seed(m);
        var response = await service.SendEmailVerificationLinkAsync(new SendEmailVerificationRequest("ama@x.com"));
        Assert.Contains("already verified", response.Message);
        Assert.Null((await Db().Members.IgnoreQueryFilters().SingleAsync()).EmailVerificationToken);
    }

    [Fact]
    public async Task The_verification_link_marks_the_email_verified_once_and_rejects_bad_expired_or_reset_tokens()
    {
        var m = Person(); m.EmailVerificationToken = "verify_good"; m.EmailVerificationSentAt = DateTime.UtcNow;
        await Seed(m);

        Assert.Equal(400, (await service.VerifyEmailAsync("verify_bad", "ama@x.com")).Code);
        Assert.Equal(404, (await service.VerifyEmailAsync("verify_good", "nobody@x.com")).Code);
        Assert.Equal(200, (await service.VerifyEmailAsync("verify_good", " AMA@x.com ")).Code);
        var saved = await Db().Members.IgnoreQueryFilters().SingleAsync();
        Assert.True(saved.IsEmailVerified);
        Assert.Null(saved.EmailVerificationToken);
        Assert.Contains("already", (await service.VerifyEmailAsync("verify_good", "ama@x.com")).Message);
    }

    [Fact]
    public async Task An_expired_or_wrong_kind_of_token_does_not_verify_the_email()
    {
        var expired = Person(); expired.EmailVerificationToken = "verify_old"; expired.EmailVerificationSentAt = DateTime.UtcNow.AddHours(-30);
        var wrongKind = Person(email: "b@x.com", id: "m2"); wrongKind.EmailVerificationToken = "reset_abc"; wrongKind.EmailVerificationSentAt = DateTime.UtcNow;
        await Seed(expired, wrongKind);

        Assert.Contains("expired", (await service.VerifyEmailAsync("verify_old", "ama@x.com")).Message);
        Assert.Equal(400, (await service.VerifyEmailAsync("reset_abc", "b@x.com")).Code);
    }

    // ── Profile ─────────────────────────────────────────────────────────

    private static IFormFile Upload(string name, long length = 100) =>
        new FormFile(new MemoryStream(new byte[Math.Min(length, 1024)]), 0, length, "f", name) { Headers = new HeaderDictionary(), ContentType = "image/png" };

    [Fact]
    public async Task Profile_updates_change_only_the_fields_that_are_sent()
    {
        var m = Person(); m.Company = "Old Co"; m.Bio = "Old bio";
        await Seed(m);

        var response = await service.UpdateProfileAsync(new UpdateProfileRequest { JobTitle = "Engineer", Skills = ["C#", "SQL"], ShowEmailOnDirectory = true }, new AuthData { Id = "m1" });

        Assert.Equal(200, response.Code);
        var saved = await Db().Members.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(("Engineer", "Old Co", "Old bio", true, "m1"), (saved.JobTitle, saved.Company, saved.Bio, saved.ShowEmailOnDirectory, saved.UpdatedBy));
        Assert.Equal(new[] { "C#", "SQL" }, saved.Skills);
    }

    [Fact]
    public async Task Appearing_on_the_map_needs_a_location_and_it_is_rounded_to_roughly_city_level()
    {
        await Seed(Person());
        var me = new AuthData { Id = "m1" };

        var missing = await service.UpdateProfileAsync(new UpdateProfileRequest { ShowOnAlumniMap = true }, me);
        Assert.Equal(400, missing.Code);
        Assert.Contains("Location is required", missing.Message);

        var ok = await service.UpdateProfileAsync(new UpdateProfileRequest { ShowOnAlumniMap = true, MapLatitude = 5.34567, MapLongitude = -1.98765 }, me);
        Assert.Equal(200, ok.Code);
        var saved = await Db().Members.IgnoreQueryFilters().SingleAsync();
        Assert.Equal((5.3, -2.0), (saved.MapLatitude, saved.MapLongitude));
    }

    [Fact]
    public async Task Opting_out_of_the_map_erases_the_stored_location()
    {
        var m = Person(); m.ShowOnAlumniMap = true; m.MapLatitude = 5.3; m.MapLongitude = -2.0;
        await Seed(m);

        await service.UpdateProfileAsync(new UpdateProfileRequest { ShowOnAlumniMap = false }, new AuthData { Id = "m1" });

        var saved = await Db().Members.IgnoreQueryFilters().SingleAsync();
        Assert.False(saved.ShowOnAlumniMap);
        Assert.Null(saved.MapLatitude);
        Assert.Null(saved.MapLongitude);
    }

    [Fact]
    public async Task A_member_already_on_the_map_can_edit_other_fields_without_resending_coordinates()
    {
        var m = Person(); m.ShowOnAlumniMap = true; m.MapLatitude = 5.3; m.MapLongitude = -2.0;
        await Seed(m);
        var response = await service.UpdateProfileAsync(new UpdateProfileRequest { Bio = "Hello" }, new AuthData { Id = "m1" });
        Assert.Equal(200, response.Code);
        Assert.Equal(5.3, (await Db().Members.IgnoreQueryFilters().SingleAsync()).MapLatitude);
    }

    [Fact]
    public async Task A_pensioner_cannot_go_back_to_employed_but_can_become_one()
    {
        await Seed(Person());
        var me = new AuthData { Id = "m1" };

        Assert.Equal(200, (await service.UpdateProfileAsync(new UpdateProfileRequest { EmploymentStatus = "Pensioner" }, me)).Code);
        var back = await service.UpdateProfileAsync(new UpdateProfileRequest { EmploymentStatus = "Employed" }, me);
        Assert.Equal(400, back.Code);
        Assert.Contains("cannot be changed back", back.Message);
        Assert.Equal("Pensioner", (await Db().Members.IgnoreQueryFilters().SingleAsync()).EmploymentStatus);
    }

    [Fact]
    public async Task An_unrecognised_employment_status_is_ignored()
    {
        await Seed(Person());
        await service.UpdateProfileAsync(new UpdateProfileRequest { EmploymentStatus = "Astronaut" }, new AuthData { Id = "m1" });
        Assert.Equal("Employed", (await Db().Members.IgnoreQueryFilters().SingleAsync()).EmploymentStatus);
    }

    [Theory]
    [InlineData("photo.jpg", true)]
    [InlineData("photo.PNG", true)]
    [InlineData("photo.webp", true)]
    [InlineData("photo.gif", true)]
    [InlineData("photo.exe", false)]
    [InlineData("photo.svg", false)]
    [InlineData("photo", false)]
    public async Task Only_image_files_are_accepted_as_a_profile_picture(string fileName, bool allowed)
    {
        await Seed(Person());

        var response = await service.UpdateProfileAsync(new UpdateProfileRequest { ProfilePicture = Upload(fileName) }, new AuthData { Id = "m1" });

        Assert.Equal(allowed ? 200 : 400, response.Code);
        Assert.Equal(allowed ? "https://cdn/pic.png" : null, (await Db().Members.IgnoreQueryFilters().SingleAsync()).ProfilePictureUrl);
    }

    [Fact]
    public async Task A_profile_picture_over_five_megabytes_is_refused()
    {
        await Seed(Person());
        var big = await service.UpdateProfileAsync(new UpdateProfileRequest { ProfilePicture = Upload("a.png", 5 * 1024 * 1024 + 1) }, new AuthData { Id = "m1" });
        Assert.Equal(400, big.Code);
        Assert.Contains("5 MB", big.Message);
        var exactly = await service.UpdateProfileAsync(new UpdateProfileRequest { ProfilePicture = Upload("a.png", 5 * 1024 * 1024) }, new AuthData { Id = "m1" });
        Assert.Equal(200, exactly.Code);
    }

    [Fact]
    public async Task Connection_type_is_trimmed_and_a_blank_value_clears_it()
    {
        await Seed(Person());
        var me = new AuthData { Id = "m1" };
        await service.UpdateProfileAsync(new UpdateProfileRequest { ConnectionType = "  Alumnus " }, me);
        Assert.Equal("Alumnus", (await Db().Members.IgnoreQueryFilters().SingleAsync()).ConnectionType);
        await service.UpdateProfileAsync(new UpdateProfileRequest { ConnectionType = "  " }, me);
        Assert.Null((await Db().Members.IgnoreQueryFilters().SingleAsync()).ConnectionType);
        await service.UpdateProfileAsync(new UpdateProfileRequest { }, me);   // null leaves it alone
        Assert.Null((await Db().Members.IgnoreQueryFilters().SingleAsync()).ConnectionType);
    }

    [Fact]
    public async Task The_profile_can_be_read_and_unknown_members_are_404()
    {
        await Seed(Person());
        var profile = (await service.GetProfileAsync(new AuthData { Id = "m1" })).Data!;
        Assert.Equal(("m1", "Ama", "ama@x.com", "Active"), (profile.Id, profile.FirstName, profile.Email, profile.Status));
        Assert.Equal(404, (await service.GetProfileAsync(new AuthData { Id = "ghost" })).Code);
        Assert.Equal(404, (await service.UpdateProfileAsync(new UpdateProfileRequest(), new AuthData { Id = "ghost" })).Code);
    }
}
