using ReservEase.Alumni.Redis.Sdk.Models;

namespace ReservEase.Alumni.Institution.Api.Options;

public class InstitutionRedisConfig : IRedisDatabaseConfig
{
    public string Alias { get; set; } = "Institution";
    public int DbNumber { get; set; } = 0;
    public string ConnectionString { get; set; } = string.Empty;
    public TimeSpan? DefaultExpiry => TimeSpan.FromMinutes(30);
}
