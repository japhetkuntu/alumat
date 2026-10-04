using ReservEase.Alumni.Mailtrap.Sdk.Models;
using ReservEase.Alumni.Notifications.Sdk.Models;

namespace ReservEase.Alumni.Notifications.Tests;

public class NotificationRequestTests
{
    private const string Inst = "inst-1";

    [Fact]
    public void JobAlert_carries_only_kind_institution_and_job()
    {
        var r = NotificationRequest.JobAlert(Inst, "job-1");
        Assert.Equal(NotificationKind.JobAlert, r.Kind);
        Assert.Equal(Inst, r.InstitutionId);
        Assert.Equal("job-1", r.JobId);
        Assert.Null(r.CampaignId);
    }

    [Fact]
    public void CampaignAlert_EventReminder_SpotlightAlert_set_their_own_entity_id()
    {
        Assert.Equal("c1", NotificationRequest.CampaignAlert(Inst, "c1").CampaignId);
        Assert.Equal("e1", NotificationRequest.EventReminder(Inst, "e1").EventId);
        Assert.Equal("s1", NotificationRequest.SpotlightAlert(Inst, "s1").SpotlightId);
        Assert.Equal(NotificationKind.CampaignAlert, NotificationRequest.CampaignAlert(Inst, "c1").Kind);
        Assert.Equal(NotificationKind.EventReminder, NotificationRequest.EventReminder(Inst, "e1").Kind);
        Assert.Equal(NotificationKind.SpotlightAlert, NotificationRequest.SpotlightAlert(Inst, "s1").Kind);
    }

    [Fact]
    public void PaymentReceivedToAdmins_maps_every_argument()
    {
        var r = NotificationRequest.PaymentReceivedToAdmins(Inst, "Ama Mensah", "ama@x.com", 150.5m, "Library fund", "con-1");
        Assert.Equal(NotificationKind.PaymentReceivedToAdmins, r.Kind);
        Assert.Equal("Ama Mensah", r.MemberName);
        Assert.Equal("ama@x.com", r.MemberEmail);
        Assert.Equal(150.5m, r.Amount);
        Assert.Equal("Library fund", r.CampaignTitle);
        Assert.Equal("con-1", r.ContributionId);
    }

    [Fact]
    public void ContributionConfirmed_maps_member_and_amount()
    {
        var r = NotificationRequest.ContributionConfirmed(Inst, "m1", "m@x.com", "Kofi", 20m, "Fund", "con-2");
        Assert.Equal(NotificationKind.ContributionConfirmed, r.Kind);
        Assert.Equal(("m1", "m@x.com", "Kofi"), (r.MemberId, r.MemberEmail, r.MemberFirstName));
        Assert.Equal(20m, r.Amount);
        Assert.Equal("con-2", r.ContributionId);
    }

    [Fact]
    public void ContributionRejected_keeps_the_optional_reason()
    {
        Assert.Equal("Duplicate", NotificationRequest.ContributionRejected(Inst, "m", "e", "n", "t", "Duplicate", "c").Reason);
        Assert.Null(NotificationRequest.ContributionRejected(Inst, "m", "e", "n", "t", null, "c").Reason);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void MentorProfileDecision_records_approval(bool approved)
    {
        var r = NotificationRequest.MentorProfileDecision(Inst, "m1", approved, "p1");
        Assert.Equal(approved, r.Approved);
        Assert.Equal("p1", r.ProfileId);
        Assert.Equal(NotificationKind.MentorProfileDecision, r.Kind);
    }

    [Fact]
    public void StoreDeliveryStatusUpdated_maps_order_fields()
    {
        var r = NotificationRequest.StoreDeliveryStatusUpdated(Inst, "m1", "o1", "ORD-9", "Shipped");
        Assert.Equal(("o1", "ORD-9", "Shipped"), (r.OrderId, r.OrderNumber, r.NewStatus));
    }

    [Fact]
    public void ServiceRequestUpdated_maps_request_fields()
    {
        var r = NotificationRequest.ServiceRequestUpdated(Inst, "m1", "r1", "REQ-3", "Transcript", "Processing");
        Assert.Equal(("r1", "REQ-3", "Transcript", "Processing"), (r.RequestId, r.RequestNumber, r.ServiceTypeName, r.NewStage));
        Assert.Equal(NotificationKind.ServiceRequestUpdated, r.Kind);
    }

    [Fact]
    public void Broadcast_maps_recipients_title_message_channels_and_image()
    {
        var recipients = new List<BroadcastRecipient> { new("1", "a@x.com", "A", "0241"), new("2", "b@x.com", "B", null) };
        var r = NotificationRequest.Broadcast(Inst, recipients, "Hello", "Body", ["Email", "Sms"], "https://img");
        Assert.Equal(NotificationKind.Broadcast, r.Kind);
        Assert.Same(recipients, r.Recipients);
        Assert.Equal("Hello", r.Title);
        Assert.Equal("Body", r.Message);
        Assert.Equal(new[] { "Email", "Sms" }, r.Channels);
        Assert.Equal("https://img", r.ImageUrl);
    }

    [Fact]
    public void Broadcast_image_and_title_are_optional()
    {
        var r = NotificationRequest.Broadcast(Inst, [], null, "Body", ["Email"]);
        Assert.Null(r.Title);
        Assert.Null(r.ImageUrl);
    }

    [Fact]
    public void ClassNoteAlert_MentorshipAndForum_requests_map_ids()
    {
        var note = NotificationRequest.ClassNoteAlert(Inst, "n1", "Kojo");
        Assert.Equal(("n1", "Kojo"), (note.NoteId, note.AuthorName));

        var received = NotificationRequest.MentorshipRequestReceived(Inst, "mentor", "Mentee", "Engineering", "req");
        Assert.Equal(("mentor", "Mentee", "Engineering", "req"), (received.MentorMemberId, received.MenteeName, received.Area, received.RequestId));

        var decision = NotificationRequest.MentorshipRequestDecision(Inst, "mentee", true, "Engineering", "req");
        Assert.Equal(("mentee", true), (decision.MenteeId, decision.Accepted));

        var reply = NotificationRequest.ForumReply(Inst, "author", "Replier", "Thread", "t1");
        Assert.Equal(("author", "Replier", "Thread", "t1"), (reply.ThreadAuthorId, reply.ReplierName, reply.ThreadTitle, reply.ThreadId));
    }

    [Fact]
    public void MemberStatusChanged_and_NewMemberPendingApproval_map_fields()
    {
        var status = NotificationRequest.MemberStatusChanged(Inst, "m1", "Ama", "Suspended", "Policy");
        Assert.Equal(("Ama", "Suspended", "Policy"), (status.MemberFirstName, status.NewStatus, status.Reason));

        var pending = NotificationRequest.NewMemberPendingApproval(Inst, "m2", "Kofi A", "k@x.com");
        Assert.Equal(("m2", "Kofi A", "k@x.com"), (pending.MemberId, pending.MemberName, pending.MemberEmail));
    }

    [Fact]
    public void ReferralRegistered_EventRsvpConfirmed_SpotlightDecision_map_fields()
    {
        var referral = NotificationRequest.ReferralRegistered(Inst, "ref", "New Person");
        Assert.Equal(("ref", "New Person"), (referral.ReferrerId, referral.ReferredName));

        var rsvp = NotificationRequest.EventRsvpConfirmed(Inst, "m", "e", "Gala");
        Assert.Equal(("m", "e", "Gala"), (rsvp.MemberId, rsvp.EventId, rsvp.EventTitle));

        var spot = NotificationRequest.SpotlightDecision(Inst, "m", false, "Not suitable", "s");
        Assert.Equal((false, "Not suitable", "s"), (spot.Approved, spot.Reason, spot.SpotlightId));
    }

    [Theory]
    [InlineData(NotificationKind.EventCancelled, "Event cancelled", "\"Gala\" has been cancelled.")]
    [InlineData(NotificationKind.EventDetailsChanged, "Event details changed", "The date, time, location, or capacity for \"Gala\" changed.")]
    [InlineData(NotificationKind.EventRsvpCancelled, "RSVP cancelled", "Your RSVP for \"Gala\" was cancelled.")]
    public void Event_lifecycle_requests_have_the_expected_copy_and_metadata(NotificationKind kind, string title, string message)
    {
        var r = kind switch
        {
            NotificationKind.EventCancelled => NotificationRequest.EventCancelled(Inst, "m1", "ev1", "Gala"),
            NotificationKind.EventDetailsChanged => NotificationRequest.EventDetailsChanged(Inst, "m1", "ev1", "Gala"),
            _ => NotificationRequest.EventRsvpCancelled(Inst, "m1", "ev1", "Gala"),
        };

        Assert.Equal(kind, r.Kind);
        Assert.Equal(title, r.NotificationTitle);
        Assert.Equal(message, r.NotificationMessage);
        Assert.Equal(kind.ToString(), r.NotificationType);
        Assert.Equal("Event", r.RelatedEntityType);
        Assert.Equal("ev1", r.EventId);
        Assert.Equal("m1", r.MemberId);
    }

    [Fact]
    public void MemberLifecycle_allows_a_custom_entity_type_and_missing_entity_id()
    {
        var r = NotificationRequest.MemberLifecycle(NotificationKind.EventCancelled, Inst, "m", "T", "M", entityType: "Campaign");
        Assert.Equal("Campaign", r.RelatedEntityType);
        Assert.Null(r.EventId);
    }

    [Fact]
    public void BirthdayShoutout_targets_the_celebrant()
    {
        var r = NotificationRequest.BirthdayShoutout(Inst, "celeb", "Ama", "thread");
        Assert.Equal(("celeb", "Ama", "thread"), (r.MemberId, r.MemberName, r.ThreadId));
    }

    [Fact]
    public void Email_request_has_no_institution_and_carries_the_context()
    {
        var email = new SendEmailRequest { TemplateId = "welcome" };
        var r = NotificationRequest.Email(email, "welcome-flow");
        Assert.Equal(NotificationKind.Email, r.Kind);
        Assert.Equal(string.Empty, r.InstitutionId);
        Assert.Same(email, r.EmailRequest);
        Assert.Equal("welcome-flow", r.EmailContext);
    }

    [Fact]
    public void Every_NotificationKind_has_a_unique_value()
    {
        var values = Enum.GetValues<NotificationKind>().Select(k => (int)k).ToList();
        Assert.Equal(values.Count, values.Distinct().Count());
    }

    [Fact]
    public void NotificationRequest_survives_a_json_round_trip_because_temporal_serialises_it()
    {
        var original = NotificationRequest.Broadcast(Inst, [new("1", "a@x.com", "A", null)], "T", "M", ["Email"]);
        var json = System.Text.Json.JsonSerializer.Serialize(original);
        var copy = System.Text.Json.JsonSerializer.Deserialize<NotificationRequest>(json)!;
        Assert.Equal(original.Kind, copy.Kind);
        Assert.Equal("a@x.com", copy.Recipients![0].Email);
        Assert.Equal("T", copy.Title);
    }
}
