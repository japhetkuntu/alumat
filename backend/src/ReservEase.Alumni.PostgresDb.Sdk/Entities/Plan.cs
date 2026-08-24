namespace ReservEase.Alumni.PostgresDb.Sdk.Entities;

/// <summary>A subscription tier institutions can be placed on. Global, not tenant-scoped.</summary>
public class Plan : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    /// <summary>Null means custom/negotiated pricing (shown to staff as "Custom").</summary>
    public decimal? Price { get; set; }
    public string BillingInterval { get; set; } = "monthly"; // monthly, annual
    /// <summary>Null means unlimited.</summary>
    public int? MemberLimit { get; set; }
    /// <summary>Null means unlimited.</summary>
    public int? StorageLimitGb { get; set; }
    public List<string> Modules { get; set; } = [];
    public string SupportLevel { get; set; } = "Standard support";
    public bool IsMostUsed { get; set; }
    public int SortOrder { get; set; }
}
