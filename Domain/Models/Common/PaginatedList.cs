namespace Domain.Models.Common;

public class PaginatedList<T>
{
	public List<T> Data { get; init; } = [];
	public int TotalCount { get; init; }
	public int Page { get; init; }
	public int PageSize { get; init; }
}