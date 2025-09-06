using Domain.Models;
using Domain.Models.Common;

namespace Domain.Interfaces;

public interface IUserService
{
    Task<UserModel?> GetUserAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PaginatedList<UserModel>> GetUsersPaginatedAsync(UserQuery query,
        CancellationToken cancellationToken = default);
}