using ReservEase.Alumni.Member.Api.Models;

namespace ReservEase.Alumni.Member.Api.Services;

/// <summary>
/// Picks the few things most worth a member's attention, by fixed rules: finish what makes them findable, take part in what is
/// coming up, see what is new, join in. Nothing is invented: each step exists only if the thing it points at does. Money is never
/// asked for here; the portal should give a reason to come back that is not a request to pay.
/// </summary>
public static class NextStepRules
{
    public const int MaxSteps = 3;

    public static IReadOnlyList<NextStepDto> Evaluate(NextStepFacts f, DateTime now)
    {
        var steps = new List<NextStepDto>();

        if (f.MissingProfileParts.Count > 0)
            steps.Add(new("complete-profile", "Finish your profile",
                $"Add your {Join(f.MissingProfileParts)} so classmates and the community can find and recognise you.",
                "Update profile", "/profile"));

        if (f.EventsEnabled && f.NextEventNotSignedUpFor is { } e)
        {
            var days = Math.Max(0, (int)Math.Ceiling((e.StartsAt - now).TotalDays));
            steps.Add(new("sign-up-event", $"Sign up for {e.Title}",
                days == 0 ? "It is today." : days == 1 ? "It is tomorrow." : $"It is in {days} days.",
                "See the event", $"/events/{e.Id}"));
        }

        if (f.JobsEnabled && f.NewOpportunities > 0)
            steps.Add(new("new-opportunities", $"{f.NewOpportunities} new {(f.NewOpportunities == 1 ? "opportunity" : "opportunities")} for you",
                "Jobs, internships and openings shared by your community since you were last here.", "See opportunities", "/jobs"));

        if (f.ForumEnabled && f.RecentDiscussions > 0 && !f.PostedInDiscussionsRecently)
            steps.Add(new("join-discussion", "Join the conversation",
                $"{f.RecentDiscussions} {(f.RecentDiscussions == 1 ? "discussion was" : "discussions were")} started in the last two weeks. Add your voice.",
                "Open the forum", "/forum"));

        // Only when there is little else, so it is discoverable without becoming a nag.
        if (f.JobsEnabled && steps.Count < MaxSteps && steps.All(s => s.Key != "new-opportunities"))
            steps.Add(new("share-opportunity", "Know of a role or opening?",
                "Suggest it to the community. An administrator reviews it before it goes live.", "Suggest an opportunity", "/jobs?suggest=1"));

        return steps.Take(MaxSteps).ToList();
    }

    private static string Join(IReadOnlyList<string> parts) => parts.Count switch
    {
        1 => parts[0],
        2 => $"{parts[0]} and {parts[1]}",
        _ => $"{string.Join(", ", parts.Take(parts.Count - 1))} and {parts[^1]}",
    };
}
