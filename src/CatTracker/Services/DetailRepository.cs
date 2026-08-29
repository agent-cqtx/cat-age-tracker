using CatTracker.Models;

namespace CatTracker.Services;

public class DetailRepository
{
    private const string FileName = "details.json";
    private readonly JsonFileStore _store;

    public DetailRepository(JsonFileStore store) => _store = store;

    public async Task<List<DetailEntry>> GetAllAsync()
    {
        var list = await _store.LoadAsync(FileName, () => new List<DetailEntry>());
        return list.OrderByDescending(e => e.CreatedAt).ToList();
    }

    public Task<DetailEntry> AddAsync(DetailEntry entry) =>
        _store.MutateAsync<List<DetailEntry>, DetailEntry>(FileName, () => new List<DetailEntry>(), list =>
        {
            list.Add(entry);
            return entry;
        });

    public Task<DetailEntry?> UpdateAsync(Guid id, Action<DetailEntry> apply) =>
        _store.MutateAsync<List<DetailEntry>, DetailEntry?>(FileName, () => new List<DetailEntry>(), list =>
        {
            var existing = list.FirstOrDefault(e => e.Id == id);
            if (existing is null) return null;

            apply(existing);
            existing.UpdatedAt = DateTime.UtcNow;
            return existing;
        });

    public Task<bool> DeleteAsync(Guid id) =>
        _store.MutateAsync(FileName, () => new List<DetailEntry>(), list => list.RemoveAll(e => e.Id == id) > 0);
}
