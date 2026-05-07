namespace Umat.Alumni.Member.Api.Models;

public class UpdateProfileRequest
{
    public string? Company { get; set; }
    public string? JobTitle { get; set; }
    public string? Location { get; set; }
    public string? LinkedInUrl { get; set; }
    public string? Bio { get; set; }
    public string? Phone { get; set; }
    public string? EmploymentStatus { get; set; }
    public IFormFile? ProfilePicture { get; set; }
}
