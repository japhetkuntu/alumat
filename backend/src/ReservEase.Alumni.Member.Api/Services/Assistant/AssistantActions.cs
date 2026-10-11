using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Member.Api.Models;
using ReservEase.Alumni.Member.Api.Services.Implementations;
using ReservEase.Alumni.Member.Api.Services.Interfaces;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.PostgresDb.Sdk.Services;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.Member.Api.Services.Assistant;

public sealed record ActionResult(bool Ok, string Message);

public sealed record OpportunityDraft(string Title, string Company, string Location, string Type, string? ApplyUrl);

/// <summary>
/// The things a verified member can do from WhatsApp. Each one goes through the same service the portal uses, as that member,
/// so the portal's own rules (community membership, capacity, review before publishing, limits) apply unchanged. Nothing here
/// is a second implementation.
/// </summary>
public interface IAssistantActions
{
    IReadOnlyList<string> OpportunityTypes { get; }
    Task<ActionResult> RsvpAsync(AssistantCommunity c, string eventId, CancellationToken ct = default);
    Task<ActionResult> CancelRsvpAsync(AssistantCommunity c, string eventId, CancellationToken ct = default);
    Task<ActionResult> SuggestOpportunityAsync(AssistantCommunity c, OpportunityDraft draft, CancellationToken ct = default);
    Task<ActionResult> PostUpdateAsync(AssistantCommunity c, string content, CancellationToken ct = default);
}

public class AssistantActions(
    ICurrentTenantService tenant,
    IMemberEventService eventService,
    IMemberJobService jobService,
    IClassNoteService classNoteService,
    IAlumniPgRepository<MemberEntity> memberRepo) : IAssistantActions
{
    public IReadOnlyList<string> OpportunityTypes => MemberJobService.OpportunityTypeNames;

    public async Task<ActionResult> RsvpAsync(AssistantCommunity c, string eventId, CancellationToken ct = default)
    {
        var who = await WhoAsync(c);
        if (who is null) return NotYou;
        if (!await IsInMyEventsAsync(c, eventId)) return new ActionResult(false, "I can't find that event any more. Reply *2* to see the events again.");

        var res = await eventService.RsvpAsync(new RsvpRequest(eventId), who);
        return res.Code is >= 200 and < 300
            ? new ActionResult(true, "You're booked in. I've added your RSVP.")
            : new ActionResult(false, res.Message ?? "I couldn't book that. Please try on the portal.");
    }

    public async Task<ActionResult> CancelRsvpAsync(AssistantCommunity c, string eventId, CancellationToken ct = default)
    {
        var who = await WhoAsync(c);
        if (who is null) return NotYou;
        var res = await eventService.CancelRsvpAsync(eventId, who);
        return res.Code is >= 200 and < 300
            ? new ActionResult(true, "Your RSVP is cancelled.")
            : new ActionResult(false, res.Message ?? "I couldn't cancel that. Please try on the portal.");
    }

    public async Task<ActionResult> SuggestOpportunityAsync(AssistantCommunity c, OpportunityDraft d, CancellationToken ct = default)
    {
        if (!c.IsMember || c.MemberId is null) return NotYou;
        tenant.SetInstitutionId(c.InstitutionId, c.Slug);
        var res = await jobService.SuggestAsync(c.MemberId, new SuggestOpportunityRequest
        {
            Title = d.Title, Company = d.Company, Location = d.Location, Type = d.Type, ApplyUrl = d.ApplyUrl,
        });
        return res.Code is >= 200 and < 300
            ? new ActionResult(true, "Thank you. An administrator will review it before it is shared with the community.")
            : new ActionResult(false, res.Message ?? "I couldn't send that. Please try on the portal.");
    }

    public async Task<ActionResult> PostUpdateAsync(AssistantCommunity c, string content, CancellationToken ct = default)
    {
        var who = await WhoAsync(c);
        if (who is null) return NotYou;
        var res = await classNoteService.CreateClassNoteAsync(new CreateClassNoteRequest(content, null), who);
        return res.Code is >= 200 and < 300
            ? new ActionResult(true, "Posted to your year group's wall.")
            : new ActionResult(false, res.Message ?? "I couldn't post that. Please try on the portal.");
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static readonly ActionResult NotYou = new(false, "I can't do that until your account is connected. Reply *link* to connect it.");

    /// <summary>The member as the portal would know them, with the tenant pinned to their community. Null unless the number is verified.</summary>
    private async Task<AuthData?> WhoAsync(AssistantCommunity c)
    {
        if (!c.IsMember || c.MemberId is null) return null;
        tenant.SetInstitutionId(c.InstitutionId, c.Slug);
        var m = await memberRepo.GetByIdAsync(c.MemberId);
        if (m is null || m.Status != "Active") return null;
        return new AuthData
        {
            Id = m.Id, Email = m.Email, FirstName = m.FirstName, LastName = m.LastName, ProfilePictureUrl = m.ProfilePictureUrl,
            GraduationYear = m.GraduationYear, Role = "Member",
        };
    }

    /// <summary>An RSVP is only accepted for an event the member could have been shown: the portal's own list is the authority.</summary>
    private async Task<bool> IsInMyEventsAsync(AssistantCommunity c, string eventId)
    {
        tenant.SetInstitutionId(c.InstitutionId, c.Slug);
        var page = await eventService.GetEventsAsync(new EventFilter { Page = 1, PageSize = 30, Status = "Upcoming" }, c.MemberId!);
        return (page.Data?.Results ?? []).Any(e => e.Id == eventId);
    }
}
