using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.Institution.Api.Services.Implementations;
using ReservEase.Alumni.Institution.Api.Services.Interfaces;
using ReservEase.Alumni.Notifications.Sdk.Models;
using ReservEase.Alumni.PostgresDb.Sdk.DbContexts;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.Storage.Sdk.Services;
using ReservEase.Alumni.Temporal.Sdk;
using ReservEase.Alumni.TestKit;

namespace ReservEase.Alumni.Institution.Api.Tests;

/// <summary>An organiser posting updates to an event: any mix of parts, validated, uploaded, and announced to the people going.</summary>
public class EventUpdateServiceTests
{
    private const string Tenant = "inst-1";
    private static readonly AuthData Admin = new() { Id = "admin1", FirstName = "Kojo", LastName = "Mensah", Role = "SuperAdmin" };
    private static readonly IReadOnlyDictionary<string, List<IFormFile>> NoFiles = new Dictionary<string, List<IFormFile>>();

    private sealed class Rig
    {
        public string DbName { get; } = TestDb.NewName();
        public Mock<IStorageService> Storage { get; } = new();
        public CapturingLogger<EventUpdateService> Log { get; } = new();
        public EventUpdateService Service { get; private set; } = null!;
        public int Uploaded;

        public Rig()
        {
            Storage.Setup(s => s.UploadFileAsync(It.IsAny<IFormFile>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync((IFormFile _, string name, string _, string _) => { Interlocked.Increment(ref Uploaded); return $"https://cdn/{name}"; });
            Fresh();
        }

        public EventUpdateService Fresh()
        {
            var db = TestDb.Create(DbName, Tenant);
            var temporal = new Mock<ITemporalClientProvider>(); temporal.SetupGet(t => t.IsAvailable).Returns(false);   // a dropped notification is logged, so tests can count them
            return Service = new EventUpdateService(
                new AlumniPgRepository<AlumniEvent>(db), new AlumniPgRepository<EventUpdate>(db), new AlumniPgRepository<EventRsvp>(db),
                Storage.Object, temporal.Object, TestDb.Tenant(Tenant, "umat"), Mock.Of<IInstitutionAuditLogService>(), Log);
        }

        public AlumniDbContext Db() => TestDb.Create(DbName, Tenant);

        public async Task Seed(params object[] entities)
        {
            using var db = Db();
            foreach (var e in entities) { if (e is PostgresDb.Sdk.Entities.ITenantScoped t) t.InstitutionId = Tenant; db.Add(e); }
            await db.SaveChangesAsync();
            Fresh();
        }

        public int NotificationsDropped => Log.Entries.Count(e => e.Message.Contains("EventUpdatePosted"));
    }

    private static AlumniEvent Event(string id = "e1", List<int>? years = null, string createdBy = "someone") =>
        new() { Id = id, Title = "Gala", Venue = "Hall", StartDate = DateTime.UtcNow.AddDays(5), Status = "Upcoming", YearGroups = years, CreatedBy = createdBy };

    private static IFormFile File(string name, int bytes = 1000, string type = "application/octet-stream")
        => new FormFile(new MemoryStream(new byte[bytes]), 0, bytes, "x", name) { Headers = new HeaderDictionary(), ContentType = type };

    private static Dictionary<string, List<IFormFile>> Files(string blockId, params IFormFile[] files) => new() { [blockId] = [.. files] };

    private static EventUpdateBlock Text(string id = "t1", string text = "Doors open at 5pm.") => new() { Id = id, Type = "Text", Text = text };
    private static EventUpdateBlock Images(string id = "i1") => new() { Id = id, Type = "Images" };
    private static EventUpdateBlock Docs(string id = "f1") => new() { Id = id, Type = "Files" };
    private static EventUpdateBlock Video(string url, string id = "v1") => new() { Id = id, Type = "Video", Url = url };
    private static EventUpdateBlock Link(string url, string id = "l1") => new() { Id = id, Type = "Link", Url = url, Label = "Parking map" };

    private static EventUpdateSaveRequest Save(string title, string category, params EventUpdateBlock[] blocks) =>
        new() { Title = title, Category = category, Blocks = [.. blocks] };

    private static EventRsvp Seat(string member, string status = "Confirmed") => new() { Id = $"r-{member}", EventId = "e1", MemberId = member, Status = status };

    // ── A mix of parts, as many as needed ───────────────────────────────

    [Fact]
    public async Task One_update_can_mix_text_photos_video_links_and_files()
    {
        var rig = new Rig();
        await rig.Seed(Event());
        var files = new Dictionary<string, List<IFormFile>>
        {
            ["i1"] = [File("one.jpg"), File("two.png"), File("three.webp")],
            ["f1"] = [File("programme.pdf"), File("budget.xlsx")],
        };

        var response = await rig.Service.CreateAsync("e1",
            Save("Day 1 in pictures", "Photos & video", Text(), Images(), Video("https://youtu.be/dQw4w9WgXcQ"), Link("https://maps.example.com/hall"), Docs()),
            files, Admin);

        Assert.Equal(201, response.Code);
        var saved = await rig.Db().EventUpdates.SingleAsync();
        Assert.Equal(new[] { "Text", "Images", "Video", "Link", "Files" }, saved.Blocks.Select(b => b.Type));
        Assert.Equal(new[] { "one.jpg", "two.png", "three.webp" }, saved.Blocks[1].Items.Select(i => i.Name));
        Assert.Equal(new[] { "programme.pdf", "budget.xlsx" }, saved.Blocks[4].Items.Select(i => i.Name));
        Assert.Equal("https://www.youtube.com/watch?v=dQw4w9WgXcQ", saved.Blocks[2].Url);   // any YouTube address is stored the same way
        Assert.All(saved.Blocks[1].Items, i => Assert.StartsWith("https://cdn/", i.Url));
        Assert.Equal(5, rig.Uploaded);
        Assert.Equal("Kojo Mensah", saved.PostedByName);
    }

    [Fact]
    public async Task There_is_no_small_cap_on_how_many_photos_or_parts_an_update_holds()
    {
        var rig = new Rig();
        await rig.Seed(Event());
        var many = Enumerable.Range(0, 55).Select(i => File($"p{i}.jpg", 100)).ToArray();

        var response = await rig.Service.CreateAsync("e1", Save("The whole day", "Photos & video", Images()), Files("i1", many), Admin);

        Assert.Equal(201, response.Code);
        Assert.Equal(55, (await rig.Db().EventUpdates.SingleAsync()).Blocks[0].Items.Count);

        var thirtyParts = Enumerable.Range(0, 30).Select(i => Text($"t{i}", $"Part {i}")).ToArray();
        Assert.Equal(201, (await rig.Service.CreateAsync("e1", Save("Long read", "Recap", thirtyParts), NoFiles, Admin)).Code);
    }

    [Fact]
    public async Task Parts_keep_the_order_the_organiser_gave_them()
    {
        var rig = new Rig();
        await rig.Seed(Event());
        await rig.Service.CreateAsync("e1", Save("Order", "Announcement", Link("https://a.example", "l1"), Text("t1"), Link("https://b.example", "l2")), NoFiles, Admin);

        Assert.Equal(new[] { "l1", "t1", "l2" }, (await rig.Db().EventUpdates.SingleAsync()).Blocks.Select(b => b.Id));
    }

    // ── Categories ──────────────────────────────────────────────────────

    [Theory]
    [InlineData("schedule", "Schedule")]            // a standard one typed in other letters becomes the standard one
    [InlineData("  Recap ", "Recap")]
    [InlineData("Day 2 highlights", "Day 2 highlights")]   // or the organiser's own words
    public async Task A_category_is_a_standard_one_or_the_organisers_own_label(string typed, string stored)
    {
        var rig = new Rig();
        await rig.Seed(Event());
        await rig.Service.CreateAsync("e1", Save("T", typed, Text()), NoFiles, Admin);
        Assert.Equal(stored, (await rig.Db().EventUpdates.SingleAsync()).Category);
    }

    [Fact]
    public async Task A_category_is_required_and_cannot_be_long()
    {
        var rig = new Rig();
        await rig.Seed(Event());
        Assert.Equal(400, (await rig.Service.CreateAsync("e1", Save("T", "  ", Text()), NoFiles, Admin)).Code);
        Assert.Equal(400, (await rig.Service.CreateAsync("e1", Save("T", new string('x', 41), Text()), NoFiles, Admin)).Code);
    }

    // ── Refusing what shouldn't be saved ────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task An_update_needs_a_title(string title)
    {
        var rig = new Rig();
        await rig.Seed(Event());
        Assert.Equal("Give the update a title.", (await rig.Service.CreateAsync("e1", Save(title, "Recap", Text()), NoFiles, Admin)).Message);
    }

    [Fact]
    public async Task An_update_with_nothing_in_it_is_refused()
    {
        var rig = new Rig();
        await rig.Seed(Event());
        Assert.Equal(400, (await rig.Service.CreateAsync("e1", Save("Empty", "Recap"), NoFiles, Admin)).Code);
        Assert.Equal(400, (await rig.Service.CreateAsync("e1", Save("Blank text", "Recap", Text(text: "  ")), NoFiles, Admin)).Code);
        Assert.Equal(400, (await rig.Service.CreateAsync("e1", Save("No photos", "Recap", Images()), NoFiles, Admin)).Code);
        Assert.Equal(400, (await rig.Service.CreateAsync("e1", Save("No files", "Recap", Docs()), NoFiles, Admin)).Code);
        Assert.Empty(rig.Db().EventUpdates);
    }

    [Theory]
    [InlineData("https://vimeo.com/123456")]
    [InlineData("not a link")]
    [InlineData("javascript:alert(1)")]
    [InlineData("https://youtube.com.evil.example/watch?v=dQw4w9WgXcQ")]
    public async Task A_video_must_be_a_real_youtube_address(string url)
    {
        var rig = new Rig();
        await rig.Seed(Event());
        Assert.Equal(400, (await rig.Service.CreateAsync("e1", Save("V", "Recap", Video(url)), NoFiles, Admin)).Code);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    [InlineData("ftp://files.example/x")]
    [InlineData("example.com/no-scheme")]
    public async Task A_link_can_only_be_a_web_address_so_it_can_never_run_code(string url)
    {
        var rig = new Rig();
        await rig.Seed(Event());
        Assert.Equal(400, (await rig.Service.CreateAsync("e1", Save("L", "Recap", Link(url)), NoFiles, Admin)).Code);
    }

    [Fact]
    public async Task Photos_must_be_photos_and_files_must_be_documents()
    {
        var rig = new Rig();
        await rig.Seed(Event());

        var notAPhoto = await rig.Service.CreateAsync("e1", Save("T", "Recap", Images()), Files("i1", File("slides.pdf")), Admin);
        Assert.Equal(400, notAPhoto.Code);
        Assert.Contains("isn't a photo", notAPhoto.Message);

        var notADoc = await rig.Service.CreateAsync("e1", Save("T", "Recap", Docs()), Files("f1", File("run.exe")), Admin);
        Assert.Equal(400, notADoc.Code);
        Assert.Contains("can't be attached", notADoc.Message);

        Assert.Equal(0, rig.Uploaded);   // nothing is uploaded when the form is rejected
    }

    [Fact]
    public async Task Oversized_files_are_refused_before_anything_is_uploaded()
    {
        var rig = new Rig();
        await rig.Seed(Event());
        var tooBigPhoto = File("huge.jpg", (int)EventUpdateRules.MaxImageBytes + 1);
        var response = await rig.Service.CreateAsync("e1", Save("T", "Recap", Images(), Docs()), new Dictionary<string, List<IFormFile>> { ["i1"] = [File("ok.jpg"), tooBigPhoto] }, Admin);

        Assert.Equal(400, response.Code);
        Assert.Contains("bigger than", response.Message);
        Assert.Equal(0, rig.Uploaded);
    }

    [Fact]
    public async Task A_file_sent_for_a_part_that_takes_no_files_is_refused()
    {
        var rig = new Rig();
        await rig.Seed(Event());
        Assert.Equal(400, (await rig.Service.CreateAsync("e1", Save("T", "Recap", Text()), Files("t1", File("a.jpg")), Admin)).Code);
        Assert.Equal(400, (await rig.Service.CreateAsync("e1", Save("T", "Recap", Text()), Files("ghost", File("a.jpg")), Admin)).Code);
    }

    [Fact]
    public async Task Two_parts_cannot_share_an_id_and_the_part_count_has_a_ceiling()
    {
        var rig = new Rig();
        await rig.Seed(Event());
        Assert.Equal(400, (await rig.Service.CreateAsync("e1", Save("T", "Recap", Text("a"), Text("a")), NoFiles, Admin)).Code);
        var tooMany = Enumerable.Range(0, EventUpdateRules.MaxBlocks + 1).Select(i => Text($"t{i}")).ToArray();
        Assert.Equal(400, (await rig.Service.CreateAsync("e1", Save("T", "Recap", tooMany), NoFiles, Admin)).Code);
    }

    // ── Editing ─────────────────────────────────────────────────────────

    private async Task<(Rig rig, EventUpdateDto update)> Posted()
    {
        var rig = new Rig();
        await rig.Seed(Event());
        var created = await rig.Service.CreateAsync("e1", Save("Photos", "Photos & video", Images(), Text()),
            Files("i1", File("a.jpg"), File("b.jpg")), Admin);
        return (rig, created.Data!);
    }

    [Fact]
    public async Task Editing_keeps_the_files_already_there_adds_new_ones_and_drops_the_ones_removed()
    {
        var (rig, update) = await Posted();
        var images = update.Blocks[0];
        var kept = new EventUpdateItem { Url = images.Items[0].Url, Name = images.Items[0].Name, SizeBytes = images.Items[0].SizeBytes };   // b.jpg is dropped
        var edited = Save("Photos, more", "Photos & video", new EventUpdateBlock { Id = "i1", Type = "Images", Items = [kept] }, Text());

        var response = await rig.Service.UpdateAsync("e1", update.Id, edited, Files("i1", File("c.jpg")), Admin);

        Assert.Equal(200, response.Code);
        var saved = await rig.Db().EventUpdates.SingleAsync();
        Assert.Equal(new[] { "a.jpg", "c.jpg" }, saved.Blocks[0].Items.Select(i => i.Name));
        Assert.Equal("Photos, more", saved.Title);
        Assert.NotNull(saved.UpdatedAt);
    }

    [Fact]
    public async Task Editing_cannot_smuggle_in_a_file_address_that_was_never_uploaded_here()
    {
        var (rig, update) = await Posted();
        var forged = new EventUpdateBlock { Id = "i1", Type = "Images", Items = [new EventUpdateItem { Url = "https://evil.example/x.jpg", Name = "x.jpg" }] };

        var response = await rig.Service.UpdateAsync("e1", update.Id, Save("Photos", "Recap", forged), NoFiles, Admin);

        Assert.Equal(400, response.Code);
        Assert.Equal(2, (await rig.Db().EventUpdates.SingleAsync()).Blocks[0].Items.Count);
    }

    [Fact]
    public async Task Editing_does_not_notify_anyone_again()
    {
        var (rig, update) = await Posted();
        await rig.Seed(Seat("m1"));
        var before = rig.NotificationsDropped;

        await rig.Service.UpdateAsync("e1", update.Id, Save("Photos", "Recap", Text("t1", "Fixed a typo.")), NoFiles, Admin);

        Assert.Equal(before, rig.NotificationsDropped);
    }

    [Fact]
    public async Task An_update_belonging_to_another_event_cannot_be_edited_through_this_one()
    {
        var (rig, update) = await Posted();
        await rig.Seed(Event("e2"));
        Assert.Equal(404, (await rig.Service.UpdateAsync("e2", update.Id, Save("T", "Recap", Text()), NoFiles, Admin)).Code);
        Assert.Equal(404, (await rig.Service.DeleteAsync("e2", update.Id, Admin)).Code);
        Assert.Equal(404, (await rig.Service.SetPinnedAsync("e2", update.Id, true, Admin)).Code);
    }

    // ── Telling the people who are going ────────────────────────────────

    [Fact]
    public async Task Posting_notifies_everyone_with_a_seat_and_nobody_else()
    {
        var rig = new Rig();
        await rig.Seed(Event(), Seat("m1"), Seat("m2"), Seat("m3", "Cancelled"), Seat("m4", "PendingPayment"), Seat("m5", "PaymentFailed"));

        await rig.Service.CreateAsync("e1", Save("Schedule", "Schedule", Text()), NoFiles, Admin);

        Assert.Equal(2, rig.NotificationsDropped);
    }

    [Fact]
    public async Task A_rejected_update_notifies_nobody()
    {
        var rig = new Rig();
        await rig.Seed(Event(), Seat("m1"));
        await rig.Service.CreateAsync("e1", Save("", "Schedule", Text()), NoFiles, Admin);
        Assert.Equal(0, rig.NotificationsDropped);
    }

    // ── Who may do this ─────────────────────────────────────────────────

    [Fact]
    public async Task A_scoped_admin_cannot_post_to_an_event_outside_their_scope()
    {
        var rig = new Rig();
        await rig.Seed(Event(years: [2010]));
        var scoped = new AuthData { Id = "scoped", Role = "ScopedAdmin", YearGroups = [2020] };

        Assert.Equal(404, (await rig.Service.CreateAsync("e1", Save("T", "Recap", Text()), NoFiles, scoped)).Code);
        Assert.Equal(404, (await rig.Service.ListAsync("e1", scoped)).Code);
    }

    [Fact]
    public async Task An_unknown_event_is_404()
    {
        var rig = new Rig();
        Assert.Equal(404, (await rig.Service.CreateAsync("nope", Save("T", "Recap", Text()), NoFiles, Admin)).Code);
    }

    // ── Pinning, deleting, listing ──────────────────────────────────────

    [Fact]
    public async Task Pinned_updates_come_first_then_the_newest()
    {
        var rig = new Rig();
        await rig.Seed(Event(),
            new EventUpdate { Id = "old", EventId = "e1", Title = "Old", Category = "Recap", Blocks = [Text()], CreatedAt = DateTime.UtcNow.AddDays(-3) },
            new EventUpdate { Id = "new", EventId = "e1", Title = "New", Category = "Recap", Blocks = [Text()], CreatedAt = DateTime.UtcNow },
            new EventUpdate { Id = "pinned", EventId = "e1", Title = "Pinned", Category = "Recap", Blocks = [Text()], CreatedAt = DateTime.UtcNow.AddDays(-9), IsPinned = true });

        var list = (await rig.Service.ListAsync("e1", Admin)).Data!;

        Assert.Equal(new[] { "pinned", "new", "old" }, list.Select(u => u.Id));
    }

    [Fact]
    public async Task An_update_can_be_pinned_unpinned_and_deleted()
    {
        var (rig, update) = await Posted();

        Assert.True((await rig.Service.SetPinnedAsync("e1", update.Id, true, Admin)).Data!.IsPinned);
        Assert.False((await rig.Service.SetPinnedAsync("e1", update.Id, false, Admin)).Data!.IsPinned);
        Assert.Equal(200, (await rig.Service.DeleteAsync("e1", update.Id, Admin)).Code);
        Assert.Empty(rig.Db().EventUpdates);
    }

    [Fact]
    public async Task Deleting_an_event_deletes_its_updates_and_only_its_own()
    {
        var rig = new Rig();
        await rig.Seed(Event("e1"), Event("e2"),
            new EventUpdate { Id = "u1", EventId = "e1", Title = "A", Category = "Recap", Blocks = [Text()] },
            new EventUpdate { Id = "u2", EventId = "e2", Title = "B", Category = "Recap", Blocks = [Text()] });
        var db = rig.Db();
        var events = new EventService(new AlumniPgRepository<AlumniEvent>(db), new AlumniPgRepository<EventRsvp>(db), new AlumniPgRepository<ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member>(db),
            new AlumniPgRepository<ReservEase.Alumni.PostgresDb.Sdk.Entities.Institution>(db), new AlumniPgRepository<EventUpdate>(db), Mock.Of<IStorageService>(), Mock.Of<ITemporalClientProvider>(t => t.IsAvailable == false),
            TestDb.Tenant(Tenant, "umat"), Mock.Of<ReservEase.Alumni.Redis.Sdk.Services.IRedisService<ReservEase.Alumni.Common.Sdk.Options.PublicContentCacheConfig>>(), Mock.Of<IInstitutionAuditLogService>(), NullLogger<EventService>.Instance);

        Assert.Equal(200, (await events.DeleteEventAsync("e1", Admin)).Code);

        Assert.Equal(new[] { "u2" }, rig.Db().EventUpdates.Select(u => u.Id).ToArray());
    }

    // ── The rules themselves ────────────────────────────────────────────

    [Theory]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    [InlineData("https://youtu.be/dQw4w9WgXcQ?t=42", "dQw4w9WgXcQ")]
    [InlineData("https://m.youtube.com/watch?v=dQw4w9WgXcQ&list=x", "dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/embed/dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/shorts/dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/watch?v=short", null)]
    [InlineData("https://vimeo.com/dQw4w9WgXcQ", null)]
    [InlineData(null, null)]
    public void YouTube_addresses_in_their_usual_forms_give_the_same_video_id(string? url, string? expected)
        => Assert.Equal(expected, EventUpdateRules.YouTubeId(url));
}
