namespace Domain.Models;

public class UserQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;

    public string? Search { get; set; }
    public string? Sort { get; set; }
}