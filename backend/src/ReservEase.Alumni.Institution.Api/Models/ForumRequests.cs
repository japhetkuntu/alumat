namespace ReservEase.Alumni.Institution.Api.Models;

public record CreateCategoryRequest(string Name, string? Description);
public record UpdateCategoryRequest(string Name, string? Description);
