namespace ReservEase.Alumni.PostgresDb.Sdk.Services;

/// <summary>
/// The first year a member can owe membership dues for. For someone with a graduation year it is that year, as it always was.
/// A community that does not use graduation years has no such anchor, so a member owes from the year they joined, never for
/// years before they were there.
/// </summary>
public static class MembershipPeriods
{
    public static int RequiredFromYear(int graduationYear, DateTime joinedAt) => graduationYear > 0 ? graduationYear : joinedAt.Year;
}
