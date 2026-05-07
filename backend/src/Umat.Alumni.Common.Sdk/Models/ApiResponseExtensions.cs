namespace Umat.Alumni.Common.Sdk.Models;

public static class ApiResponseExtensions
{
    public static IApiResponse<T> ToOkApiResponse<T>(this T data, string message = "Success")
        => new ApiResponse<T> { Message = message, Code = 200, Data = data };

    public static IApiResponse<T> ToCreatedApiResponse<T>(this T data, string message = "Created")
        => new ApiResponse<T> { Message = message, Code = 201, Data = data };

    public static IApiResponse<T> ToNotFoundApiResponse<T>(string message = "Not found")
        => new ApiResponse<T> { Message = message, Code = 404 };

    public static IApiResponse<T> ToBadRequestApiResponse<T>(string message = "Bad request", object? errors = null)
        => new ApiResponse<T> { Message = message, Code = 400, Errors = errors };

    public static IApiResponse<T> ToUnauthorizedApiResponse<T>(string message = "Unauthorized")
        => new ApiResponse<T> { Message = message, Code = 401 };

    public static IApiResponse<T> ToForbiddenApiResponse<T>(string message = "Forbidden")
        => new ApiResponse<T> { Message = message, Code = 403 };

    public static IApiResponse<T> ToServerErrorApiResponse<T>(string message = "An error occurred")
        => new ApiResponse<T> { Message = message, Code = 500 };

    public static IApiResponse<T> ToConflictApiResponse<T>(string message = "Conflict")
        => new ApiResponse<T> { Message = message, Code = 409 };
}
