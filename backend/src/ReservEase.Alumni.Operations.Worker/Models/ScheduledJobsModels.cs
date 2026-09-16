using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;

namespace ReservEase.Alumni.Operations.Worker.Models;

public sealed class DigestInstitutionInfo
{
    public required string Id { get; init; }
    public required string Slug { get; init; }
    public required string Name { get; init; }
    public string? PortalName { get; init; }
    public string? PrimaryColorHex { get; init; }
    public string? SecondaryColorHex { get; init; }
    public string? LogoUrl { get; init; }
}

public sealed class BirthdayCelebrant
{
    public required string Id { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public string? Email { get; init; }
    public string? ProfilePictureUrl { get; init; }
    public string? MemberNumber { get; init; }
}

public sealed class RecurringGiftDue
{
    public required string Id { get; init; }
    public required string MemberId { get; init; }
    public string? MemberEmail { get; init; }
    public string? MemberFirstName { get; init; }
    public MemberSnapshot? Member { get; init; }
    public required string CampaignId { get; init; }
    public CampaignSnapshot? Campaign { get; init; }
    public required decimal Amount { get; init; }
    public required string AuthorizationCode { get; init; }
    public required int FailedAttemptCount { get; init; }
}

public sealed class RecurringCampaignInfo
{
    public required string Id { get; init; }
    public required string InstitutionId { get; init; }
    public required string Title { get; init; }
    public required bool IsActive { get; init; }
    public List<int>? YearGroups { get; init; }
}

public sealed class RecurringInstitutionInfo
{
    public required string Id { get; init; }
    public string? PaystackSubaccountCode { get; init; }
    public required decimal PlatformFeePercentage { get; init; }
    public decimal? PlatformFeeFlatThreshold { get; init; }
    public decimal? PlatformFeeFlatAmount { get; init; }
}

public sealed class RecurringChargeResult
{
    public required bool Succeeded { get; init; }
    public decimal? GrossAmountSubunit { get; init; }
    public decimal? FeeSubunit { get; init; }
    public string? GatewayResponse { get; init; }
    public string? Message { get; init; }
}

/// <summary>The handful of PaystackConfig fields the Zero-Deduction charge math needs —
/// workflow code has no DI/IConfiguration access, so this travels back from an activity
/// rather than being read directly by RecurringGivingWorkflow.</summary>
public sealed class PaystackGatewayFeeConfig
{
    public required decimal GatewayFeePercentage { get; init; }
    public required long GatewayFixedFeeSubunit { get; init; }
    public long? GatewayFeeCapSubunit { get; init; }
    public required long GatewayFeeSafetyBufferSubunit { get; init; }
}
