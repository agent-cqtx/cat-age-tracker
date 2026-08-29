using CatTracker.Models;

namespace CatTracker.Services;

public class ProfileRepository
{
    private const string FileName = "profile.json";
    private readonly JsonFileStore _store;

    public ProfileRepository(JsonFileStore store) => _store = store;

    public Task<CatProfile> GetAsync() => _store.LoadAsync(FileName, () => new CatProfile());

    public Task SaveAsync(CatProfile profile) => _store.SaveAsync(FileName, profile);

    /// <summary>
    /// Returns the profile plus whether it has ever been saved. A profile
    /// that's never been saved (fresh install, no profile.json yet) means
    /// onboarding hasn't happened — this is the single source of truth for
    /// that, rather than a separate persisted flag that could drift.
    /// </summary>
    public async Task<(CatProfile Profile, bool IsOnboarded)> GetWithStatusAsync()
    {
        var isOnboarded = await _store.ExistsAsync(FileName);
        var profile = await GetAsync();
        return (profile, isOnboarded);
    }
}
