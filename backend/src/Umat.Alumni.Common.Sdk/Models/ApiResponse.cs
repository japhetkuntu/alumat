namespace Umat.Alumni.Common.Sdk.Models;

public sealed record ApiResponse<T> : IApiResponse<T>
{
    public string Message { get; init; } = string.Empty;
    public int Code { get; init; }
    public string SubCode { get; init; } = "0";
    public T? Data { get; init; }
    public object? Errors { get; init; }
}
