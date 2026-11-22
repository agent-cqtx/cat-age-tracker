using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CatTracker.Services;

/// <summary>
/// A tiny embedded document store: each "table" is one JSON file on disk
/// under the configured data directory. Reads and writes to the same file
/// are serialized through a per-file lock, and writes go through a temp
/// file + atomic rename so a crash mid-write can't corrupt the data.
///
/// This is intentionally not a real database engine. For a single-user app
/// with a handful of appointments and notes, it's simpler to reason about
/// and has zero external dependencies (see the .csproj). If this ever needs
/// to scale beyond one person's cat, swapping this out for EF Core + SQLite
/// behind the same repository classes would be a small, contained change.
/// </summary>
public class JsonFileStore
{
    private readonly string _basePath;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public JsonFileStore(string basePath)
    {
        _basePath = basePath;
        Directory.CreateDirectory(_basePath);
    }

    /// <summary>The data directory this store persists under (read-only).</summary>
    public string BasePath => _basePath;

    private SemaphoreSlim LockFor(string fileName) =>
        _locks.GetOrAdd(fileName, _ => new SemaphoreSlim(1, 1));

    private string PathFor(string fileName) => Path.Combine(_basePath, fileName);

    public async Task<T> LoadAsync<T>(string fileName, Func<T> defaultFactory)
    {
        var sem = LockFor(fileName);
        await sem.WaitAsync();
        try
        {
            return await LoadUnsafeAsync(fileName, defaultFactory);
        }
        finally
        {
            sem.Release();
        }
    }

    /// <summary>Whether this "table" has ever been saved. Used to detect first-run state.</summary>
    public async Task<bool> ExistsAsync(string fileName)
    {
        var sem = LockFor(fileName);
        await sem.WaitAsync();
        try
        {
            return File.Exists(PathFor(fileName));
        }
        finally
        {
            sem.Release();
        }
    }

    public async Task SaveAsync<T>(string fileName, T data)
    {
        var sem = LockFor(fileName);
        await sem.WaitAsync();
        try
        {
            await SaveUnsafeAsync(fileName, data);
        }
        finally
        {
            sem.Release();
        }
    }

    /// <summary>
    /// Loads, applies <paramref name="mutate"/>, and saves — all while
    /// holding the file's lock, so concurrent add/update/delete calls can't
    /// interleave and lose an update.
    /// </summary>
    public async Task<TResult> MutateAsync<T, TResult>(string fileName, Func<T> defaultFactory, Func<T, TResult> mutate)
    {
        var sem = LockFor(fileName);
        await sem.WaitAsync();
        try
        {
            var data = await LoadUnsafeAsync(fileName, defaultFactory);
            var result = mutate(data);
            await SaveUnsafeAsync(fileName, data);
            return result;
        }
        finally
        {
            sem.Release();
        }
    }

    private async Task<T> LoadUnsafeAsync<T>(string fileName, Func<T> defaultFactory)
    {
        var path = PathFor(fileName);
        if (!File.Exists(path))
        {
            return defaultFactory();
        }

        await using var stream = File.OpenRead(path);
        var result = await JsonSerializer.DeserializeAsync<T>(stream, Options);
        return result ?? defaultFactory();
    }

    private async Task SaveUnsafeAsync<T>(string fileName, T data)
    {
        var path = PathFor(fileName);
        var tmpPath = path + ".tmp";

        await using (var stream = File.Create(tmpPath))
        {
            await JsonSerializer.SerializeAsync(stream, data, Options);
        }

        File.Move(tmpPath, path, overwrite: true);
    }
}
