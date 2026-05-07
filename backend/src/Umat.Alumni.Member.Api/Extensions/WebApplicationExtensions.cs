using System.Net;
using Microsoft.AspNetCore.Diagnostics;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Umat.Alumni.Common.Sdk.Models;

namespace Umat.Alumni.Member.Api.Extensions;

public static class WebApplicationExtensions
{
    public static void UseExceptionHandler(
        this WebApplication app,
        bool returnStackTrace = false)
    {
        var logger = app.Services.GetRequiredService<ILogger<Program>>();

        app.UseExceptionHandler(appError =>
        {
            appError.Run(async context =>
            {
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                context.Response.ContentType = "application/json";

                var contextFeature = context.Features.Get<IExceptionHandlerFeature>();
                if (contextFeature is null) return;

                logger.LogError(contextFeature.Error, "Unhandled exception occurred");

                object? errors = null;
                if (returnStackTrace)
                {
                    errors = new[]
                    {
                        new
                        {
                            Field = contextFeature.Error?.Message ?? "Exception occurred",
                            ErrorMessage = contextFeature.Error?.StackTrace ?? "Exception occurred"
                        }
                    };
                }

                var response = new ApiResponse<object>
                {
                    Message = "Oops, something went wrong. Please try again later.",
                    Code = 500,
                    Errors = errors,
                };

                var json = JsonConvert.SerializeObject(response, new JsonSerializerSettings
                {
                    ContractResolver = new CamelCasePropertyNamesContractResolver(),
                    NullValueHandling = NullValueHandling.Ignore,
                });

                context.Response.ContentLength = json.Length;
                await context.Response.WriteAsync(json);
            });
        });
    }
}
