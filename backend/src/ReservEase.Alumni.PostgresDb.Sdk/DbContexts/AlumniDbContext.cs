using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Services;

namespace ReservEase.Alumni.PostgresDb.Sdk.DbContexts;

/// <summary>Converts any serialisable type to/from a PostgreSQL jsonb column.</summary>
internal sealed class JsonbConverter<T>(JsonSerializerOptions opts) : ValueConverter<T?, string?>(    v => v == null ? null : JsonSerializer.Serialize(v, opts),
    v => string.IsNullOrEmpty(v) ? null : JsonSerializer.Deserialize<T>(v!, opts))
    where T : class;

/// <summary>Leaves dictionary keys exactly as given — used to opt Dictionary&lt;string,T&gt; properties out of PropertyNamingPolicy's camel-casing, which otherwise applies to dictionary keys just as it does to class property names.</summary>
internal sealed class IdentityJsonNamingPolicy : JsonNamingPolicy
{
    public static readonly IdentityJsonNamingPolicy Instance = new();
    public override string ConvertName(string name) => name;
}

public class AlumniDbContext(DbContextOptions<AlumniDbContext> options, ICurrentTenantService currentTenant) : DbContext(options)
{
    public DbSet<Institution> Institutions => Set<Institution>();
    public DbSet<PlatformStaff> PlatformStaff => Set<PlatformStaff>();
    public DbSet<SupportCase> SupportCases => Set<SupportCase>();
    public DbSet<OnboardingLead> OnboardingLeads => Set<OnboardingLead>();
    public DbSet<PlatformNotification> PlatformNotifications => Set<PlatformNotification>();
    public DbSet<Announcement> Announcements => Set<Announcement>();
    public DbSet<AuditLogEntry> AuditLogEntries => Set<AuditLogEntry>();
    public DbSet<InstitutionStaff> InstitutionStaff => Set<InstitutionStaff>();
    public DbSet<Member> Members => Set<Member>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Batch> Batches => Set<Batch>();
    public DbSet<Community> Communities => Set<Community>();
    public DbSet<CommunityMembership> CommunityMemberships => Set<CommunityMembership>();
    public DbSet<Campaign> Campaigns => Set<Campaign>();
    public DbSet<CampaignUpdate> CampaignUpdates => Set<CampaignUpdate>();
    public DbSet<Contribution> Contributions => Set<Contribution>();
    public DbSet<AlumniEvent> Events => Set<AlumniEvent>();
    public DbSet<EventRsvp> EventRsvps => Set<EventRsvp>();
    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<NewsPost> NewsPosts => Set<NewsPost>();
    public DbSet<ForumCategory> ForumCategories => Set<ForumCategory>();
    public DbSet<ForumThread> ForumThreads => Set<ForumThread>();
    public DbSet<ForumPost> ForumPosts => Set<ForumPost>();
    public DbSet<MentorProfile> MentorProfiles => Set<MentorProfile>();
    public DbSet<MentorshipRequest> MentorshipRequests => Set<MentorshipRequest>();
    public DbSet<Resource> Resources => Set<Resource>();
    public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();
    public DbSet<MemberBadge> MemberBadges => Set<MemberBadge>();
    public DbSet<Spotlight> Spotlights => Set<Spotlight>();
    public DbSet<Referral> Referrals => Set<Referral>();
    public DbSet<ClassNote> ClassNotes => Set<ClassNote>();
    public DbSet<ClassNoteLike> ClassNoteLikes => Set<ClassNoteLike>();
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();
    public DbSet<AdminNotificationPreference> AdminNotificationPreferences => Set<AdminNotificationPreference>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<StoreProduct> StoreProducts => Set<StoreProduct>();
    public DbSet<StoreOrder> StoreOrders => Set<StoreOrder>();
    public DbSet<StoreProductVariant> StoreProductVariants => Set<StoreProductVariant>();
    public DbSet<PhotoAlbum> PhotoAlbums => Set<PhotoAlbum>();
    public DbSet<AlbumPhoto> AlbumPhotos => Set<AlbumPhoto>();
    public DbSet<BusinessListing> BusinessListings => Set<BusinessListing>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("alumni");

        // ── JSONB snapshot converters (no FK constraints anywhere) ──────────
        // PropertyNamingPolicy also camel-cases Dictionary<string,T> KEYS (not just
        // class property names) unless DictionaryKeyPolicy says otherwise — without
        // this, StoreProductVariant.Options / StoreOrderItem.VariantOptions keys like
        // "Size"/"Color" would silently collapse to "size"/"color" on save.
        var jsonOpts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, DictionaryKeyPolicy = IdentityJsonNamingPolicy.Instance };

            var jsonStringListComparer = new ValueComparer<List<string>>(
                (l1, l2) => (l1 == null && l2 == null) || (l1 != null && l2 != null && l1.SequenceEqual(l2)),
                l => l == null ? 0 : l.Aggregate(0, (a, v) => HashCode.Combine(a, v == null ? 0 : v.GetHashCode())),
                l => l == null ? null : new List<string>(l));

        modelBuilder.Entity<ForumThread>().Property(e => e.Category).HasColumnType("jsonb").HasConversion(new JsonbConverter<ForumCategorySnapshot>(jsonOpts));
        modelBuilder.Entity<ForumThread>().Property(e => e.Author)  .HasColumnType("jsonb").HasConversion(new JsonbConverter<MemberSnapshot>(jsonOpts));

        modelBuilder.Entity<ForumPost>().Property(e => e.Thread).HasColumnType("jsonb").HasConversion(new JsonbConverter<ForumThreadSnapshot>(jsonOpts));
        modelBuilder.Entity<ForumPost>().Property(e => e.Author).HasColumnType("jsonb").HasConversion(new JsonbConverter<MemberSnapshot>(jsonOpts));

        modelBuilder.Entity<MentorProfile>().Property(e => e.Member).HasColumnType("jsonb").HasConversion(new JsonbConverter<MemberSnapshot>(jsonOpts));

        // Contributions: persist member/campaign snapshots as jsonb for display.
        modelBuilder.Entity<Contribution>().Property(c => c.Member).HasColumnType("jsonb").HasConversion(new JsonbConverter<MemberSnapshot>(jsonOpts));
        modelBuilder.Entity<Contribution>().Property(c => c.Campaign).HasColumnType("jsonb").HasConversion(new JsonbConverter<CampaignSnapshot>(jsonOpts));

        // PaymentTransactions: persist member/campaign snapshots as jsonb.
        modelBuilder.Entity<PaymentTransaction>().Property(p => p.Member).HasColumnType("jsonb").HasConversion(new JsonbConverter<MemberSnapshot>(jsonOpts));
        modelBuilder.Entity<PaymentTransaction>().Property(p => p.Campaign).HasColumnType("jsonb").HasConversion(new JsonbConverter<CampaignSnapshot>(jsonOpts));

        modelBuilder.Entity<MentorshipRequest>().Property(e => e.MentorProfile).HasColumnType("jsonb").HasConversion(new JsonbConverter<MentorProfileSnapshot>(jsonOpts));
        modelBuilder.Entity<MentorshipRequest>().Property(e => e.Mentee)       .HasColumnType("jsonb").HasConversion(new JsonbConverter<MemberSnapshot>(jsonOpts));

        // EventRsvp currently keeps event/member snapshot in code only (for compatibility with older schema).
        // Incoming EF queries will not require these columns directly.
        modelBuilder.Entity<EventRsvp>().Ignore(r => r.Event);
        modelBuilder.Entity<EventRsvp>().Ignore(r => r.Member);

        // StoreOrder: member snapshot + line-item list, same jsonb pattern as Contribution.
        modelBuilder.Entity<StoreOrder>().Property(o => o.Member).HasColumnType("jsonb").HasConversion(new JsonbConverter<MemberSnapshot>(jsonOpts));
        var storeItemListComparer = new ValueComparer<List<StoreOrderItem>>(
            (l1, l2) => (l1 == null && l2 == null) || (l1 != null && l2 != null && l1.SequenceEqual(l2)),
            l => l == null ? 0 : l.Aggregate(0, (a, v) => HashCode.Combine(a, v == null ? 0 : v.GetHashCode())),
            l => l == null ? null : new List<StoreOrderItem>(l));
        modelBuilder.Entity<StoreOrder>().Property(o => o.Items).HasColumnType("jsonb")
            .HasConversion(new JsonbConverter<List<StoreOrderItem>>(jsonOpts)).Metadata.SetValueComparer(storeItemListComparer);

        var deliveryHistoryComparer = new ValueComparer<List<StoreOrderDeliveryEvent>>(
            (l1, l2) => (l1 == null && l2 == null) || (l1 != null && l2 != null && l1.SequenceEqual(l2)),
            l => l == null ? 0 : l.Aggregate(0, (a, v) => HashCode.Combine(a, v == null ? 0 : v.GetHashCode())),
            l => l == null ? null : new List<StoreOrderDeliveryEvent>(l));
        modelBuilder.Entity<StoreOrder>().Property(o => o.DeliveryStatusHistory).HasColumnType("jsonb")
            .HasConversion(new JsonbConverter<List<StoreOrderDeliveryEvent>>(jsonOpts)).Metadata.SetValueComparer(deliveryHistoryComparer);

        modelBuilder.Entity<StoreProduct>().Property(p => p.VariantOptionTypes).HasColumnType("jsonb")
            .HasConversion(new JsonbConverter<List<string>>(jsonOpts)).Metadata.SetValueComparer(jsonStringListComparer);

        modelBuilder.Entity<Institution>().Property(i => i.StoreDeliveryStages).HasColumnType("jsonb")
            .HasConversion(new JsonbConverter<List<string>>(jsonOpts)).Metadata.SetValueComparer(jsonStringListComparer);

        var stringDictComparer = new ValueComparer<Dictionary<string, string>>(
            (d1, d2) => (d1 == null && d2 == null) || (d1 != null && d2 != null && d1.OrderBy(kv => kv.Key).SequenceEqual(d2.OrderBy(kv => kv.Key))),
            d => d == null ? 0 : d.Aggregate(0, (a, kv) => HashCode.Combine(a, kv.Key.GetHashCode(), kv.Value == null ? 0 : kv.Value.GetHashCode())),
            d => d == null ? null : new Dictionary<string, string>(d));

        modelBuilder.Entity<StoreProductVariant>().Property(v => v.Options).HasColumnType("jsonb")
            .HasConversion(new JsonbConverter<Dictionary<string, string>>(jsonOpts)).Metadata.SetValueComparer(stringDictComparer);
        // Note: VariantOptions on StoreOrderItem is nested inside StoreOrder.Items' jsonb blob above — no separate column needed.

        modelBuilder.Entity<StoreProductVariant>().HasIndex(v => v.ProductId);

        // AlbumPhoto: list a given album's photos
        modelBuilder.Entity<AlbumPhoto>().HasIndex(p => p.AlbumId);

        // BusinessListing: owner snapshot + pending-edit proposal, same jsonb pattern as MentorProfile/Contribution.
        modelBuilder.Entity<BusinessListing>().Property(b => b.Member).HasColumnType("jsonb").HasConversion(new JsonbConverter<MemberSnapshot>(jsonOpts));
        modelBuilder.Entity<BusinessListing>().Property(b => b.PendingChanges).HasColumnType("jsonb").HasConversion(new JsonbConverter<BusinessListingPendingChanges>(jsonOpts));
        modelBuilder.Entity<BusinessListing>().HasIndex(b => b.MemberId);
        modelBuilder.Entity<BusinessListing>().HasIndex(b => b.Status);

        // ── JSONB array columns ──────────────────────────────────────────────
        // YearGroups stored as integer array to allow efficient filtering by member graduation year.
        modelBuilder.Entity<Campaign>().Property(c => c.YearGroups).HasColumnType("integer[]");
        modelBuilder.Entity<Campaign>().Property(c => c.BankAccount).HasColumnType("jsonb").HasConversion(new JsonbConverter<ManualPaymentBankAccount>(jsonOpts));
        modelBuilder.Entity<Campaign>().Property(c => c.MobileMoneyAccount).HasColumnType("jsonb").HasConversion(new JsonbConverter<ManualPaymentMobileMoneyAccount>(jsonOpts));

        modelBuilder.Entity<AlumniEvent>().Property(e => e.YearGroups).HasColumnType("integer[]");
        modelBuilder.Entity<InstitutionStaff>().Property(a => a.YearGroups).HasColumnType("integer[]");
        modelBuilder.Entity<InstitutionStaff>().Property(a => a.CommunityIds)
            .HasColumnType("jsonb")
            .HasConversion(new JsonbConverter<List<string>>(jsonOpts))
            .Metadata
            .SetValueComparer(jsonStringListComparer);
        modelBuilder.Entity<AlumniEvent>().Property(e => e.ImageUrls)
            .HasColumnType("jsonb")
            .HasConversion(new JsonbConverter<List<string>>(jsonOpts))
            .Metadata
            .SetValueComparer(jsonStringListComparer);
        modelBuilder.Entity<AlumniEvent>().Property(e => e.YoutubeVideoUrls)
            .HasColumnType("jsonb")
            .HasConversion(new JsonbConverter<List<string>>(jsonOpts))
            .Metadata
            .SetValueComparer(jsonStringListComparer);
        modelBuilder.Entity<NewsPost>().Property(e => e.Author)          .HasColumnType("jsonb").HasConversion(new JsonbConverter<MemberSnapshot>(jsonOpts));
        modelBuilder.Entity<NewsPost>().Property(e => e.ImageUrls)
            .HasColumnType("jsonb")
            .HasConversion(new JsonbConverter<List<string>>(jsonOpts))
            .Metadata
            .SetValueComparer(jsonStringListComparer);
        modelBuilder.Entity<NewsPost>().Property(e => e.YoutubeVideoUrls)
            .HasColumnType("jsonb")
            .HasConversion(new JsonbConverter<List<string>>(jsonOpts))
            .Metadata
            .SetValueComparer(jsonStringListComparer);
        modelBuilder.Entity<StoreProduct>().Property(p => p.ImageUrls)
            .HasColumnType("jsonb")
            .HasConversion(new JsonbConverter<List<string>>(jsonOpts))
            .Metadata
            .SetValueComparer(jsonStringListComparer);

        // ── Unique indexes ──────────────────────────────────────────

        // Email uniqueness is scoped per institution, not global — two different
        // institutions may legitimately have a member/admin with the same email.
        modelBuilder.Entity<InstitutionStaff>()
            .HasIndex(a => new { a.InstitutionId, a.Email }).IsUnique();

        modelBuilder.Entity<Member>()
            .HasIndex(m => new { m.InstitutionId, m.Email }).IsUnique();

        // ── Lookup / filter indexes ─────────────────────────────────

        // Member: directory search by graduation year + department, status filter
        modelBuilder.Entity<Member>()
            .HasIndex(m => m.Status);
        modelBuilder.Entity<Member>()
            .HasIndex(m => new { m.DepartmentId, m.GraduationYear });

        // Campaign: list by status + sort by deadline
        modelBuilder.Entity<Campaign>().Property(c => c.YearGroups).HasColumnType("integer[]");

        modelBuilder.Entity<Campaign>().Property(c => c.Status)
            .HasColumnType("text")
            .HasConversion<string>();

        modelBuilder.Entity<Campaign>().Property(c => c.BankAccount)
            .HasColumnType("jsonb").HasConversion(new JsonbConverter<ManualPaymentBankAccount>(jsonOpts));
        modelBuilder.Entity<Campaign>().Property(c => c.MobileMoneyAccount)
            .HasColumnType("jsonb").HasConversion(new JsonbConverter<ManualPaymentMobileMoneyAccount>(jsonOpts));
        modelBuilder.Entity<Campaign>()
            .HasIndex(c => c.Status);

        // CampaignUpdate: list an individual campaign's updates, newest first
        modelBuilder.Entity<CampaignUpdate>()
            .HasIndex(u => u.CampaignId);

        // Contribution: member's contributions, filter by campaign, status
        modelBuilder.Entity<Contribution>()
            .HasIndex(c => c.MemberId);
        modelBuilder.Entity<Contribution>()
            .HasIndex(c => c.CampaignId);
        modelBuilder.Entity<Contribution>()
            .HasIndex(c => c.Status);
        modelBuilder.Entity<Contribution>()
            .HasIndex(c => c.TransactionRef);

        // PaymentTransaction: lookup by Paystack reference
        modelBuilder.Entity<PaymentTransaction>()
            .HasIndex(t => t.Reference)
            .IsUnique();

        // Event: list by status, sort by date
        modelBuilder.Entity<AlumniEvent>()
            .HasIndex(e => e.Status);
        modelBuilder.Entity<AlumniEvent>()
            .HasIndex(e => e.StartDate);

        // EventRsvp: member's RSVPs, event attendees
        modelBuilder.Entity<EventRsvp>()
            .HasIndex(r => r.MemberId);
        modelBuilder.Entity<EventRsvp>()
            .HasIndex(r => r.EventId);
        modelBuilder.Entity<EventRsvp>()
            .HasIndex(r => new { r.EventId, r.MemberId }).IsUnique();

        // Job: filter by type, status
        modelBuilder.Entity<Job>()
            .HasIndex(j => j.Status);
        modelBuilder.Entity<Job>()
            .HasIndex(j => j.Type);

        // NewsPost: filter by category, status, sort by published date
        modelBuilder.Entity<NewsPost>()
            .HasIndex(n => n.Status);
        modelBuilder.Entity<NewsPost>()
            .HasIndex(n => n.Category);
        modelBuilder.Entity<NewsPost>()
            .HasIndex(n => n.PublishedAt);

        // ForumThread: filter by category, sort by created date
        modelBuilder.Entity<ForumThread>()
            .HasIndex(t => t.CategoryId);
        modelBuilder.Entity<ForumThread>()
            .HasIndex(t => t.AuthorId);

        // ForumPost: list by thread, sort by created date
        modelBuilder.Entity<ForumPost>()
            .HasIndex(p => p.ThreadId);
        modelBuilder.Entity<ForumPost>()
            .HasIndex(p => p.AuthorId);

        // ForumThread: community-scoped threads (null = institution-wide, unchanged)
        modelBuilder.Entity<ForumThread>()
            .HasIndex(t => t.CommunityId);

        // Communities: one membership row per (community, member) — a repeat
        // join request re-uses/updates the same row instead of duplicating it.
        modelBuilder.Entity<CommunityMembership>()
            .HasIndex(m => new { m.CommunityId, m.MemberId }).IsUnique();
        modelBuilder.Entity<CommunityMembership>()
            .HasIndex(m => m.MemberId);

        // MentorProfile: filter by status, member lookup
        modelBuilder.Entity<MentorProfile>()
            .HasIndex(mp => mp.MemberId);
        modelBuilder.Entity<MentorProfile>()
            .HasIndex(mp => mp.Status);

        // MentorshipRequest: mentor's requests, mentee's requests, status filter
        modelBuilder.Entity<MentorshipRequest>()
            .HasIndex(mr => mr.MentorProfileId);
        modelBuilder.Entity<MentorshipRequest>()
            .HasIndex(mr => mr.MenteeId);
        modelBuilder.Entity<MentorshipRequest>()
            .HasIndex(mr => mr.Status);

        // Resource: filter by category
        modelBuilder.Entity<Resource>()
            .HasIndex(r => r.Category);

        // ── New feature entities ────────────────────────────────────────────

        // MemberBadge
        modelBuilder.Entity<MemberBadge>().Property(b => b.Member).HasColumnType("jsonb").HasConversion(new JsonbConverter<MemberSnapshot>(jsonOpts));
        modelBuilder.Entity<MemberBadge>().HasIndex(b => b.MemberId);
        modelBuilder.Entity<MemberBadge>().HasIndex(b => b.BadgeType);

        // Spotlight
        modelBuilder.Entity<Spotlight>().Property(s => s.Member).HasColumnType("jsonb").HasConversion(new JsonbConverter<MemberSnapshot>(jsonOpts));
        modelBuilder.Entity<Spotlight>().HasIndex(s => s.MemberId);
        modelBuilder.Entity<Spotlight>().HasIndex(s => s.Status);

        // Referral
        modelBuilder.Entity<Referral>().Property(r => r.Referrer).HasColumnType("jsonb").HasConversion(new JsonbConverter<MemberSnapshot>(jsonOpts));
        modelBuilder.Entity<Referral>().Property(r => r.ReferredMember).HasColumnType("jsonb").HasConversion(new JsonbConverter<MemberSnapshot>(jsonOpts));
        modelBuilder.Entity<Referral>().HasIndex(r => r.ReferrerId);
        modelBuilder.Entity<Referral>().HasIndex(r => r.ReferredEmail);

        // ClassNote
        modelBuilder.Entity<ClassNote>().Property(n => n.Author).HasColumnType("jsonb").HasConversion(new JsonbConverter<MemberSnapshot>(jsonOpts));
        modelBuilder.Entity<ClassNote>().HasIndex(n => n.YearGroup);
        modelBuilder.Entity<ClassNote>().HasIndex(n => n.AuthorId);

        // ClassNoteLike
        modelBuilder.Entity<ClassNoteLike>().HasIndex(l => l.ClassNoteId);
        modelBuilder.Entity<ClassNoteLike>().HasIndex(l => new { l.ClassNoteId, l.MemberId }).IsUnique();

        // NotificationPreference
        modelBuilder.Entity<NotificationPreference>().HasIndex(np => np.MemberId).IsUnique();

        // AdminNotificationPreference
        modelBuilder.Entity<AdminNotificationPreference>().HasIndex(np => np.StaffId).IsUnique();

        // Notification
        modelBuilder.Entity<Notification>().HasIndex(n => n.RecipientId);
        modelBuilder.Entity<Notification>().HasIndex(n => new { n.RecipientId, n.RecipientType });
        modelBuilder.Entity<Notification>().HasIndex(n => n.IsRead);

        // ── Institution (tenant) ────────────────────────────────────────────
        modelBuilder.Entity<Institution>().HasIndex(i => i.Slug).IsUnique();
        modelBuilder.Entity<Institution>().HasIndex(i => i.CustomDomain).IsUnique();
        modelBuilder.Entity<Institution>().HasIndex(i => i.Status);
        modelBuilder.Entity<Institution>().Property(i => i.DisabledFeatures).HasColumnType("jsonb")
            .HasConversion(new JsonbConverter<List<string>>(jsonOpts)).Metadata.SetValueComparer(jsonStringListComparer);

        var landingStoryListComparer = new ValueComparer<List<LandingPageStory>>(
            (l1, l2) => (l1 == null && l2 == null) || (l1 != null && l2 != null && l1.SequenceEqual(l2)),
            l => l == null ? 0 : l.Aggregate(0, (a, v) => HashCode.Combine(a, v == null ? 0 : v.GetHashCode())),
            l => l == null ? null : new List<LandingPageStory>(l));
        modelBuilder.Entity<Institution>().Property(i => i.LandingPageStories).HasColumnType("jsonb")
            .HasConversion(new JsonbConverter<List<LandingPageStory>>(jsonOpts)).Metadata.SetValueComparer(landingStoryListComparer);
        modelBuilder.Entity<Institution>().Property(i => i.NewsBanner).HasColumnType("jsonb")
            .HasConversion(new JsonbConverter<NewsBanner>(jsonOpts));
        modelBuilder.Entity<Institution>().Property(i => i.HeroImageUrls).HasColumnType("jsonb")
            .HasConversion(new JsonbConverter<List<string>>(jsonOpts)).Metadata.SetValueComparer(jsonStringListComparer);

        // ── PlatformStaff (global, not tenant-scoped) ───────────────────────
        modelBuilder.Entity<PlatformStaff>().HasIndex(p => p.Email).IsUnique();

        // ── Support / announcements / audit log (global) ─────
        modelBuilder.Entity<SupportCase>().HasIndex(c => c.Status);
        modelBuilder.Entity<SupportCase>().HasIndex(c => c.InstitutionId);
        modelBuilder.Entity<OnboardingLead>().HasIndex(l => l.Status);
        modelBuilder.Entity<Announcement>().HasIndex(a => a.SentAt);
        modelBuilder.Entity<AuditLogEntry>().HasIndex(a => a.CreatedAt);

        // ── Multi-tenancy: scope every ITenantScoped entity to the current
        // request's resolved institution. Applies to every existing query with
        // no per-repository/controller changes required. ────────────────────
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ITenantScoped).IsAssignableFrom(entityType.ClrType)) continue;

            modelBuilder.Entity(entityType.ClrType).HasIndex(nameof(ITenantScoped.InstitutionId));

            var method = typeof(AlumniDbContext)
                .GetMethod(nameof(ApplyTenantFilter), BindingFlags.NonPublic | BindingFlags.Instance)!
                .MakeGenericMethod(entityType.ClrType);
            method.Invoke(this, [modelBuilder]);
        }
    }

    private void ApplyTenantFilter<TEntity>(ModelBuilder modelBuilder) where TEntity : class, ITenantScoped
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(e => e.InstitutionId == currentTenant.InstitutionId);
    }

    /// <summary>
    /// Auto-stamps InstitutionId on every newly-added ITenantScoped entity from the
    /// current request's resolved tenant — mirrors OnModelCreating's automatic query
    /// filter so tenant scoping is enforced on both reads and writes with no
    /// per-service code required. Never overwrites an InstitutionId a service set
    /// explicitly (e.g. Platform.Api onboarding a new institution's first admin,
    /// where the "current tenant" isn't the institution being created for).
    /// </summary>
    private void StampTenantIds()
    {
        if (string.IsNullOrEmpty(currentTenant.InstitutionId)) return;

        foreach (var entry in ChangeTracker.Entries<ITenantScoped>())
        {
            if (entry.State == EntityState.Added && string.IsNullOrEmpty(entry.Entity.InstitutionId))
            {
                entry.Entity.InstitutionId = currentTenant.InstitutionId;
            }
        }
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampTenantIds();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        StampTenantIds();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }
}
