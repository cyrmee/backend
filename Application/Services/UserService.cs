using Application.Common;
using Domain.Entities;
using Domain.Interfaces;
using Domain.Models;
using Domain.Models.Common;
using Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Application.Services;

public class UserService(ApplicationDbContext context) : IUserService
{
    public async Task<UserModel?> GetUserAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var query = context.Users.AsNoTracking()
            .Where(u => u.Id == id)
            .ProjectTo<User, UserModel>();

        var result = await query.FirstOrDefaultAsync(cancellationToken);

        return result;
    }

    public async Task<PaginatedList<UserModel>> GetUsersPaginatedAsync(UserQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var usersQuery = context.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            usersQuery = usersQuery.Where(u =>
                (u.UserName != null && u.UserName.Contains(s)) || (u.Email != null && u.Email.Contains(s)) ||
                (u.Name != null && u.Name.Contains(s)));
        }

        usersQuery = usersQuery.OrderBy(u => u.CreatedAt);

        var total = await usersQuery.CountAsync(cancellationToken);
        var skip = (page - 1) * pageSize;
        var data = await usersQuery.Skip(skip).Take(pageSize)
            .ProjectTo<User, UserModel>()
            .ToListAsync(cancellationToken);

        return new PaginatedList<UserModel>
        {
            Data = data,
            TotalCount = total,
            PageNumber = page,
            PageSize = pageSize
        };
    }
}