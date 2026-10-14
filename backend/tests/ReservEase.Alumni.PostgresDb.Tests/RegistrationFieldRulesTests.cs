using ReservEase.Alumni.PostgresDb.Sdk.Entities;
using ReservEase.Alumni.PostgresDb.Sdk.Services;

namespace ReservEase.Alumni.PostgresDb.Tests;

public class RegistrationFieldRulesTests
{
    private static RegistrationField Text(string label, bool required = false, string id = "") => new() { Id = id, Label = label, Required = required };
    private static RegistrationField Pick(string label, params string[] options) => new() { Label = label, Type = RegistrationFieldTypes.Select, Options = [.. options] };

    [Fact]
    public void New_questions_get_a_stable_id_from_the_label_and_keep_it_when_renamed()
    {
        var first = RegistrationFieldRules.Normalize([Text("Church branch")], out var e1)!;
        Assert.Null(e1);
        Assert.Equal("church-branch", first[0].Id);

        var renamed = RegistrationFieldRules.Normalize([Text("Which branch?", id: first[0].Id)], out _)!;
        Assert.Equal("church-branch", renamed[0].Id);   // answers already given stay attached
    }

    [Fact]
    public void Two_questions_with_the_same_label_do_not_share_an_id_and_blank_ones_are_dropped()
    {
        var list = RegistrationFieldRules.Normalize([Text("Chapter"), Text("Chapter"), Text("  ")], out _)!;
        Assert.Equal(["chapter", "chapter-2"], list.Select(f => f.Id));
    }

    [Fact]
    public void A_choice_question_needs_at_least_two_options_and_the_list_is_capped()
    {
        Assert.Null(RegistrationFieldRules.Normalize([Pick("Region", "North")], out var error));
        Assert.Contains("Region", error);

        var many = Enumerable.Range(1, RegistrationFieldRules.MaxFields + 1).Select(i => Text($"Q{i}"));
        Assert.Null(RegistrationFieldRules.Normalize(many, out _));
    }

    [Fact]
    public void Answers_are_checked_against_the_questions()
    {
        var fields = RegistrationFieldRules.Normalize([Text("Profession", required: true), Pick("Region", "North", "South"), Text("Hobby")], out _)!;

        // Required left blank
        Assert.Null(RegistrationFieldRules.Validate(fields, new Dictionary<string, string> { ["region"] = "north" }, out var missing));
        Assert.Contains("Profession", missing);

        // A choice that is not on the list
        Assert.Null(RegistrationFieldRules.Validate(fields, new Dictionary<string, string> { ["profession"] = "Nurse", ["region"] = "East" }, out var bad));
        Assert.Contains("Region", bad);

        // Good answers are tidied: choice matched to its listed spelling, unknown keys and blanks dropped
        var ok = RegistrationFieldRules.Validate(fields,
            new Dictionary<string, string> { ["profession"] = "  Nurse ", ["region"] = "north", ["hobby"] = " ", ["injected"] = "x" }, out var none)!;
        Assert.Null(none);
        Assert.Equal(new Dictionary<string, string> { ["profession"] = "Nurse", ["region"] = "North" }, ok);
    }
}
