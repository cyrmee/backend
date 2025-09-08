using Domain.Models.Common;
using Domain.Models.User;

namespace Domain.Interfaces;

public interface IUserService
{
    Task<UserModel?> GetUserAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PaginatedList<UserModel>> GetUsersPaginatedAsync(UserQuery query,
        CancellationToken cancellationToken = default);
}