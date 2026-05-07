namespace Umat.Alumni.Admin.Api.Models;

public class CreateCampaignRequest
{
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public decimal TargetAmount { get; set; }
    public decimal AmountPerMember { get; set; }
    public decimal? PensionerAmountPerMember { get; set; }
    public DateTime Deadline { get; set; }
    public List<int>? YearGroups { get; set; }
    public IFormFile? BannerImage { get; set; }
    public string? YoutubeVideoUrl { get; set; }

    public bool AllowOnlinePayments { get; set; } = true;
    public bool AllowManualPayments { get; set; } = false;
    public bool IsMembershipCampaign { get; set; } = false;
    public int? MembershipYear { get; set; }

    public string? BankAccountNumber { get; set; }
    public string? BankAccountName { get; set; }
    public string? BankName { get; set; }
    public string? BankBranch { get; set; }

    public string? MobileMoneyNumber { get; set; }
    public string? MobileMoneyName { get; set; }
    public string? MobileMoneyProvider { get; set; }
}

public class UpdateCampaignRequest
{
    public string CampaignId { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public DateTime Deadline { get; set; }
    public string Status { get; set; } = "";
    public decimal? TargetAmount { get; set; }
    public decimal? AmountPerMember { get; set; }
    public decimal? PensionerAmountPerMember { get; set; }
    public List<int>? YearGroups { get; set; }
    public IFormFile? BannerImage { get; set; }
    public string? YoutubeVideoUrl { get; set; }

    public bool AllowOnlinePayments { get; set; } = true;
    public bool AllowManualPayments { get; set; } = false;
    public bool IsMembershipCampaign { get; set; } = false;
    public int? MembershipYear { get; set; }

    public string? BankAccountNumber { get; set; }
    public string? BankAccountName { get; set; }
    public string? BankName { get; set; }
    public string? BankBranch { get; set; }

    public string? MobileMoneyNumber { get; set; }
    public string? MobileMoneyName { get; set; }
    public string? MobileMoneyProvider { get; set; }
}
