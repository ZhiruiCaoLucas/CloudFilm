namespace CloudFilm.Application.Abstractions;

/// <summary>
/// A distributed key/value cache with mandatory expiry.
/// </summary>
/// <remarks>
/// CloudFilm caches imaging metadata and short lived access grants outside the
/// hospital network. Every entry therefore carries a time to live; there is
/// deliberately no API for writing an entry that never expires.
/// </remarks>
public interface ICacheStore
{
    /// <summary>
    /// Reads a value from the cache.
    /// </summary>
    /// <typeparam name="T">The type the stored payload deserialises to.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The cached value, or <see langword="null"/> when absent or expired.</returns>
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        where T : class;

    /// <summary>
    /// Writes a value to the cache.
    /// </summary>
    /// <typeparam name="T">The type of the payload.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="value">The value to store.</param>
    /// <param name="timeToLive">How long the entry remains valid.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes once the value has been written.</returns>
    Task SetAsync<T>(string key, T value, TimeSpan timeToLive, CancellationToken cancellationToken = default)
        where T : class;

    /// <summary>
    /// Removes a value from the cache.
    /// </summary>
    /// <param name="key">The cache key.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes once the value has been removed.</returns>
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
}
