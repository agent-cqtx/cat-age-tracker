using CatTracker.Models;

namespace CatTracker.Services;

/// <summary>
/// Metadata lives in attachments.json (same pattern as DetailRepository);
/// blob bytes live in an "attachments" subdirectory of the data directory,
/// each file named by the attachment Id in "N" Guid format, no extension.
/// Display names are NEVER used for paths — only Ids are, which keeps the
/// file layer immune to path-traversal tricks in uploaded filenames.
/// </summary>
public class AttachmentRepository
{
    private const string FileName = "attachments.json";
    private const string BlobDirName = "attachments";

    private readonly JsonFileStore _store;
    private readonly string _blobDir;

    public AttachmentRepository(JsonFileStore store)
    {
        _store = store;
        _blobDir = Path.Combine(store.BasePath, BlobDirName);
    }

    public async Task<List<Attachment>> GetAllAsync()
    {
        var list = await _store.LoadAsync(FileName, () => new List<Attachment>());
        return list.OrderByDescending(a => a.CreatedAt).ToList();
    }

    /// <summary>General (not appointment-tied) attachments, newest first.</summary>
    public async Task<List<Attachment>> GetGeneralAsync() =>
        (await GetAllAsync()).Where(a => a.AppointmentId is null).ToList();

    public async Task<List<Attachment>> GetForAppointmentAsync(Guid appointmentId) =>
        (await GetAllAsync()).Where(a => a.AppointmentId == appointmentId).ToList();

    public async Task<Attachment?> GetByIdAsync(Guid id)
    {
        var list = await _store.LoadAsync(FileName, () => new List<Attachment>());
        return list.FirstOrDefault(a => a.Id == id);
    }

    public Task<Attachment> AddAsync(Attachment attachment) =>
        _store.MutateAsync<List<Attachment>, Attachment>(FileName, () => new List<Attachment>(), list =>
        {
            list.Add(attachment);
            return attachment;
        });

    /// <summary>Deletes the metadata row; returns it (so the caller can remove the blob) or null.</summary>
    public Task<Attachment?> DeleteAsync(Guid id) =>
        _store.MutateAsync(FileName, () => new List<Attachment>(), list =>
        {
            var existing = list.FirstOrDefault(a => a.Id == id);
            if (existing is null) return null;
            list.Remove(existing);
            return existing;
        });

    /// <summary>Removes every attachment row tied to an appointment. Returns the removed rows so their blobs can be deleted too.</summary>
    public Task<List<Attachment>> DeleteForAppointmentAsync(Guid appointmentId) =>
        _store.MutateAsync(FileName, () => new List<Attachment>(), list =>
        {
            var removed = list.FindAll(a => a.AppointmentId == appointmentId);
            if (removed.Count > 0)
            {
                list.RemoveAll(a => a.AppointmentId == appointmentId);
            }
            return removed;
        });

    // ---- Blob storage ------------------------------------------------------
    // One Guid-\"N\" file per attachment, no extension. Directory is created
    // on demand before each write so a fresh checkout with an empty data
    // volume needs no setup step.

    public string BlobPathFor(Guid id) => Path.Combine(_blobDir, id.ToString("N"));

    public async Task SaveBlobAsync(Guid id, Stream content)
    {
        Directory.CreateDirectory(_blobDir);
        var path = BlobPathFor(id);
        var tmpPath = path + ".tmp";
        await using (var fs = File.Create(tmpPath))
        {
            await content.CopyToAsync(fs);
        }
        File.Move(tmpPath, path, overwrite: true);
    }

    public Stream OpenBlobRead(Guid id) => File.OpenRead(BlobPathFor(id));

    public bool BlobExists(Guid id) => File.Exists(BlobPathFor(id));

    public void TryDeleteBlob(Guid id)
    {
        try
        {
            var path = BlobPathFor(id);
            if (File.Exists(path)) File.Delete(path);
        }
        catch (IOException)
        {
            // A leftover orphan blob is harmless; never fail the API for it.
        }
    }
}
