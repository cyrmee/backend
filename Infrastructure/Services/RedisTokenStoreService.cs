using Domain.Interfaces;
using StackExchange.Redis;

namespace Infrastructure.Services;

public class RedisTokenStoreService(IConnectionMultiplexer connection) : ITokenStoreService
{
    private readonly IDatabase _db = connection.GetDatabase();

    public Task StoreAccessJtiAsync(string userId, string jti, TimeSpan ttl)
    {
        return _db.StringSetAsync(AccessKey(userId, jti), "1", ttl);
    }

    public Task StoreRefreshJtiAsync(string userId, string jti, TimeSpan ttl)
    {
        return _db.StringSetAsync(RefreshKey(userId, jti), "1", ttl);
    }

    public Task<bool> IsAccessJtiActiveAsync(string userId, string jti)
    {
        return _db.KeyExistsAsync(AccessKey(userId, jti));
    }

    public Task<bool> IsRefreshJtiActiveAsync(string userId, string jti)
    {
        return _db.KeyExistsAsync(RefreshKey(userId, jti));
    }

    public Task RevokeAccessJtiAsync(string userId, string jti)
    {
        return _db.KeyDeleteAsync(AccessKey(userId, jti));
    }

    public Task RevokeRefreshJtiAsync(string userId, string jti)
    {
        return _db.KeyDeleteAsync(RefreshKey(userId, jti));
    }

    public async Task RevokeAllForUserAsync(string userId)
    {
        foreach (var endpoint in connection.GetEndPoints())
        {
            var server = connection.GetServer(endpoint);
            foreach (var key in server.Keys(pattern: $"access_jti:{userId}:*"))
                await _db.KeyDeleteAsync(key).ConfigureAwait(false);
            foreach (var key in server.Keys(pattern: $"refresh_jti:{userId}:*"))
                await _db.KeyDeleteAsync(key).ConfigureAwait(false);
        }
    }

    private static string AccessKey(string userId, string jti)
    {
        return $"access_jti:{userId}:{jti}";
    }

    private static string RefreshKey(string userId, string jti)
    {
        return $"refresh_jti:{userId}:{jti}";
    }
}