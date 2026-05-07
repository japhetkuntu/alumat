using Umat.Alumni.Redis.Sdk.Models;

namespace Umat.Alumni.Admin.Api.Options;

public class AdminRedisConfig : IRedisDatabaseConfig
{
    public string Alias { get; set; } = "Admin";
    public int DbNumber { get; set; } = 0;
    public string ConnectionString { get; set; } = string.Empty;
    public TimeSpan? DefaultExpiry => TimeSpan.FromMinutes(30);
}
