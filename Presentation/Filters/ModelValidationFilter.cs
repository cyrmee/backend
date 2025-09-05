using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Domain.DTOs.Common;

namespace Presentation.Filters;

public class ModelValidationFilter : IActionFilter
{
    public void OnActionExecuted(ActionExecutedContext context)
    {
        // We only need to check before the action executes
    }

    public void OnActionExecuting(ActionExecutingContext context)
    {
        if (context.ModelState.IsValid) return;

        // Convert model state errors to ApiError list
        var apiErrors = (from modelError in context.ModelState
                         from error in modelError.Value.Errors
                         select new ApiError
                         {
                             Code = "VALIDATION_ERROR",
                             Message = $"Validation failed for field '{modelError.Key}'",
                             Details = error.ErrorMessage
                         }).ToList();

        // Log the validation errors
        var logger = context.HttpContext.RequestServices
            .GetService<ILogger<ModelValidationFilter>>();

        logger?.LogWarning(
            "Validation failed: {ValidationErrors}",
            string.Join(", ", apiErrors.Select(e => $"{e.Message}: {e.Details}")));

        // Create a ResponseDto error response
        var responseDto = new ResponseDto<object>(apiErrors);

        // Return a standardized error response
        context.Result = new UnprocessableEntityObjectResult(responseDto);
    }
}
