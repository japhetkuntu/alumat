using ReservEase.Alumni.Redis.Sdk.Models;

namespace ReservEase.Alumni.Platform.Api.Options;

public class PlatformRedisConfig : IRedisDatabaseConfig
{
    public string Alias { get; set; } = "Platform";
    public int DbNumber { get; set; } = 2;
    public string ConnectionString { get; set; } = string.Empty;
    public TimeSpan? DefaultExpiry => TimeSpan.FromMinutes(30);
}
