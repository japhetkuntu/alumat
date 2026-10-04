using Microsoft.AspNetCore.Mvc;
using ReservEase.Alumni.Common.Sdk.Extensions;
using ReservEase.Alumni.Common.Sdk.Models;

namespace ReservEase.Alumni.Common.Tests;

public class ApiResponseTests
{
    [Fact]
    public void Ok_wraps_data_with_code_200_and_default_message()
    {
        var response = "payload".ToOkApiResponse();
        Assert.Equal(200, response.Code);
        Assert.Equal("Success", response.Message);
        Assert.Equal("payload", response.Data);
        Assert.Equal("0", response.SubCode);
        Assert.Null(response.Errors);
    }

    [Fact]
    public void Created_uses_201_and_a_custom_message()
    {
        var response = 42.ToCreatedApiResponse("Made it");
        Assert.Equal((201, "Made it", 42), (response.Code, response.Message, response.Data));
    }

    [Theory]
    [InlineData(404, "Not found")]
    [InlineData(401, "Unauthorized")]
    [InlineData(403, "Forbidden")]
    [InlineData(500, "An error occurred")]
    [InlineData(409, "Conflict")]
    [InlineData(400, "Bad request")]
    public void Error_helpers_have_the_right_code_default_message_and_no_data(int code, string message)
    {
        IApiResponse<string> response = code switch
        {
            404 => ApiResponseExtensions.ToNotFoundApiResponse<string>(),
            401 => ApiResponseExtensions.ToUnauthorizedApiResponse<string>(),
            403 => ApiResponseExtensions.ToForbiddenApiResponse<string>(),
            500 => ApiResponseExtensions.ToServerErrorApiResponse<string>(),
            409 => ApiResponseExtensions.ToConflictApiResponse<string>(),
            _ => ApiResponseExtensions.ToBadRequestApiResponse<string>(),
        };
        Assert.Equal(code, response.Code);
        Assert.Equal(message, response.Message);
        Assert.Null(response.Data);
    }

    [Fact]
    public void BadRequest_carries_validation_errors()
    {
        var errors = new Dictionary<string, string[]> { ["email"] = ["required"] };
        var response = ApiResponseExtensions.ToBadRequestApiResponse<object>("Invalid", errors);
        Assert.Same(errors, response.Errors);
        Assert.Equal("Invalid", response.Message);
    }

    [Fact]
    public void ApiResponse_is_a_value_record()
    {
        Assert.Equal(1.ToOkApiResponse(), 1.ToOkApiResponse());
        Assert.NotEqual(1.ToOkApiResponse(), 2.ToOkApiResponse());
    }

    [Fact]
    public void ErrorResponse_defaults_to_sub_code_1()
    {
        var e = new ErrorResponse { Message = "boom", Code = 500 };
        Assert.Equal("1", e.SubCode);
        Assert.Null(e.Errors);
    }

    [Theory]
    [InlineData(200, typeof(OkObjectResult), 200)]
    [InlineData(201, typeof(ObjectResult), 201)]
    [InlineData(400, typeof(BadRequestObjectResult), 400)]
    [InlineData(401, typeof(UnauthorizedObjectResult), 401)]
    [InlineData(403, typeof(ObjectResult), 403)]
    [InlineData(404, typeof(NotFoundObjectResult), 404)]
    [InlineData(409, typeof(ConflictObjectResult), 409)]
    [InlineData(422, typeof(ObjectResult), 422)]
    [InlineData(500, typeof(ObjectResult), 500)]
    public void ToActionResult_maps_the_code_to_the_matching_http_result(int code, Type type, int status)
    {
        var response = new ApiResponse<string> { Code = code, Message = "m" };

        var result = Assert.IsAssignableFrom<ObjectResult>(response.ToActionResult());

        Assert.Equal(type, result.GetType());
        Assert.Equal(status, result.StatusCode);
        Assert.Same(response, result.Value);
    }

    [Fact]
    public void PagedResult_and_BaseFilter_defaults()
    {
        var page = new PagedResult<int>();
        Assert.Empty(page.Results);
        Assert.Equal(0, page.TotalCount);

        var filter = new BaseFilter();
        Assert.Equal((1, 10), (filter.Page, filter.PageSize));
        Assert.Null(filter.Search);
    }
}
