using ReservEase.Alumni.PostgresDb.Sdk.Repositories;
using StaffEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.InstitutionStaff;

namespace ReservEase.Alumni.Institution.Api.Services.Implementations;

/// <summary>Another institution this person administers, and the address of its portal's sign-in page.</summary>
public sealed record StaffInstitution(string InstitutionId, string Slug, string Name, string? LogoUrl, string LoginUrl);

/// <summary>
/// The other institutions where the signed-in staff member's email has an administrator account, so they can open them from
/// one place. This only lists and links: it never signs anyone in at the other portal. An administrator account can be created
/// with a password its creator chose, so a sign-in on one institution says nothing about who controls the same email on
/// another. The other portal's own sign-in stays in the way, with the email filled in.
/// </summary>
public interface IStaffInstitutionsService
{
    Task<IReadOnlyList<StaffInstitution>> ListAsync(string staffId);
}

public class StaffInstitutionsService(
    IAlumniPgRepository<StaffEntity> staffRepo,
    IAlumniPgRepository<PostgresDb.Sdk.Entities.Institution> institutionRepo,
    IConfiguration configuration) : IStaffInstitutionsService
{
    public async Task<IReadOnlyList<StaffInstitution>> ListAsync(string staffId)
    {
        var domain = configuration["AdminPortalBaseDomain"];
        if (string.IsNullOrWhiteSpace(domain)) return [];   // without the admin domain there is no address to build

        var me = await staffRepo.GetByIdAsync(staffId);
        if (me is null || me.IsDisabled || string.IsNullOrWhiteSpace(me.Email)) return [];

        var email = me.Email.Trim().ToLowerInvariant();
        var theirs = (await staffRepo.GetAllAsync(s => s.Email.ToLower() == email && s.Id != me.Id && !s.IsDisabled, ignoreQueryFilters: true)).ToList();
        var ids = theirs.Select(s => s.InstitutionId).Where(i => i != me.InstitutionId).Distinct().ToList();
        if (ids.Count == 0) return [];

        var institutions = await institutionRepo.GetAllAsync(i => ids.Contains(i.Id) && i.Status == "Active", ignoreQueryFilters: true);
        return institutions
            .Select(i => new StaffInstitution(i.Id, i.Slug, string.IsNullOrWhiteSpace(i.PortalName) ? i.Name : i.PortalName!, i.LogoUrl,
                $"https://{i.Slug}.{domain.Trim().TrimStart('.')}/login?email={Uri.EscapeDataString(me.Email)}"))
            .OrderBy(i => i.Name)
            .ToList();
    }
}
