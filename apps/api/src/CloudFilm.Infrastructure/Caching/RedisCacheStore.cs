using System.Text.Json;
using CloudFilm.Application.Abstractions;
using Microsoft.Extensions.Caching.Distributed;

namespace CloudFilm.Infrastructure.Caching;

/// <summary>
/// Redis backed implementation of <see cref="ICacheStore"/>.
/// </summary>
/// <remarks>
/// Values are stored as UTF-8 JSON. Every write carries an absolute expiry, which
/// is what keeps imaging metadata outside the hospital network transient.
/// </remarks>
internal sealed class RedisCacheStore(IDistributedCache cache) : ICacheStore
{
    private static readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IDistributedCache _cache = cache;

    /// <inheritdoc />
    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        where T : class
    {
        byte[]? payload = await _cache.GetAsync(key, cancellationToken).ConfigureAwait(false);

        return payload is null or { Length: 0 }
            ? null
            : JsonSerializer.Deserialize<T>(payload, _serializerOptions);
    }

    /// <inheritdoc />
    public Task SetAsync<T>(string key, T value, TimeSpan timeToLive, CancellationToken cancellationToken = default)
        where T : class
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeToLive, TimeSpan.Zero);

        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(value, _serializerOptions);
        DistributedCacheEntryOptions options = new() { AbsoluteExpirationRelativeToNow = timeToLive };

        return _cache.SetAsync(key, payload, options, cancellationToken);
    }

    /// <inheritdoc />
    public Task RemoveAsync(string key, CancellationToken cancellationToken = default) =>
        _cache.RemoveAsync(key, cancellationToken);
}
