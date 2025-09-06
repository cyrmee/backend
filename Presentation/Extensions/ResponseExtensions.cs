using Domain.Constants;
using Domain.Models.Common;

namespace Presentation.Extensions;

public static class ResponseExtensions
{
    public static ResponseModel<T> ToResponse<T>(this T data)
    {
        return new ResponseModel<T>(data);
    }

    public static ResponseModel<List<T>> ToResponse<T>(this List<T> data, Pagination? pagination = null)
    {
        return new ResponseModel<List<T>>(data, pagination);
    }

    public static ResponseModel<T> Error<T>(this IEnumerable<ApiError> errors)
    {
        return new ResponseModel<T>([.. errors]);
    }

    public static ResponseModel<List<T>> ToResponse<T>(this PaginatedList<T> paged)
    {
        return new ResponseModel<List<T>>(paged.Data,
            new Pagination { Page = paged.PageNumber, PageSize = paged.PageSize, TotalCount = paged.TotalCount });
    }

    public static ApiError ToApiError(this string message, string code = ErrorCodes.BadRequest, string? details = null)
    {
        return new ApiError { Code = code, Message = message, Details = details };
    }
}