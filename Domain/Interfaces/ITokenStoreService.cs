namespace Domain.Interfaces;

public interface ITokenStoreService
{
    Task StoreAccessJtiAsync(string userId, string jti, TimeSpan ttl);
    Task StoreRefreshJtiAsync(string userId, string jti, TimeSpan ttl);
    Task<bool> IsAccessJtiActiveAsync(string userId, string jti);
    Task<bool> IsRefreshJtiActiveAsync(string userId, string jti);
    Task RevokeAccessJtiAsync(string userId, string jti);
    Task RevokeRefreshJtiAsync(string userId, string jti);
    Task RevokeAllForUserAsync(string userId);
}