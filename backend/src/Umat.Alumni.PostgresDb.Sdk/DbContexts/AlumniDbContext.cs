using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Umat.Alumni.PostgresDb.Sdk.Entities.Alumni;

namespace Umat.Alumni.PostgresDb.Sdk.DbContexts;

/// <summary>Converts any serialisable type to/from a PostgreSQL jsonb column.</summary>
internal sealed class JsonbConverter<T>(JsonSerializerOptions opts) : ValueConverter<T?, string?>(    v => v == null ? null : JsonSerializer.Serialize(v, opts),
    v => string.IsNullOrEmpty(v) ? null : JsonSerializer.Deserialize<T>(v!, opts))
    where T : class;

public class AlumniDbContext(DbContextOptions<AlumniDbContext> options) : DbContext(options)
{
    public DbSet<Admin> Admins => Set<Admin>();
    public DbSet<Member> Members => Set<Member>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Campaign> Campaigns => Set<Campaign>();
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
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("alumni");

        // ── JSONB snapshot converters (no FK constraints anywhere) ──────────
        var jsonOpts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

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

        // ── JSONB array columns ──────────────────────────────────────────────
        // YearGroups stored as integer array to allow efficient filtering by member graduation year.
        modelBuilder.Entity<Campaign>().Property(c => c.YearGroups).HasColumnType("integer[]");
        modelBuilder.Entity<Campaign>().Property(c => c.BankAccount).HasColumnType("jsonb").HasConversion(new JsonbConverter<ManualPaymentBankAccount>(jsonOpts));
        modelBuilder.Entity<Campaign>().Property(c => c.MobileMoneyAccount).HasColumnType("jsonb").HasConversion(new JsonbConverter<ManualPaymentMobileMoneyAccount>(jsonOpts));

        modelBuilder.Entity<AlumniEvent>().Property(e => e.YearGroups).HasColumnType("integer[]");
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

        // ── Unique indexes ──────────────────────────────────────────

        modelBuilder.Entity<Admin>()
            .HasIndex(a => a.Email).IsUnique();

        modelBuilder.Entity<Member>()
            .HasIndex(m => m.Email).IsUnique();

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

        // Notification
        modelBuilder.Entity<Notification>().HasIndex(n => n.RecipientId);
        modelBuilder.Entity<Notification>().HasIndex(n => new { n.RecipientId, n.RecipientType });
        modelBuilder.Entity<Notification>().HasIndex(n => n.IsRead);
    }
}
