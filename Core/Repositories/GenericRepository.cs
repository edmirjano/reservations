using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Reflection;
using Core.Interfaces;
using Core.Models;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Core.Repositories;

/// <summary>
/// Generic repository implementation for basic CRUD operations
/// </summary>
/// <typeparam name="T">Entity type that inherits from GenericModel</typeparam>
/// <typeparam name="TContext">DbContext type for database operations</typeparam>
public class GenericRepository<T, TContext> : IGeneric<T>
    where T : GenericModel
    where TContext : DbContext
{
    #region Properties
    protected readonly TContext _context;
    private readonly DbSet<T> _entity;
    private readonly Func<IDbConnection> _dbConnectionFactory;
    #endregion

    protected GenericRepository(TContext context, Func<IDbConnection> dbConnectionFactory)
    {
        _context = context;
        _entity = context.Set<T>();
        _dbConnectionFactory = dbConnectionFactory;
    }

    #region Syncronous Methods
    /// <summary>
    /// Retrieves all entities of type T from the database
    /// </summary>
    /// <returns>A list containing all entities of type T</returns>
    public IEnumerable<T> GetAll()
    {
        return _entity.ToList();
    }

    /// <summary>
    /// Retrieves a specific entity by its unique identifier
    /// </summary>
    /// <param name="id">The unique identifier of the entity to retrieve</param>
    /// <returns>The entity matching the provided ID, if it exists and is not deleted</returns>
    public T GetById(Guid id)
    {
        return _entity
            .AsNoTracking()
            .Where(s => s.Id == id && s.IsDeleted == false)
            .FirstOrDefault();
    }

    /// <summary>
    /// Creates a new entity in the database with automatic timestamp and state management
    /// </summary>
    /// <param name="entity">The entity to create</param>
    /// <param name="isActive">Optional parameter to set the initial active state (defaults to true)</param>
    /// <returns>The created entity with generated ID and timestamps</returns>
    public T Create(T entity, bool isActive = true)
    {
        entity.CreatedAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.IsActive = isActive;
        entity.IsDeleted = false;

        _entity.Add(entity);
        _context.SaveChanges();

        return entity;
    }

    /// <summary>
    /// Updates an existing entity in the database, handling entity tracking and timestamp updates
    /// </summary>
    /// <param name="entity">The entity with updated values</param>
    /// <returns>The updated entity with refreshed timestamp</returns>
    public T Update(T entity)
    {
        var trackedEntity = _context
            .ChangeTracker.Entries<T>()
            .FirstOrDefault(e => e.Entity.Id == entity.Id);
        if (trackedEntity != null)
        {
            _context.Entry(trackedEntity.Entity).State = EntityState.Detached;
        }

        entity.UpdatedAt = DateTime.UtcNow;
        _entity.Update(entity);
        _context.SaveChanges();
        return entity;
    }

    /// <summary>
    /// Soft deletes an entity by setting IsDeleted to true and IsActive to false
    /// </summary>
    /// <param name="id">The unique identifier of the entity to delete</param>
    /// <returns>The soft-deleted entity with updated state and timestamp</returns>
    public T Delete(Guid id)
    {
        T entity = GetById(id);
        entity.IsDeleted = true;
        entity.IsActive = false;
        entity.UpdatedAt = DateTime.UtcNow;
        _entity.Update(entity);
        _context.SaveChanges();

        return entity;
    }
    #endregion

    #region Asyncronous Methods
    /// <summary>
    /// Asynchronously retrieves all entities of type T from the database
    /// </summary>
    /// <returns>A task containing a list of all entities of type T</returns>
    public async Task<IEnumerable<T>> GetAllAsync()
    {
        return await _entity.Where(s => s.IsDeleted == false).ToListAsync();
    }

    /// <summary>
    /// Asynchronously retrieves a specific entity by its unique identifier
    /// </summary>
    /// <param name="id">The unique identifier of the entity to retrieve</param>
    /// <returns>A task containing the entity matching the provided ID, if it exists and is not deleted, or null</returns>
    public async Task<T?> GetByIdAsync(Guid id)
    {
        try
        {
            return await _entity
                .AsNoTracking()
                .Where(s => s.Id == id && s.IsDeleted == false)
                .FirstOrDefaultAsync();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ERROR] Failed to get entity by ID {id}: {ex.Message}");
            Console.Error.WriteLine($"StackTrace: {ex.StackTrace}");
            throw;
        }
    }

    protected async Task<T> CreateAsync(T entity, bool isActive = true)
    {
        try
        {
            entity.CreatedAt = DateTime.UtcNow;
            entity.UpdatedAt = DateTime.UtcNow;
            entity.IsActive = isActive;
            entity.IsDeleted = false;

            await _entity.AddAsync(entity);
            await _context.SaveChangesAsync();

            return entity;
        }
        catch (DbUpdateException dbEx)
        {
            var innerMessage = dbEx.InnerException?.Message ?? "No inner exception";

            Console.Error.WriteLine($"[DB ERROR] Failed to save changes: {innerMessage}");
            Console.Error.WriteLine($"StackTrace: {dbEx.StackTrace}");

            foreach (var entry in dbEx.Entries)
            {
                var entityType = entry.Entity.GetType().Name;
                var state = entry.State.ToString();
                var values = entry.CurrentValues.Properties.ToDictionary(
                    p => p.Name,
                    p => entry.CurrentValues[p]
                );

                Console.Error.WriteLine($"Entity type: {entityType}, State: {state}");
                foreach (var kv in values)
                {
                    Console.Error.WriteLine($"  {kv.Key}: {kv.Value}");
                }
            }

            throw;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ERROR] Unexpected error during CreateAsync: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Asynchronously updates an existing entity in the database, handling entity tracking and timestamp updates
    /// </summary>
    /// <param name="entity">The entity with updated values</param>
    /// <returns>A task containing the updated entity with refreshed timestamp</returns>
    public async Task<T> UpdateAsync(T entity)
    {
        try
        {
            // Detach any existing tracked entity
            var trackedEntity = _context
                .ChangeTracker.Entries<T>()
                .FirstOrDefault(e => e.Entity.Id == entity.Id);
            if (trackedEntity != null)
            {
                _context.Entry(trackedEntity.Entity).State = EntityState.Detached;
            }

            // Set the entity state to modified
            _context.Entry(entity).State = EntityState.Modified;
            entity.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return entity;
        }
        catch (DbUpdateException dbEx)
        {
            var innerMessage = dbEx.InnerException?.Message ?? "No inner exception";
            Console.Error.WriteLine($"[DB ERROR] Failed to update entity: {innerMessage}");
            Console.Error.WriteLine($"StackTrace: {dbEx.StackTrace}");
            throw;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ERROR] Unexpected error during UpdateAsync: {ex.Message}");
            Console.Error.WriteLine($"StackTrace: {ex.StackTrace}");
            throw;
        }
    }

    /// <summary>
    /// Asynchronously soft deletes an entity by setting IsDeleted to true and IsActive to false
    /// </summary>
    /// <param name="id">The unique identifier of the entity to delete</param>
    /// <returns>A task containing the soft-deleted entity with updated state and timestamp, or null if not found</returns>
    public async Task<T?> DeleteAsync(Guid id)
    {
        T? entity = await GetByIdAsync(id);
        if (entity == null)
            return null;

        // Detach any tracked entity with the same key
        var trackedEntity = _context
            .ChangeTracker.Entries<T>()
            .FirstOrDefault(e => e.Entity.Id == id);
        if (trackedEntity != null)
        {
            _context.Entry(trackedEntity.Entity).State = EntityState.Detached;
        }

        entity.IsDeleted = true;
        entity.IsActive = false;
        entity.UpdatedAt = DateTime.UtcNow;
        _entity.Update(entity);
        await _context.SaveChangesAsync();

        return entity;
    }
    #endregion

    #region Helper Methods

    /// <summary>
    /// Opens a database connection with proper async handling for different connection types
    /// </summary>
    public static async Task OpenConnectionAsync(IDbConnection connection)
    {
        if (connection.State == ConnectionState.Open)
        {
            return;
        }

        if (connection is NpgsqlConnection npgsqlConnection)
        {
            await npgsqlConnection.OpenAsync();
        }
        else
        {
            connection.Open();
        }
    }

    public string ValidateSlug(string name)
    {
        // Generate the initial slug
        string slug = SlugGenerator.GenerateSlug(name);
        string newSlug = slug;
        int counter = 1;

        // Dynamically get the table name based on the type T
        string tableName = GetTableName<T>();

        // Query to check for an existing slug in the appropriate table
        while (true)
        {
            var existingSlug = QueryFirstOrDefault<string>(
                $@"SELECT ""Slug"" FROM ""{tableName}"" WHERE ""Slug"" = @Slug",
                new { Slug = newSlug }
            );

            if (existingSlug == null)
            {
                return newSlug;
            }

            newSlug = $"{slug}{counter}";
            counter++;
        }
    }

    // Helper method to retrieve the table name of the entity type T
    private string GetTableName<TEntity>()
    {
        // First check if the entity type has a TableAttribute
        var tableAttribute = typeof(TEntity).GetCustomAttribute<TableAttribute>();
        if (tableAttribute != null)
        {
            return tableAttribute.Name;
        }

        // Fallback: Return the class name if no TableAttribute is found
        return typeof(TEntity).Name;
    }
    #endregion

    #region Dapper Execute Methods
    /// <summary>
    /// Executes an SQL command while ensuring DateTime fields are converted to UTC.
    /// </summary>
    public int Execute(string sql, object parameters = null)
    {
        using var dbConnection = _dbConnectionFactory.Invoke();
        dbConnection.Open();
        return dbConnection.Execute(sql, parameters);
    }

    /// <summary>
    /// Executes an SQL command asynchronously while ensuring DateTime fields are converted to UTC.
    /// </summary>
    public async Task<int> ExecuteAsync(
        string sql,
        object parameters = null,
        int? commandTimeout = null,
        CommandType? commandType = null
    )
    {
        using var dbConnection = _dbConnectionFactory.Invoke();
        await OpenConnectionAsync(dbConnection);
        return await dbConnection.ExecuteAsync(sql, parameters, null, commandTimeout, commandType);
    }

    /// <summary>
    /// Executes a scalar query and returns the first column of the first row in the result set.
    /// </summary>
    public T1 ExecuteScalar<T1>(string sql, object parameters = null)
    {
        using var dbConnection = _dbConnectionFactory.Invoke();
        dbConnection.Open();
        return dbConnection.ExecuteScalar<T1>(sql, parameters);
    }

    /// <summary>
    /// Executes a scalar query asynchronously and returns the first column of the first row in the result set.
    /// </summary>
    public async Task<T1> ExecuteScalarAsync<T1>(string sql, object parameters = null)
    {
        using var dbConnection = _dbConnectionFactory.Invoke();
        await OpenConnectionAsync(dbConnection);
        return await dbConnection.ExecuteScalarAsync<T1>(sql, parameters);
    }
    #endregion

    #region Dapper Query Methods
    /// <summary>
    /// Executes an SQL query asynchronously while ensuring DateTime fields are converted to UTC.
    /// </summary>
    public async Task<IEnumerable<T1>> QueryAsync<T1>(string sql, object parameters = null)
    {
        using var dbConnection = _dbConnectionFactory.Invoke();
        await OpenConnectionAsync(dbConnection);
        return await dbConnection.QueryAsync<T1>(sql, parameters);
    }

    /// <summary>
    /// Executes an SQL query while ensuring DateTime fields are converted to UTC.
    /// </summary>
    public IEnumerable<T1> Query<T1>(string sql, object parameters = null)
    {
        using var dbConnection = _dbConnectionFactory.Invoke();
        dbConnection.Open();
        return dbConnection.Query<T1>(sql, parameters);
    }

    /// <summary>
    /// Executes an SQL query asynchronously with mapping to multiple types, while ensuring DateTime fields are converted to UTC.
    /// </summary>
    public async Task<IEnumerable<TReturn>> QueryAsync<T1, T2, TReturn>(
        string sql,
        Func<T1, T2, TReturn> map,
        object parameters = null,
        string splitOn = "Id"
    )
    {
        using var dbConnection = _dbConnectionFactory.Invoke();
        await OpenConnectionAsync(dbConnection);
        return await dbConnection.QueryAsync(sql, map, parameters, null, splitOn: splitOn);
    }

    /// <summary>
    /// Executes an SQL query asynchronously with mapping to multiple types, while ensuring DateTime fields are converted to UTC.
    /// </summary>
    protected async Task<IEnumerable<TReturn>> QueryAsync<T1, T2, T3, TReturn>(
        string sql,
        Func<T1, T2, T3, TReturn> map,
        object parameters = null,
        string splitOn = "Id"
    )
    {
        using var dbConnection = _dbConnectionFactory.Invoke();
        await OpenConnectionAsync(dbConnection);
        return await dbConnection.QueryAsync(sql, map, parameters, null, splitOn: splitOn);
    }

    /// <summary>
    /// Executes an SQL query asynchronously with mapping to multiple types, while ensuring DateTime fields are converted to UTC.
    /// </summary>
    public async Task<IEnumerable<TReturn>> QueryAsync<T1, T2, T3, T4, TReturn>(
        string sql,
        Func<T1, T2, T3, T4, TReturn> map,
        object parameters = null,
        string splitOn = "Id"
    )
    {
        using var dbConnection = _dbConnectionFactory.Invoke();
        await OpenConnectionAsync(dbConnection);
        return await dbConnection.QueryAsync(sql, map, parameters, null, splitOn: splitOn);
    }

    /// <summary>
    /// Executes an SQL query asynchronously with mapping to multiple types, while ensuring DateTime fields are converted to UTC.
    /// </summary>
    public async Task<IEnumerable<TReturn>> QueryAsync<T1, T2, T3, T4, T5, TReturn>(
        string sql,
        Func<T1, T2, T3, T4, T5, TReturn> map,
        object parameters = null,
        string splitOn = "Id"
    )
    {
        using var dbConnection = _dbConnectionFactory.Invoke();
        await OpenConnectionAsync(dbConnection);
        return await dbConnection.QueryAsync(sql, map, parameters, null, splitOn: splitOn);
    }

    /// <summary>
    /// Executes an SQL query asynchronously with mapping to multiple types, while ensuring DateTime fields are converted to UTC.
    /// </summary>
    public async Task<IEnumerable<TReturn>> QueryAsync<T1, T2, T3, T4, T5, T6, TReturn>(
        string sql,
        Func<T1, T2, T3, T4, T5, T6, TReturn> map,
        object parameters = null,
        string splitOn = "Id"
    )
    {
        using var dbConnection = _dbConnectionFactory.Invoke();
        await OpenConnectionAsync(dbConnection);
        return await dbConnection.QueryAsync(sql, map, parameters, null, splitOn: splitOn);
    }

    /// <summary>
    /// Executes an SQL query asynchronously with mapping to multiple types, while ensuring DateTime fields are converted to UTC.
    /// </summary>
    public async Task<IEnumerable<TReturn>> QueryAsync<T1, T2, T3, T4, T5, T6, T7, TReturn>(
        string sql,
        Func<T1, T2, T3, T4, T5, T6, T7, TReturn> map,
        object parameters = null,
        string splitOn = "Id"
    )
    {
        using var dbConnection = _dbConnectionFactory.Invoke();
        await OpenConnectionAsync(dbConnection);
        return await dbConnection.QueryAsync(sql, map, parameters, null, splitOn: splitOn);
    }

    /// <summary>
    /// Executes an SQL query and returns the first result, or a default value if no result exists.
    /// </summary>
    public T1 QueryFirstOrDefault<T1>(string sql, object parameters = null)
    {
        using var dbConnection = _dbConnectionFactory.Invoke();
        dbConnection.Open();
        return dbConnection.QueryFirstOrDefault<T1>(sql, parameters);
    }

    /// <summary>
    /// Executes an SQL query asynchronously and returns the first result, or a default value if no result exists.
    /// </summary>
    public async Task<T1> QueryFirstOrDefaultAsync<T1>(string sql, object parameters = null)
    {
        using var dbConnection = _dbConnectionFactory.Invoke();
        await OpenConnectionAsync(dbConnection);
        return await dbConnection.QueryFirstOrDefaultAsync<T1>(sql, parameters);
    }

    /// <summary>
    /// Executes an SQL query and returns the single result, or throws an exception if zero or multiple results exist.
    /// </summary>
    public T1 QuerySingle<T1>(string sql, object parameters = null)
    {
        using var dbConnection = _dbConnectionFactory.Invoke();
        dbConnection.Open();
        return dbConnection.QuerySingle<T1>(sql, parameters);
    }

    /// <summary>
    /// Executes an SQL query asynchronously and returns the single result, or throws an exception if zero or multiple results exist.
    /// </summary>
    public async Task<T1> QuerySingleAsync<T1>(string sql, object parameters = null)
    {
        using var dbConnection = _dbConnectionFactory.Invoke();
        await OpenConnectionAsync(dbConnection);
        return await dbConnection.QuerySingleAsync<T1>(sql, parameters);
    }

    /// <summary>
    /// Executes an SQL query and returns the single result, or a default value if no result exists.
    /// </summary>
    public T1 QuerySingleOrDefault<T1>(string sql, object parameters = null)
    {
        using var dbConnection = _dbConnectionFactory.Invoke();
        dbConnection.Open();
        return dbConnection.QuerySingleOrDefault<T1>(sql, parameters);
    }

    /// <summary>
    /// Executes an SQL query asynchronously and returns the single result, or a default value if no result exists.
    /// </summary>
    public async Task<T1> QuerySingleOrDefaultAsync<T1>(string sql, object parameters = null)
    {
        using var dbConnection = _dbConnectionFactory.Invoke();
        await OpenConnectionAsync(dbConnection);
        return await dbConnection.QuerySingleOrDefaultAsync<T1>(sql, parameters);
    }

    /// <summary>
    /// Executes a query allowing multiple result sets to be processed using separate reader objects.
    /// The connection and reader are managed internally and disposed of correctly.
    /// </summary>
    /// <typeparam name="TResult">The type of the result returned by the processor.</typeparam>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="parameters">The parameters for the SQL query.</param>
    /// <param name="processor">A function that takes a GridReader and returns a result of type TResult.</param>
    /// <returns>The result processed by the processor function.</returns>
    public TResult QueryMultiple<TResult>(
        string sql,
        object parameters,
        Func<SqlMapper.GridReader, TResult> processor
    )
    {
        using var dbConnection = _dbConnectionFactory.Invoke();
        dbConnection.Open(); // Open synchronously for this modified method
        using var multi = dbConnection.QueryMultiple(sql, parameters);
        return processor(multi);
    }

    /// <summary>
    /// Asynchronously executes a query allowing multiple result sets to be processed.
    /// The connection and reader are managed internally and disposed of correctly.
    /// </summary>
    /// <typeparam name="TResult">The type of the result returned by the processor.</typeparam>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="parameters">The parameters for the SQL query.</param>
    /// <param name="processor">A function that takes a GridReader and returns a Task of TResult.</param>
    /// <returns>A task that represents the asynchronous operation, containing the result processed by the processor function.</returns>
    public async Task<TResult> QueryMultipleAsync<TResult>(
        string sql,
        object parameters,
        Func<SqlMapper.GridReader, Task<TResult>> processor
    )
    {
        using var dbConnection = _dbConnectionFactory.Invoke();
        await OpenConnectionAsync(dbConnection);
        using var multi = await dbConnection.QueryMultipleAsync(sql, parameters);
        return await processor(multi);
    }
    #endregion
}
