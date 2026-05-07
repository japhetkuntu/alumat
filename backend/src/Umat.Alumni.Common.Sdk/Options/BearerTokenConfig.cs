namespace Umat.Alumni.Common.Sdk.Options;

public class BearerTokenConfig
{
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string AdminSigningKey { get; set; } = string.Empty;
    public string MemberSigningKey { get; set; } = string.Empty;
    public int AccessTokenLifetime { get; set; } = 8;
    public int RefreshTokenLifetime { get; set; } = 30;
}
