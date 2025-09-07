using Core.Interfaces;
using Newtonsoft.Json;
using StackExchange.Redis;

namespace Core.Services;

public class CacheService : ICacheService
{
    private readonly IConnectionMultiplexer _redisConnection;
    private readonly IDatabase _database;

    public CacheService(IConnectionMultiplexer redisConnection)
    {
        _redisConnection = redisConnection;
        _database = redisConnection.GetDatabase();
    }

    /// <summary>
    /// Retrieves a value from the cache by its key.
    /// </summary>
    /// <typeparam name="T">Type of the object to retrieve.</typeparam>
    /// <param name="key">Key of the object to retrieve.</param>
    /// <returns>An instance of the object if found; otherwise, default value of <typeparamref name="T"/>.</returns>
    public async Task<T?> GetAsync<T>(string key)
    {
        try
        {
            var json = await _database.StringGetAsync(key);
            return DeserializeGenericOrString<T>(json);
        }
        catch (StackExchange.Redis.RedisConnectionException ex)
        {
            // Log and fallback if Redis is unavailable
            Console.WriteLine($"Redis unavailable: {ex.Message}");
            return default(T);
        }
    }

    /// <summary>
    /// Retrieves or creates a value in the cache by its key.
    /// </summary>
    /// <typeparam name="T">Type of the object to retrieve or create.</typeparam>
    /// <param name="key">Key of the object to retrieve or create.</param>
    /// <param name="action">Callback to execute if the object is not found in the cache.</param>
    /// <param name="cacheTimeInMinutes">Time-to-live for the cache entry, in minutes.</param>
    /// <returns>The retrieved or created object.</returns>
    public async Task<T> GetOrCreateAsync<T>(
        string key,
        Func<Task<T>> action,
        int cacheTimeInMinutes
    )
    {
        try
        {
            var item = await GetAsync<T>(key);
            if (item != null)
            {
                return item;
            }
            var actionResult = await action.Invoke();
            await SetAsync(key, actionResult, cacheTimeInMinutes);
            return actionResult;
        }
        catch (StackExchange.Redis.RedisConnectionException ex)
        {
            // Log and fallback to DB if Redis is unavailable
            Console.WriteLine($"Redis unavailable: {ex.Message}");
            return await action.Invoke();
        }
    }

    /// <summary>
    /// Adds or updates a value in the cache.
    /// </summary>
    /// <typeparam name="T">Type of the object to add or update.</typeparam>
    /// <param name="key">Key of the object to add or update.</param>
    /// <param name="data">Content of the object to store.</param>
    /// <param name="cacheTimeInMinutes">Time-to-live for the cache entry, in minutes.</param>
    public async Task SetAsync<T>(string key, T data, int cacheTimeInMinutes)
    {
        var json =
            typeof(T) == typeof(string)
                ? (
                    data == null
                        ? string.Empty
                        : (string)Convert.ChangeType(data, typeof(string))
                )
                : JsonConvert.SerializeObject(data);

        var expiry = TimeSpan.FromMinutes(cacheTimeInMinutes);
        await _database.StringSetAsync(key, json, expiry);
    }

    /// <summary>
    /// Removes a value from the cache by its key.
    /// </summary>
    /// <param name="key">Key of the object to remove.</param>
    public async Task RemoveAsync(string key)
    {
        await _database.KeyDeleteAsync(key);
    }

    /// <summary>
    /// Attempts to retrieve a value from the cache by its key.
    /// </summary>
    /// <typeparam name="T">Type of the object to retrieve.</typeparam>
    /// <param name="key">Key of the object to retrieve.</param>
    /// <param name="result">The retrieved object, if found.</param>
    /// <returns>True if the object was found; otherwise, false.</returns>
    public bool TryGetValue<T>(string key, out T result)
    {
        try
        {
            var json = _database.StringGet(key);
            var deserialized = DeserializeGenericOrString<T>(json);
            if (deserialized is not null)
            {
                result = deserialized;
                return true;
            }
            else
            {
                result = default!;
                return false;
            }
        }
        catch
        {
            result = default!;
            return false;
        }
    }

    private static T? DeserializeGenericOrString<T>(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return default;
        }

        return typeof(T) == typeof(string)
            ? (T)Convert.ChangeType(json, typeof(T))
            : JsonConvert.DeserializeObject<T>(json);
    }
}