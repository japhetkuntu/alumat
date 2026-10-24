using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;

namespace ReservEase.Alumni.PostgresDb.Sdk.Extensions;

/// <summary>
/// Reading members without the heavy columns. A member row carries the password hash, long profile text and several lists, none of
/// which a job that sends a message or builds a list of names ever looks at. Loading ten thousand of them whole is hundreds of
/// megabytes on a small server; these load the same people with only the columns such work uses. The results are not tracked and
/// are never saved back: anything that changes a member loads the whole row instead.
/// </summary>
public static class MemberProjections
{
    /// <summary>Who they are and how to reach them, their year group and department, and the dates and membership facts messages and reminders use.</summary>
    public static IQueryable<Member> Light(this IQueryable<Member> members) =>
        members.Select(m => new Member
        {
            Id = m.Id,
            InstitutionId = m.InstitutionId,
            FirstName = m.FirstName,
            LastName = m.LastName,
            Email = m.Email,
            Phone = m.Phone,
            Status = m.Status,
            StudentId = m.StudentId,
            GraduationYear = m.GraduationYear,
            DepartmentId = m.DepartmentId,
            ProfilePictureUrl = m.ProfilePictureUrl,
            MemberNumber = m.MemberNumber,
            DateOfBirth = m.DateOfBirth,
            CreatedAt = m.CreatedAt,
            LastLoginAt = m.LastLoginAt,
            IsMembershipActive = m.IsMembershipActive,
            MembershipExpiry = m.MembershipExpiry,
            MembershipYearsPaid = m.MembershipYearsPaid,
            LastMembershipPaidAt = m.LastMembershipPaidAt,
        });

    /// <summary><see cref="Light"/> plus the answers to the community's own registration questions, for filtering on what someone wrote.</summary>
    public static IQueryable<Member> LightWithAnswers(this IQueryable<Member> members) =>
        members.Select(m => new Member
        {
            Id = m.Id,
            InstitutionId = m.InstitutionId,
            FirstName = m.FirstName,
            LastName = m.LastName,
            Email = m.Email,
            Phone = m.Phone,
            Status = m.Status,
            StudentId = m.StudentId,
            GraduationYear = m.GraduationYear,
            DepartmentId = m.DepartmentId,
            ProfilePictureUrl = m.ProfilePictureUrl,
            MemberNumber = m.MemberNumber,
            DateOfBirth = m.DateOfBirth,
            CreatedAt = m.CreatedAt,
            LastLoginAt = m.LastLoginAt,
            IsMembershipActive = m.IsMembershipActive,
            MembershipExpiry = m.MembershipExpiry,
            MembershipYearsPaid = m.MembershipYearsPaid,
            LastMembershipPaidAt = m.LastMembershipPaidAt,
            CustomFields = m.CustomFields,
        });

    /// <summary>Just enough to show a person on a card or in a list: their name, photo, email, year group and department.</summary>
    public static IQueryable<Member> Card(this IQueryable<Member> members) =>
        members.Select(m => new Member
        {
            Id = m.Id,
            FirstName = m.FirstName,
            LastName = m.LastName,
            Email = m.Email,
            ProfilePictureUrl = m.ProfilePictureUrl,
            GraduationYear = m.GraduationYear,
            DepartmentId = m.DepartmentId,
        });
}
