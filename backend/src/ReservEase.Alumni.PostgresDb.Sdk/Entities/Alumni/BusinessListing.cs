namespace ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;

/// <summary>
/// A member's business listing in the institution's business directory, or an
/// admin-authored listing added directly. Members submit for admin approval
/// (Status starts "Pending"); admins can also add a listing directly, which is
/// auto-approved and has no owning member (MemberId/Member stay null).
///
/// Once a listing is Approved, the OWNER editing it does not overwrite the
/// live fields immediately — the proposed values land in
/// <see cref="PendingChanges"/> and wait for admin approval (see
/// <see cref="HasPendingEdit"/>). An admin's own direct edit bypasses this
/// entirely and overwrites the live fields regardless of Status.
/// </summary>
public class BusinessListing : BaseEntity, ITenantScoped
{
    public string InstitutionId { get; set; } = string.Empty;

    /// <summary>Null when this listing was created directly by an admin (no owning member).</summary>
    public string? MemberId { get; set; }

    /// <summary>Denormalized owner snapshot for admin display — null when admin-created.</summary>
    public MemberSnapshot? Member { get; set; }

    public string BusinessName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public string? BannerUrl { get; set; }
    public string Location { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public string? WebsiteUrl { get; set; }
    public string? ExternalLinkUrl { get; set; }

    /// <summary>Pending, Approved, Rejected, Blacklisted.</summary>
    public string Status { get; set; } = "Pending";
    public string? AdminNotes { get; set; }

    /// <summary>Member-owned visibility toggle, independent of Status — no approval needed to flip this.</summary>
    public bool IsHiddenByMember { get; set; }

    /// <summary>
    /// Pending-edit mechanism: when a member edits an ALREADY-APPROVED listing,
    /// the proposed values land here instead of overwriting the live fields
    /// above. Admin approving the edit copies these onto the live fields and
    /// clears both PendingChanges and HasPendingEdit. Admin rejecting just
    /// clears both — the live listing is untouched. When editing a
    /// still-Pending (never-approved) or Rejected listing, this isn't used —
    /// the live fields are overwritten directly since nothing is live yet to
    /// protect.
    /// </summary>
    public BusinessListingPendingChanges? PendingChanges { get; set; }
    public bool HasPendingEdit { get; set; }
}

public class BusinessListingPendingChanges
{
    public string? BusinessName { get; set; }
    public string? Description { get; set; }
    public string? LogoUrl { get; set; }
    public string? BannerUrl { get; set; }
    public string? Location { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public string? WebsiteUrl { get; set; }
    public string? ExternalLinkUrl { get; set; }
}
