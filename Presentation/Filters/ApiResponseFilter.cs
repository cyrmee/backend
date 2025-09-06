using Domain.Models.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Presentation.Filters;

public class ApiResponseFilter : IAsyncResultFilter, IAlwaysRunResultFilter
{
    public void OnResultExecuting(ResultExecutingContext context)
    {
        // Not used (logic handled in async path)
    }

    public void OnResultExecuted(ResultExecutedContext context)
    {
        // No post-processing
    }

    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        NormalizeFrameworkErrors(context);
        WrapIfNeeded(context);
        await next();
    }

    private static void WrapIfNeeded(ResultExecutingContext context)
    {
        switch (context.Result)
        {
            case ObjectResult { Value: null }:
                return;

            case ObjectResult objectResult when IsAlreadyWrapped(objectResult.Value):
                return;

            case ObjectResult objectResult:
                objectResult.Value = new ResponseModel<object>(objectResult.Value);
                break;
            case EmptyResult:

                context.Result = new ObjectResult(new ResponseModel<object>(null!)) { StatusCode = 200 };
                break;
        }
    }

    private static bool IsAlreadyWrapped(object value)
    {
        var type = value.GetType();
        return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ResponseModel<>);
    }

    private static void NormalizeFrameworkErrors(ResultExecutingContext context)
    {
        switch (context.Result)
        {
            case UnsupportedMediaTypeResult:
                ReplaceUnsupported(context);
                break;
            case BadRequestObjectResult badReq:
                ReplaceBadRequest(context, badReq);
                break;
            case ObjectResult { StatusCode: 415 }:
                ReplaceUnsupported(context);
                break;
            case ObjectResult { StatusCode: 400 } obj400:
                ReplaceBadRequest(context, obj400);
                break;
        }
    }

    private static void ReplaceUnsupported(ResultExecutingContext context)
    {
        var errors = new List<ApiError>
        {
            new()
            {
                Code = "UNSUPPORTED_MEDIA_TYPE",
                Message = "The request content type is not supported. Expected 'application/json'.",
                Details = "Ensure Content-Type: application/json header and valid JSON body."
            }
        };
        context.Result = new ObjectResult(new ResponseModel<object>(errors)) { StatusCode = 415 };
    }

    private static void ReplaceBadRequest(ResultExecutingContext context, IActionResult actionResult)
    {
        if (actionResult is ObjectResult { Value: ResponseModel<object> }) return;

        var errors = new List<ApiError>
        {
            new()
            {
                Code = "BAD_REQUEST",
                Message = "The request is invalid.",
                Details = "Check request format and try again."
            }
        };

        if (actionResult is BadRequestObjectResult { Value: ValidationProblemDetails validation })
        {
            errors.Clear();
            errors.AddRange(validation.Errors.Select(e => new ApiError
            {
                Code = "VALIDATION_ERROR",
                Message = $"Validation failed for field '{e.Key}'",
                Details = string.Join("; ", e.Value)
            }));
        }

        context.Result = new ObjectResult(new ResponseModel<object>(errors)) { StatusCode = 400 };
    }
}