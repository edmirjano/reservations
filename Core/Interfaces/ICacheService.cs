namespace Core.Interfaces;

/// <summary>
/// Interface for cache service operations.
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// Retrieves a value from the cache by its key.
    /// </summary>
    /// <typeparam name="T">Type of the object to retrieve.</typeparam>
    /// <param name="key">Key of the object to retrieve.</param>
    /// <returns>An instance of the object if found; otherwise, default value of <typeparamref name="T"/>.</returns>
    Task<T?> GetAsync<T>(string key);

    /// <summary>
    /// Retrieves or creates a value in the cache by its key.
    /// </summary>
    /// <typeparam name="T">Type of the object to retrieve or create.</typeparam>
    /// <param name="key">Key of the object to retrieve or create.</param>
    /// <param name="action">Callback to execute if the object is not found in the cache.</param>
    /// <param name="cacheTimeInMinutes">Time-to-live for the cache entry, in minutes.</param>
    /// <returns>The retrieved or created object.</returns>
    Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> action, int cacheTimeInMinutes);

    /// <summary>
    /// Adds or updates a value in the cache.
    /// </summary>
    /// <typeparam name="T">Type of the object to add or update.</typeparam>
    /// <param name="key">Key of the object to add or update.</param>
    /// <param name="data">Content of the object to store.</param>
    /// <param name="cacheTimeInMinutes">Time-to-live for the cache entry, in minutes.</param>
    Task SetAsync<T>(string key, T data, int cacheTimeInMinutes);

    /// <summary>
    /// Removes a value from the cache by its key.
    /// </summary>
    /// <param name="key">Key of the object to remove.</param>
    Task RemoveAsync(string key);

    /// <summary>
    /// Attempts to retrieve a value from the cache by its key.
    /// </summary>
    /// <typeparam name="T">Type of the object to retrieve.</typeparam>
    /// <param name="key">Key of the object to retrieve.</param>
    /// <param name="result">The retrieved object, if found.</param>
    /// <returns>True if the object was found; otherwise, false.</returns>
    bool TryGetValue<T>(string key, out T result);
}
