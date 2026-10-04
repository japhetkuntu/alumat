using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Member.Api.Services.Implementations;
using ReservEase.Alumni.PostgresDb.Sdk.DbContexts;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.TestKit;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.Member.Api.Tests;

public class MemberAccountDeletionServiceTests
{
    private const string Tenant = "inst-1";

    private sealed class Rig
    {
        public string DbName { get; } = TestDb.NewName();
        public AuthData Me { get; } = new() { Id = "m1" };
        public MemberAccountDeletionService Service { get; private set; } = null!;
        public Rig() => Fresh();

        public MemberAccountDeletionService Fresh()
        {
            var db = TestDb.Create(DbName, Tenant);
            return Service = new MemberAccountDeletionService(
                new AlumniPgRepository<MemberEntity>(db), new AlumniPgRepository<Notification>(db), new AlumniPgRepository<NotificationPreference>(db),
                new AlumniPgRepository<PushSubscription>(db), new AlumniPgRepository<MentorProfile>(db), new AlumniPgRepository<MentorshipRequest>(db),
                new AlumniPgRepository<BusinessListing>(db), new AlumniPgRepository<Spotlight>(db), new AlumniPgRepository<CommunityMembership>(db),
                new AlumniPgRepository<EventRsvp>(db), new AlumniPgRepository<MemberBadge>(db), new AlumniPgRepository<RecurringContribution>(db),
                new AlumniPgRepository<Pledge>(db), new AlumniPgRepository<Contribution>(db), new AlumniPgRepository<PaymentTransaction>(db),
                new AlumniPgRepository<StoreOrder>(db), new AlumniPgRepository<ServiceRequest>(db),
                new AlumniPgRepository<ForumThread>(db), new AlumniPgRepository<ForumPost>(db), new AlumniPgRepository<ClassNote>(db),
                new AlumniPgRepository<ClassNoteLike>(db), new AlumniPgRepository<Referral>(db), NullLogger<MemberAccountDeletionService>.Instance);
        }

        public AlumniDbContext Db() => TestDb.Create(DbName, Tenant);

        public async Task Seed(params object[] entities)
        {
            using var db = Db();
            foreach (var e in entities) { if (e is PostgresDb.Sdk.Entities.ITenantScoped t) t.InstitutionId = Tenant; db.Add(e); }
            await db.SaveChangesAsync();
            Fresh();
        }
    }

    private static MemberEntity FullMember() => new()
    {
        Id = "m1", FirstName = "Ama", LastName = "Mensah", Email = "ama@x.com", Phone = "0241", Password = "hash", StudentId = "S1",
        DateOfBirth = new DateTime(1990, 1, 1), Program = "Mining", Company = "Acme", JobTitle = "Engineer", Location = "Tarkwa", ShowOnAlumniMap = true,
        MapLatitude = 5.3, MapLongitude = -1.99, LinkedInUrl = "https://li", Bio = "About", ProfilePictureUrl = "https://pic", EmailVerificationToken = "tok",
        BanReason = "x", YearOfEntry = 2011, House = "Red", StudentStatus = "Alumnus", PrefectStatus = "Prefect", ClubsAndSocieties = ["Chess"], LeadershipRoles = ["Chair"],
        Achievements = "Award", Skills = ["C#"], Interests = ["Chess"], ReferralCode = "AMA-1", IsMembershipActive = true, Status = "Active", MemberNumber = "UMAT-2015-0001",
    };

    private static MemberSnapshot Snap() => new() { Id = "m1", FirstName = "Ama", LastName = "Mensah", Email = "ama@x.com", ProfilePictureUrl = "https://pic", MemberNumber = "UMAT-2015-0001" };

    [Theory]
    [InlineData("")]
    [InlineData("delete")]
    [InlineData("Delete")]
    [InlineData("YES")]
    [InlineData(null)]
    public async Task Anything_other_than_the_exact_word_DELETE_is_refused_and_nothing_changes(string? word)
    {
        var rig = new Rig();
        await rig.Seed(FullMember());

        var response = await rig.Service.DeleteMyAccountAsync(rig.Me, word!);

        Assert.Equal(400, response.Code);
        Assert.Equal("Ama", (await rig.Db().Members.SingleAsync()).FirstName);
    }

    [Fact]
    public async Task The_confirmation_word_may_have_surrounding_spaces()
    {
        var rig = new Rig();
        await rig.Seed(FullMember());
        Assert.Equal(200, (await rig.Service.DeleteMyAccountAsync(rig.Me, "  DELETE ")).Code);
    }

    [Fact]
    public async Task An_unknown_account_is_404_and_an_already_deleted_one_is_a_harmless_success()
    {
        var rig = new Rig();
        Assert.Equal(404, (await rig.Service.DeleteMyAccountAsync(rig.Me, "DELETE")).Code);

        var gone = FullMember(); gone.Status = "Deleted";
        await rig.Seed(gone);
        var again = await rig.Service.DeleteMyAccountAsync(rig.Me, "DELETE");
        Assert.Equal(200, again.Code);
        Assert.Contains("already deleted", again.Message);
    }

    [Fact]
    public async Task The_member_row_keeps_its_id_and_number_but_every_personal_field_is_cleared()
    {
        var rig = new Rig();
        await rig.Seed(FullMember());

        await rig.Service.DeleteMyAccountAsync(rig.Me, "DELETE");

        var m = await rig.Db().Members.SingleAsync();
        Assert.Equal(("m1", "UMAT-2015-0001", "Deleted"), (m.Id, m.MemberNumber, m.Status));
        Assert.Equal(("Former", "member", "deleted-m1@removed.invalid"), (m.FirstName, m.LastName, m.Email));
        Assert.Null(m.Phone); Assert.Null(m.StudentId); Assert.Null(m.DateOfBirth); Assert.Null(m.Program); Assert.Null(m.Company);
        Assert.Null(m.JobTitle); Assert.Null(m.Location); Assert.Null(m.MapLatitude); Assert.Null(m.MapLongitude); Assert.Null(m.LinkedInUrl);
        Assert.Null(m.Bio); Assert.Null(m.ProfilePictureUrl); Assert.Null(m.EmailVerificationToken); Assert.Null(m.BanReason); Assert.Null(m.YearOfEntry);
        Assert.Null(m.House); Assert.Null(m.StudentStatus); Assert.Null(m.PrefectStatus); Assert.Null(m.ClubsAndSocieties); Assert.Null(m.LeadershipRoles);
        Assert.Null(m.Achievements); Assert.Null(m.Skills); Assert.Null(m.Interests); Assert.Null(m.ReferralCode);
        Assert.False(m.ShowOnAlumniMap);
        Assert.False(m.IsMembershipActive);
        Assert.Equal("m1", m.UpdatedBy);
    }

    [Fact]
    public async Task Signing_in_becomes_impossible_because_the_password_is_replaced_with_an_unknown_hash()
    {
        var rig = new Rig();
        await rig.Seed(FullMember());

        await rig.Service.DeleteMyAccountAsync(rig.Me, "DELETE");

        var m = await rig.Db().Members.SingleAsync();
        Assert.NotEqual("hash", m.Password);
        Assert.StartsWith("$2", m.Password);   // a bcrypt hash of a random value nobody knows
    }

    [Fact]
    public async Task Personal_content_and_preferences_are_removed_outright()
    {
        var rig = new Rig();
        await rig.Seed(FullMember(),
            new Notification { Id = "n1", RecipientId = "m1", RecipientType = "Member" },
            new NotificationPreference { Id = "p1", MemberId = "m1" },
            new PushSubscription { Id = "ps1", OwnerId = "m1", OwnerType = PushSubscriptionOwnerTypes.Member, Endpoint = "e" },
            new MemberBadge { Id = "b1", MemberId = "m1", BadgeType = "Referrer" },
            new CommunityMembership { Id = "cm1", MemberId = "m1", CommunityId = "c" },
            new EventRsvp { Id = "r1", MemberId = "m1", EventId = "e" },
            new Pledge { Id = "pl1", MemberId = "m1", CampaignId = "c" },
            new BusinessListing { Id = "bl1", MemberId = "m1", BusinessName = "Shop" },
            new Spotlight { Id = "s1", MemberId = "m1", Title = "Hero" });

        await rig.Service.DeleteMyAccountAsync(rig.Me, "DELETE");

        using var db = rig.Db();
        Assert.Equal(0, await db.Notifications.CountAsync());
        Assert.Equal(0, await db.NotificationPreferences.CountAsync());
        Assert.Equal(0, await db.PushSubscriptions.CountAsync());
        Assert.Equal(0, await db.MemberBadges.CountAsync());
        Assert.Equal(0, await db.CommunityMemberships.CountAsync());
        Assert.Equal(0, await db.EventRsvps.CountAsync());
        Assert.Equal(0, await db.Pledges.CountAsync());
        Assert.Equal(0, await db.BusinessListings.CountAsync());
        Assert.Equal(0, await db.Spotlights.CountAsync());
    }

    [Fact]
    public async Task Other_members_content_is_never_touched()
    {
        var rig = new Rig();
        await rig.Seed(FullMember(),
            new Notification { Id = "n1", RecipientId = "m2", RecipientType = "Member" },
            new Notification { Id = "n2", RecipientId = "m1", RecipientType = "Admin" },   // same id, but a staff recipient
            new NotificationPreference { Id = "p1", MemberId = "m2" },
            new PushSubscription { Id = "ps1", OwnerId = "m1", OwnerType = PushSubscriptionOwnerTypes.InstitutionStaff, Endpoint = "staff-device" },
            new MemberBadge { Id = "b1", MemberId = "m2", BadgeType = "Referrer" },
            new EventRsvp { Id = "r1", MemberId = "m2", EventId = "e" });

        await rig.Service.DeleteMyAccountAsync(rig.Me, "DELETE");

        using var db = rig.Db();
        Assert.Equal(2, await db.Notifications.CountAsync());
        Assert.Equal(1, await db.NotificationPreferences.CountAsync());
        Assert.Equal(1, await db.PushSubscriptions.CountAsync());   // a staff account that happens to share the id keeps its devices
        Assert.Equal(1, await db.MemberBadges.CountAsync());
        Assert.Equal(1, await db.EventRsvps.CountAsync());
    }

    [Fact]
    public async Task Mentoring_removes_the_profile_the_requests_made_to_it_and_the_members_own_requests_as_a_mentee()
    {
        var rig = new Rig();
        await rig.Seed(FullMember(),
            new MentorProfile { Id = "mp1", MemberId = "m1", Area = "Eng" },
            new MentorProfile { Id = "mp2", MemberId = "m2", Area = "Law" },
            new MentorshipRequest { Id = "to-me", MentorProfileId = "mp1", MenteeId = "m3" },
            new MentorshipRequest { Id = "by-me", MentorProfileId = "mp2", MenteeId = "m1" },
            new MentorshipRequest { Id = "unrelated", MentorProfileId = "mp2", MenteeId = "m3" });

        await rig.Service.DeleteMyAccountAsync(rig.Me, "DELETE");

        using var db = rig.Db();
        Assert.Equal(new[] { "mp2" }, await db.MentorProfiles.Select(p => p.Id).ToListAsync());
        Assert.Equal(new[] { "unrelated" }, await db.MentorshipRequests.Select(r => r.Id).ToListAsync());
    }

    [Fact]
    public async Task A_monthly_gift_is_cancelled_and_loses_the_members_identity_but_is_kept()
    {
        var rig = new Rig();
        await rig.Seed(FullMember(), new RecurringContribution { Id = "rc", MemberId = "m1", CampaignId = "c", Status = "Active", Member = Snap() });

        await rig.Service.DeleteMyAccountAsync(rig.Me, "DELETE");

        var gift = await rig.Db().RecurringContributions.SingleAsync();
        Assert.Equal("Cancelled", gift.Status);
        Assert.Equal(("Former", "member", ""), (gift.Member!.FirstName, gift.Member.LastName, gift.Member.Email));
    }

    [Fact]
    public async Task Money_and_order_records_are_kept_for_the_books_with_the_identity_removed_but_the_member_number_retained()
    {
        var rig = new Rig();
        await rig.Seed(FullMember(),
            new Contribution { Id = "c1", MemberId = "m1", CampaignId = "x", Amount = 100, Status = "Successful", Member = Snap() },
            new PaymentTransaction { Id = "t1", MemberId = "m1", CampaignId = "x", Reference = "CN_1", Amount = 100, Member = Snap() },
            new StoreOrder { Id = "o1", MemberId = "m1", OrderNumber = "ORD-1", TotalAmount = 20, Member = Snap() },
            new ServiceRequest { Id = "sr1", MemberId = "m1", RequestNumber = "REQ-1", Amount = 30, Member = Snap() },
            new Contribution { Id = "other", MemberId = "m2", CampaignId = "x", Amount = 5, Member = new MemberSnapshot { Id = "m2", FirstName = "Kofi", LastName = "B", Email = "k@x.com" } });

        await rig.Service.DeleteMyAccountAsync(rig.Me, "DELETE");

        using var db = rig.Db();
        var snapshots = new[]
        {
            (await db.Contributions.SingleAsync(c => c.Id == "c1")).Member!,
            (await db.PaymentTransactions.SingleAsync()).Member!,
            (await db.StoreOrders.SingleAsync()).Member!,
            (await db.ServiceRequests.SingleAsync()).Member!,
        };
        Assert.All(snapshots, s =>
        {
            Assert.Equal(("m1", "Former", "member", "", "UMAT-2015-0001"), (s.Id, s.FirstName, s.LastName, s.Email, s.MemberNumber));
            Assert.Null(s.ProfilePictureUrl);
        });
        Assert.Equal(100m, (await db.Contributions.SingleAsync(c => c.Id == "c1")).Amount);   // the amounts are untouched
        Assert.Equal("Kofi", (await db.Contributions.SingleAsync(c => c.Id == "other")).Member!.FirstName);
    }

    [Fact]
    public async Task Rows_without_a_snapshot_are_left_without_one_and_deleting_twice_is_safe()
    {
        var rig = new Rig();
        await rig.Seed(FullMember(), new Contribution { Id = "c1", MemberId = "m1", CampaignId = "x", Member = null });

        await rig.Service.DeleteMyAccountAsync(rig.Me, "DELETE");
        rig.Fresh();
        var second = await rig.Service.DeleteMyAccountAsync(rig.Me, "DELETE");

        Assert.Equal(200, second.Code);
        Assert.Null((await rig.Db().Contributions.SingleAsync()).Member);
    }

    [Fact]
    public async Task Forum_threads_posts_and_class_notes_stay_for_everyone_else_but_no_longer_name_the_member()
    {
        var rig = new Rig();
        await rig.Seed(FullMember(),
            new ForumThread { Id = "t1", AuthorId = "m1", Title = "Best first job?", Author = Snap() },
            new ForumThread { Id = "t2", AuthorId = "m2", Title = "Other", Author = new MemberSnapshot { Id = "m2", FirstName = "Kofi", LastName = "B", Email = "k@x.com" } },
            new ForumPost { Id = "p1", AuthorId = "m1", Content = "My reply", Author = Snap() },
            new ClassNote { Id = "n1", AuthorId = "m1", Content = "Hello class", Author = Snap() });

        await rig.Service.DeleteMyAccountAsync(rig.Me, "DELETE");

        using var db = rig.Db();
        var mine = await db.ForumThreads.SingleAsync(t => t.Id == "t1");
        Assert.Equal(("Best first job?", "Former", "member", ""), (mine.Title, mine.Author!.FirstName, mine.Author.LastName, mine.Author.Email));
        Assert.Null(mine.Author.ProfilePictureUrl);
        Assert.Equal("Former", (await db.ForumPosts.SingleAsync()).Author!.FirstName);
        Assert.Equal("My reply", (await db.ForumPosts.SingleAsync()).Content);
        var note = await db.ClassNotes.SingleAsync();
        Assert.Equal(("Hello class", "Former", ""), (note.Content, note.Author!.FirstName, note.Author.Email));
        Assert.Equal("Kofi", (await db.ForumThreads.SingleAsync(t => t.Id == "t2")).Author!.FirstName);
    }

    [Fact]
    public async Task Likes_the_member_gave_are_removed_and_the_notes_like_counts_come_back_down()
    {
        var rig = new Rig();
        await rig.Seed(FullMember(),
            new ClassNote { Id = "n1", AuthorId = "m2", Content = "a", LikeCount = 3 },
            new ClassNote { Id = "n2", AuthorId = "m2", Content = "b", LikeCount = 1 },
            new ClassNoteLike { Id = "l1", ClassNoteId = "n1", MemberId = "m1" },
            new ClassNoteLike { Id = "l2", ClassNoteId = "n1", MemberId = "m3" },
            new ClassNoteLike { Id = "l3", ClassNoteId = "n2", MemberId = "m1" });

        await rig.Service.DeleteMyAccountAsync(rig.Me, "DELETE");

        using var db = rig.Db();
        Assert.Equal(new[] { "l2" }, await db.ClassNoteLikes.Select(l => l.Id).ToListAsync());
        var counts = await db.ClassNotes.ToDictionaryAsync(n => n.Id, n => n.LikeCount);
        Assert.Equal((2, 0), (counts["n1"], counts["n2"]));
    }

    [Fact]
    public async Task A_like_count_never_goes_below_zero()
    {
        var rig = new Rig();
        await rig.Seed(FullMember(), new ClassNote { Id = "n1", AuthorId = "m2", Content = "a", LikeCount = 0 }, new ClassNoteLike { Id = "l1", ClassNoteId = "n1", MemberId = "m1" });
        await rig.Service.DeleteMyAccountAsync(rig.Me, "DELETE");
        Assert.Equal(0, (await rig.Db().ClassNotes.SingleAsync()).LikeCount);
    }

    [Fact]
    public async Task Referrals_lose_the_members_name_and_email_but_keep_the_other_persons_details()
    {
        var rig = new Rig();
        await rig.Seed(FullMember(),
            new Referral { Id = "i-referred", ReferrerId = "m1", ReferredEmail = "friend@x.com", Status = "Registered", ReferredMemberId = "m9",
                Referrer = Snap(), ReferredMember = new MemberSnapshot { Id = "m9", FirstName = "Yaw", LastName = "D", Email = "friend@x.com" } },
            new Referral { Id = "i-was-referred", ReferrerId = "m7", ReferredEmail = "ama@x.com", Status = "MembershipPaid", ReferredMemberId = "m1",
                Referrer = new MemberSnapshot { Id = "m7", FirstName = "Esi", LastName = "O", Email = "esi@x.com" }, ReferredMember = Snap() });

        await rig.Service.DeleteMyAccountAsync(rig.Me, "DELETE");

        using var db = rig.Db();
        var asReferrer = await db.Referrals.SingleAsync(r => r.Id == "i-referred");
        Assert.Equal("Former", asReferrer.Referrer!.FirstName);
        Assert.Equal(("friend@x.com", "Yaw"), (asReferrer.ReferredEmail, asReferrer.ReferredMember!.FirstName));

        var asReferred = await db.Referrals.SingleAsync(r => r.Id == "i-was-referred");
        Assert.Equal("deleted-m1@removed.invalid", asReferred.ReferredEmail);
        Assert.Equal(("Former", ""), (asReferred.ReferredMember!.FirstName, asReferred.ReferredMember.Email));
        Assert.Equal(("Esi", "MembershipPaid"), (asReferred.Referrer!.FirstName, asReferred.Status));   // the referrer's own record is untouched
    }
}
