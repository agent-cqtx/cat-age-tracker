using HenryTracker.Models;

namespace HenryTracker.Services;

public class ProfileRepository
{
    private const string FileName = "profile.json";
    private readonly JsonFileStore _store;

    public ProfileRepository(JsonFileStore store) => _store = store;

    public Task<CatProfile> GetAsync() => _store.LoadAsync(FileName, () => new CatProfile());

    public Task SaveAsync(CatProfile profile) => _store.SaveAsync(FileName, profile);
}
