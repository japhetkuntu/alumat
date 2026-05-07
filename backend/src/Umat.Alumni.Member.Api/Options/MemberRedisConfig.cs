using Umat.Alumni.Redis.Sdk.Models;

namespace Umat.Alumni.Member.Api.Options;

public class MemberRedisConfig : IRedisDatabaseConfig
{
    public string Alias { get; set; } = "Member";
    public int DbNumber { get; set; } = 1;
    public string ConnectionString { get; set; } = string.Empty;
    public TimeSpan? DefaultExpiry => TimeSpan.FromMinutes(30);
}
