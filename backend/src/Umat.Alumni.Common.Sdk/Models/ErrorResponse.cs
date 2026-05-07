namespace Umat.Alumni.Common.Sdk.Models;

public sealed record ErrorResponse
{
    public string Message { get; init; } = string.Empty;
    public int Code { get; init; }
    public string SubCode { get; init; } = "1";
    public object? Errors { get; init; }
}
