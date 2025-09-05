using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Domain.DTOs.Common;

namespace Presentation.Filters;

public class ResponseFormatFilter : IAlwaysRunResultFilter
{
    public void OnResultExecuted(ResultExecutedContext context)
    {
        // Nothing to do after execution
    }

    public void OnResultExecuting(ResultExecutingContext context)
    {
        // Handle framework-level error responses that don't follow our ResponseDto format
        switch (context.Result)
        {
            case UnsupportedMediaTypeResult:
                HandleUnsupportedMediaType(context);
                break;
            case BadRequestObjectResult badRequestResult:
                HandleBadRequest(context, badRequestResult);
                break;
            case ObjectResult { StatusCode: 415 }:
                HandleUnsupportedMediaType(context);
                break;
            case ObjectResult { StatusCode: 400 } objectResult:
                HandleBadRequest(context, objectResult);
                break;
        }
    }

    private static void HandleUnsupportedMediaType(ResultExecutingContext context)
    {
        var errors = new List<ApiError>
        {
            new()
            {
                Code = "UNSUPPORTED_MEDIA_TYPE",
                Message = "The request content type is not supported. Expected 'application/json'.",
                Details = "Please ensure your request has Content-Type: application/json header and valid JSON body."
            }
        };

        var responseDto = new ResponseDto<object>(errors);

        context.Result = new ObjectResult(responseDto)
        {
            StatusCode = 415
        };
    }

    private static void HandleBadRequest(ResultExecutingContext context, IActionResult actionResult)
    {
        // Check if it's already a ResponseDto format
        if (actionResult is ObjectResult { Value: ResponseDto<object> }) return; // Already in correct format

        var errors = new List<ApiError>
        {
            new()
            {
                Code = "BAD_REQUEST",
                Message = "The request is invalid.",
                Details = "Please check your request format and try again."
            }
        };

        // If it's a validation error from ModelState, extract the details
        if (actionResult is BadRequestObjectResult { Value: ValidationProblemDetails validationProblem })
        {
            errors.Clear();
            errors.AddRange(validationProblem.Errors.Select(error => new ApiError
            {
                Code = "VALIDATION_ERROR",
                Message = $"Validation failed for field '{error.Key}'",
                Details = string.Join("; ", error.Value)
            }));
        }

        var responseDto = new ResponseDto<object>(errors);

        context.Result = new ObjectResult(responseDto)
        {
            StatusCode = 400
        };
    }
}
