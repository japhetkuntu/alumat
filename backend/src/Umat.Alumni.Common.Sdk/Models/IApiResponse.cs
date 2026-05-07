namespace Umat.Alumni.Common.Sdk.Models;

public interface IApiResponse<T>
{
    string Message { get; }
    int Code { get; }
    string SubCode { get; }
    T? Data { get; }
    object? Errors { get; }
}
