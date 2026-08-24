using System.ComponentModel.DataAnnotations;

namespace ReservEase.Alumni.Institution.Api.Models;

public record BatchListItem(string Id, string Name, int Year, bool IsActive);

public class CreateBatchRequest
{
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, Range(1900, 2200)]
    public int Year { get; set; }
}

public class UpdateBatchRequest
{
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, Range(1900, 2200)]
    public int Year { get; set; }

    public bool IsActive { get; set; } = true;
}
