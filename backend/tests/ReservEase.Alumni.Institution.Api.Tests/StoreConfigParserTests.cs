using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.Institution.Api.Services.Implementations;

namespace ReservEase.Alumni.Institution.Api.Tests;

public class StoreConfigParserTests
{
    private static ServiceFieldDefinitionRequest Q(string label, string type = "Text", string? key = null, bool required = false, List<string>? options = null, string? help = null) =>
        new() { Label = label, Type = type, Key = key ?? "", Required = required, Options = options, HelpText = help };

    // ── ParseJson ───────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_json_means_an_empty_list(string? json)
    {
        var (value, error) = StoreConfigParser.ParseJson<string>(json, "stages");
        Assert.Empty(value!);
        Assert.Null(error);
    }

    [Fact]
    public void Valid_json_is_parsed_case_insensitively_with_web_defaults()
    {
        var (value, error) = StoreConfigParser.ParseJson<StoreDetailItemRequest>("[{\"label\":\"Size\",\"VALUE\":\"M\"}]", "details");
        Assert.Null(error);
        var item = Assert.Single(value!);
        Assert.Equal(("Size", "M"), (item.Label, item.Value));
    }

    [Theory]
    [InlineData("not json", "stages")]
    [InlineData("{\"a\":1}", "stages")]
    [InlineData("[1,2", "order questions")]
    public void Malformed_json_returns_a_readable_error_naming_what_was_being_read(string json, string what)
    {
        var (value, error) = StoreConfigParser.ParseJson<string>(json, what);
        Assert.Null(value);
        Assert.Equal($"The {what} could not be read.", error);
    }

    [Fact]
    public void Json_null_literal_is_an_empty_list()
    {
        var (value, error) = StoreConfigParser.ParseJson<string>("null", "stages");
        Assert.Empty(value!);
        Assert.Null(error);
    }

    // ── MapFields ───────────────────────────────────────────────────────

    [Fact]
    public void A_label_with_no_key_gets_a_slug_key()
    {
        var (fields, error) = StoreConfigParser.MapFields([Q("Delivery Address (street)")], "delivery");
        Assert.Null(error);
        Assert.Equal("delivery_address_street", fields!.Single().Key);
    }

    [Fact]
    public void An_explicit_key_is_kept_trimmed()
    {
        var (fields, _) = StoreConfigParser.MapFields([Q("Name", key: "  full_name ")], "order");
        Assert.Equal("full_name", fields!.Single().Key);
    }

    [Fact]
    public void Duplicate_keys_are_made_unique_with_numeric_suffixes()
    {
        var (fields, _) = StoreConfigParser.MapFields([Q("Note"), Q("Note"), Q("Note"), Q("note")], "order");
        Assert.Equal(new[] { "note", "note_2", "note_3", "note_4" }, fields!.Select(f => f.Key));
    }

    [Fact]
    public void A_label_that_slugs_to_nothing_still_gets_a_unique_non_empty_key()
    {
        var (fields, _) = StoreConfigParser.MapFields([Q("???"), Q("!!!")], "order");
        Assert.All(fields!, f => Assert.Equal(8, f.Key.Length));
        Assert.NotEqual(fields![0].Key, fields[1].Key);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_label_is_rejected_naming_the_section(string label)
    {
        var (fields, error) = StoreConfigParser.MapFields([Q(label)], "delivery");
        Assert.Null(fields);
        Assert.Equal("Every delivery question needs a label.", error);
    }

    [Fact]
    public void An_unknown_type_is_rejected_with_the_label_and_type()
    {
        var (fields, error) = StoreConfigParser.MapFields([Q("Photo", "Image")], "order");
        Assert.Null(fields);
        Assert.Equal("\"Photo\" has an unknown type \"Image\".", error);
    }

    [Theory]
    [InlineData("Text")]
    [InlineData("TextArea")]
    [InlineData("Number")]
    [InlineData("Date")]
    [InlineData("File")]
    public void Every_documented_non_select_type_is_accepted_and_drops_any_options(string type)
    {
        var (fields, error) = StoreConfigParser.MapFields([Q("X", type, options: ["ignored"])], "order");
        Assert.Null(error);
        Assert.Null(fields!.Single().Options);
    }

    public static IEnumerable<object?[]> NoUsableOptions() => new[]
    {
        new object?[] { null },
        new object?[] { Array.Empty<string>() },
        new object?[] { new[] { "", "  " } },
    };

    [Theory, MemberData(nameof(NoUsableOptions))]
    public void A_select_needs_at_least_one_non_blank_option(string[]? options)
    {
        var (fields, error) = StoreConfigParser.MapFields([Q("Size", "Select", options: options?.ToList())], "order");
        Assert.Null(fields);
        Assert.Equal("\"Size\" is a choice question, so it needs at least one option.", error);
    }

    [Fact]
    public void Select_options_are_trimmed_and_blanks_removed()
    {
        var (fields, _) = StoreConfigParser.MapFields([Q("Size", "Select", options: [" S ", "", "M", "  "])], "order");
        Assert.Equal(new[] { "S", "M" }, fields!.Single().Options);
    }

    [Fact]
    public void Required_flag_label_and_help_text_are_carried_over_trimmed_with_blank_help_dropped()
    {
        var (fields, _) = StoreConfigParser.MapFields([Q("  Name  ", required: true, help: "  Your full name "), Q("Other", help: "   ")], "order");
        Assert.Equal(("Name", true, "Your full name"), (fields![0].Label, fields[0].Required, fields[0].HelpText));
        Assert.Null(fields[1].HelpText);
        Assert.False(fields[1].Required);
    }

    [Fact]
    public void An_empty_list_of_questions_maps_to_an_empty_list()
    {
        var (fields, error) = StoreConfigParser.MapFields([], "order");
        Assert.Empty(fields!);
        Assert.Null(error);
    }

    [Fact]
    public void Key_uniqueness_is_per_call_so_the_order_and_delivery_sections_do_not_interfere()
    {
        var (order, _) = StoreConfigParser.MapFields([Q("Phone")], "order");
        var (delivery, _) = StoreConfigParser.MapFields([Q("Phone")], "delivery");
        Assert.Equal("phone", order!.Single().Key);
        Assert.Equal("phone", delivery!.Single().Key);
    }

    // ── Stages and details ──────────────────────────────────────────────

    [Fact]
    public void Stages_are_trimmed_deduplicated_ignoring_case_and_keep_their_order()
    {
        var stages = StoreConfigParser.NormalizeStages([" Received ", "received", "", "Packed", "  ", "PACKED", "Shipped"]);
        Assert.Equal(new[] { "Received", "Packed", "Shipped" }, stages);
    }

    [Fact]
    public void Null_stages_are_an_empty_list()
        => Assert.Empty(StoreConfigParser.NormalizeStages(null));

    [Fact]
    public void Details_need_both_a_label_and_a_value_and_are_trimmed()
    {
        var details = StoreConfigParser.MapDetails([
            new() { Label = " Material ", Value = " Cotton " },
            new() { Label = "", Value = "x" },
            new() { Label = "Colour", Value = "  " },
        ]);
        var only = Assert.Single(details);
        Assert.Equal(("Material", "Cotton"), (only.Label, only.Value));
    }

    [Fact]
    public void Null_details_are_an_empty_list()
        => Assert.Empty(StoreConfigParser.MapDetails(null));
}
