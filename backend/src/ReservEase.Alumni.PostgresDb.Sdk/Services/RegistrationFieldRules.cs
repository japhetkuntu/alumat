using System.Text;
using ReservEase.Alumni.PostgresDb.Sdk.Entities;

namespace ReservEase.Alumni.PostgresDb.Sdk.Services;

/// <summary>Keeps a community's registration questions sane and checks a person's answers against them.</summary>
public static class RegistrationFieldRules
{
    public const int MaxFields = 8;
    public const int MaxLabelLength = 60;
    public const int MaxOptions = 30;
    public const int MaxAnswerLength = 200;

    /// <summary>
    /// Cleans an edited list. Existing ids are kept (so answers stay attached), new ones are made from the label.
    /// Returns null and an error when the list can't be accepted.
    /// </summary>
    public static List<RegistrationField>? Normalize(IEnumerable<RegistrationField> input, out string? error)
    {
        error = null;
        var result = new List<RegistrationField>();
        var used = new HashSet<string>();

        foreach (var f in input)
        {
            var label = (f.Label ?? string.Empty).Trim();
            if (label.Length == 0) continue;
            if (label.Length > MaxLabelLength) { error = $"A question can be at most {MaxLabelLength} characters."; return null; }

            var type = f.Type == RegistrationFieldTypes.Select ? RegistrationFieldTypes.Select : RegistrationFieldTypes.Text;
            var options = (f.Options ?? [])
                .Select(o => (o ?? string.Empty).Trim()).Where(o => o.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase).Take(MaxOptions).ToList();
            if (type == RegistrationFieldTypes.Select && options.Count < 2)
            { error = $"\"{label}\" needs at least two choices, or make it a text question."; return null; }

            var id = Slug(string.IsNullOrWhiteSpace(f.Id) ? label : f.Id);
            var unique = id; var n = 2;
            while (!used.Add(unique)) unique = $"{id}-{n++}";

            result.Add(new RegistrationField
            {
                Id = unique, Label = label, Type = type, Required = f.Required,
                Options = type == RegistrationFieldTypes.Select ? options : [],
            });
        }

        if (result.Count > MaxFields) { error = $"A community can ask at most {MaxFields} extra questions."; return null; }
        return result;
    }

    /// <summary>
    /// Checks the answers for one person. Unknown keys are dropped, blanks are not stored, a required question left blank
    /// or a choice that is not on the list is an error.
    /// </summary>
    public static Dictionary<string, string>? Validate(IEnumerable<RegistrationField> fields, IDictionary<string, string>? answers, out string? error, bool enforceRequired = true)
    {
        error = null;
        var cleaned = new Dictionary<string, string>();
        foreach (var f in fields)
        {
            string? value = null;
            if (answers is not null && answers.TryGetValue(f.Id, out var raw)) value = raw?.Trim();

            if (string.IsNullOrEmpty(value))
            {
                if (f.Required && enforceRequired) { error = $"Please answer: {f.Label}"; return null; }
                continue;
            }
            if (value.Length > MaxAnswerLength) value = value[..MaxAnswerLength];
            if (f.Type == RegistrationFieldTypes.Select)
            {
                var match = f.Options.FirstOrDefault(o => string.Equals(o, value, StringComparison.OrdinalIgnoreCase));
                if (match is null) { error = $"Please choose one of the listed options for: {f.Label}"; return null; }
                value = match;
            }
            cleaned[f.Id] = value;
        }
        return cleaned;
    }

    private static string Slug(string text)
    {
        var sb = new StringBuilder();
        foreach (var c in text.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c) && c < 128) sb.Append(c);
            else if (sb.Length > 0 && sb[^1] != '-') sb.Append('-');
        }
        var s = sb.ToString().Trim('-');
        return s.Length == 0 ? "question" : (s.Length > 40 ? s[..40] : s);
    }
}
