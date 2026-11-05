using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ReservEase.Alumni.Common.Sdk.Models;
using ReservEase.Alumni.Member.Api.Models;
using ReservEase.Alumni.Member.Api.Services.Implementations;
using ReservEase.Alumni.Member.Api.Services.Realtime;
using ReservEase.Alumni.Member.Api.Services.Security;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.Temporal.Sdk;
using ReservEase.Alumni.TestKit;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.Member.Api.Tests;

public static class ApiResponseTestExtensions
{
    public static bool Succeeded<T>(this IApiResponse<T> r) => r.Code is >= 200 and < 300;
}

public class IntroductionServiceTests
{
    private const string Tenant = "inst-1";
    private const string Note = "Same programme, 2014, and I'm moving into data work.";

    /// <summary>The same set-up for the encryption tests, which live in their own file.</summary>
    public sealed class RigAccess
    {
        private readonly Rig _rig;
        public RigAccess(IMessageProtector? protector = null) { _rig = new Rig(protector); }
        public string DbName => _rig.DbName;
        public InMemoryIntroductionEvents Events => _rig.Events;
        public IMessageProtector Protector => _rig.Protector;
        public AuthData Ama => _rig.Ama;
        public AuthData Kofi => _rig.Kofi;
        public MemberIntroductionService Service => _rig.Service;
        public MemberIntroductionService Fresh() => _rig.Fresh();
        public Task<string> ConnectedAsync() => _rig.Connected();
    }

    private sealed class Rig
    {
        public string DbName { get; } = TestDb.NewName();
        public CapturingLogger<MemberIntroductionService> Log { get; } = new();
        public AuthData Ama { get; } = new() { Id = "ama", FirstName = "Ama", LastName = "Mensah", Email = "ama@x.com" };
        public AuthData Kofi { get; } = new() { Id = "kofi", FirstName = "Kofi", LastName = "Boateng", Email = "kofi@x.com" };
        public MemberIntroductionService Service { get; private set; } = null!;
        public IMessageProtector Protector { get; }
        public InMemoryIntroductionEvents Events { get; } = new();

        public Rig(IMessageProtector? protector = null)
        {
            Protector = protector ?? MessageEncryptionTests.NewProtector();
            using var db = TestDb.Create(DbName, Tenant);
            foreach (var (id, first, last) in new[] { ("ama", "Ama", "Mensah"), ("kofi", "Kofi", "Boateng"), ("esi", "Esi", "Owusu") })
                db.Add(new MemberEntity { Id = id, InstitutionId = Tenant, FirstName = first, LastName = last, Email = $"{id}@x.com", Status = "Active", GraduationYear = 2014 });
            db.SaveChanges();
            Fresh();
        }

        public MemberIntroductionService Fresh()
        {
            var db = TestDb.Create(DbName, Tenant);
            var temporal = new Mock<ITemporalClientProvider>();
            temporal.SetupGet(t => t.IsAvailable).Returns(false);   // notices are logged as dropped, which the tests can count
            return Service = new MemberIntroductionService(
                new AlumniPgRepository<Introduction>(db), new AlumniPgRepository<IntroductionMessage>(db), new AlumniPgRepository<MemberBlock>(db),
                new AlumniPgRepository<ContentReport>(db), new AlumniPgRepository<MemberEntity>(db), new AlumniPgRepository<Notification>(db), Protector, Events,
                temporal.Object, TestDb.Tenant(Tenant, "umat"), Log);
        }

        public async Task Edit(Action<MemberEntity> change, string id)
        {
            using var db = TestDb.Create(DbName, Tenant);
            var m = db.Members.First(x => x.Id == id);
            change(m);
            await db.SaveChangesAsync();
            Fresh();
        }

        public async Task<string> Connected()
        {
            var made = await Service.RequestAsync(new("kofi", Note), Ama);
            var id = made.Data!.IntroductionId!;
            await Service.RespondAsync(id, true, Kofi);
            return id;
        }

        public int Notices => Log.Entries.Count(e => e.Message.Contains("dropped notification"));
    }

    [Fact]
    public async Task A_request_needs_a_real_note_and_cannot_be_to_yourself()
    {
        var rig = new Rig();
        Assert.False((await rig.Service.RequestAsync(new("kofi", "hi"), rig.Ama)).Succeeded());
        Assert.False((await rig.Service.RequestAsync(new("kofi", new string('x', 301)), rig.Ama)).Succeeded());
        Assert.False((await rig.Service.RequestAsync(new("ama", Note), rig.Ama)).Succeeded());
        Assert.True((await rig.Service.RequestAsync(new("kofi", Note), rig.Ama)).Succeeded());
        Assert.Equal(1, rig.Notices);   // the recipient is told once, without the note
    }

    [Fact]
    public async Task Opting_out_blocking_and_leaving_all_read_the_same_to_the_person_asking()
    {
        var rig = new Rig();
        await rig.Edit(m => m.AllowIntroductions = false, "kofi");
        var optedOut = await rig.Service.RequestAsync(new("kofi", Note), rig.Ama);

        await rig.Edit(m => m.AllowIntroductions = true, "kofi");
        await rig.Service.BlockAsync("ama", rig.Kofi);
        var blocked = await rig.Service.RequestAsync(new("kofi", Note), rig.Ama);

        await rig.Service.UnblockAsync("ama", rig.Kofi);
        await rig.Edit(m => m.Status = "Deleted", "kofi");
        var gone = await rig.Service.RequestAsync(new("kofi", Note), rig.Ama);

        Assert.All(new[] { optedOut, blocked, gone }, r => Assert.False(r.Succeeded()));
        Assert.Equal(optedOut.Message, blocked.Message);
        Assert.Equal(optedOut.Message, gone.Message);
    }

    [Fact]
    public async Task You_cannot_ask_twice_or_hold_more_than_five_requests_waiting()
    {
        var rig = new Rig();
        Assert.True((await rig.Service.RequestAsync(new("kofi", Note), rig.Ama)).Succeeded());
        Assert.False((await rig.Service.RequestAsync(new("kofi", Note), rig.Ama)).Succeeded());

        using (var db = TestDb.Create(rig.DbName, Tenant))
        {
            for (var n = 0; n < 4; n++) db.Add(new Introduction { InstitutionId = Tenant, RequesterId = "ama", RecipientId = $"other{n}", Reason = Note });
            await db.SaveChangesAsync();
        }
        rig.Fresh();
        var capped = await rig.Service.RequestAsync(new("esi", Note), rig.Ama);
        Assert.False(capped.Succeeded());
        Assert.Contains("waiting", capped.Message);
    }

    [Fact]
    public async Task A_declined_request_looks_like_no_reply_and_cannot_be_repeated_for_a_month()
    {
        var rig = new Rig();
        var id = (await rig.Service.RequestAsync(new("kofi", Note), rig.Ama)).Data!.IntroductionId!;
        Assert.True((await rig.Service.RespondAsync(id, false, rig.Kofi)).Succeeded());

        var sent = (await rig.Service.ListAsync("sent", null, 25, null, rig.Ama)).Data!.Items;
        Assert.Equal("Pending", Assert.Single(sent).Status);                       // not "Declined"
        Assert.Equal("pending", (await rig.Service.GetStateAsync("kofi", rig.Ama)).Data!.State);
        Assert.False((await rig.Service.RequestAsync(new("kofi", Note), rig.Ama)).Succeeded());
        Assert.Equal(1, rig.Notices);                                              // only the original request was announced
        Assert.Empty((await rig.Service.ListAsync("incoming", null, 25, null, rig.Kofi)).Data!.Items);
    }

    [Fact]
    public async Task Only_the_person_asked_can_answer_and_nobody_can_chat_before_a_yes()
    {
        var rig = new Rig();
        var id = (await rig.Service.RequestAsync(new("kofi", Note), rig.Ama)).Data!.IntroductionId!;
        Assert.False((await rig.Service.RespondAsync(id, true, rig.Ama)).Succeeded());          // the asker cannot accept their own
        Assert.False((await rig.Service.SendMessageAsync(id, "hello", rig.Ama)).Succeeded());   // no chat yet
        Assert.False((await rig.Service.SendMessageAsync(id, "hello", rig.Kofi)).Succeeded());
        Assert.True((await rig.Service.RespondAsync(id, true, rig.Kofi)).Succeeded());
        Assert.True((await rig.Service.SendMessageAsync(id, "hello", rig.Ama)).Succeeded());
    }

    [Fact]
    public async Task A_stranger_cannot_open_or_write_in_a_conversation()
    {
        var rig = new Rig();
        var id = await rig.Connected();
        var esi = new AuthData { Id = "esi", FirstName = "Esi", LastName = "Owusu" };
        Assert.False((await rig.Service.GetAsync(id, null, esi)).Succeeded());
        Assert.False((await rig.Service.SendMessageAsync(id, "hi", esi)).Succeeded());
        Assert.False((await rig.Service.EndAsync(id, esi)).Succeeded());
        Assert.False((await rig.Service.ReportAsync(id, new("Other", null), esi)).Succeeded());
    }

    [Fact]
    public async Task Messages_are_unread_until_opened_and_a_busy_chat_is_one_notice()
    {
        var rig = new Rig();
        var id = await rig.Connected();
        var before = rig.Notices;

        await rig.Service.SendMessageAsync(id, "one", rig.Ama);
        await rig.Service.SendMessageAsync(id, "two", rig.Ama);
        await rig.Service.SendMessageAsync(id, "three", rig.Ama);
        Assert.Equal(before + 1, rig.Notices);                                       // the first unread message only

        Assert.Equal(1, (await rig.Service.GetSummaryAsync(rig.Kofi)).Data!.UnreadChats);
        Assert.True(Assert.Single((await rig.Service.ListAsync("chats", null, 25, null, rig.Kofi)).Data!.Items).Unread);
        Assert.Equal(0, (await rig.Service.GetSummaryAsync(rig.Ama)).Data!.UnreadChats);   // your own messages are never unread to you

        var opened = (await rig.Service.GetAsync(id, null, rig.Kofi)).Data!;
        Assert.Equal(new[] { "one", "two", "three" }, opened.Messages.Select(m => m.Body));
        Assert.Equal(0, (await rig.Service.GetSummaryAsync(rig.Kofi)).Data!.UnreadChats);   // opening it marked it read

        await rig.Service.SendMessageAsync(id, "reply", rig.Kofi);
        Assert.Equal(before + 2, rig.Notices);                                       // Ama had caught up, so Kofi's reply is announced
    }

    [Fact]
    public async Task Polling_returns_only_newer_messages()
    {
        var rig = new Rig();
        var id = await rig.Connected();
        var first = (await rig.Service.SendMessageAsync(id, "one", rig.Ama)).Data!;
        await Task.Delay(5);
        await rig.Service.SendMessageAsync(id, "two", rig.Ama);
        var newer = (await rig.Service.GetAsync(id, first.CreatedAt, rig.Kofi)).Data!.Messages;
        Assert.Equal("two", Assert.Single(newer).Body);
    }

    [Fact]
    public async Task A_blocked_conversation_stays_readable_for_the_one_who_blocked_and_can_be_unblocked_from_inside()
    {
        var rig = new Rig();
        var id = await rig.Connected();
        await rig.Service.SendMessageAsync(id, "before the block", rig.Ama);
        await rig.Service.BlockAsync("ama", rig.Kofi);

        // Kofi, who blocked, still has the conversation in his list and can read it, but cannot send.
        var mine = Assert.Single((await rig.Service.ListAsync("archived", null, 25, null, rig.Kofi)).Data!.Items);
        Assert.Equal("blocked", mine.Condition);
        var open = (await rig.Service.GetAsync(id, null, rig.Kofi)).Data!;
        Assert.False(open.CanSend);
        Assert.Equal("blocked", open.Item.Condition);
        Assert.Equal("before the block", Assert.Single(open.Messages).Body);
        Assert.False((await rig.Service.SendMessageAsync(id, "no", rig.Kofi)).Succeeded());
        Assert.Equal("blocked", (await rig.Service.GetStateAsync("ama", rig.Kofi)).Data!.State);

        // Unblocking from the conversation restores it exactly as it was.
        await rig.Service.UnblockAsync("ama", rig.Kofi);
        Assert.True((await rig.Service.GetAsync(id, null, rig.Kofi)).Data!.CanSend);
        Assert.True((await rig.Service.SendMessageAsync(id, "back", rig.Kofi)).Succeeded());
        Assert.Equal("open", Assert.Single((await rig.Service.ListAsync("chats", null, 25, null, rig.Ama)).Data!.Items).Condition);
    }

    [Fact]
    public async Task The_person_who_was_blocked_sees_a_neutral_unavailable_conversation_and_is_never_told_why()
    {
        var rig = new Rig();
        var id = await rig.Connected();
        await rig.Service.SendMessageAsync(id, "hello", rig.Ama);
        await rig.Service.BlockAsync("ama", rig.Kofi);

        var theirs = Assert.Single((await rig.Service.ListAsync("archived", null, 25, null, rig.Ama)).Data!.Items);
        Assert.Equal("unavailable", theirs.Condition);                   // not "blocked"
        var view = (await rig.Service.GetAsync(id, null, rig.Ama)).Data!;
        Assert.False(view.CanSend);
        Assert.Equal("hello", Assert.Single(view.Messages).Body);          // history stays readable
        Assert.Null(view.OtherReadAt);                                   // no "Seen" leaks across a block

        var failed = await rig.Service.SendMessageAsync(id, "are you there?", rig.Ama);
        Assert.False(failed.Succeeded());
        Assert.Equal("You can't send messages in this conversation right now.", failed.Message);
        Assert.Equal("unavailable", (await rig.Service.GetStateAsync("kofi", rig.Ama)).Data!.State);
        Assert.False((await rig.Service.RequestAsync(new("kofi", Note), rig.Ama)).Succeeded());

        using var db = TestDb.Create(rig.DbName, Tenant);
        Assert.Single(db.Set<IntroductionMessage>().ToList());            // nothing was stored from the failed attempt
    }

    [Fact]
    public async Task Nothing_flows_in_either_direction_while_blocked_and_resumes_after()
    {
        var rig = new Rig();
        var id = await rig.Connected();
        await rig.Service.BlockAsync("kofi", rig.Ama);
        Assert.False((await rig.Service.SendMessageAsync(id, "from the blocker", rig.Ama)).Succeeded());
        Assert.False((await rig.Service.SendMessageAsync(id, "from the blocked", rig.Kofi)).Succeeded());
        Assert.Equal(0, (await rig.Service.GetSummaryAsync(rig.Kofi)).Data!.UnreadChats);
        await rig.Service.UnblockAsync("kofi", rig.Ama);
        Assert.True((await rig.Service.SendMessageAsync(id, "after", rig.Kofi)).Succeeded());
    }

    [Fact]
    public async Task A_request_between_people_who_have_blocked_each_other_is_never_shown()
    {
        var rig = new Rig();
        var id = (await rig.Service.RequestAsync(new("kofi", Note), rig.Ama)).Data!.IntroductionId!;
        await rig.Service.BlockAsync("ama", rig.Kofi);
        Assert.Empty((await rig.Service.ListAsync("incoming", null, 25, null, rig.Kofi)).Data!.Items);
        Assert.Empty((await rig.Service.ListAsync("sent", null, 25, null, rig.Ama)).Data!.Items);
        Assert.False((await rig.Service.GetAsync(id, null, rig.Kofi)).Succeeded());
        Assert.False((await rig.Service.GetAsync(id, null, rig.Ama)).Succeeded());
    }

    [Fact]
    public async Task Whoever_ended_a_conversation_can_resume_it_and_the_other_person_cannot()
    {
        var rig = new Rig();
        var id = await rig.Connected();
        await rig.Service.SendMessageAsync(id, "hello", rig.Ama);
        await rig.Service.EndAsync(id, rig.Kofi);

        // Both still see it in their list, marked ended, and can read it.
        var asEnder = Assert.Single((await rig.Service.ListAsync("archived", null, 25, null, rig.Kofi)).Data!.Items);
        Assert.Equal(("ended", true), (asEnder.Condition, asEnder.EndedByMe));
        var asOther = Assert.Single((await rig.Service.ListAsync("archived", null, 25, null, rig.Ama)).Data!.Items);
        Assert.Equal(("ended", false), (asOther.Condition, asOther.EndedByMe));
        Assert.Equal("resumable", (await rig.Service.GetStateAsync("ama", rig.Kofi)).Data!.State);
        Assert.Equal("reconnectable", (await rig.Service.GetStateAsync("kofi", rig.Ama)).Data!.State);   // the other person can ask, and the same chat comes back

        Assert.False((await rig.Service.ResumeAsync(id, rig.Ama)).Succeeded());                   // not theirs to reopen
        Assert.True((await rig.Service.ResumeAsync(id, rig.Kofi)).Succeeded());
        Assert.True((await rig.Service.GetAsync(id, null, rig.Ama)).Data!.CanSend);
        Assert.True((await rig.Service.SendMessageAsync(id, "welcome back", rig.Ama)).Succeeded());
        Assert.Equal("hello", (await rig.Service.GetAsync(id, null, rig.Kofi)).Data!.Messages[0].Body);   // the whole history survived
        Assert.False((await rig.Service.ResumeAsync(id, rig.Kofi)).Succeeded());                  // already open
    }

    [Fact]
    public async Task A_conversation_cannot_be_resumed_while_blocked_or_after_the_other_person_left()
    {
        var rig = new Rig();
        var id = await rig.Connected();
        await rig.Service.EndAsync(id, rig.Kofi);
        await rig.Service.BlockAsync("ama", rig.Kofi);
        Assert.False((await rig.Service.ResumeAsync(id, rig.Kofi)).Succeeded());
        await rig.Service.UnblockAsync("ama", rig.Kofi);
        await rig.Edit(m => m.Status = "Deleted", "ama");
        Assert.False((await rig.Service.ResumeAsync(id, rig.Kofi)).Succeeded());
    }

    [Fact]
    public async Task A_pending_request_reports_when_it_was_sent()
    {
        var rig = new Rig();
        var created = (await rig.Service.RequestAsync(new("kofi", Note), rig.Ama)).Data!;
        var state = (await rig.Service.GetStateAsync("kofi", rig.Ama)).Data!;
        Assert.Equal("pending", state.State);
        Assert.NotNull(state.AskedAt);
        Assert.Equal(created.IntroductionId, state.IntroductionId);
    }

    [Fact]
    public async Task Ending_a_conversation_stops_messages_but_keeps_the_history_readable()
    {
        var rig = new Rig();
        var id = await rig.Connected();
        await rig.Service.SendMessageAsync(id, "hello", rig.Ama);
        Assert.True((await rig.Service.EndAsync(id, rig.Kofi)).Succeeded());
        Assert.False((await rig.Service.SendMessageAsync(id, "more", rig.Ama)).Succeeded());
        var view = (await rig.Service.GetAsync(id, null, rig.Ama)).Data!;
        Assert.False(view.CanSend);
        Assert.Single(view.Messages);
    }

    [Fact]
    public async Task Messages_are_limited_in_length_and_rate()
    {
        var rig = new Rig();
        var id = await rig.Connected();
        Assert.False((await rig.Service.SendMessageAsync(id, "   ", rig.Ama)).Succeeded());
        Assert.False((await rig.Service.SendMessageAsync(id, new string('x', 2001), rig.Ama)).Succeeded());
        for (var n = 0; n < MemberIntroductionService.MessagesPerMinute; n++)
            Assert.True((await rig.Service.SendMessageAsync(id, $"m{n}", rig.Ama)).Succeeded());
        Assert.False((await rig.Service.SendMessageAsync(id, "one too many", rig.Ama)).Succeeded());
    }

    [Fact]
    public async Task A_report_carries_the_recent_messages_to_the_admins_and_only_once()
    {
        var rig = new Rig();
        var id = await rig.Connected();
        await rig.Service.SendMessageAsync(id, "send me money please", rig.Kofi);

        Assert.True((await rig.Service.ReportAsync(id, new("Scam or fraud", "He asked for money"), rig.Ama)).Succeeded());
        Assert.True((await rig.Service.ReportAsync(id, new("Scam or fraud", null), rig.Ama)).Succeeded());
        Assert.False((await rig.Service.ReportAsync(id, new("Not a reason", null), rig.Ama)).Succeeded());

        using var db = TestDb.Create(rig.DbName, Tenant);
        var report = Assert.Single(db.ContentReports.ToList());
        Assert.Equal(ContentReportTypes.Introduction, report.EntityType);
        Assert.Contains("Kofi Boateng: send me money please", report.Details);
        Assert.Contains("He asked for money", report.Details);
        Assert.Contains("Ama Mensah", report.EntityTitle);
    }

    [Fact]
    public async Task Names_come_from_the_live_member_and_never_include_an_email()
    {
        var rig = new Rig();
        await rig.Connected();
        await rig.Edit(m => m.LastName = "Mensah-Quaye", "ama");
        var other = Assert.Single((await rig.Service.ListAsync("chats", null, 25, null, rig.Kofi)).Data!.Items).Other;
        Assert.Equal("Ama Mensah-Quaye", other.Name);
        Assert.DoesNotContain("@", System.Text.Json.JsonSerializer.Serialize(other));
    }

    [Fact]
    public async Task A_headline_shows_job_and_company_only_when_the_member_shows_their_company()
    {
        var rig = new Rig();
        await rig.Edit(m => { m.JobTitle = "Data Engineer"; m.Company = "MTN Ghana"; m.ShowCompanyOnDirectory = true; }, "kofi");
        await rig.Connected();
        Assert.Equal("Data Engineer at MTN Ghana", Assert.Single((await rig.Service.ListAsync("chats", null, 25, null, rig.Ama)).Data!.Items).Other.Headline);

        await rig.Edit(m => m.ShowCompanyOnDirectory = false, "kofi");
        Assert.Null(Assert.Single((await rig.Service.ListAsync("chats", null, 25, null, rig.Ama)).Data!.Items).Other.Headline);
    }

    [Fact]
    public async Task The_chat_says_when_the_other_person_last_read_it()
    {
        var rig = new Rig();
        var id = await rig.Connected();
        await rig.Service.SendMessageAsync(id, "hello", rig.Ama);
        Assert.Null((await rig.Service.GetAsync(id, null, rig.Ama)).Data!.OtherReadAt);          // Kofi has not opened it yet
        await rig.Service.GetAsync(id, null, rig.Kofi);
        Assert.NotNull((await rig.Service.GetAsync(id, null, rig.Ama)).Data!.OtherReadAt);        // now he has
    }

    [Fact]
    public async Task Opening_a_conversation_clears_its_notices_in_the_bell_and_only_its_own()
    {
        var rig = new Rig();
        var id = await rig.Connected();
        using (var db = TestDb.Create(rig.DbName, Tenant))
        {
            db.Add(new Notification { InstitutionId = Tenant, RecipientId = "kofi", Title = "New message", Type = "IntroductionUpdate", RelatedEntityType = "Introduction", RelatedEntityId = "a-message-id", ActionUrl = $"https://umat.members.test/connections/{id}" });
            db.Add(new Notification { InstitutionId = Tenant, RecipientId = "kofi", Title = "Other", Type = "IntroductionUpdate", RelatedEntityType = "Introduction", RelatedEntityId = "some-other" });
            db.Add(new Notification { InstitutionId = Tenant, RecipientId = "ama", Title = "Not yours", Type = "IntroductionUpdate", RelatedEntityType = "Introduction", RelatedEntityId = id, ActionUrl = $"https://umat.members.test/connections/{id}" });
            await db.SaveChangesAsync();
        }
        rig.Fresh();
        await rig.Service.GetAsync(id, null, rig.Kofi);
        using var check = TestDb.Create(rig.DbName, Tenant);
        Assert.True(check.Notifications.Single(n => n.Title == "New message").IsRead);
        Assert.False(check.Notifications.Single(n => n.Title == "Other").IsRead);
        Assert.False(check.Notifications.Single(n => n.Title == "Not yours").IsRead);
    }

    [Fact]
    public async Task Asking_to_reconnect_after_a_conversation_was_ended_reopens_the_same_chat_with_all_its_messages()
    {
        var rig = new Rig();
        var chat = await rig.Connected();
        await rig.Service.SendMessageAsync(chat, "our first chat", rig.Ama);
        await rig.Service.EndAsync(chat, rig.Kofi);                                                        // Kofi ends it

        Assert.Equal("reconnectable", (await rig.Service.GetStateAsync("kofi", rig.Ama)).Data!.State);     // Ama cannot reopen it herself, but can ask
        Assert.Equal("resumable", (await rig.Service.GetStateAsync("ama", rig.Kofi)).Data!.State);
        var request = (await rig.Service.RequestAsync(new("kofi", "Hello again, I would like to reconnect."), rig.Ama)).Data!.IntroductionId!;

        // Kofi sees it as a request to pick the earlier conversation back up.
        var waiting = Assert.Single((await rig.Service.ListAsync("incoming", null, 25, null, rig.Kofi)).Data!.Items);
        Assert.Equal(chat, waiting.ReopensIntroductionId);
        Assert.Equal(chat, (await rig.Service.GetAsync(request, null, rig.Kofi)).Data!.Item.ReopensIntroductionId);

        var answer = await rig.Service.RespondAsync(request, true, rig.Kofi);
        Assert.Equal(chat, ((IntroductionTargetDto)answer.Data!).IntroductionId);                           // the same conversation, not a new one

        var chats = (await rig.Service.ListAsync("chats", null, 25, null, rig.Ama)).Data!.Items;
        var only = Assert.Single(chats);                                                                    // one entry in the list, not two
        Assert.Equal((chat, "open"), (only.Id, only.Condition));
        Assert.Equal("our first chat", (await rig.Service.GetAsync(chat, null, rig.Ama)).Data!.Messages[0].Body);   // history intact
        Assert.True((await rig.Service.SendMessageAsync(chat, "good to be back", rig.Ama)).Succeeded());
        Assert.Empty((await rig.Service.ListAsync("incoming", null, 25, null, rig.Kofi)).Data!.Items);
        Assert.Empty((await rig.Service.ListAsync("sent", null, 25, null, rig.Ama)).Data!.Items);                                // the request itself is retired
        Assert.False((await rig.Service.RespondAsync(request, true, rig.Kofi)).Succeeded());              // and cannot be answered twice
    }

    [Fact]
    public async Task Declining_a_request_to_reconnect_leaves_the_earlier_conversation_ended_and_unharmed()
    {
        var rig = new Rig();
        var chat = await rig.Connected();
        await rig.Service.SendMessageAsync(chat, "hello", rig.Ama);
        await rig.Service.EndAsync(chat, rig.Kofi);
        var request = (await rig.Service.RequestAsync(new("kofi", "Hello again, I would like to reconnect."), rig.Ama)).Data!.IntroductionId!;
        Assert.True((await rig.Service.RespondAsync(request, false, rig.Kofi)).Succeeded());

        var view = (await rig.Service.GetAsync(chat, null, rig.Ama)).Data!;
        Assert.Equal("ended", view.Item.Condition);
        Assert.False(view.CanSend);
        Assert.Equal("hello", Assert.Single(view.Messages).Body);
        Assert.Equal("pending", (await rig.Service.GetStateAsync("kofi", rig.Ama)).Data!.State);           // she is simply left waiting, never told
    }

    [Fact]
    public async Task The_person_who_ended_a_conversation_resumes_it_instead_of_asking()
    {
        var rig = new Rig();
        var chat = await rig.Connected();
        await rig.Service.EndAsync(chat, rig.Kofi);
        var asked = await rig.Service.RequestAsync(new("ama", "Hello again, I would like to reconnect."), rig.Kofi);
        Assert.False(asked.Succeeded());
        Assert.Contains("resume", asked.Message);
        Assert.True((await rig.Service.ResumeAsync(chat, rig.Kofi)).Succeeded());
    }
}
