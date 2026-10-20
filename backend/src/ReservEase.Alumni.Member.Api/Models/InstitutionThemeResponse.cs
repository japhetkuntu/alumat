using ReservEase.Alumni.PostgresDb.Sdk.Entities;

namespace ReservEase.Alumni.Member.Api.Models;

public record InstitutionThemeResponse(
    string Slug,
    string PortalName,
    string DisplayName,
    string PrimaryColorHex,
    string? SecondaryColorHex,
    string? LogoUrl,
    string? Tagline,
    string? IconUrl,
    string? PortalTitle,
    string? AuthHeadline,
    string? AuthSubtext,
    bool RequireStudentId,
    bool ProgramOfStudyEnabled,
    List<string> ProgramsOfStudy,
    Dictionary<string, string> SocialLinks,
    bool PromptMembershipActivationAtSignup,
    bool SmsNotificationsEnabled,
    List<string> DisabledFeatures,
    List<LandingPageStory> LandingPageStories,
    NewsBanner? NewsBanner,
    List<string> HeroImageUrls,
    string? HeroHeadline,
    string OrganizationType,
    string? CohortLabel,
    string? CohortLabelPlural,
    /// <summary>Off, Optional or Required: whether registration and the portal use graduation years.</summary>
    string GraduationYearMode = "Required",
    /// <summary>Whether registration shows the member or student ID field at all.</summary>
    bool CollectMemberId = true,
    /// <summary>The community's own extra registration questions, in order.</summary>
    List<RegistrationField>? RegistrationFields = null);
