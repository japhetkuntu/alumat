namespace ReservEase.Alumni.Temporal.Sdk;

public class TemporalConfig
{
    public string Address { get; set; } = "localhost:7233";
    public string Namespace { get; set; } = "default";
    public string? ApiKey { get; set; }
}
