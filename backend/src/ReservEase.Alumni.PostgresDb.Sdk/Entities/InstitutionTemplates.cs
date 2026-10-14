namespace ReservEase.Alumni.PostgresDb.Sdk.Entities;

/// <summary>A starting point for a new community. It only pre-fills settings; nothing it sets is locked, and all of it can be changed later.</summary>
public sealed record InstitutionTemplate(
    string Id, string Name, string Description, string GraduationYearMode, string? CohortLabel, string? CohortLabelPlural);

/// <summary>
/// The head starts offered when a community is set up. They are deliberately few and generic: a community is not "a kind of
/// organisation", it is whatever its administrators shape it into, and a template is only the shortest route to a sensible first
/// shape. "Start from scratch" assumes nothing at all.
/// </summary>
public static class InstitutionTemplates
{
    public const string Blank = "blank";

    public static readonly IReadOnlyList<InstitutionTemplate> All =
    [
        new(Blank, "Start from scratch", "Nothing assumed. Add the groups, fields and features you want, when you want them.", GraduationYearModes.Off, null, null),
        new("alumni", "Alumni or old students", "Members belong to the year they graduated, and the year groups are the spine of the community.", GraduationYearModes.Required, "Class", "Classes"),
        new("school", "School community", "Students, parents and former students together. A year is optional for each member.", GraduationYearModes.Optional, "Year group", "Year groups"),
        new("club", "Club or society", "Members and committees, with no years to think about.", GraduationYearModes.Off, null, null),
        new("faith", "Faith community", "Congregations, groups and ministries with no years to think about.", GraduationYearModes.Off, null, null),
        new("professional", "Professional body", "Members by chapter or region. A year of joining or qualifying is optional.", GraduationYearModes.Optional, "Cohort", "Cohorts"),
    ];

    public static InstitutionTemplate? Find(string? id) => All.FirstOrDefault(t => string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase));
}
