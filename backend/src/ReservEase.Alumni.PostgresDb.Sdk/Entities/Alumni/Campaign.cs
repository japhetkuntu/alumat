using ReservEase.Alumni.PostgresDb.Sdk.Entities;

namespace ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;

public enum MobileMoneyProvider
{
    MTN,
    Telecel,
    AT
}

public enum CampaignStatus
{
    Active,
    Closed,
    Completed,
    Archived
}

public class ManualPaymentBankAccount
{
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public string Branch { get; set; } = string.Empty;
}

public class ManualPaymentMobileMoneyAccount
{
    public string MobileMoneyNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public MobileMoneyProvider Provider { get; set; } = MobileMoneyProvider.MTN;
}

public static class PublicNamePolicy
{
    /// <summary>Only givers who ticked "show my name" at payment time.</summary>
    public const string OptedIn = "OptedIn";
    /// <summary>Every giver with a confirmed contribution, whether or not they ticked the box.</summary>
    public const string Everyone = "Everyone";

    public static bool IsValid(string? value) => value is OptedIn or Everyone;
}

/// <summary>
/// Admin-controlled settings for a fundraiser's public, shareable page. Nothing here can expose an
/// individual's amount; the Show* flags only choose which fundraiser-wide figures appear.
/// </summary>
public class CampaignPublicPage
{
    public bool IsPublished { get; set; }
    public DateTime? PublishedAt { get; set; }
    public string NamePolicy { get; set; } = PublicNamePolicy.OptedIn;
    public bool ShowTotalRaised { get; set; } = true;
    public bool ShowTarget { get; set; } = true;
    public bool ShowProgress { get; set; } = true;
    public bool ShowContributorCount { get; set; } = true;
    public bool ShowDeadline { get; set; } = true;
    /// <summary>Optional short thank-you / note from the institution, shown above the names.</summary>
    public string? Message { get; set; }
}

public class Campaign : BaseEntity, ITenantScoped
{
    public string InstitutionId { get; set; } = string.Empty;

    /// <summary>Null = institution-wide campaign (unchanged, pre-existing behavior). Set = belongs to one Community; only its approved members/leaders can see or contribute to it.</summary>
    public string? CommunityId { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal TargetAmount { get; set; }
    public decimal AmountPerMember { get; set; }
    public decimal? PensionerAmountPerMember { get; set; }
    public DateTime Deadline { get; set; }
    public CampaignStatus Status { get; set; } = CampaignStatus.Active;
    public decimal CollectedAmount { get; set; }
    public int PaidCount { get; set; }
    public List<int>? YearGroups { get; set; }
    public string? BannerImageUrl { get; set; }
    public string? YoutubeVideoUrl { get; set; }

    public bool IsPaystackDisbursed { get; set; } = false;
    public DateTime? PaystackDisbursedAt { get; set; }
    public string? PaystackDisbursedBy { get; set; }

    public bool AllowManualPayments { get; set; } = true;

    /// <summary>
    /// Whether members can pledge to this fundraiser (a non-binding promise to give by a date). Off unless an
    /// administrator turns it on for this fundraiser; never applies to membership dues.
    /// </summary>
    public bool AllowPledges { get; set; }

    // Special campaign used for membership renewal (context: this is handled by SuperAdmin and can be scaled by years).
    public bool IsMembershipCampaign { get; set; } = false;
    // Membership campaign year to which this campaign belongs, e.g. 2024.
    public int? MembershipYear { get; set; }

    /// <summary>Null = never configured (page not published).</summary>
    public CampaignPublicPage? PublicPage { get; set; }

    public ManualPaymentBankAccount? BankAccount { get; set; }
    public ManualPaymentMobileMoneyAccount? MobileMoneyAccount { get; set; }
}
