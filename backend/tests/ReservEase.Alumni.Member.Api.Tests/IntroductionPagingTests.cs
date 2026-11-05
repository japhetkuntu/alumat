using ReservEase.Alumni.Member.Api.Services.Security;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.TestKit;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.Member.Api.Tests;

/// <summary>Lists and conversations are read one page at a time, so a member with thousands of connections or messages costs no more than one with a few.</summary>
public class IntroductionPagingTests
{
    private const string Tenant = "inst-1";

    /// <summary>Counts how much text is decrypted, which is the work a page of messages should be limited to.</summary>
    private sealed class CountingProtector(IMessageProtector inner) : IMessageProtector
    {
        public int Unprotected;
        public string Protect(string plaintext, string context) => inner.Protect(plaintext, context);
        public string Unprotect(string stored, string context) { Interlocked.Increment(ref Unprotected); return inner.Unprotect(stored, context); }
        public bool IsProtected(string? stored) => inner.IsProtected(stored);
    }

    /// <summary>Ama with many accepted conversations, each with a different person and a distinct, increasing last activity.</summary>
    private static async Task<IntroductionServiceTests.RigAccess> ManyConnectionsAsync(int count)
    {
        var rig = new IntroductionServiceTests.RigAccess();
        using var db = TestDb.Create(rig.DbName, Tenant);
        var start = DateTime.UtcNow.AddDays(-count);
        for (var n = 0; n < count; n++)
        {
            db.Add(new MemberEntity { Id = $"p{n:D4}", InstitutionId = Tenant, FirstName = n % 7 == 0 ? "Zainab" : "Person", LastName = $"Number{n:D4}", Email = $"p{n}@x.com", Status = "Active", GraduationYear = 2010 });
            db.Add(new Introduction { Id = $"c{n:D4}", InstitutionId = Tenant, RequesterId = n % 2 == 0 ? "ama" : $"p{n:D4}", RecipientId = n % 2 == 0 ? $"p{n:D4}" : "ama", Reason = "A reason", Status = IntroductionStatuses.Accepted, LastActivityAt = start.AddDays(n), CreatedAt = start.AddDays(n) });
        }
        await db.SaveChangesAsync();
        rig.Fresh();
        return rig;
    }

    [Fact]
    public async Task A_long_list_is_read_in_pages_newest_first_with_every_connection_seen_exactly_once()
    {
        var rig = await ManyConnectionsAsync(130);
        var seen = new List<string>();
        string? cursor = null;
        var pages = 0;
        do
        {
            var page = (await rig.Service.ListAsync("chats", cursor, 25, null, rig.Ama)).Data!;
            Assert.True(page.Items.Count <= 25);
            seen.AddRange(page.Items.Select(i => i.Id));
            cursor = page.NextCursor;
            pages++;
        } while (cursor is not null);

        Assert.Equal(6, pages);                                         // 130 connections at 25 a page
        Assert.Equal(130, seen.Count);
        Assert.Equal(130, seen.Distinct().Count());                     // none repeated, none skipped
        Assert.Equal(seen.OrderByDescending(x => x).ToList(), seen);    // the newest activity first, all the way down
    }

    [Fact]
    public async Task The_page_size_is_capped_and_a_bad_cursor_just_starts_from_the_top()
    {
        var rig = await ManyConnectionsAsync(80);
        Assert.Equal(50, (await rig.Service.ListAsync("chats", null, 5000, null, rig.Ama)).Data!.Items.Count);
        var first = (await rig.Service.ListAsync("chats", "this-is-not-a-cursor", 10, null, rig.Ama)).Data!;
        Assert.Equal(10, first.Items.Count);
        Assert.Equal("c0079", first.Items[0].Id);
    }

    [Fact]
    public async Task Activity_moves_a_connection_to_the_top_without_disturbing_the_paging()
    {
        var rig = await ManyConnectionsAsync(60);
        var page1 = (await rig.Service.ListAsync("chats", null, 25, null, rig.Ama)).Data!;
        // The oldest conversation gets a message while the member is scrolling.
        await rig.Service.SendMessageAsync("c0000", "hello from the past", rig.Ama);
        var page2 = (await rig.Service.ListAsync("chats", page1.NextCursor, 25, null, rig.Ama)).Data!;
        Assert.DoesNotContain(page2.Items, i => page1.Items.Any(p => p.Id == i.Id));
        Assert.Equal("c0000", (await rig.Service.ListAsync("chats", null, 25, null, rig.Ama)).Data!.Items[0].Id);   // and a fresh look shows it first
    }

    [Fact]
    public async Task Searching_by_name_finds_matches_across_all_pages_without_loading_the_rest()
    {
        var rig = await ManyConnectionsAsync(100);   // every 7th is a "Zainab": 15 of them
        var found = new List<string>();
        string? cursor = null;
        do
        {
            var page = (await rig.Service.ListAsync("chats", cursor, 10, "zainab", rig.Ama)).Data!;
            found.AddRange(page.Items.Select(i => i.Other.Name));
            cursor = page.NextCursor;
        } while (cursor is not null);
        Assert.Equal(15, found.Count);
        Assert.All(found, n => Assert.StartsWith("Zainab", n));
        Assert.Empty((await rig.Service.ListAsync("chats", null, 10, "nobody by this name", rig.Ama)).Data!.Items);
    }

    [Fact]
    public async Task Ended_and_blocked_conversations_have_their_own_list_and_count()
    {
        var rig = await ManyConnectionsAsync(30);
        await rig.Service.EndAsync("c0010", rig.Ama);
        await rig.Service.BlockAsync("p0011", rig.Ama);
        var summary = (await rig.Service.GetSummaryAsync(rig.Ama)).Data!;
        Assert.Equal(2, summary.ArchivedChats);
        var archived = (await rig.Service.ListAsync("archived", null, 25, null, rig.Ama)).Data!.Items;
        Assert.Equal(new[] { "c0010", "c0011" }, archived.Select(i => i.Id));   // the one ended just now is the most recent activity
        Assert.DoesNotContain((await rig.Service.ListAsync("chats", null, 50, null, rig.Ama)).Data!.Items, i => i.Id is "c0010" or "c0011");
    }

    [Fact]
    public async Task The_unread_count_is_counted_not_loaded_and_only_counts_what_is_unread()
    {
        var rig = await ManyConnectionsAsync(40);
        using (var db = TestDb.Create(rig.DbName, Tenant))
        {
            foreach (var i in db.Set<Introduction>().Where(x => x.Id.CompareTo("c0030") >= 0).ToList())
            { i.LastMessageAt = DateTime.UtcNow; i.LastMessageSenderId = i.RequesterId == "ama" ? i.RecipientId : i.RequesterId; }
            await db.SaveChangesAsync();
        }
        rig.Fresh();
        Assert.Equal(10, (await rig.Service.GetSummaryAsync(rig.Ama)).Data!.UnreadChats);
        await rig.Service.GetAsync("c0035", null, rig.Ama);                       // opening one reads it
        Assert.Equal(9, (await rig.Service.GetSummaryAsync(rig.Ama)).Data!.UnreadChats);
    }

    // ── A conversation with a long history ─────────────────────────────────────

    private static async Task<(IntroductionServiceTests.RigAccess Rig, CountingProtector Counter, string Id)> LongChatAsync(int messages)
    {
        var counter = new CountingProtector(MessageEncryptionTests.NewProtector());
        var rig = new IntroductionServiceTests.RigAccess(counter);
        var id = await rig.ConnectedAsync();
        using (var db = TestDb.Create(rig.DbName, Tenant))
        {
            var start = DateTime.UtcNow.AddHours(-messages);
            for (var n = 0; n < messages; n++)
            {
                var m = new IntroductionMessage { Id = $"m{n:D5}", InstitutionId = Tenant, IntroductionId = id, SenderId = n % 2 == 0 ? "ama" : "kofi", CreatedAt = start.AddMinutes(n) };
                m.Body = counter.Protect($"message number {n}", $"intro:{id}:message:{m.Id}");
                db.Add(m);
            }
            await db.SaveChangesAsync();
        }
        rig.Fresh();
        return (rig, counter, id);
    }

    [Fact]
    public async Task Opening_a_long_conversation_reads_and_decrypts_only_the_newest_page()
    {
        var (rig, counter, id) = await LongChatAsync(3000);
        counter.Unprotected = 0;
        var view = (await rig.Service.GetAsync(id, null, rig.Ama)).Data!;
        Assert.Equal(40, view.Messages.Count);
        Assert.True(view.HasMoreBefore);
        Assert.Equal("message number 2999", view.Messages[^1].Body);
        Assert.Equal("message number 2960", view.Messages[0].Body);       // oldest first within the page
        Assert.True(counter.Unprotected <= 41, $"decrypted {counter.Unprotected} texts for one page");   // the page plus the note, never the 3,000
    }

    [Fact]
    public async Task Scrolling_back_through_a_long_history_returns_every_message_once_and_in_order()
    {
        var (rig, _, id) = await LongChatAsync(210);
        var all = new List<string>();
        var view = (await rig.Service.GetAsync(id, null, rig.Ama)).Data!;
        all.InsertRange(0, view.Messages.Select(m => m.Body));
        var guard = 0;
        while (view.HasMoreBefore && guard++ < 20)
        {
            var oldest = view.Messages[0].CreatedAt;
            view = (await rig.Service.GetAsync(id, null, rig.Ama, before: oldest)).Data!;
            all.InsertRange(0, view.Messages.Select(m => m.Body));
        }
        Assert.Equal(210, all.Count);
        Assert.Equal(Enumerable.Range(0, 210).Select(n => $"message number {n}"), all);   // complete, ordered, no duplicates
        Assert.False(view.HasMoreBefore);
    }

    [Fact]
    public async Task Catching_up_after_a_long_absence_returns_newer_messages_in_order_and_says_when_there_are_more()
    {
        var (rig, _, id) = await LongChatAsync(250);
        var first = (await rig.Service.GetAsync(id, null, rig.Ama, limit: 5)).Data!;     // the member last saw the newest five
        // 250 more arrive while they are away.
        using (var db = TestDb.Create(rig.DbName, Tenant))
        {
            var from = first.Messages[^1].CreatedAt;
            for (var n = 0; n < 250; n++)
            {
                var m = new IntroductionMessage { Id = $"n{n:D5}", InstitutionId = Tenant, IntroductionId = id, SenderId = "kofi", CreatedAt = from.AddMinutes(n + 1) };
                m.Body = rig.Protector.Protect($"later {n}", $"intro:{id}:message:{m.Id}");
                db.Add(m);
            }
            await db.SaveChangesAsync();
        }
        rig.Fresh();
        var got = new List<string>();
        var cursor = first.Messages[^1].CreatedAt;
        var rounds = 0;
        for (; ; rounds++)
        {
            var page = (await rig.Service.GetAsync(id, cursor, rig.Ama)).Data!;
            got.AddRange(page.Messages.Select(m => m.Body));
            if (!page.HasMoreAfter) break;
            cursor = page.Messages[^1].CreatedAt;
        }
        Assert.Equal(Enumerable.Range(0, 250).Select(n => $"later {n}"), got);   // nothing lost, in order, across several replies
        Assert.True(rounds >= 2);
    }
}
