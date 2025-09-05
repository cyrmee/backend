namespace Domain.DTOs.Common;

public class ResponseDto<T>
{
    // Constructor for successful responses
    public ResponseDto(T data, Pagination? pagination = null, string? apiVersion = null)
    {
        Data = data;
        Pagination = pagination;
        Errors = [];
    }

    // Constructor for error responses
    public ResponseDto(List<ApiError> errors, string? apiVersion = null)
    {
        Errors = errors;
    }

    // It's good practice to have a parameterless constructor for things like model binding
    public ResponseDto()
    {
        Errors = [];
    }

    public List<ApiError>? Errors { get; set; }
    public T? Data { get; set; }
    public Pagination? Pagination { get; set; }
}

public class ApiError
{
    public string? Code { get; set; }
    public string? Message { get; set; }
    public string? Details { get; set; }
}

public abstract class Pagination
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
}