using System.Net;
using System.Text.Json;
using Npgsql;
using Domain.Exceptions;
using Domain.DTOs.Common;

namespace Presentation.Middleware;

public class ErrorHandlingMiddleware(
	RequestDelegate next,
	ILogger<ErrorHandlingMiddleware> logger,
	IWebHostEnvironment env)
{
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase
	};

	public async Task InvokeAsync(HttpContext context)
	{
		try
		{
			await next(context);

			// If no exception, check for 403 Forbidden
			if (context.Response is { StatusCode: StatusCodes.Status403Forbidden, HasStarted: false })
			{
				context.Response.ContentType = "application/json";
				var error = new ApiError
					{ Code = "FORBIDDEN", Message = "You do not have permission to perform this action." };
				var response = new ResponseDto<object>([error]);
				var json = JsonSerializer.Serialize(response, JsonOptions);
				await context.Response.WriteAsync(json);
			}
		}
		catch (PostgresException pgEx)
		{
			if (!context.Response.HasStarted)
			{
				if (pgEx is not { Message: null } &&
				    pgEx.Message.Contains("value too long", StringComparison.CurrentCultureIgnoreCase))
				{
					await HandleExceptionAsync(context,
						new BadRequestException(
							"One or more fields are too long. Please ensure all text fields are within their allowed length.",
							pgEx));
					return;
				}

				Exception mappedEx = pgEx.SqlState switch
				{
					"23505" => new ConflictException("This information already exists. Please use different values.",
						pgEx),
					"23503" => new BadRequestException(
						"We couldn't find a related record, or it is still in use. Please check your input.", pgEx),
					"23514" => new BadRequestException(
						"Some of the provided information doesn't meet our requirements. Please review your input.",
						pgEx),
					"23502" => new BadRequestException(
						"Some required information is missing. Please fill in all fields.", pgEx),
					_ => pgEx
				};
				await HandleExceptionAsync(context, mappedEx);
			}
		}
		catch (Exception ex)
		{
			if (!context.Response.HasStarted) await HandleExceptionAsync(context, ex);
			// else: response already started, cannot write error
		}
	}

	private async Task HandleExceptionAsync(HttpContext context, Exception exception)
	{
		HttpStatusCode statusCode;
		string errorMessage;
		string errorCode;
		object? errorDetails = null;
		string path = context.Request.Path;

		switch (exception)
		{
			case ValidationException appValidationException:
				statusCode = HttpStatusCode.UnprocessableEntity; // 422
				errorMessage = appValidationException.Message;
				errorCode = "VALIDATION_ERROR";
				errorDetails = appValidationException.Errors;
				logger.LogWarning("Validation error: {ValidationErrors}",
					string.Join(", ",
						appValidationException.Errors.Select(e => $"{e.Key}: {string.Join(", ", e.Value)}")));
				break;
			case ConflictException conflictException:
				statusCode = HttpStatusCode.Conflict; // 409
				errorMessage = conflictException.Message;
				errorCode = "CONFLICT";
				break;
			case NotFoundException:
				statusCode = HttpStatusCode.NotFound; // 404
				errorMessage = exception.Message;
				errorCode = "NOT_FOUND";
				break;
			case BadRequestException:
				statusCode = HttpStatusCode.BadRequest; // 400
				errorMessage = exception.Message;
				errorCode = "BAD_REQUEST";
				break;
			case ForbiddenAccessException:
				statusCode = HttpStatusCode.Forbidden; // 403
				errorMessage = exception.Message;
				errorCode = "FORBIDDEN";
				break;
			case UnauthorizedException:
				statusCode = HttpStatusCode.Unauthorized; // 401
				errorMessage = exception.Message;
				errorCode = "UNAUTHORIZED";
				break;
			case InvalidOperationException:
				statusCode = HttpStatusCode.BadRequest;
				errorMessage = exception.Message;
				errorCode = "INVALID_OPERATION";
				break;
			case KeyNotFoundException:
				statusCode = HttpStatusCode.NotFound;
				errorMessage = exception.Message;
				errorCode = "NOT_FOUND";
				break;
			case JsonException:
				statusCode = HttpStatusCode.BadRequest;
				errorMessage = "Invalid JSON in the request";
				errorCode = "BAD_REQUEST";
				break;
			default:
				statusCode = HttpStatusCode.InternalServerError;
				errorMessage = env.IsDevelopment() ? exception.Message : "An unexpected error occurred";
				errorCode = "INTERNAL_SERVER_ERROR";
				if (env.IsDevelopment())
					errorDetails = new
					{
						ExceptionType = exception.GetType().Name,
						exception.StackTrace,
						Path = path
					};
				break;
		}

		var errors = new List<ApiError>
		{
			new()
			{
				Code = errorCode,
				Message = errorMessage,
				Details = errorDetails?.ToString()
			}
		};

		var errorResponse = new ResponseDto<object>(errors);

		if (statusCode != HttpStatusCode.Unauthorized && statusCode != HttpStatusCode.Forbidden)
		{
			logger.LogError("{Method} {Path} {StatusCode}: {ErrorMessage}", context.Request.Method, path,
				(int)statusCode, errorMessage);
			if (exception.StackTrace != null) logger.LogDebug("Stack Trace: {StackTrace}", exception.StackTrace);
		}

		context.Response.ContentType = "application/json";
		context.Response.StatusCode = (int)statusCode;
		await context.Response.WriteAsync(JsonSerializer.Serialize(errorResponse, JsonOptions));
	}
}
