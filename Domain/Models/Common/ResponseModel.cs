namespace Domain.Models.Common;

public class ResponseModel<T>
{
    // Constructor for successful responses
    public ResponseModel(T data, Pagination? pagination = null)
    {
        Data = data;
        Pagination = pagination;
        Errors = [];
    }

    // Constructor for error responses
    public ResponseModel(List<ApiError> errors)
    {
        Errors = errors;
    }

    // It's good practice to have a parameterless constructor for things like model binding
    public ResponseModel()
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

public class Pagination
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;
}