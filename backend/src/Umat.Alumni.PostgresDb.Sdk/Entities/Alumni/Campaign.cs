namespace Umat.Alumni.PostgresDb.Sdk.Entities.Alumni;

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

public class Campaign : BaseEntity
{
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

    public bool AllowOnlinePayments { get; set; } = true;
    public bool AllowManualPayments { get; set; } = true;

    // Special campaign used for membership renewal (context: this is handled by SuperAdmin and can be scaled by years).
    public bool IsMembershipCampaign { get; set; } = false;
    // Membership campaign year to which this campaign belongs, e.g. 2024.
    public int? MembershipYear { get; set; }

    public ManualPaymentBankAccount? BankAccount { get; set; }
    public ManualPaymentMobileMoneyAccount? MobileMoneyAccount { get; set; }
}
