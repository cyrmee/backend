using System.Collections.Concurrent;
using Domain.Interfaces;

namespace Infrastructure.Services;

public class InMemoryTokenStoreService : ITokenStoreService
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _store = new();

    private static string AccessKey(string userId, string jti) => $"access_jti:{userId}:{jti}";
    private static string RefreshKey(string userId, string jti) => $"refresh_jti:{userId}:{jti}";

    public Task StoreAccessJtiAsync(string userId, string jti, TimeSpan ttl)
    {
        _store[AccessKey(userId, jti)] = DateTimeOffset.UtcNow.Add(ttl);
        return Task.CompletedTask;
    }

    public Task StoreRefreshJtiAsync(string userId, string jti, TimeSpan ttl)
    {
        _store[RefreshKey(userId, jti)] = DateTimeOffset.UtcNow.Add(ttl);
        return Task.CompletedTask;
    }

    public Task<bool> IsAccessJtiActiveAsync(string userId, string jti)
        => Task.FromResult(IsActive(AccessKey(userId, jti)));

    public Task<bool> IsRefreshJtiActiveAsync(string userId, string jti)
        => Task.FromResult(IsActive(RefreshKey(userId, jti)));

    public Task RevokeAccessJtiAsync(string userId, string jti)
    {
        _store.TryRemove(AccessKey(userId, jti), out _);
        return Task.CompletedTask;
    }

    public Task RevokeRefreshJtiAsync(string userId, string jti)
    {
        _store.TryRemove(RefreshKey(userId, jti), out _);
        return Task.CompletedTask;
    }

    public Task RevokeAllForUserAsync(string userId)
    {
        foreach (var key in _store.Keys)
        {
            if (key.StartsWith($"access_jti:{userId}:", StringComparison.Ordinal) ||
                key.StartsWith($"refresh_jti:{userId}:", StringComparison.Ordinal))
            {
                _store.TryRemove(key, out _);
            }
        }
        return Task.CompletedTask;
    }

    private bool IsActive(string key)
    {
        if (_store.TryGetValue(key, out var expiry))
        {
            if (expiry > DateTimeOffset.UtcNow)
            {
                return true;
            }
            // expired, clean up
            _store.TryRemove(key, out _);
        }
        return false;
    }
}
