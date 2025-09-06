using Domain.Models.Common;

namespace Domain.Interfaces;

public interface ICurrentUserService
{
    AuthValidationResponse GetCurrentUser(string token);
}