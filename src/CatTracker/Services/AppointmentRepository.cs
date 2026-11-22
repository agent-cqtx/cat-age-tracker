using CatTracker.Models;

namespace CatTracker.Services;

public class AppointmentRepository
{
    private const string FileName = "appointments.json";
    private readonly JsonFileStore _store;

    public AppointmentRepository(JsonFileStore store) => _store = store;

    public async Task<List<Appointment>> GetAllAsync()
    {
        var list = await _store.LoadAsync(FileName, () => new List<Appointment>());
        return list.OrderBy(a => a.When).ToList();
    }

    public Task<Appointment> AddAsync(Appointment appointment) =>
        _store.MutateAsync<List<Appointment>, Appointment>(FileName, () => new List<Appointment>(), list =>
        {
            list.Add(appointment);
            return appointment;
        });

    public async Task<Appointment?> GetByIdAsync(Guid id)
    {
        var list = await _store.LoadAsync(FileName, () => new List<Appointment>());
        return list.FirstOrDefault(a => a.Id == id);
    }

    public Task<Appointment?> UpdateAsync(Guid id, Action<Appointment> apply) =>
        _store.MutateAsync<List<Appointment>, Appointment?>(FileName, () => new List<Appointment>(), list =>
        {
            var existing = list.FirstOrDefault(a => a.Id == id);
            if (existing is null) return null;

            apply(existing);
            existing.UpdatedAt = DateTime.UtcNow;
            return existing;
        });

    public Task<bool> DeleteAsync(Guid id) =>
        _store.MutateAsync(FileName, () => new List<Appointment>(), list => list.RemoveAll(a => a.Id == id) > 0);
}
