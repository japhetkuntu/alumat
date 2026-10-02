using System.Text.Json;
using System.Text.RegularExpressions;
using ReservEase.Alumni.Institution.Api.Models;
using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;

namespace ReservEase.Alumni.Institution.Api.Services.Implementations;

/// <summary>Turns what the admin submits for a product's (or template's) configuration into validated entities. Shared by products and templates so both enforce the same rules.</summary>
internal static class StoreConfigParser
{
    private static readonly string[] FieldTypes = ["Text", "TextArea", "Number", "Date", "Select", "File"];
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static string Slugify(string label)
    {
        var slug = Regex.Replace(label.Trim().ToLowerInvariant(), @"[^a-z0-9]+", "_").Trim('_');
        return string.IsNullOrEmpty(slug) ? Guid.NewGuid().ToString("N")[..8] : slug;
    }

    public static (List<T>? Value, string? Error) ParseJson<T>(string? json, string what)
    {
        if (string.IsNullOrWhiteSpace(json)) return ([], null);
        try { return (JsonSerializer.Deserialize<List<T>>(json, JsonOptions) ?? [], null); }
        catch (JsonException) { return (null, $"The {what} could not be read."); }
    }

    public static (List<ServiceFieldDefinition>? Fields, string? Error) MapFields(IEnumerable<ServiceFieldDefinitionRequest> requests, string section)
    {
        var usedKeys = new HashSet<string>();
        var fields = new List<ServiceFieldDefinition>();
        foreach (var r in requests)
        {
            if (string.IsNullOrWhiteSpace(r.Label))
                return (null, $"Every {section} question needs a label.");
            if (!FieldTypes.Contains(r.Type))
                return (null, $"\"{r.Label}\" has an unknown type \"{r.Type}\".");
            var options = r.Options?.Select(o => o.Trim()).Where(o => o.Length > 0).ToList();
            if (r.Type == "Select" && (options is null || options.Count == 0))
                return (null, $"\"{r.Label}\" is a choice question, so it needs at least one option.");

            var key = string.IsNullOrWhiteSpace(r.Key) ? Slugify(r.Label) : r.Key.Trim();
            var candidate = key;
            var suffix = 1;
            while (!usedKeys.Add(candidate))
                candidate = $"{key}_{++suffix}";

            fields.Add(new ServiceFieldDefinition
            {
                Key = candidate,
                Label = r.Label.Trim(),
                Type = r.Type,
                Required = r.Required,
                Options = r.Type == "Select" ? options : null,
                HelpText = string.IsNullOrWhiteSpace(r.HelpText) ? null : r.HelpText.Trim(),
            });
        }
        return (fields, null);
    }

    public static List<string> NormalizeStages(IEnumerable<string>? stages) =>
        (stages ?? []).Select(s => s.Trim()).Where(s => s.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    public static List<StoreDetailItem> MapDetails(IEnumerable<StoreDetailItemRequest>? details) =>
        (details ?? [])
            .Where(d => !string.IsNullOrWhiteSpace(d.Label) && !string.IsNullOrWhiteSpace(d.Value))
            .Select(d => new StoreDetailItem { Label = d.Label.Trim(), Value = d.Value.Trim() })
            .ToList();
}
