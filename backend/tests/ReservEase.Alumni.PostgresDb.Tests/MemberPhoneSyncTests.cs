using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.TestKit;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.PostgresDb.Tests;

/// <summary>The normalised phone and its verification follow the typed phone on every write path, with no caller remembering to.</summary>
public class MemberPhoneSyncTests
{
    private const string Tenant = "inst-1";

    private static MemberEntity Person(string phone, DateTime? verifiedAt = null) => new()
    {
        Id = "m1", InstitutionId = Tenant, FirstName = "A", LastName = "B", Email = "a@x.com", Phone = phone, PhoneVerifiedAt = verifiedAt,
    };

    [Fact]
    public async Task A_new_member_gets_a_normalised_number_whatever_way_it_was_typed()
    {
        var name = TestDb.NewName();
        await using (var db = TestDb.Create(name, Tenant)) { db.Add(Person("024 412 3456")); await db.SaveChangesAsync(); }

        await using var check = TestDb.Create(name, Tenant);
        Assert.Equal("233244123456", check.Set<MemberEntity>().Single().PhoneNormalized);
    }

    [Fact]
    public async Task Changing_the_number_updates_the_normalised_form_and_clears_the_verification()
    {
        var name = TestDb.NewName();
        await using (var db = TestDb.Create(name, Tenant)) { db.Add(Person("0244123456", DateTime.UtcNow)); await db.SaveChangesAsync(); }

        await using (var db = TestDb.Create(name, Tenant))
        {
            var m = db.Set<MemberEntity>().Single();
            Assert.NotNull(m.PhoneVerifiedAt);
            m.Phone = "0501234567";
            await db.SaveChangesAsync();
        }

        await using var check = TestDb.Create(name, Tenant);
        var after = check.Set<MemberEntity>().Single();
        Assert.Equal(("233501234567", null), (after.PhoneNormalized, after.PhoneVerifiedAt));
    }

    [Fact]
    public async Task Saving_something_else_leaves_the_verification_alone()
    {
        var name = TestDb.NewName();
        await using (var db = TestDb.Create(name, Tenant)) { db.Add(Person("0244123456", DateTime.UtcNow)); await db.SaveChangesAsync(); }

        await using (var db = TestDb.Create(name, Tenant)) { db.Set<MemberEntity>().Single().FirstName = "Renamed"; await db.SaveChangesAsync(); }

        await using var check = TestDb.Create(name, Tenant);
        Assert.NotNull(check.Set<MemberEntity>().Single().PhoneVerifiedAt);
    }

    [Fact]
    public async Task Verifying_in_the_same_save_that_sets_the_number_is_kept()
    {
        var name = TestDb.NewName();
        await using (var db = TestDb.Create(name, Tenant)) { db.Add(Person("0244123456")); await db.SaveChangesAsync(); }
        var when = new DateTime(2026, 10, 11, 9, 0, 0, DateTimeKind.Utc);

        await using (var db = TestDb.Create(name, Tenant))
        {
            var m = db.Set<MemberEntity>().Single();
            m.Phone = "233501234567";   // the number the WhatsApp message actually came from
            m.PhoneVerifiedAt = when;
            await db.SaveChangesAsync();
        }

        await using var check = TestDb.Create(name, Tenant);
        var after = check.Set<MemberEntity>().Single();
        Assert.Equal(("233501234567", when), (after.PhoneNormalized, after.PhoneVerifiedAt));
    }

    [Fact]
    public async Task A_number_that_cannot_be_a_phone_normalises_to_nothing()
    {
        var name = TestDb.NewName();
        await using (var db = TestDb.Create(name, Tenant)) { db.Add(Person("n/a")); await db.SaveChangesAsync(); }

        await using var check = TestDb.Create(name, Tenant);
        Assert.Null(check.Set<MemberEntity>().Single().PhoneNormalized);
    }
}
