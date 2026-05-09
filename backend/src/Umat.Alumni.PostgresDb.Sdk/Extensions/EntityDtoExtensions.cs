using Umat.Alumni.Common.Sdk.Models;
using Umat.Alumni.PostgresDb.Sdk.Entities.Alumni;

namespace Umat.Alumni.PostgresDb.Sdk.Extensions;

public static class EntityDtoExtensions
{
    public static JobDto ToDto(this Job job) => new()
    {
        Id = job.Id,
        Title = job.Title,
        Company = job.Company,
        Location = job.Location,
        Type = job.Type,
        Description = job.Description,
        ApplyUrl = job.ApplyUrl,
        Deadline = job.Deadline,
        Status = job.Status,
        YearGroups = job.YearGroups,
        BannerImageUrl = job.BannerImageUrl,
        CreatedAt = job.CreatedAt,
    };

    public static AlumniEventDto ToDto(this AlumniEvent data) => new()
    {
        Id = data.Id,
        Title = data.Title,
        Description = data.Description,
        StartDate = data.StartDate,
        EndDate = data.EndDate,
        Venue = data.Venue,
        Capacity = data.Capacity,
        RsvpCount = data.RsvpCount,
        IsTicketed = data.IsTicketed,
        TicketPrice = data.TicketPrice,
        Status = data.Status,
        YearGroups = data.YearGroups,
        GoogleLocationUrl = data.GoogleLocationUrl,
        BannerImageUrl = data.BannerImageUrl,
        ImageUrls = data.ImageUrls,
        YoutubeVideoUrls = data.YoutubeVideoUrls,
        CreatedAt = data.CreatedAt,
    };

    public static NewsPostDto ToDto(this NewsPost post) => new()
    {
        Id = post.Id,
        Title = post.Title,
        Content = post.Content,
        Category = post.Category,
        IsPinned = post.IsPinned,
        Status = post.Status,
        PublishedAt = post.PublishedAt,
        AuthorId = post.AuthorId,
        AuthorName = post.Author != null ? $"{post.Author.FirstName} {post.Author.LastName}" : null,
        ImageUrls = post.ImageUrls,
        YoutubeVideoUrls = post.YoutubeVideoUrls,
        YearGroups = post.YearGroups,
        CreatedAt = post.CreatedAt,
    };

    public static ResourceDto ToDto(this Resource r) => new()
    {
        Id = r.Id,
        Title = r.Title,
        Description = r.Description,
        Category = r.Category,
        Type = r.Type,
        ExternalUrl = r.ExternalUrl,
        FileUrl = r.FileUrl,
        BannerImageUrl = r.BannerImageUrl,
        DownloadCount = r.DownloadCount,
        YearGroups = r.YearGroups,
        CreatedAt = r.CreatedAt,
    };

    public static CampaignDto ToDto(this Campaign c) => new()
    {
        Id = c.Id,
        Title = c.Title,
        Description = c.Description,
        TargetAmount = c.TargetAmount,
        AmountPerMember = c.AmountPerMember,
        PensionerAmountPerMember = c.PensionerAmountPerMember,
        Deadline = c.Deadline,
        Status = c.Status.ToString(),
        CollectedAmount = c.CollectedAmount,
        PaidCount = c.PaidCount,
        BannerImageUrl = c.BannerImageUrl,
        YoutubeVideoUrl = c.YoutubeVideoUrl,
        AllowOnlinePayments = c.AllowOnlinePayments,
        AllowManualPayments = c.AllowManualPayments,
        BankAccount = c.BankAccount != null ? new ManualPaymentBankAccountDto
        {
            AccountNumber = c.BankAccount.AccountNumber,
            AccountName = c.BankAccount.AccountName,
            BankName = c.BankAccount.BankName,
            Branch = c.BankAccount.Branch,
        } : null,
        MobileMoneyAccount = c.MobileMoneyAccount != null ? new ManualPaymentMobileMoneyAccountDto
        {
            MobileMoneyNumber = c.MobileMoneyAccount.MobileMoneyNumber,
            Name = c.MobileMoneyAccount.Name,
            Provider = c.MobileMoneyAccount.Provider.ToString(),
        } : null,
        IsMembershipCampaign = c.IsMembershipCampaign,
        MembershipYear = c.MembershipYear,
        YearGroups = c.YearGroups,
        IsPaystackDisbursed = c.IsPaystackDisbursed,
        PaystackDisbursedAt = c.PaystackDisbursedAt,
        PaystackDisbursedBy = c.PaystackDisbursedBy,
        CreatedAt = c.CreatedAt,
    };

    public static ContributionDto ToDto(this Contribution c) => new()
    {
        Id = c.Id,
        CampaignId = c.CampaignId,
        CampaignTitle = c.Campaign?.Title,
        MemberId = c.MemberId,
        MemberName = c.Member != null ? $"{c.Member.FirstName} {c.Member.LastName}" : null,
        MemberEmail = c.Member?.Email,
        MemberNumber = c.Member?.MemberNumber ?? c.MemberId,
        MemberProfilePictureUrl = c.Member?.ProfilePictureUrl,
        Amount = c.Amount,
        PaymentMethod = c.PaymentMethod,
        TransactionRef = c.TransactionRef,
        Notes = c.Notes,
        Status = c.Status,
        ConfirmedAt = c.ConfirmedAt,
        ConfirmedBy = c.ConfirmedBy,
        CreatedAt = c.CreatedAt,
    };

    public static MentorProfileDto ToDto(this MentorProfile p) => new()
    {
        Id = p.Id,
        MemberId = p.MemberId,
        MemberName = p.Member != null ? $"{p.Member.FirstName} {p.Member.LastName}" : null,
        MemberProfilePictureUrl = p.Member?.ProfilePictureUrl,
        Area = p.Area,
        Bio = p.Bio,
        MaxMentees = p.MaxMentees,
        CurrentMenteeCount = p.CurrentMenteeCount,
        Status = p.Status,
        YearGroups = p.YearGroups,
        CreatedAt = p.CreatedAt,
    };

    public static MentorshipRequestDto ToDto(this MentorshipRequest r) => new()
    {
        Id = r.Id,
        MentorProfileId = r.MentorProfileId,
        MentorProfileName = r.MentorProfile != null ? $"{r.MentorProfile.Member?.FirstName} {r.MentorProfile.Member?.LastName}" : null,
        MenteeId = r.MenteeId,
        MenteeName = r.Mentee != null ? $"{r.Mentee.FirstName} {r.Mentee.LastName}" : null,
        MenteeProfilePictureUrl = r.Mentee?.ProfilePictureUrl,
        Area = r.Area,
        Message = r.Message,
        Status = r.Status,
        CreatedAt = r.CreatedAt,
    };

    public static ForumCategoryDto ToDto(this ForumCategory c) => new()
    {
        Id = c.Id,
        Name = c.Name,
        Description = c.Description,
        SortOrder = c.SortOrder,
        CreatedAt = c.CreatedAt,
    };

    public static ForumThreadDto ToDto(this ForumThread t) => new()
    {
        Id = t.Id,
        CategoryId = t.CategoryId,
        CategoryName = t.Category?.Name,
        Title = t.Title,
        AuthorId = t.AuthorId,
        AuthorName = t.Author != null ? $"{t.Author.FirstName} {t.Author.LastName}" : null,
        AuthorProfilePictureUrl = t.Author?.ProfilePictureUrl,
        IsPinned = t.IsPinned,
        IsClosed = t.IsClosed,
        ReplyCount = t.ReplyCount,
        CreatedAt = t.CreatedAt,
    };

    public static ForumPostDto ToDto(this ForumPost p) => new()
    {
        Id = p.Id,
        ThreadId = p.ThreadId,
        AuthorId = p.AuthorId,
        AuthorName = p.Author != null ? $"{p.Author.FirstName} {p.Author.LastName}" : null,
        AuthorProfilePictureUrl = p.Author?.ProfilePictureUrl,
        Content = p.Content,
        CreatedAt = p.CreatedAt,
    };

    public static EventRsvpDto ToDto(this EventRsvp r) => new()
    {
        Id = r.Id,
        EventId = r.EventId,
        EventTitle = r.Event?.Title,
        MemberId = r.MemberId,
        MemberName = r.Member != null ? $"{r.Member.FirstName} {r.Member.LastName}" : null,
        MemberEmail = r.Member?.Email,
        MemberProfilePictureUrl = r.Member?.ProfilePictureUrl,
        Status = r.Status,
        CreatedAt = r.CreatedAt,
        UpdatedAt = r.UpdatedAt,
    };

    public static DirectoryMemberDto ToDto(this Member m) => new()
    {
        Id = m.Id,
        FirstName = m.FirstName,
        LastName = m.LastName,
        Email = m.Email,
        Phone = m.Phone,
        GraduationYear = m.GraduationYear,
        DepartmentId = m.DepartmentId,
        Company = m.Company,
        JobTitle = m.JobTitle,
        Location = m.Location,
        LinkedInUrl = m.LinkedInUrl,
        Bio = m.Bio,
        ProfilePictureUrl = m.ProfilePictureUrl,
        MemberNumber = m.MemberNumber,
    };

    public static MemberBadgeDto ToDto(this MemberBadge b) => new()
    {
        Id = b.Id,
        MemberId = b.MemberId,
        MemberName = b.Member != null ? $"{b.Member.FirstName} {b.Member.LastName}" : null,
        BadgeType = b.BadgeType,
        Description = b.Description,
        EarnedAt = b.EarnedAt,
        CreatedAt = b.CreatedAt,
    };

    public static SpotlightDto ToDto(this Spotlight s) => new()
    {
        Id = s.Id,
        MemberId = s.MemberId,
        MemberName = s.Member != null ? $"{s.Member.FirstName} {s.Member.LastName}" : null,
        MemberProfilePictureUrl = s.Member?.ProfilePictureUrl,
        Title = s.Title,
        Story = s.Story,
        ImageUrl = s.ImageUrl,
        Status = s.Status,
        FeaturedMonth = s.FeaturedMonth,
        CreatedAt = s.CreatedAt,
    };

    public static ReferralDto ToDto(this Referral r) => new()
    {
        Id = r.Id,
        ReferrerId = r.ReferrerId,
        ReferrerName = r.Referrer != null ? $"{r.Referrer.FirstName} {r.Referrer.LastName}" : null,
        ReferredEmail = r.ReferredEmail,
        ReferredMemberId = r.ReferredMemberId,
        ReferredMemberName = r.ReferredMember != null ? $"{r.ReferredMember.FirstName} {r.ReferredMember.LastName}" : null,
        Status = r.Status,
        CreatedAt = r.CreatedAt,
    };

    public static ClassNoteDto ToDto(this ClassNote n, bool isLikedByMe = false) => new()
    {
        Id = n.Id,
        AuthorId = n.AuthorId,
        AuthorName = n.Author != null ? $"{n.Author.FirstName} {n.Author.LastName}" : null,
        AuthorProfilePictureUrl = n.Author?.ProfilePictureUrl,
        YearGroup = n.YearGroup,
        Content = n.Content,
        ImageUrl = n.ImageUrl,
        LikeCount = n.LikeCount,
        IsLikedByMe = isLikedByMe,
        CreatedAt = n.CreatedAt,
    };

    public static NotificationPreferenceDto ToDto(this NotificationPreference np) => new()
    {
        Id = np.Id,
        MembershipReminders = np.MembershipReminders,
        CampaignAlerts = np.CampaignAlerts,
        EventReminders = np.EventReminders,
        JobAlerts = np.JobAlerts,
        ClassNoteAlerts = np.ClassNoteAlerts,
        SpotlightAlerts = np.SpotlightAlerts,
    };

    public static NotificationDto ToDto(this Notification n) => new()
    {
        Id = n.Id,
        RecipientId = n.RecipientId,
        RecipientType = n.RecipientType,
        Title = n.Title,
        Body = n.Body,
        Type = n.Type,
        IsRead = n.IsRead,
        ReadAt = n.ReadAt,
        RelatedEntityId = n.RelatedEntityId,
        RelatedEntityType = n.RelatedEntityType,
        ActionUrl = n.ActionUrl,
        CreatedAt = n.CreatedAt,
    };
}