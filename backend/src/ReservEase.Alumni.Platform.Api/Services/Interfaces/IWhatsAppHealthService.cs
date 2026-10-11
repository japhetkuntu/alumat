using ReservEase.Alumni.Common.Sdk.Models;

namespace ReservEase.Alumni.Platform.Api.Services.Interfaces;

/// <summary>Whether the WhatsApp channel is configured and connected, and how widely it is used. Counts and states only: no numbers, no members, no secrets.</summary>
public record WhatsAppHealthDto(
    bool Enabled, bool ApiKeyConfigured, bool WebhookSecretConfigured, bool AssistantNumberConfigured,
    bool Connected, string SessionState, int InstitutionsWithGroups, int ActiveGroups, int VerifiedMembers, int InstitutionsOptedOut,
    WhatsAppAdoptionSummary? Adoption = null);

/// <summary>
/// The case for the channel in numbers: how much of the alert traffic went by WhatsApp instead of SMS. Counts come from the
/// notification worker's monthly tallies. <see cref="EstimatedSmsSaved"/> is only present when a per-SMS cost is configured
/// (WhatsAppAssistant:SmsUnitCost), in the same currency, and is an estimate: each delivered WhatsApp alert times that cost.
/// </summary>
public record WhatsAppAdoptionSummary(
    int ActiveMembers, int AlertsOn, List<WhatsAppAdoptionMonth> Months, decimal? SmsUnitCost, decimal? EstimatedSmsSaved,
    List<WhatsAppAdoptionInstitution> Institutions);

public record WhatsAppAdoptionMonth(string Month, int ByWhatsApp, int BySms);

public record WhatsAppAdoptionInstitution(string Name, int ActiveMembers, int Connected, int ByWhatsAppThisMonth, int BySmsThisMonth);

public interface IWhatsAppHealthService
{
    Task<IApiResponse<WhatsAppHealthDto>> GetAsync();
}
