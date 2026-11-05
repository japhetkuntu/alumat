using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ReservEase.Alumni.Member.Api.Services.Security;
using ReservEase.Alumni.PostgresDb.Sdk.DbContexts;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using ReservEase.Alumni.TestKit;

namespace ReservEase.Alumni.Member.Api.Tests;

public class MessageEncryptionTests
{
    public static string NewKey() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    public static IMessageProtector NewProtector(string id = "k1", string? key = null)
        => new AesGcmMessageProtector(new MessageEncryptionOptions { CurrentKeyId = id, Keys = { [id] = key ?? Convert.ToBase64String(new byte[32].Select((_, i) => (byte)(i + 7)).ToArray()) } });

    [Theory]
    [InlineData("Hello")]
    [InlineData("Café Kwae, Thursday 4pm ☕ 👋 — “quoted”")]
    [InlineData("line one\nline two\n\ttabbed")]
    public void Text_survives_a_round_trip_exactly(string text)
    {
        var p = NewProtector();
        var stored = p.Protect(text, "ctx");
        Assert.True(p.IsProtected(stored));
        Assert.DoesNotContain(text.Split(' ')[0], stored);
        Assert.Equal(text, p.Unprotect(stored, "ctx"));
    }

    [Fact]
    public void The_same_text_never_looks_the_same_twice()
    {
        var p = NewProtector();
        Assert.NotEqual(p.Protect("same words", "ctx"), p.Protect("same words", "ctx"));
    }

    [Fact]
    public void Text_cannot_be_read_in_another_place_or_after_a_change()
    {
        var p = NewProtector();
        var stored = p.Protect("private", "intro:1:message:a");
        Assert.ThrowsAny<CryptographicException>(() => p.Unprotect(stored, "intro:1:message:b"));   // moved to another message
        var tampered = stored[..^3] + (stored[^3] == 'A' ? "BBB" : "AAA");
        Assert.ThrowsAny<Exception>(() => p.Unprotect(tampered, "intro:1:message:a"));             // altered
        Assert.ThrowsAny<CryptographicException>(() => NewProtector("k1", NewKey()).Unprotect(stored, "intro:1:message:a"));   // wrong key
    }

    [Fact]
    public void Text_saved_before_encryption_existed_still_reads_and_is_not_mistaken_for_ciphertext()
    {
        var p = NewProtector();
        Assert.False(p.IsProtected("Thank you for accepting!"));
        Assert.Equal("Thank you for accepting!", p.Unprotect("Thank you for accepting!", "ctx"));
        Assert.Equal("v1.k1.looks-similar", p.Unprotect("v1.k1.looks-similar", "ctx"));   // too short to be a real value
    }

    [Fact]
    public void A_key_can_be_replaced_without_losing_older_messages()
    {
        var oldKey = NewKey();
        var before = NewProtector("k1", oldKey).Protect("written last year", "ctx");
        var rotated = new AesGcmMessageProtector(new MessageEncryptionOptions { CurrentKeyId = "k2", Keys = { ["k1"] = oldKey, ["k2"] = NewKey() } });
        Assert.Equal("written last year", rotated.Unprotect(before, "ctx"));
        Assert.StartsWith("v1.k2.", rotated.Protect("written today", "ctx"));
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("k1", "")]
    [InlineData("k9", "k1")]
    public void A_missing_or_wrong_setup_refuses_to_start(string current, string listed)
    {
        var keys = listed == "" ? new Dictionary<string, string>() : new Dictionary<string, string> { [listed] = NewKey() };
        Assert.Throws<InvalidOperationException>(() => new AesGcmMessageProtector(new MessageEncryptionOptions { CurrentKeyId = current, Keys = keys }));
        Assert.Throws<InvalidOperationException>(() => new AesGcmMessageProtector(new MessageEncryptionOptions { CurrentKeyId = "k1", Keys = { ["k1"] = Convert.ToBase64String(new byte[16]) } }));   // too short
        Assert.Throws<InvalidOperationException>(() => new AesGcmMessageProtector(new MessageEncryptionOptions { CurrentKeyId = "k1", Keys = { ["k1"] = "not base64!" } }));
    }

    // ── What actually lands in the database ──────────────────────────────────────

    private const string Tenant = "inst-1";

    [Fact]
    public async Task The_database_holds_only_ciphertext_and_members_still_read_plain_text()
    {
        var rig = new IntroductionServiceTests.RigAccess();
        var id = await rig.ConnectedAsync();
        await rig.Service.SendMessageAsync(id, "My salary expectation is GH₵ 14,000", rig.Ama);

        using var db = TestDb.Create(rig.DbName, Tenant);
        var intro = db.Set<Introduction>().Single();
        var message = db.Set<IntroductionMessage>().Single();
        Assert.StartsWith("v1.", intro.Reason);
        Assert.StartsWith("v1.", intro.LastMessagePreview);
        Assert.StartsWith("v1.", message.Body);
        Assert.DoesNotContain("salary", message.Body + intro.LastMessagePreview);
        Assert.DoesNotContain("moving into data work", intro.Reason);

        var read = (await rig.Service.GetAsync(id, null, rig.Kofi)).Data!;
        Assert.Equal("My salary expectation is GH₵ 14,000", Assert.Single(read.Messages).Body);
        Assert.Contains("moving into data work", read.Item.Reason);
        Assert.StartsWith("My salary expectation", Assert.Single((await rig.Service.ListAsync("chats", rig.Kofi)).Data!).LastMessagePreview);
    }

    [Fact]
    public async Task A_message_copied_onto_another_message_is_refused_instead_of_shown()
    {
        var rig = new IntroductionServiceTests.RigAccess();
        var id = await rig.ConnectedAsync();
        await rig.Service.SendMessageAsync(id, "first, secret", rig.Ama);
        await Task.Delay(5);
        await rig.Service.SendMessageAsync(id, "second", rig.Ama);

        using (var db = TestDb.Create(rig.DbName, Tenant))
        {
            var rows = db.Set<IntroductionMessage>().OrderBy(m => m.CreatedAt).ToList();
            rows[1].Body = rows[0].Body;       // someone with database access pastes one message over another
            await db.SaveChangesAsync();
        }
        rig.Fresh();
        var shown = (await rig.Service.GetAsync(id, null, rig.Kofi)).Data!.Messages;
        Assert.DoesNotContain(shown, m => m.Body.Contains("secret") && m.Body != "first, secret");
        Assert.Contains(shown, m => m.Body == "[This message can't be shown]");
    }

    [Fact]
    public async Task A_report_carries_the_readable_text_not_the_scrambled_one()
    {
        var rig = new IntroductionServiceTests.RigAccess();
        var id = await rig.ConnectedAsync();
        await rig.Service.SendMessageAsync(id, "please send money", rig.Kofi);
        await rig.Service.ReportAsync(id, new("Scam or fraud", null), rig.Ama);
        using var db = TestDb.Create(rig.DbName, Tenant);
        var details = db.Set<ContentReport>().Single().Details!;
        Assert.Contains("please send money", details);
        Assert.Contains("moving into data work", details);
        Assert.DoesNotContain("v1.", details);
    }

    [Fact]
    public async Task Earlier_plain_text_is_encrypted_once_at_start_and_a_second_run_changes_nothing()
    {
        var name = TestDb.NewName();
        using (var db = TestDb.Create(name, Tenant))
        {
            db.Add(new Introduction { Id = "i1", InstitutionId = Tenant, RequesterId = "a", RecipientId = "b", Reason = "Old plain reason for connecting", LastMessagePreview = "old preview", Status = IntroductionStatuses.Accepted });
            db.Add(new IntroductionMessage { Id = "m1", InstitutionId = Tenant, IntroductionId = "i1", SenderId = "a", Body = "old plain message" });
            await db.SaveChangesAsync();
        }
        var protector = NewProtector();
        var services = new ServiceCollection();
        services.AddScoped(_ => TestDb.Create(name, null));
        services.AddScoped<IAlumniPgRepository<Introduction>>(sp => new AlumniPgRepository<Introduction>(sp.GetRequiredService<AlumniDbContext>()));
        services.AddScoped<IAlumniPgRepository<IntroductionMessage>>(sp => new AlumniPgRepository<IntroductionMessage>(sp.GetRequiredService<AlumniDbContext>()));
        var scopes = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

        for (var run = 0; run < 2; run++)
        {
            var job = new IntroductionEncryptionBackfill(scopes, protector, NullLogger<IntroductionEncryptionBackfill>.Instance);
            await job.StartAsync(CancellationToken.None);
            await job.ExecuteTask!;
        }

        using var check = TestDb.Create(name, Tenant);
        var intro = check.Set<Introduction>().Single();
        var msg = check.Set<IntroductionMessage>().Single();
        Assert.True(protector.IsProtected(intro.Reason) && protector.IsProtected(intro.LastMessagePreview) && protector.IsProtected(msg.Body));
        Assert.Equal("Old plain reason for connecting", protector.Unprotect(intro.Reason, "intro:i1:reason"));
        Assert.Equal("old plain message", protector.Unprotect(msg.Body, "intro:i1:message:m1"));
        Assert.Equal(2, intro.Reason.Split('.').Length - 1);   // encrypted exactly once, not twice
    }
}
