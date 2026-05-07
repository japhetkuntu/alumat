using Newtonsoft.Json;

namespace Umat.Alumni.Member.Api.Models;

/// <summary>
/// Lightweight read-only status returned to the activation callback page.
/// Does NOT expose the payment reference and does NOT trigger any processing.
/// </summary>
public class ActivationStatusResponse
{
    [JsonProperty("status")]
    public string Status { get; set; } = string.Empty;

    [JsonProperty("email")]
    public string? Email { get; set; }

    [JsonProperty("memberNumber")]
    public string? MemberNumber { get; set; }

    [JsonProperty("message")]
    public string? Message { get; set; }
}
