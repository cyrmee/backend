using Domain.DTOs.Common;

namespace Domain.Interfaces;

public interface ICurrentUserService
{
    AuthValidationResponse GetCurrentUser(string token);
}
