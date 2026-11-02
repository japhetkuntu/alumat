using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Common.Sdk.Options;
using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.Institution.Api.Services.Implementations;
using ReservEase.Alumni.Institution.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.DbContexts;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Website;
using ReservEase.Alumni.Redis.Sdk.Services;
using ReservEase.Alumni.TestKit;
using InstitutionEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Institution;

namespace ReservEase.Alumni.Institution.Api.Tests;

/// <summary>Editing a website draft, publishing it, taking it down, and putting an earlier version back.</summary>
public class WebsiteServiceTests
{
    private const string Tenant = "inst-1";
    private static readonly AuthData Admin = new() { Id = "admin1", FirstName = "Ama", LastName = "Mensah", Role = "SuperAdmin" };

    private sealed class Rig
    {
        public string DbName { get; } = TestDb.NewName();
        public Mock<IRedisService<PublicContentCacheConfig>> Cache { get; } = new();
        public Mock<IInstitutionAuditLogService> Audit { get; } = new();
        public WebsiteService Service { get; private set; } = null!;

        public Rig(params string[] disabled)
        {
            using var seed = TestDb.Create(DbName, Tenant);
            seed.Institutions.Add(new InstitutionEntity { Id = Tenant, Slug = "greenfield", Name = "Greenfield", PortalName = "Greenfield Alumni", PrimaryColorHex = "#0e6b5f", DisabledFeatures = disabled.ToList() });
            seed.SaveChanges();
            Fresh();
        }

        public WebsiteService Fresh()
        {
            var db = TestDb.Create(DbName, Tenant);
            return Service = new WebsiteService(
                new AlumniPgRepository<InstitutionWebsite>(db), new AlumniPgRepository<InstitutionWebsiteVersion>(db), new AlumniPgRepository<InstitutionEntity>(db),
                TestDb.Tenant(Tenant, "greenfield"), Cache.Object, Audit.Object, NullLogger<WebsiteService>.Instance);
        }

        public AlumniDbContext Db() => TestDb.Create(DbName, Tenant);
    }

    private static Newtonsoft.Json.Linq.JObject Doc(params object[] sections) => Newtonsoft.Json.Linq.JObject.Parse(JsonSerializer.Serialize(new { sections }, WebsiteDocument.Json));
    private static object Sec(string id, string type = "text", bool hidden = false, string heading = "Hello")
        => new { id, type, variant = "", bg = "plain", space = "n", hidden, count = 3, menu = new { on = false, label = "" }, props = new { heading }, imgs = new { }, links = new { } };

    private static async Task<WebsiteStatusDto> Save(Rig rig, Newtonsoft.Json.Linq.JObject doc, int baseRevision)
    {
        var r = await rig.Service.SaveDraftAsync(new SaveWebsiteDraftRequest { Document = doc, BaseRevision = baseRevision }, Admin);
        Assert.Equal(200, r.Code);
        return r.Data!;
    }

    // ── Opening the editor ──────────────────────────────────────────────

    [Fact]
    public async Task A_new_institution_opens_the_editor_with_no_draft_and_its_own_branding()
    {
        var r = await new Rig().Service.GetAsync();
        Assert.Equal(200, r.Code);
        Assert.Null(r.Data!.Draft);
        Assert.False(r.Data.Status.IsLive);
        Assert.Equal("Greenfield Alumni", r.Data.Brand.Name);
        Assert.Equal("#0e6b5f", r.Data.Brand.PrimaryColor);
    }

    // ── Saving the draft ────────────────────────────────────────────────

    [Fact]
    public async Task Saving_creates_the_draft_and_each_later_save_moves_the_revision_on()
    {
        var rig = new Rig();
        var s1 = await Save(rig, Doc(Sec("a")), 0);
        Assert.Equal(1, s1.DraftRevision);
        Assert.True(s1.HasUnpublishedChanges);
        var s2 = await Save(rig.Fresh() is { } ? rig : rig, Doc(Sec("a"), Sec("b")), 1);
        Assert.Equal(2, s2.DraftRevision);

        var opened = (await rig.Fresh().GetAsync()).Data!;
        Assert.Equal(2, Newtonsoft.Json.Linq.JObject.Parse(opened.Draft!.ToString())["sections"]!.Count());
    }

    [Fact]
    public async Task A_save_from_an_out_of_date_window_is_refused_instead_of_overwriting()
    {
        var rig = new Rig();
        await Save(rig, Doc(Sec("a")), 0);
        await Save(rig.Fresh() is { } ? rig : rig, Doc(Sec("a"), Sec("b")), 1);

        var stale = await rig.Fresh().SaveDraftAsync(new SaveWebsiteDraftRequest { Document = Doc(Sec("a"), Sec("c")), BaseRevision = 1 }, Admin);
        Assert.Equal(409, stale.Code);
        using var db = rig.Db();
        Assert.Contains("\"b\"", (await db.InstitutionWebsites.SingleAsync()).DraftJson);
    }

    [Fact]
    public async Task A_first_save_that_claims_an_existing_revision_is_refused()
        => Assert.Equal(409, (await new Rig().Service.SaveDraftAsync(new SaveWebsiteDraftRequest { Document = Doc(Sec("a")), BaseRevision = 5 }, Admin)).Code);

    [Fact]
    public async Task A_page_with_something_unsafe_in_it_is_not_stored_at_all()
    {
        var rig = new Rig();
        var r = await rig.Service.SaveDraftAsync(new SaveWebsiteDraftRequest { Document = Doc(Sec("a", type: "iframe")), BaseRevision = 0 }, Admin);
        Assert.Equal(400, r.Code);
        using var db = rig.Db();
        Assert.Empty(await db.InstitutionWebsites.ToListAsync());
    }

    [Fact]
    public async Task What_is_stored_is_the_cleaned_page_not_what_was_sent()
    {
        var rig = new Rig();
        await Save(rig, Doc(Sec("a", heading: "<script>alert(1)</script>Welcome")), 0);
        using var db = rig.Db();
        Assert.DoesNotContain("<script", (await db.InstitutionWebsites.SingleAsync()).DraftJson, StringComparison.OrdinalIgnoreCase);
    }

    // ── Publishing ──────────────────────────────────────────────────────

    [Fact]
    public async Task Publishing_makes_the_page_live_records_a_version_clears_the_cache_and_is_logged()
    {
        var rig = new Rig();
        await Save(rig, Doc(Sec("a")), 0);
        var r = await rig.Fresh().PublishAsync(new PublishWebsiteRequest { Revision = 1 }, Admin);

        Assert.Equal(200, r.Code);
        Assert.True(r.Data!.IsLive);
        Assert.False(r.Data.HasUnpublishedChanges);
        using var db = rig.Db();
        var site = await db.InstitutionWebsites.SingleAsync();
        Assert.NotNull(site.PublishedJson);
        Assert.Equal("Ama Mensah", site.PublishedBy);
        Assert.Single(await db.InstitutionWebsiteVersions.ToListAsync());
        rig.Cache.Verify(c => c.RemoveAsync(PublicContentCacheKeys.Site(Tenant)), Times.Once);
        rig.Audit.Verify(a => a.LogAsync(Admin, "Website Published", It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task Editing_after_publishing_shows_unpublished_changes_and_leaves_visitors_on_the_published_page()
    {
        var rig = new Rig();
        await Save(rig, Doc(Sec("a", heading: "First")), 0);
        await rig.Fresh().PublishAsync(new PublishWebsiteRequest { Revision = 1 }, Admin);
        var edited = await Save(rig.Fresh() is { } ? rig : rig, Doc(Sec("a", heading: "Second")), 1);
        Assert.True(edited.HasUnpublishedChanges);
        Assert.True(edited.IsLive);
        using var db = rig.Db();
        var site = await db.InstitutionWebsites.SingleAsync();
        Assert.Contains("First", site.PublishedJson);
        Assert.Contains("Second", site.DraftJson);
    }

    [Fact]
    public async Task Publishing_a_page_that_changed_since_it_was_opened_is_refused()
    {
        var rig = new Rig();
        await Save(rig, Doc(Sec("a")), 0);
        Assert.Equal(409, (await rig.Fresh().PublishAsync(new PublishWebsiteRequest { Revision = 7 }, Admin)).Code);
    }

    [Fact]
    public async Task Nothing_to_publish_or_only_hidden_sections_is_a_clear_refusal()
    {
        var rig = new Rig();
        Assert.Equal(404, (await rig.Service.PublishAsync(new PublishWebsiteRequest { Revision = 1 }, Admin)).Code);
        await Save(rig, Doc(Sec("a", hidden: true)), 0);
        var r = await rig.Fresh().PublishAsync(new PublishWebsiteRequest { Revision = 1 }, Admin);
        Assert.Equal(400, r.Code);
        Assert.Contains("section", r.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_feature_the_platform_switched_off_is_hidden_in_what_goes_live()
    {
        var rig = new Rig("Services");
        await Save(rig, Doc(Sec("a"), Sec("s", type: "services")), 0);
        await rig.Fresh().PublishAsync(new PublishWebsiteRequest { Revision = 1 }, Admin);
        using var db = rig.Db();
        var published = JsonSerializer.Deserialize<WebsiteDocument>((await db.InstitutionWebsites.SingleAsync()).PublishedJson!, WebsiteDocument.Json)!;
        Assert.True(published.Sections.Single(s => s.Id == "s").Hidden);
        Assert.False(published.Sections.Single(s => s.Id == "a").Hidden);
    }

    [Fact]
    public async Task Only_the_latest_versions_are_kept()
    {
        var rig = new Rig();
        await Save(rig, Doc(Sec("a")), 0);
        await rig.Fresh().PublishAsync(new PublishWebsiteRequest { Revision = 1 }, Admin);
        for (var i = 2; i <= WebsiteService.VersionsKept + 3; i++)
        {
            await Save(rig.Fresh() is { } ? rig : rig, Doc(Sec("a", heading: $"v{i}")), i - 1);
            await rig.Fresh().PublishAsync(new PublishWebsiteRequest { Revision = i }, Admin);
        }
        using var db = rig.Db();
        Assert.Equal(WebsiteService.VersionsKept, await db.InstitutionWebsiteVersions.CountAsync());
        Assert.Equal(WebsiteService.VersionsKept, (await rig.Fresh().GetVersionsAsync()).Data!.Count);
    }

    // ── Taking it down and putting a version back ───────────────────────

    [Fact]
    public async Task Unpublishing_returns_visitors_to_the_standard_page_but_keeps_everything()
    {
        var rig = new Rig();
        await Save(rig, Doc(Sec("a")), 0);
        await rig.Fresh().PublishAsync(new PublishWebsiteRequest { Revision = 1 }, Admin);
        rig.Cache.Invocations.Clear();

        var r = await rig.Fresh().UnpublishAsync(Admin);
        Assert.False(r.Data!.IsLive);
        using var db = rig.Db();
        var site = await db.InstitutionWebsites.SingleAsync();
        Assert.NotNull(site.PublishedJson);
        Assert.NotEqual("{}", site.DraftJson);
        rig.Cache.Verify(c => c.RemoveAsync(PublicContentCacheKeys.Site(Tenant)), Times.Once);
    }

    [Fact]
    public async Task Putting_an_earlier_version_back_only_changes_the_draft()
    {
        var rig = new Rig();
        await Save(rig, Doc(Sec("a", heading: "Original")), 0);
        await rig.Fresh().PublishAsync(new PublishWebsiteRequest { Revision = 1 }, Admin);
        await Save(rig.Fresh() is { } ? rig : rig, Doc(Sec("a", heading: "Changed")), 1);
        await rig.Fresh().PublishAsync(new PublishWebsiteRequest { Revision = 2 }, Admin);

        var versions = (await rig.Fresh().GetVersionsAsync()).Data!;
        var first = versions.Single(v => v.Revision == 1);
        var restored = await rig.Fresh().RestoreVersionAsync(first.Id, Admin);
        Assert.Equal(200, restored.Code);
        Assert.True(restored.Data!.HasUnpublishedChanges);

        using var db = rig.Db();
        var site = await db.InstitutionWebsites.SingleAsync();
        Assert.Contains("Original", site.DraftJson);
        Assert.Contains("Changed", site.PublishedJson);   // visitors still see the last publish
    }

    [Fact]
    public async Task An_unknown_version_is_not_found()
        => Assert.Equal(404, (await new Rig().Service.RestoreVersionAsync("nope", Admin)).Code);

    // ── The wire format: the API reads and writes JSON with Newtonsoft, which does not understand System.Text.Json types ──

    private static readonly Newtonsoft.Json.JsonSerializerSettings Wire = new()
    {
        ContractResolver = new Newtonsoft.Json.Serialization.DefaultContractResolver { NamingStrategy = new Newtonsoft.Json.Serialization.CamelCaseNamingStrategy { ProcessDictionaryKeys = false } },
        NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore,
    };

    [Fact]
    public void The_page_sent_by_the_editor_arrives_whole_with_its_own_keys_untouched()
    {
        var body = "{\"document\":{\"sections\":[{\"id\":\"a\",\"props\":{\"Heading\":\"Hi\"}}]},\"baseRevision\":3}";
        var request = Newtonsoft.Json.JsonConvert.DeserializeObject<SaveWebsiteDraftRequest>(body, Wire)!;
        Assert.Equal(3, request.BaseRevision);
        Assert.Contains("\"Heading\":\"Hi\"", request.Document!.ToString(Newtonsoft.Json.Formatting.None));
    }

    [Fact]
    public void The_draft_is_written_back_as_the_page_itself_not_as_an_object_wrapper()
    {
        var dto = new WebsiteEditorDto(new WebsiteStatusDto(false, null, null, 1, 0, true), new Newtonsoft.Json.Linq.JRaw("{\"sections\":[]}"), new WebsiteBrandDto("G", null, "#0e6b5f", null, null, "g"), []);
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(dto, Wire);
        Assert.Contains("\"draft\":{\"sections\":[]}", json);
    }
}
