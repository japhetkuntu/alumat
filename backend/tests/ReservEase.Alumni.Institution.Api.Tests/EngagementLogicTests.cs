using ReservEase.Alumni.PostgresDb.Sdk.Engagement;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;

namespace ReservEase.Alumni.Institution.Api.Tests;

public class HealthScoreTests
{
    private static readonly DateTime Now = new(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);

    private static EngagementMetrics Metrics(Func<EngagementMetrics, EngagementMetrics>? tweak = null)
    {
        var m = new EngagementMetrics { Now = Now, PeriodDays = 30, ActiveMembers = 100, Activated = 70, Participants = 25, PreviousParticipants = 20, RetainedParticipants = 10 };
        return tweak is null ? m : tweak(m);
    }

    [Fact]
    public void A_community_meeting_every_target_scores_full_marks_and_is_healthy()
    {
        var r = HealthScoreCalculator.Calculate(Metrics(m => m with { RetainedParticipants = 10, PendingMembers = 0 }));

        Assert.Equal(100, r.Score);
        Assert.Equal(HealthClassifications.Healthy, r.Classification);
    }

    [Fact]
    public void Too_few_members_gives_no_score_instead_of_a_misleadingly_low_one()
    {
        var r = HealthScoreCalculator.Calculate(Metrics(m => m with { ActiveMembers = 3, Activated = 0, Participants = 0 }));

        Assert.Null(r.Score);
        Assert.Equal(HealthClassifications.InsufficientData, r.Classification);
    }

    [Fact]
    public void Ambassadors_are_left_out_and_the_other_weights_are_scaled_to_fill_the_whole()
    {
        var r = HealthScoreCalculator.Calculate(Metrics());

        var ambassadors = r.Factors.Single(f => f.Key == "ambassadors");
        Assert.Null(ambassadors.Score);
        Assert.Equal(0, ambassadors.EffectiveWeight);
        Assert.InRange(r.Factors.Sum(f => f.EffectiveWeight), 99.8m, 100.2m); // rounding only
        Assert.Equal(25m / 85m * 100, r.Factors.Single(f => f.Key == "activation").EffectiveWeight, 0);
    }

    [Fact]
    public void Retention_is_left_out_when_the_previous_period_is_too_small_to_compare()
    {
        var r = HealthScoreCalculator.Calculate(Metrics(m => m with { PreviousParticipants = 3, RetainedParticipants = 3 }));

        Assert.Null(r.Factors.Single(f => f.Key == "retention").Score);
        Assert.Contains("Left out of the score", r.Factors.Single(f => f.Key == "retention").Detail);
    }

    [Fact]
    public void Participation_is_measured_against_its_target_and_capped_at_full_marks()
    {
        var half = HealthScoreCalculator.Calculate(Metrics(m => m with { Participants = 12 })).Factors.Single(f => f.Key == "participation");
        var huge = HealthScoreCalculator.Calculate(Metrics(m => m with { Participants = 90 })).Factors.Single(f => f.Key == "participation");

        Assert.Equal(48, half.Score); // 12% of a 25% target
        Assert.Equal(100, huge.Score);
    }

    [Theory]
    [InlineData(0, null, 100)]
    [InlineData(2, 1, 100)]
    [InlineData(2, 2, 100)]
    [InlineData(2, 8, 50)]
    [InlineData(2, 14, 0)]
    [InlineData(2, 40, 0)]
    public void Responsiveness_falls_the_longer_someone_has_waited_for_approval(int pending, int? oldestDays, int expected)
    {
        var r = HealthScoreCalculator.Calculate(Metrics(m => m with { PendingMembers = pending, OldestPendingDays = oldestDays }));

        Assert.Equal(expected, r.Factors.Single(f => f.Key == "responsiveness").Score);
    }

    [Theory]
    [InlineData(80, HealthClassifications.Healthy)]
    [InlineData(60, HealthClassifications.NeedsAttention)]
    [InlineData(30, HealthClassifications.AtRisk)]
    [InlineData(10, HealthClassifications.Inactive)]
    public void The_cut_offs_come_from_configuration(int activationScorePercent, string expected)
    {
        // Only activation carries weight here, so the overall score is that dimension's score.
        var config = new HealthScoreConfig { ParticipationWeight = 0, RetentionWeight = 0, AmbassadorWeight = 0, ResponsivenessWeight = 0, ActivationTarget = 1m };
        var r = HealthScoreCalculator.Calculate(Metrics(m => m with { Activated = activationScorePercent }), config);

        Assert.Equal(activationScorePercent, r.Score);
        Assert.Equal(expected, r.Classification);
    }

    [Fact]
    public void Changing_a_weight_changes_the_score_without_touching_the_logic()
    {
        var m = Metrics(x => x with { Participants = 0, Activated = 70 });
        var normal = HealthScoreCalculator.Calculate(m).Score;
        var participationIgnored = HealthScoreCalculator.Calculate(m, new HealthScoreConfig { ParticipationWeight = 0 }).Score;

        Assert.True(participationIgnored > normal);
    }

    [Fact]
    public void Every_dimension_explains_itself_in_words()
        => Assert.All(HealthScoreCalculator.Calculate(Metrics()).Factors, f => Assert.False(string.IsNullOrWhiteSpace(f.Detail)));
}

public class RecommendationRuleTests
{
    private static readonly DateTime Now = new(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);
    private static readonly HashSet<string> None = [];
    private static EngagementMetrics Calm() => new() { Now = Now, PeriodDays = 30, ActiveMembers = 100, MeaningfulActionsLast7Days = 12 };

    [Fact]
    public void A_calm_healthy_community_gets_no_recommendations()
        => Assert.Empty(RecommendationRules.Evaluate(Calm(), None));

    [Fact]
    public void New_members_are_suggested_for_a_welcome_with_the_real_count()
    {
        var c = Assert.Single(RecommendationRules.Evaluate(Calm() with { JoinedLast7Days = 8 }, None));

        Assert.Equal("welcome-new-members", c.RuleId);
        Assert.Equal("Welcome 8 new members", c.Title);
        Assert.Equal("/members", c.ActionUrl);
    }

    [Fact]
    public void People_waiting_a_long_time_for_approval_is_high_priority_but_a_fresh_request_is_not_flagged()
    {
        var late = RecommendationRules.Evaluate(Calm() with { PendingMembers = 3, OldestPendingDays = 5 }, None);
        var fresh = RecommendationRules.Evaluate(Calm() with { PendingMembers = 3, OldestPendingDays = 1 }, None);

        Assert.Equal(RecommendationPriorities.High, Assert.Single(late).Priority);
        Assert.Empty(fresh);
    }

    [Fact]
    public void A_quiet_week_is_only_flagged_when_there_are_enough_members_to_judge_it()
    {
        var quiet = Calm() with { MeaningfulActionsLast7Days = 0 };

        Assert.Contains(RecommendationRules.Evaluate(quiet, None), r => r.RuleId == "quiet-community");
        Assert.DoesNotContain(RecommendationRules.Evaluate(quiet with { ActiveMembers = 3 }, None), r => r.RuleId == "quiet-community");
    }

    [Fact]
    public void The_quiet_week_suggestion_points_at_a_feature_the_institution_actually_has()
    {
        var quiet = Calm() with { MeaningfulActionsLast7Days = 0 };

        Assert.Equal("/events", RecommendationRules.Evaluate(quiet, new HashSet<string> { InstitutionFeatures.News }).Single().ActionUrl);
        Assert.Empty(RecommendationRules.Evaluate(quiet, new HashSet<string> { InstitutionFeatures.News, InstitutionFeatures.Events }));
    }

    [Fact]
    public void An_event_with_few_sign_ups_is_flagged_by_name_and_a_well_attended_or_distant_one_is_not()
    {
        var m = Calm() with { UpcomingEvents = [new("e1", "Homecoming", Now.AddDays(5), 2), new("e2", "Dinner", Now.AddDays(6), 40), new("e3", "Far", Now.AddDays(25), 0)] };

        var c = Assert.Single(RecommendationRules.Evaluate(m, None));

        Assert.Equal("promote-event:e1", c.DedupeKey);
        Assert.Contains("Homecoming", c.Title);
        Assert.Equal("/events/e1", c.ActionUrl);
    }

    [Fact]
    public void A_fundraiser_with_no_recent_update_is_flagged_but_a_fresh_one_is_not()
    {
        var m = Calm() with { ActiveCampaigns = [
            new("c1", "Library", Now.AddDays(-40), null, Now.AddDays(30)),
            new("c2", "Hall", Now.AddDays(-40), Now.AddDays(-2), Now.AddDays(30)),
            new("c3", "New", Now.AddDays(-3), null, Now.AddDays(60))] };

        Assert.Equal(["campaign-update:c1"], RecommendationRules.Evaluate(m, None).Select(r => r.DedupeKey));
    }

    [Fact]
    public void Fundraiser_suggestions_vanish_when_contributions_are_switched_off()
    {
        var m = Calm() with { ActiveCampaigns = [new("c1", "Library", Now.AddDays(-40), null, Now.AddDays(30))] };

        Assert.Empty(RecommendationRules.Evaluate(m, new HashSet<string> { InstitutionFeatures.Contributions }));
    }

    [Fact]
    public void Members_suggestions_waiting_are_surfaced_with_a_link_to_the_pending_list_unless_jobs_are_off()
    {
        var m = Calm() with { PendingOpportunities = 2 };

        var c = Assert.Single(RecommendationRules.Evaluate(m, None));
        Assert.Equal("/jobs?status=Pending", c.ActionUrl);
        Assert.Equal("Review 2 opportunities suggested by members", c.Title);
        Assert.Empty(RecommendationRules.Evaluate(m, new HashSet<string> { InstitutionFeatures.Jobs }));
    }

    [Fact]
    public void A_dormant_segment_needs_to_be_both_large_enough_and_a_real_share_of_the_community()
    {
        Assert.Contains(RecommendationRules.Evaluate(Calm() with { Dormant = 30 }, None), r => r.RuleId == "dormant-members");
        Assert.DoesNotContain(RecommendationRules.Evaluate(Calm() with { Dormant = 8 }, None), r => r.RuleId == "dormant-members");   // under the minimum
        Assert.DoesNotContain(RecommendationRules.Evaluate(Calm() with { Dormant = 15 }, None), r => r.RuleId == "dormant-members");  // under 20% of 100
    }

    [Fact]
    public void Declining_participation_is_compared_with_an_adequate_previous_period()
    {
        var m = Calm() with { Participants = 8, PreviousParticipants = 20 };

        Assert.Contains(RecommendationRules.Evaluate(m, None), r => r.RuleId == "declining-participation");
        Assert.DoesNotContain(RecommendationRules.Evaluate(m with { PreviousParticipants = 6 }, None), r => r.RuleId == "declining-participation");
        Assert.DoesNotContain(RecommendationRules.Evaluate(m with { Participants = 18 }, None), r => r.RuleId == "declining-participation");
    }

    [Fact]
    public void Every_suggestion_has_an_action_link_and_a_future_review_date()
    {
        var m = Calm() with { JoinedLast7Days = 2, PendingMembers = 1, OldestPendingDays = 9, MeaningfulActionsLast7Days = 0, Dormant = 40, Participants = 1, PreviousParticipants = 30,
            UpcomingEvents = [new("e1", "X", Now.AddDays(3), 0)], ActiveCampaigns = [new("c1", "Y", Now.AddDays(-30), null, Now.AddDays(20))] };

        var all = RecommendationRules.Evaluate(m, None);

        Assert.True(all.Count >= 6);
        Assert.All(all, c => { Assert.StartsWith("/", c.ActionUrl); Assert.True(c.ExpiresAt > Now); Assert.False(string.IsNullOrWhiteSpace(c.Explanation)); });
        Assert.Equal(all.Count, all.Select(c => c.DedupeKey).Distinct().Count());
    }
}

public class RecommendationLifecycleTests
{
    private static readonly DateTime Now = new(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);
    private static RecommendationCandidate Want(string key = "k1", int cooldownDays = 5) =>
        new("rule", key, "T", "E", RecommendationPriorities.Medium, "Go", "/x", Now.AddDays(7), TimeSpan.FromDays(cooldownDays));
    private static EngagementRecommendation Row(string key, string status, DateTime? resolved = null, DateTime? snoozedUntil = null, DateTime? expires = null, DateTime? created = null) =>
        new() { DedupeKey = key, Status = status, ResolvedAt = resolved, SnoozedUntil = snoozedUntil, ExpiresAt = expires ?? Now.AddDays(3), CreatedAt = created ?? Now.AddDays(-10) };

    [Fact]
    public void A_new_situation_is_created()
        => Assert.Single(RecommendationLifecycle.Plan([], [Want()], Now).ToCreate);

    [Fact]
    public void An_open_one_is_never_raised_a_second_time()
    {
        var plan = RecommendationLifecycle.Plan([Row("k1", RecommendationStatuses.Open)], [Want()], Now);

        Assert.Empty(plan.ToCreate);
        Assert.Empty(plan.ToExpire);
    }

    [Fact]
    public void A_completed_one_stays_quiet_through_its_cooldown_then_may_return()
    {
        var recent = Row("k1", RecommendationStatuses.Completed, resolved: Now.AddDays(-2));
        var old = Row("k1", RecommendationStatuses.Completed, resolved: Now.AddDays(-9));

        Assert.Empty(RecommendationLifecycle.Plan([recent], [Want(cooldownDays: 5)], Now).ToCreate);
        Assert.Single(RecommendationLifecycle.Plan([old], [Want(cooldownDays: 5)], Now).ToCreate);
    }

    [Fact]
    public void A_dismissed_one_is_not_nagged_back_inside_its_cooldown()
        => Assert.Empty(RecommendationLifecycle.Plan([Row("k1", RecommendationStatuses.Dismissed, resolved: Now.AddDays(-1))], [Want()], Now).ToCreate);

    [Fact]
    public void A_snoozed_one_stays_hidden_until_its_time_then_reopens()
    {
        var sleeping = Row("k1", RecommendationStatuses.Snoozed, snoozedUntil: Now.AddDays(2));
        var awake = Row("k1", RecommendationStatuses.Snoozed, snoozedUntil: Now.AddHours(-1));

        Assert.Empty(RecommendationLifecycle.Plan([sleeping], [Want()], Now).ToReopen);
        Assert.Single(RecommendationLifecycle.Plan([awake], [Want()], Now).ToReopen);
        Assert.Empty(RecommendationLifecycle.Plan([awake], [Want()], Now).ToCreate);
    }

    [Fact]
    public void An_open_one_whose_situation_has_cleared_is_retired_not_deleted()
    {
        var plan = RecommendationLifecycle.Plan([Row("gone", RecommendationStatuses.Open)], [], Now);

        Assert.Single(plan.ToExpire);
    }

    [Fact]
    public void An_open_one_past_its_review_date_is_retired_even_if_still_true()
    {
        var plan = RecommendationLifecycle.Plan([Row("k1", RecommendationStatuses.Open, expires: Now.AddHours(-2))], [Want()], Now);

        Assert.Single(plan.ToExpire);
    }

    [Fact]
    public void An_expired_situation_that_returns_is_raised_again()
        => Assert.Single(RecommendationLifecycle.Plan([Row("k1", RecommendationStatuses.Expired)], [Want()], Now).ToCreate);

    [Fact]
    public void Running_the_same_plan_twice_after_applying_it_changes_nothing()
    {
        var first = RecommendationLifecycle.Plan([], [Want("a"), Want("b")], Now);
        var stored = first.ToCreate.Select(c => Row(c.DedupeKey, RecommendationStatuses.Open)).ToList();

        var second = RecommendationLifecycle.Plan(stored, [Want("a"), Want("b")], Now);

        Assert.Empty(second.ToCreate); Assert.Empty(second.ToExpire); Assert.Empty(second.ToReopen);
    }
}

public class ChecklistTests
{
    private static readonly DateTime Now = new(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc); // a Wednesday
    private static readonly HashSet<string> None = [];

    [Fact]
    public void The_week_starts_on_Monday_in_UTC()
        => Assert.Equal(new DateTime(2026, 10, 5), EngagementChecklist.WeekStart(Now));

    [Fact]
    public void Sunday_belongs_to_the_week_that_began_the_Monday_before()
        => Assert.Equal(new DateTime(2026, 10, 5), EngagementChecklist.WeekStart(new DateTime(2026, 10, 11, 23, 0, 0, DateTimeKind.Utc)));

    [Fact]
    public void Items_that_cannot_apply_are_left_off()
    {
        var items = EngagementChecklist.Build(new EngagementMetrics { Now = Now }, None, None);

        Assert.DoesNotContain(items, i => i.Key == "welcome-new-members");
        Assert.DoesNotContain(items, i => i.Key == "promote-event");
    }

    [Fact]
    public void Switched_off_features_leave_their_items_off()
    {
        var items = EngagementChecklist.Build(new EngagementMetrics { Now = Now }, new HashSet<string> { InstitutionFeatures.News, InstitutionFeatures.Jobs, InstitutionFeatures.Spotlights }, None);

        Assert.DoesNotContain(items, i => i.Key is "publish-update" or "share-opportunity" or "highlight-member");
    }

    [Fact]
    public void Items_complete_themselves_from_real_activity()
    {
        var items = EngagementChecklist.Build(new EngagementMetrics { Now = Now, NewsPublishedThisWeek = 1, JobsPostedThisWeek = 0, PendingMembers = 0 }, None, None);

        Assert.True(items.Single(i => i.Key == "publish-update").AutomaticallyDone);
        Assert.False(items.Single(i => i.Key == "share-opportunity").Done);
        Assert.True(items.Single(i => i.Key == "review-pending").Done);
    }

    [Fact]
    public void A_ticked_item_shows_as_done_but_not_as_automatic()
    {
        var items = EngagementChecklist.Build(new EngagementMetrics { Now = Now, JoinedLast7Days = 3 }, None, new HashSet<string> { "welcome-new-members" });

        var item = items.Single(i => i.Key == "welcome-new-members");
        Assert.True(item.Done);
        Assert.False(item.AutomaticallyDone);
    }
}

public class AmbassadorHealthTests
{
    private static readonly DateTime Now = new(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);
    private static EngagementMetrics Base() => new() { Now = Now, PeriodDays = 30, ActiveMembers = 100, Activated = 70, Participants = 25, AmbassadorsAvailable = true, CohortsWithMembers = 10 };

    [Theory]
    [InlineData(7, 10, 100)]   // 70% covered earns full coverage marks; everyone active
    [InlineData(7, 5, 80)]     // full coverage marks (60) + half the activity share (20)
    [InlineData(0, 0, 0)]
    [InlineData(3, 0, 26)]     // 30% of a 70% target: 43% of the 60-point coverage share
    public void The_ambassador_factor_combines_coverage_and_activity(int covered, int active, int expected)
    {
        var m = Base() with { CohortsCovered = covered, AmbassadorCount = covered == 0 ? 0 : 10, ActiveAmbassadors = active };

        Assert.Equal(expected, HealthScoreCalculator.Calculate(m).Factors.Single(f => f.Key == "ambassadors").Score);
    }

    [Fact]
    public void With_no_year_groups_to_cover_the_factor_is_left_out()
        => Assert.Null(HealthScoreCalculator.Calculate(Base() with { CohortsWithMembers = 0 }).Factors.Single(f => f.Key == "ambassadors").Score);

    [Fact]
    public void Uncovered_year_groups_are_summarised_largest_first_with_their_sizes()
    {
        var m = Base() with { UncoveredCohorts = [new(2014, 8), new(2019, 30), new(2011, 12), new(2008, 5)] };

        var c = Assert.Single(RecommendationRules.Evaluate(m, new HashSet<string>()), r => r.RuleId == "unassigned-cohort");

        Assert.Equal("4 year groups have no ambassador", c.Title);
        Assert.Contains("2019 (30), 2011 (12), 2014 (8)", c.Explanation);
    }

    [Fact]
    public void Each_inactive_ambassador_gets_their_own_suggestion_and_nothing_is_changed_automatically()
    {
        var m = Base() with { InactiveAmbassadors = [new("a1", "Esi One", 30), new("a2", "Esi Two", 45)] };

        var found = RecommendationRules.Evaluate(m, new HashSet<string>()).Where(r => r.RuleId == "inactive-ambassador").ToList();

        Assert.Equal(2, found.Count);
        Assert.All(found, r => Assert.Contains("Nothing changes automatically", r.Explanation));
    }
}

public class AutomationPolicyTests
{
    private static readonly DateTime Now = new(2026, 10, 9, 10, 0, 0, DateTimeKind.Utc);
    private static readonly IReadOnlyDictionary<(string, string, string?), DateTime> NothingSent = new Dictionary<(string, string, string?), DateTime>();
    private static StaffState Admin(string id, int? daysAway) => new(id, $"Admin {id}", $"{id}@x.com", daysAway is null ? null : Now.AddDays(-daysAway.Value));

    [Theory]
    [InlineData(7, false)] [InlineData(8, true)] [InlineData(13, true)] [InlineData(17, true)] [InlineData(18, false)] [InlineData(23, false)] [InlineData(2, false)]
    public void Messages_are_only_queued_in_the_daytime_window(int hour, bool expected)
        => Assert.Equal(expected, AutomationPolicy.InSendWindow(new DateTime(2026, 10, 9, hour, 30, 0, DateTimeKind.Utc)));

    [Fact]
    public void An_administrator_away_for_two_weeks_is_reminded_when_work_is_waiting()
    {
        var plans = AutomationPolicy.PlanAdminMessages([Admin("a", 20)], openRecommendations: 3, pendingMembers: 0, Now, NothingSent);

        var p = Assert.Single(plans);
        Assert.Equal((EngagementMessageKinds.AdminReminder, 20), (p.Kind, p.DaysAway));
    }

    [Fact]
    public void Nobody_is_reminded_when_nothing_is_waiting()
        => Assert.Empty(AutomationPolicy.PlanAdminMessages([Admin("a", 60)], 0, 0, Now, NothingSent));

    [Fact]
    public void An_administrator_who_is_around_is_left_alone()
        => Assert.Empty(AutomationPolicy.PlanAdminMessages([Admin("a", 3)], 5, 2, Now, NothingSent));

    [Fact]
    public void A_reminder_is_not_repeated_inside_its_cooldown_but_is_after_it()
    {
        var recent = new Dictionary<(string, string, string?), DateTime> { [("a", EngagementMessageKinds.AdminReminder, null)] = Now.AddDays(-5) };
        var old = new Dictionary<(string, string, string?), DateTime> { [("a", EngagementMessageKinds.AdminReminder, null)] = Now.AddDays(-15) };

        Assert.Empty(AutomationPolicy.PlanAdminMessages([Admin("a", 20)], 1, 0, Now, recent));
        Assert.Single(AutomationPolicy.PlanAdminMessages([Admin("a", 20)], 1, 0, Now, old));
    }

    [Fact]
    public void A_colleague_away_a_month_is_reported_to_the_administrators_still_active_and_never_to_themselves()
    {
        var plans = AutomationPolicy.PlanAdminMessages([Admin("away", 45), Admin("here", 1), Admin("also", 2)], 2, 0, Now, NothingSent);

        var escalations = plans.Where(p => p.Kind == EngagementMessageKinds.AdminEscalation).ToList();
        Assert.Equal(["also", "here"], escalations.Select(e => e.RecipientId).Order());
        Assert.All(escalations, e => Assert.Equal("away", e.Subject));
        Assert.Contains(plans, p => p.Kind == EngagementMessageKinds.AdminReminder && p.RecipientId == "away");
    }

    [Fact]
    public void With_no_active_administrator_there_is_nobody_inside_to_escalate_to()
        => Assert.DoesNotContain(AutomationPolicy.PlanAdminMessages([Admin("a", 45), Admin("b", 50)], 2, 0, Now, NothingSent), p => p.Kind == EngagementMessageKinds.AdminEscalation);

    [Fact]
    public void An_administrator_who_has_never_signed_in_counts_as_away_with_no_day_count_claimed()
    {
        var p = Assert.Single(AutomationPolicy.PlanAdminMessages([Admin("new", null)], 1, 0, Now, NothingSent));

        Assert.Null(p.DaysAway);
    }

    [Fact]
    public void Administrators_without_an_email_are_never_planned_for()
        => Assert.Empty(AutomationPolicy.PlanAdminMessages([new StaffState("a", "A", "", null)], 2, 0, Now, NothingSent));

    private static bool Reengage(bool optedOut = false, bool emailOn = true, bool hasEmail = true, int joinedDaysAgo = 200, int? lastLoginDaysAgo = 90,
        bool tookPart = false, int? lastReDaysAgo = null, int? lastAnyDaysAgo = null, int content = 2)
        => AutomationPolicy.ShouldReengage(optedOut, emailOn, hasEmail, Now.AddDays(-joinedDaysAgo), lastLoginDaysAgo is null ? null : Now.AddDays(-lastLoginDaysAgo.Value), tookPart,
            lastReDaysAgo is null ? null : Now.AddDays(-lastReDaysAgo.Value), lastAnyDaysAgo is null ? null : Now.AddDays(-lastAnyDaysAgo.Value), content, Now);

    [Fact] public void A_quiet_member_with_something_to_show_is_eligible() => Assert.True(Reengage());
    [Fact] public void Someone_who_opted_out_is_never_written_to() => Assert.False(Reengage(optedOut: true));
    [Fact] public void An_institution_with_email_switched_off_sends_nothing() => Assert.False(Reengage(emailOn: false));
    [Fact] public void No_content_means_no_message_however_quiet_they_are() => Assert.False(Reengage(content: 0));
    [Fact] public void A_member_who_joined_recently_is_still_being_welcomed_not_chased() => Assert.False(Reengage(joinedDaysAgo: 10, lastLoginDaysAgo: null));
    [Fact] public void A_member_who_signed_in_recently_is_not_quiet() => Assert.False(Reengage(lastLoginDaysAgo: 5));
    [Fact] public void A_member_who_took_part_recently_is_not_quiet() => Assert.False(Reengage(tookPart: true));
    [Fact] public void One_re_engagement_note_a_month_at_most() { Assert.False(Reengage(lastReDaysAgo: 20)); Assert.True(Reengage(lastReDaysAgo: 31)); }
    [Fact] public void Never_two_engagement_messages_in_one_week() { Assert.False(Reengage(lastAnyDaysAgo: 3)); Assert.True(Reengage(lastAnyDaysAgo: 8)); }
    [Fact] public void A_member_who_never_signed_in_but_joined_long_ago_is_eligible() => Assert.True(Reengage(lastLoginDaysAgo: null));
}
