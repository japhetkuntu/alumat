using ReservEase.Alumni.Notifications.Sdk.Models;

namespace ReservEase.Alumni.Member.Api.Services;

/// <summary>
/// The in-app alert an institution's administrators get when a member submits something that needs a decision. One place for
/// the wording and the page each is reviewed on, so the four submit points cannot drift apart. Each carries the date so that a
/// later request about the same item is not mistaken for a repeat of an earlier one.
/// </summary>
public static class ReviewAlerts
{
    private static string Who(string name) => string.IsNullOrWhiteSpace(name) ? "A member" : name.Trim();
    private static string On(DateTime now) => now.ToString("d MMM yyyy");

    public static NotificationRequest SuggestedOpportunity(string institutionId, string jobId, string member, string title, DateTime now) =>
        NotificationRequest.RequestAwaitingReview(institutionId, "OpportunitySuggested", "Opportunity suggested",
            $"{Who(member)} suggested \"{title}\" on {On(now)}. It stays hidden until you approve it.", "Job", jobId, "/jobs?status=Pending");

    public static NotificationRequest BusinessSubmitted(string institutionId, string listingId, string member, string business, DateTime now) =>
        NotificationRequest.RequestAwaitingReview(institutionId, "BusinessListingSubmitted", "Business listing to review",
            $"{Who(member)} submitted \"{business}\" to the business directory on {On(now)}.", "BusinessListing", listingId, "/business-directory?status=Pending");

    public static NotificationRequest BusinessChanged(string institutionId, string listingId, string member, string business, bool resubmitted, DateTime now) =>
        NotificationRequest.RequestAwaitingReview(institutionId, "BusinessListingChanged", resubmitted ? "Business listing resubmitted" : "Business listing changes to review",
            resubmitted ? $"{Who(member)} resubmitted \"{business}\" after changes on {On(now)}." : $"{Who(member)} changed \"{business}\" on {On(now)}. The live listing stays as it is until you approve the changes.",
            "BusinessListing", listingId, "/business-directory?status=Pending");

    public static NotificationRequest SpotlightSubmitted(string institutionId, string spotlightId, string member, string title, DateTime now) =>
        NotificationRequest.RequestAwaitingReview(institutionId, "SpotlightSubmitted", "Spotlight story to review",
            $"{Who(member)} shared a story, \"{title}\", on {On(now)}.", "Spotlight", spotlightId, "/spotlights?status=Pending");

    public static NotificationRequest MentorProfileSubmitted(string institutionId, string profileId, string member, bool resubmitted, DateTime now) =>
        NotificationRequest.RequestAwaitingReview(institutionId, "MentorProfileSubmitted", "Mentor profile to review",
            $"{Who(member)} {(resubmitted ? "resubmitted" : "offered")} a mentor profile on {On(now)}.", "MentorProfile", profileId, "/mentorship?status=Pending");
}
