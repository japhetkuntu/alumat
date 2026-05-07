namespace Umat.Alumni.Redis.Sdk.Models;

public interface IRedisDatabaseConfig
{
    string Alias { get; set; }
    int DbNumber { get; set; }
    string ConnectionString { get; set; }
    TimeSpan? DefaultExpiry { get; }
}
