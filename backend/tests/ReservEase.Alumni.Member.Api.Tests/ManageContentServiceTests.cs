using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ReservEase.Alumni.Member.Api.Models;
using ReservEase.Alumni.Member.Api.Services.Implementations;
using ReservEase.Alumni.PostgresDb.Sdk.DbContexts;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using ReservEase.Alumni.Temporal.Sdk;
using ReservEase.Alumni.TestKit;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.Member.Api.Tests;

/// <summary>News, jobs and shared links a leader makes for their own group: only with the matching privilege in that exact group, only theirs to change.</summary>
public class ManageContentServiceTests
{
    private const string Tenant = "inst-1";
    private const string Other = "inst-2";
    private const string News = LeadershipPrivileges.PostNews;
    private const string Jobs = LeadershipPrivileges.PostJobs;
    private const string Links = LeadershipPrivileges.ShareResources;

    private sealed class Rig
    {
        public string DbName { get; } = TestDb.NewName();
        public CapturingLogger<ManageContentService> Log { get; } = new();
        public FakeStorage Storage { get; } = new();

        public ManageContentService Service(string tenant = Tenant)
        {
            var db = TestDb.Create(DbName, tenant);
            var access = new LeadershipAccess(new AlumniPgRepository<LeadershipPost>(db), new AlumniPgRepository<LeadershipRole>(db));
            var temporal = new Mock<ITemporalClientProvider>();
            temporal.SetupGet(t => t.IsAvailable).Returns(false);
            return new ManageContentService(new GroupGuard(access, new AlumniPgRepository<Batch>(db), new AlumniPgRepository<Community>(db)), Storage.Media(TestDb.Tenant(tenant, "umat")),
                new AlumniPgRepository<MemberEntity>(db), new AlumniPgRepository<NewsPost>(db), new AlumniPgRepository<Job>(db), new AlumniPgRepository<Resource>(db),
                new AlumniPgRepository<InstitutionAuditLogEntry>(db), TestDb.Tenant(tenant, "umat"), temporal.Object, Log);
        }

        public AlumniDbContext Db(string tenant = Tenant) => TestDb.Create(DbName, tenant);
        public int Queued(string kind) => Log.Entries.Count(e => e.Message.Contains($"dropped notification {kind} "));

        private static MemberEntity Person(string id, string tenant = Tenant) => new() { Id = id, InstitutionId = tenant, FirstName = id, LastName = "Test", Email = $"{id}@x.test", Status = "Active" };

        /// <summary>ama: news for 2015. bo: jobs for 2015. cat: links for 2015. kojo: news and jobs for chapter c1. esi: all three, institution-wide. yaa: a title only. xama: patron of institution 2.</summary>
        public async Task Seed()
        {
            using var db = Db();
            db.AddRange(
                new Batch { Id = "b2015", InstitutionId = Tenant, Name = "Class of 2015", Year = 2015 }, new Batch { Id = "b2016", InstitutionId = Tenant, Name = "Class of 2016", Year = 2016 },
                new Community { Id = "c1", InstitutionId = Tenant, Name = "Accra Chapter" }, new Community { Id = "c2", InstitutionId = Tenant, Name = "Kumasi Chapter" },
                Person("ama"), Person("bo"), Person("cat"), Person("kojo"), Person("esi"), Person("yaa"), Person("ben"),
                new LeadershipRole { Id = "news", InstitutionId = Tenant, Name = "Editor", Privileges = [News] },
                new LeadershipRole { Id = "jobs", InstitutionId = Tenant, Name = "Careers", Privileges = [Jobs] },
                new LeadershipRole { Id = "links", InstitutionId = Tenant, Name = "Librarian", Privileges = [Links] },
                new LeadershipRole { Id = "both", InstitutionId = Tenant, Name = "Communicator", Privileges = [News, Jobs] },
                new LeadershipRole { Id = "all", InstitutionId = Tenant, Name = "Patron", Privileges = [News, Jobs, Links] },
                new LeadershipRole { Id = "title", InstitutionId = Tenant, Name = "Honorary", Privileges = [] },
                new LeadershipPost { InstitutionId = Tenant, MemberId = "ama", RoleId = "news", ScopeType = "Batch", ScopeId = "b2015" },
                new LeadershipPost { InstitutionId = Tenant, MemberId = "bo", RoleId = "jobs", ScopeType = "Batch", ScopeId = "b2015" },
                new LeadershipPost { InstitutionId = Tenant, MemberId = "cat", RoleId = "links", ScopeType = "Batch", ScopeId = "b2015" },
                new LeadershipPost { InstitutionId = Tenant, MemberId = "kojo", RoleId = "both", ScopeType = "Community", ScopeId = "c1" },
                new LeadershipPost { InstitutionId = Tenant, MemberId = "esi", RoleId = "all", ScopeType = "Institution", ScopeId = "" },
                new LeadershipPost { InstitutionId = Tenant, MemberId = "yaa", RoleId = "title", ScopeType = "Batch", ScopeId = "b2015" });
            await db.SaveChangesAsync();

            using var other = Db(Other);
            other.AddRange(Person("xama", Other), new Batch { Id = "bx", InstitutionId = Other, Name = "Theirs", Year = 2015 },
                new LeadershipRole { Id = "xall", InstitutionId = Other, Name = "Patron", Privileges = [News, Jobs, Links] },
                new LeadershipPost { InstitutionId = Other, MemberId = "xama", RoleId = "xall", ScopeType = "Institution", ScopeId = "" });
            await other.SaveChangesAsync();
        }
    }

    private static async Task<Rig> Seeded() { var r = new Rig(); await r.Seed(); return r; }

    private static SaveGroupNewsRequest Post(string title = "Reunion news", string content = "We meet on Saturday.", string? category = "Announcement") => new() { Title = title, Content = content, Category = category };
    private static SaveGroupJobRequest Job(string title = "Site engineer", string url = "https://jobs.example.com/1", DateTime? deadline = null, string type = "Full-time")
        => new() { Title = title, Company = "Acme", Location = "Accra", Type = type, Description = "Mining projects.", ApplyUrl = url, Deadline = deadline ?? DateTime.UtcNow.AddDays(30) };
    private static SaveGroupResourceRequest Link(string title = "Alumni handbook", string url = "https://docs.example.com/handbook") => new() { Title = title, ExternalUrl = url, Description = "Start here.", Category = "Guides" };

    // ── News ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_year_group_editor_posts_news_aimed_at_that_year_group_only_and_not_public()
    {
        var rig = await Seeded();

        var result = await rig.Service().CreateNewsAsync("ama", "Batch", "b2015", Post(content: "<p>Hello</p><script>alert(1)</script>"));

        Assert.Equal(201, result.Code);
        var n = await rig.Db().Set<NewsPost>().SingleAsync();
        Assert.Equal(("Reunion news", "Announcement", "Published", "ama", false), (n.Title, n.Category, n.Status, n.AuthorId, n.IsPublic));
        Assert.Equal([2015], n.YearGroups);
        Assert.Null(n.CommunityId);
        Assert.NotNull(n.PublishedAt);
        Assert.Equal("ama", n.Author!.Id);
        Assert.DoesNotContain("script", n.Content);
        Assert.Equal("News Posted", (await rig.Db().Set<InstitutionAuditLogEntry>().SingleAsync()).Action);
    }

    [Fact]
    public async Task A_community_leaders_news_belongs_to_that_community()
    {
        var rig = await Seeded();

        Assert.Equal(201, (await rig.Service().CreateNewsAsync("kojo", "Community", "c1", Post())).Code);

        var n = await rig.Db().Set<NewsPost>().SingleAsync();
        Assert.Equal("c1", n.CommunityId);
        Assert.Null(n.YearGroups);
    }

    [Theory]
    [InlineData("bo", "Batch", "b2015")]        // jobs, not news
    [InlineData("cat", "Batch", "b2015")]       // links, not news
    [InlineData("ama", "Batch", "b2016")]       // news, but another year group
    [InlineData("ama", "Community", "c1")]      // news, but a community
    [InlineData("kojo", "Community", "c2")]     // news, another community
    [InlineData("yaa", "Batch", "b2015")]       // a title with no privileges
    [InlineData("ben", "Batch", "b2015")]       // an ordinary member
    [InlineData("xama", "Batch", "b2015")]      // a patron of another institution
    public async Task News_cannot_be_posted_or_listed_without_the_privilege_in_that_exact_group(string who, string type, string id)
    {
        var rig = await Seeded();

        Assert.Equal(403, (await rig.Service().CreateNewsAsync(who, type, id, Post())).Code);
        Assert.Equal(403, (await rig.Service().GetNewsAsync(who, type, id)).Code);
        Assert.Equal(0, await rig.Db().Set<NewsPost>().CountAsync());
    }

    [Fact]
    public async Task Bad_news_is_refused_and_nothing_is_saved()
    {
        var rig = await Seeded();

        Assert.Equal(400, (await rig.Service().CreateNewsAsync("ama", "Batch", "b2015", Post(title: "  "))).Code);
        Assert.Equal(400, (await rig.Service().CreateNewsAsync("ama", "Batch", "b2015", Post(title: new string('a', 201)))).Code);
        Assert.Equal(400, (await rig.Service().CreateNewsAsync("ama", "Batch", "b2015", Post(content: "   "))).Code);
        Assert.Equal(400, (await rig.Service().CreateNewsAsync("ama", "Batch", "b2015", Post(content: "<p></p>"))).Code);
        Assert.Equal(0, await rig.Db().Set<NewsPost>().CountAsync());
    }

    [Fact]
    public async Task An_unknown_category_falls_back_to_announcement()
    {
        var rig = await Seeded();

        await rig.Service().CreateNewsAsync("ama", "Batch", "b2015", Post(category: "Spam & Junk"));

        Assert.Equal("Announcement", (await rig.Db().Set<NewsPost>().SingleAsync()).Category);
    }

    [Fact]
    public async Task Only_the_leaders_own_news_can_be_edited_or_deleted_and_the_groups_news_is_visible()
    {
        var rig = await Seeded();
        using (var db = rig.Db()) { db.Add(new NewsPost { Id = "admins", InstitutionId = Tenant, Title = "From the admins", Content = "x", Category = "News", YearGroups = [2015], AuthorId = "admin1", Status = "Published", PublishedAt = DateTime.UtcNow }); await db.SaveChangesAsync(); }
        await rig.Service().CreateNewsAsync("esi", "Batch", "b2015", Post("Patron's note"));
        var mine = (await rig.Service().CreateNewsAsync("ama", "Batch", "b2015", Post("Ama's note"))).Data!;

        var seen = (await rig.Service().GetNewsAsync("ama", "Batch", "b2015")).Data!;

        Assert.Equal(["Ama's note", "From the admins", "Patron's note"], seen.Select(n => n.Title).Order().ToArray());
        Assert.Equal(["Ama's note"], seen.Where(n => n.CanEdit).Select(n => n.Title).ToArray());
        Assert.Equal(404, (await rig.Service().UpdateNewsAsync("ama", "Batch", "b2015", "admins", Post("Hijack"))).Code);
        Assert.Equal(404, (await rig.Service().DeleteNewsAsync("ama", "Batch", "b2015", "admins")).Code);
        Assert.Equal(200, (await rig.Service().UpdateNewsAsync("ama", "Batch", "b2015", mine.Id, Post("Ama's note, edited"))).Code);
        Assert.Equal(200, (await rig.Service().DeleteNewsAsync("ama", "Batch", "b2015", mine.Id)).Code);
        Assert.Equal(["From the admins", "Patron's note"], (await rig.Db().Set<NewsPost>().Select(n => n.Title).ToListAsync()).Order().ToArray());
    }

    [Fact]
    public async Task News_made_for_a_group_is_not_listed_in_or_changeable_through_another_group()
    {
        var rig = await Seeded();
        var made = (await rig.Service().CreateNewsAsync("esi", "Batch", "b2015", Post("2015 only"))).Data!;
        await rig.Service().CreateNewsAsync("esi", "Batch", "b2016", Post("2016 only"));
        await rig.Service().CreateNewsAsync("esi", "Community", "c1", Post("Chapter only"));

        Assert.Equal(["2016 only"], (await rig.Service().GetNewsAsync("esi", "Batch", "b2016")).Data!.Select(n => n.Title).ToArray());
        Assert.Equal(["Chapter only"], (await rig.Service().GetNewsAsync("esi", "Community", "c1")).Data!.Select(n => n.Title).ToArray());
        Assert.Equal(404, (await rig.Service().UpdateNewsAsync("esi", "Batch", "b2016", made.Id, Post("Moved"))).Code);
        Assert.Equal(404, (await rig.Service().DeleteNewsAsync("esi", "Community", "c1", made.Id)).Code);
        Assert.Equal(3, await rig.Db().Set<NewsPost>().CountAsync());
    }

    // ── Jobs ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_leader_shares_a_job_with_their_group_and_the_group_is_told()
    {
        var rig = await Seeded();

        var result = await rig.Service().CreateJobAsync("bo", "Batch", "b2015", Job());

        Assert.Equal(201, result.Code);
        var j = await rig.Db().Set<Job>().SingleAsync();
        Assert.Equal(("Site engineer", "Acme", "Active", "bo", false), (j.Title, j.Company, j.Status, j.PostedBy, j.IsPublic));
        Assert.Equal([2015], j.YearGroups);
        Assert.Null(j.CommunityId);
        Assert.Equal(1, rig.Queued("JobAlert"));
        Assert.Equal("Job Posted", (await rig.Db().Set<InstitutionAuditLogEntry>().SingleAsync()).Action);
    }

    [Theory]
    [InlineData("ama", "Batch", "b2015")]       // news, not jobs
    [InlineData("bo", "Batch", "b2016")]
    [InlineData("bo", "Community", "c1")]
    [InlineData("cat", "Batch", "b2015")]
    [InlineData("yaa", "Batch", "b2015")]
    [InlineData("ben", "Community", "c1")]
    [InlineData("xama", "Batch", "b2015")]
    public async Task A_job_cannot_be_shared_or_listed_without_the_privilege_in_that_exact_group(string who, string type, string id)
    {
        var rig = await Seeded();

        Assert.Equal(403, (await rig.Service().CreateJobAsync(who, type, id, Job())).Code);
        Assert.Equal(403, (await rig.Service().GetJobsAsync(who, type, id)).Code);
        Assert.Equal(0, await rig.Db().Set<Job>().CountAsync());
        Assert.Equal(0, rig.Queued("JobAlert"));
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("ftp://files.example.com/x")]
    [InlineData("not a link")]
    public async Task A_job_apply_link_must_be_a_web_address(string url)
    {
        var rig = await Seeded();

        Assert.Equal(400, (await rig.Service().CreateJobAsync("bo", "Batch", "b2015", Job(url: url))).Code);
        Assert.Equal(0, await rig.Db().Set<Job>().CountAsync());
    }

    [Fact]
    public async Task A_bad_job_is_refused()
    {
        var rig = await Seeded();

        Assert.Equal(400, (await rig.Service().CreateJobAsync("bo", "Batch", "b2015", Job(title: ""))).Code);
        Assert.Equal(400, (await rig.Service().CreateJobAsync("bo", "Batch", "b2015", Job(type: "Slavery"))).Code);
        Assert.Equal(400, (await rig.Service().CreateJobAsync("bo", "Batch", "b2015", Job(deadline: DateTime.UtcNow.AddDays(-2)))).Code);
        Assert.Equal(0, await rig.Db().Set<Job>().CountAsync());
        Assert.Equal(201, (await rig.Service().CreateJobAsync("bo", "Batch", "b2015", Job(url: ""))).Code);   // an apply link is optional
    }

    [Fact]
    public async Task Only_the_leaders_own_jobs_can_be_edited_or_closed_and_a_closed_job_is_final()
    {
        var rig = await Seeded();
        using (var db = rig.Db()) { db.Add(new Job { Id = "admins", InstitutionId = Tenant, Title = "Admin's job", Company = "X", Location = "Y", Type = "Contract", YearGroups = [2015], PostedBy = "admin1", Status = "Active" }); await db.SaveChangesAsync(); }
        var mine = (await rig.Service().CreateJobAsync("bo", "Batch", "b2015", Job())).Data!;

        Assert.Equal(404, (await rig.Service().UpdateJobAsync("bo", "Batch", "b2015", "admins", Job("Hijack"))).Code);
        Assert.Equal(404, (await rig.Service().CloseJobAsync("bo", "Batch", "b2015", "admins")).Code);
        Assert.Equal(200, (await rig.Service().UpdateJobAsync("bo", "Batch", "b2015", mine.Id, Job("Senior site engineer"))).Code);
        Assert.Equal(200, (await rig.Service().CloseJobAsync("bo", "Batch", "b2015", mine.Id)).Code);

        Assert.Equal(400, (await rig.Service().CloseJobAsync("bo", "Batch", "b2015", mine.Id)).Code);
        Assert.Equal(400, (await rig.Service().UpdateJobAsync("bo", "Batch", "b2015", mine.Id, Job("Reopened?"))).Code);
        var listed = (await rig.Service().GetJobsAsync("bo", "Batch", "b2015")).Data!;
        Assert.Equal(["Admin's job", "Senior site engineer"], listed.Select(j => j.Title).Order().ToArray());
        Assert.All(listed, j => Assert.False(j.CanEdit));   // the admin's is not hers, hers is closed
    }

    // ── Links ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_leader_shares_a_link_with_their_group()
    {
        var rig = await Seeded();

        var result = await rig.Service().CreateResourceAsync("cat", "Batch", "b2015", Link());

        Assert.Equal(201, result.Code);
        var r = await rig.Db().Set<Resource>().SingleAsync();
        Assert.Equal(("Alumni handbook", "Link", "https://docs.example.com/handbook", "Guides", "cat"), (r.Title, r.Type, r.ExternalUrl, r.Category, r.UploadedBy));
        Assert.Equal([2015], r.YearGroups);
        Assert.Null(r.FileUrl);
    }

    [Theory]
    [InlineData("ama", "Batch", "b2015")]
    [InlineData("bo", "Batch", "b2015")]
    [InlineData("cat", "Batch", "b2016")]
    [InlineData("cat", "Community", "c1")]
    [InlineData("yaa", "Batch", "b2015")]
    [InlineData("xama", "Batch", "b2015")]
    public async Task A_link_cannot_be_shared_or_listed_without_the_privilege_in_that_exact_group(string who, string type, string id)
    {
        var rig = await Seeded();

        Assert.Equal(403, (await rig.Service().CreateResourceAsync(who, type, id, Link())).Code);
        Assert.Equal(403, (await rig.Service().GetResourcesAsync(who, type, id)).Code);
        Assert.Equal(0, await rig.Db().Set<Resource>().CountAsync());
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,<script>")]
    [InlineData("")]
    public async Task A_shared_link_must_be_a_web_address(string url)
    {
        var rig = await Seeded();

        Assert.Equal(400, (await rig.Service().CreateResourceAsync("cat", "Batch", "b2015", Link(url: url))).Code);
        Assert.Equal(0, await rig.Db().Set<Resource>().CountAsync());
    }

    [Fact]
    public async Task Only_links_the_leader_shared_can_be_changed_never_the_administrators_files()
    {
        var rig = await Seeded();
        using (var db = rig.Db()) { db.Add(new Resource { Id = "file", InstitutionId = Tenant, Title = "Admin file", Type = "File", FileUrl = "https://cdn/x.pdf", YearGroups = [2015], UploadedBy = "cat", Category = "General" }); await db.SaveChangesAsync(); }
        var mine = (await rig.Service().CreateResourceAsync("cat", "Batch", "b2015", Link())).Data!;

        var listed = (await rig.Service().GetResourcesAsync("cat", "Batch", "b2015")).Data!;
        Assert.Equal(["Alumni handbook"], listed.Where(r => r.CanEdit).Select(r => r.Title).ToArray());
        Assert.Equal(404, (await rig.Service().UpdateResourceAsync("cat", "Batch", "b2015", "file", Link("Hijack"))).Code);   // even a file under her own id is not a link
        Assert.Equal(404, (await rig.Service().DeleteResourceAsync("cat", "Batch", "b2015", "file")).Code);
        Assert.Equal(200, (await rig.Service().UpdateResourceAsync("cat", "Batch", "b2015", mine.Id, Link("Handbook 2"))).Code);
        Assert.Equal(200, (await rig.Service().DeleteResourceAsync("cat", "Batch", "b2015", mine.Id)).Code);
        Assert.Equal(["Admin file"], await rig.Db().Set<Resource>().Select(r => r.Title).ToListAsync());
    }

    // ── Across privileges and institutions ───────────────────────────────────

    [Fact]
    public async Task Each_privilege_opens_only_its_own_door()
    {
        var rig = await Seeded();

        // kojo holds news and jobs for chapter c1, not links.
        Assert.Equal(201, (await rig.Service().CreateNewsAsync("kojo", "Community", "c1", Post())).Code);
        Assert.Equal(201, (await rig.Service().CreateJobAsync("kojo", "Community", "c1", Job())).Code);
        Assert.Equal(403, (await rig.Service().CreateResourceAsync("kojo", "Community", "c1", Link())).Code);
        // esi holds all three, everywhere.
        Assert.Equal(201, (await rig.Service().CreateResourceAsync("esi", "Batch", "b2016", Link())).Code);
        Assert.Equal(201, (await rig.Service().CreateJobAsync("esi", "Community", "c2", Job())).Code);
    }

    [Fact]
    public async Task A_patron_of_another_institution_cannot_see_or_change_anything_here()
    {
        var rig = await Seeded();
        var made = (await rig.Service().CreateNewsAsync("ama", "Batch", "b2015", Post())).Data!;

        Assert.Equal(403, (await rig.Service().GetNewsAsync("xama", "Batch", "b2015")).Code);
        Assert.Equal(403, (await rig.Service().UpdateNewsAsync("xama", "Batch", "b2015", made.Id, Post("Hijack"))).Code);
        Assert.Equal(403, (await rig.Service().DeleteNewsAsync("xama", "Batch", "b2015", made.Id)).Code);
        Assert.Equal(404, (await rig.Service(Other).GetNewsAsync("xama", "Batch", "b2015")).Code);   // and from their side these groups do not exist
    }

    // ── Pictures and links ───────────────────────────────────────────────────

    [Fact]
    public async Task News_can_carry_pictures_and_videos_and_an_edit_keeps_only_what_it_had()
    {
        var rig = await Seeded();
        var req = Post(); req.Images = [Files.Png("a.png"), Files.Jpeg("b.jpg")]; req.YoutubeVideoUrls = ["https://youtu.be/abc"];

        var made = (await rig.Service().CreateNewsAsync("ama", "Batch", "b2015", req)).Data!;

        var n = await rig.Db().Set<NewsPost>().SingleAsync();
        Assert.Equal((2, 1), (n.ImageUrls!.Count, n.YoutubeVideoUrls!.Count));
        Assert.Equal(2, made.ImageUrls!.Count);
        var edit = Post("Edited"); edit.ExistingImageUrls = [made.ImageUrls[0], "https://evil.example/x.png"]; edit.YoutubeVideoUrls = null;
        Assert.Equal(200, (await rig.Service().UpdateNewsAsync("ama", "Batch", "b2015", made.Id, edit)).Code);
        n = await rig.Db().Set<NewsPost>().SingleAsync();
        Assert.Equal([made.ImageUrls[0]], n.ImageUrls);
        Assert.Null(n.YoutubeVideoUrls);
    }

    [Fact]
    public async Task Bad_news_media_refuses_the_post()
    {
        var rig = await Seeded();
        var bad = Post(); bad.Images = [Files.Disguised()];
        var video = Post(); video.YoutubeVideoUrls = ["https://vimeo.com/1"];

        Assert.Equal(400, (await rig.Service().CreateNewsAsync("ama", "Batch", "b2015", bad)).Code);
        Assert.Equal(400, (await rig.Service().CreateNewsAsync("ama", "Batch", "b2015", video)).Code);
        Assert.Equal(0, await rig.Db().Set<NewsPost>().CountAsync());
        Assert.Empty(rig.Storage.Uploaded);
    }

    [Fact]
    public async Task A_job_and_a_link_can_carry_a_banner_that_can_be_replaced_or_removed()
    {
        var rig = await Seeded();
        var job = Job(); job.BannerImage = Files.Png();
        var link = Link(); link.BannerImage = Files.Webp("c.webp");

        var j = (await rig.Service().CreateJobAsync("bo", "Batch", "b2015", job)).Data!;
        var l = (await rig.Service().CreateResourceAsync("cat", "Batch", "b2015", link)).Data!;

        Assert.StartsWith("https://cdn.test/", j.BannerImageUrl); Assert.StartsWith("https://cdn.test/", l.BannerImageUrl);
        var keep = Job("Renamed"); var drop = Link("Renamed"); drop.RemoveBanner = true;
        await rig.Service().UpdateJobAsync("bo", "Batch", "b2015", j.Id, keep);
        await rig.Service().UpdateResourceAsync("cat", "Batch", "b2015", l.Id, drop);
        Assert.Equal(j.BannerImageUrl, (await rig.Db().Set<Job>().SingleAsync()).BannerImageUrl);       // not mentioned: stays
        Assert.Null((await rig.Db().Set<Resource>().SingleAsync()).BannerImageUrl);                      // removed
    }

    [Fact]
    public async Task A_disguised_banner_refuses_a_job_or_a_link()
    {
        var rig = await Seeded();
        var job = Job(); job.BannerImage = Files.Disguised();
        var link = Link(); link.BannerImage = Files.Disguised();

        Assert.Equal(400, (await rig.Service().CreateJobAsync("bo", "Batch", "b2015", job)).Code);
        Assert.Equal(400, (await rig.Service().CreateResourceAsync("cat", "Batch", "b2015", link)).Code);
        Assert.Equal(0, await rig.Db().Set<Job>().CountAsync() + await rig.Db().Set<Resource>().CountAsync());
    }
}
