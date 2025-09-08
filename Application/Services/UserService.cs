using Application.Common;
using Domain.Enums;
using Domain.Interfaces;
using Domain.Models.Common;
using Domain.Models.User;
using Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Application.Services;

public class UserService(ApplicationDbContext context) : IUserService
{
	public async Task<UserModel?> GetUserAsync(Guid id, CancellationToken cancellationToken = default)
	{
		var query = context.Users
			.AsNoTracking()
			.Where(u => u.Id == id)
			.MapTo<UserModel>();

		var result = await query.FirstOrDefaultAsync(cancellationToken);

		return result;
	}

	public async Task<PaginatedList<UserModel>> GetUsersPaginatedAsync(UserQuery query,
		CancellationToken cancellationToken = default)
	{
		var usersQuery = context.Users.AsNoTracking();

		if (!string.IsNullOrWhiteSpace(query.Search))
		{
			var s = query.Search.Trim();
			usersQuery = usersQuery.Where(u =>
				(u.UserName != null && u.UserName.Contains(s)) || (u.Email != null && u.Email.Contains(s)) ||
				(u.Name != null && u.Name.Contains(s)));
		}

		usersQuery = query.Sort == SortDirection.Ascending
			? usersQuery.OrderBy(u => u.CreatedAt)
			: usersQuery.OrderByDescending(u => u.CreatedAt);

		var total = await usersQuery.CountAsync(cancellationToken);
		var skip = (query.Page - 1) * query.PageSize;
		var data = await usersQuery.Skip(skip).Take(query.PageSize)
			.MapTo<UserModel>()
			.ToListAsync(cancellationToken);

		return new PaginatedList<UserModel>
		{
			Data = data,
			TotalCount = total,
			Page = query.Page,
			PageSize = query.PageSize
		};
	}
}