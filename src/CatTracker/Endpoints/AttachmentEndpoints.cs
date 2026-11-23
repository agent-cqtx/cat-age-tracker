using CatTracker.Models;
using CatTracker.Services;
using Microsoft.AspNetCore.Http.Features;

namespace CatTracker.Endpoints;

/// <summary>
/// File attachments, two flavors:
///  - per-appointment: /api/appointments/{id}/attachments
///  - general ("Documents"): /api/attachments (AppointmentId == null)
/// Uploads are multipart with a single "file" field. Reads (list + file
/// bytes) are open like every other GET in this app; mutations require auth.
/// </summary>
public static class AttachmentEndpoints
{
    private const long MaxSizeBytes = 10 * 1024 * 1024; // 10 MB

    // Multipart framing (boundary lines, content-disposition headers) adds
    // a few hundred bytes around the file itself; this headroom on the
    // transport cap keeps a full 10 MB file uploadable while still cutting
    // the connection off before much more than the limit is ever buffered.
    private const long MaxBodyBytes = MaxSizeBytes + 64 * 1024;

    // Sample window for the text sniff: markdown has no magic bytes, so
    // text/markdown uploads are validated negatively over the leading 8 KB.
    private const int TextSampleBytes = 8 * 1024;

    private static readonly string[] AllowedContentTypes =
    {
        "image/jpeg", "image/png", "image/webp", "application/pdf", "text/markdown"
    };

    public static void MapAttachmentEndpoints(this IEndpointRouteBuilder app)
    {
        // ---- General attachments ------------------------------------------
        var group = app.MapGroup("/api/attachments");

        group.MapGet("/", async (AttachmentRepository attachments) =>
            Results.Ok(await attachments.GetGeneralAsync()));

        group.MapPost("/", async (HttpContext ctx, AttachmentRepository attachments) =>
            await HandleUpload(ctx, null, attachments)
        ).RequireAuthorization();

        // Serves the bytes. Open (no auth) so <img src> tags work, same
        // read-open policy as the other GETs.
        group.MapGet("/{id:guid}/file", async (Guid id, HttpContext ctx, AttachmentRepository attachments) =>
        {
            var attachment = await attachments.GetByIdAsync(id);
            if (attachment is null) return Results.NotFound();

            Stream blob;
            try
            {
                blob = attachments.OpenBlobRead(id);
            }
            catch (FileNotFoundException)
            {
                // Blob vanished between the metadata read and the open
                // (concurrent delete): same story as a missing blob — 404,
                // not an unhandled 500.
                return Results.NotFound();
            }

            ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
            // INVARIANT: text/markdown is ALWAYS served as an attachment
            // (download), never inline. Inline text/markdown + nosniff is
            // not itself dangerous — no browser renders or scripts
            // text/markdown — but we pin the download disposition anyway
            // so uploaded text can never become same-origin *rendered*
            // content, whatever future changes touch the inline path for
            // the image/PDF types below. Do not remove this branch without
            // re-examining the inline invariant comment that follows.
            if (attachment.ContentType == "text/markdown")
            {
                ctx.Response.Headers["Content-Disposition"] =
                    $"attachment; filename=\"{HeaderSafeFileName(attachment.FileName)}\"";
            }
            // SECURITY INVARIANT for inline serving: no Content-Disposition
            // means the browser renders these bytes (PDFs open in the tab),
            // which is only safe because uploads pass magic-byte sniffing
            // (HasExpectedMagicBytesAsync) — the bytes here are genuinely
            // PNG/JPEG/WebP/PDF whatever content type was declared at upload
            // time — and because of the nosniff header above. A client
            // cannot store HTML here and get it rendered same-origin. Do
            // not weaken either check without re-examining the other.
            return Results.File(blob, attachment.ContentType);
        });

        // Handles BOTH kinds — metadata row + blob together.
        group.MapDelete("/{id:guid}", async (Guid id, AttachmentRepository attachments) =>
        {
            var removed = await attachments.DeleteAsync(id);
            if (removed is null) return Results.NotFound();
            attachments.TryDeleteBlob(id);
            return Results.NoContent();
        }).RequireAuthorization();

        // ---- Per-appointment attachments -----------------------------------
        var apptGroup = app.MapGroup("/api/appointments");

        apptGroup.MapGet("/{appointmentId:guid}/attachments", async (Guid appointmentId, AppointmentRepository appointments, AttachmentRepository attachments) =>
        {
            if (await appointments.GetByIdAsync(appointmentId) is null) return Results.NotFound();
            return Results.Ok(await attachments.GetForAppointmentAsync(appointmentId));
        });

        apptGroup.MapPost("/{appointmentId:guid}/attachments", async (Guid appointmentId, HttpContext ctx, AppointmentRepository appointments, AttachmentRepository attachments) =>
        {
            if (await appointments.GetByIdAsync(appointmentId) is null) return Results.NotFound();
            return await HandleUpload(ctx, appointmentId, attachments);
        }).RequireAuthorization();
    }

    private static async Task<IResult> HandleUpload(
        HttpContext ctx,
        Guid? appointmentId,
        AttachmentRepository attachments)
    {
        if (!ctx.Request.HasFormContentType)
        {
            return Results.BadRequest(new { error = "Expected a multipart form upload with a 'file' field." });
        }

        // Enforce the size limit at the transport, before Kestrel spools the
        // whole body to disk/memory. The file.Length check below stays as
        // the per-field guarantee, but without this cap the advertised 10 MB
        // limit only bites above Kestrel's 30 MB default, and an even bigger
        // body escapes ReadFormAsync as an unhandled BadHttpRequestException.
        var maxBodySizeFeature = ctx.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (maxBodySizeFeature is { IsReadOnly: false })
        {
            maxBodySizeFeature.MaxRequestBodySize = MaxBodyBytes;
        }

        IFormCollection form;
        try
        {
            form = await ctx.Request.ReadFormAsync();
        }
        catch (BadHttpRequestException ex) when (ex.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            return Results.Json(new { error = "File exceeds the 10 MB limit." },
                statusCode: StatusCodes.Status413PayloadTooLarge);
        }
        catch (BadHttpRequestException)
        {
            // Other transport rejections (malformed multipart, etc.): a
            // clean 400 instead of a Kestrel "unhandled exception" log line.
            return Results.BadRequest(new { error = "Malformed multipart upload." });
        }

        var file = form.Files["file"];
        if (file is null || file.Length == 0)
        {
            return Results.BadRequest(new { error = "No file provided." });
        }

        if (!AllowedContentTypes.Contains(file.ContentType))
        {
            return Results.Json(new { error = $"Unsupported file type: {file.ContentType}" },
                statusCode: StatusCodes.Status415UnsupportedMediaType);
        }

        // Markdown policy, deliberately narrow: text/markdown is accepted
        // ONLY together with a .md/.markdown extension on the file name.
        // text/plain is NOT accepted at all — the app has no use for
        // arbitrary text, and letting a markdown body arrive under any
        // other extension would only widen the sniff's attack surface.
        if (file.ContentType == "text/markdown" && !HasMarkdownExtension(file.FileName))
        {
            return Results.Json(new { error = "text/markdown uploads must have a .md or .markdown extension." },
                statusCode: StatusCodes.Status415UnsupportedMediaType);
        }

        if (file.Length > MaxSizeBytes)
        {
            return Results.Json(new { error = "File exceeds the 10 MB limit." },
                statusCode: StatusCodes.Status413PayloadTooLarge);
        }

        if (!await HasExpectedMagicBytesAsync(file))
        {
            return Results.Json(new { error = $"File contents do not match the declared type ({file.ContentType})." },
                statusCode: StatusCodes.Status415UnsupportedMediaType);
        }

        var attachment = new Attachment
        {
            AppointmentId = appointmentId,
            FileName = SanitizeFileName(file.FileName),
            ContentType = file.ContentType,
            SizeBytes = file.Length,
        };

        // Blob first: if the metadata write then fails, no API path can
        // reference the blob, whereas metadata pointing at a missing blob
        // would show a broken item forever. (A startup sweep in Program.cs
        // reaps any blob orphaned by such a failure.)
        try
        {
            await using var upload = file.OpenReadStream();
            await attachments.SaveBlobAsync(attachment.Id, upload);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Disk full, permissions, etc.: a clean JSON 500. Without this
            // the exception page (outside Docker, Development env) would
            // send paths and a stack trace to the client.
            return Results.Json(new { error = "The server could not store the file." },
                statusCode: StatusCodes.Status500InternalServerError);
        }

        var created = await attachments.AddAsync(attachment);
        return Results.Created($"/api/attachments/{created.Id}", created);
    }

    /// <summary>
    /// The declared content type is entirely client-controlled, so uploads
    /// must also prove it by their leading bytes. This sniff is what makes
    /// inline serving at the file endpoint safe (see comment there).
    /// BCL-only, by design — no packages are allowed in this project.
    /// </summary>
    private static async Task<bool> HasExpectedMagicBytesAsync(IFormFile file)
    {
        if (file.ContentType == "text/markdown")
        {
            // Text types carry no signature, so the sniff is negative
            // instead of positive: read the leading window and prove it
            // looks like text, not like a binary or HTML wearing a .md
            // name (see LooksLikeMarkdownText).
            var sample = new byte[TextSampleBytes];
            int read;
            await using (var textStream = file.OpenReadStream())
            {
                try
                {
                    read = await textStream.ReadAtLeastAsync(sample, sample.Length, throwOnEndOfStream: false);
                }
                catch (IOException)
                {
                    return false;
                }
            }
            // Span passed straight through as an argument — no ref-struct
            // local in this async body (not allowed by the compiler).
            return LooksLikeMarkdownText(sample.AsSpan(0, read));
        }

        var header = new byte[12];
        await using var stream = file.OpenReadStream();
        try
        {
            // Short files stay partially zero-filled; the prefix checks
            // below then simply fail.
            await stream.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false);
        }
        catch (IOException)
        {
            return false;
        }

        return MatchesMagicBytes(header, file.ContentType);
    }

    /// <summary>
    /// Negative text sniff for text/markdown uploads. Nothing else is
    /// capped or re-encoded: any NUL byte in the leading 8 KB window, or a
    /// known binary signature (PNG/JPEG/PDF/WebP-RIFF/GIF/ZIP) at the very
    /// start, means this is a binary renamed to .md. A window that opens
    /// as HTML after an optional UTF-8 BOM and whitespace ('&lt;!doctype',
    /// '&lt;html') is rejected too — that is the stored-XSS reason this
    /// sniff exists: markdown must never smuggle a live HTML page past the
    /// upload gate. Signatures are checked as a prefix (where they live by
    /// definition); only the NUL scan covers the whole window, so a prose
    /// document that merely mentions "%PDF-1.4" mid-file stays valid.
    /// </summary>
    private static bool LooksLikeMarkdownText(ReadOnlySpan<byte> sample)
    {
        if (sample.Contains((byte)0x00)) return false;

        if (sample.StartsWith("%PDF"u8)
            || sample.StartsWith("RIFF"u8)
            || sample.StartsWith("GIF8"u8)          // GIF87a / GIF89a
            || sample.StartsWith("PK\u0003\u0004"u8) // ZIP (and docx/xlsx/...)
            || (sample.Length >= 3 && sample[0] == 0xFF && sample[1] == 0xD8 && sample[2] == 0xFF) // JPEG
            || (sample.Length >= 4 && sample[0] == 0x89 && sample[1] == (byte)'P'
                && sample[2] == (byte)'N' && sample[3] == (byte)'G')) // PNG
        {
            return false;
        }

        var head = sample;
        if (head.StartsWith("\uFEFF"u8)) head = head[3..]; // UTF-8 BOM
        var trim = 0;
        while (trim < head.Length && head[trim] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n' or (byte)'\f')
        {
            trim++;
        }
        head = head[trim..];

        return !StartsWithAsciiIgnoreCase(head, "<!doctype"u8)
            && !StartsWithAsciiIgnoreCase(head, "<html"u8);
    }

    /// <summary>ASCII-case-insensitive prefix compare (prefix must be lowercase).</summary>
    private static bool StartsWithAsciiIgnoreCase(ReadOnlySpan<byte> span, ReadOnlySpan<byte> lowerPrefix)
    {
        if (span.Length < lowerPrefix.Length) return false;
        for (var i = 0; i < lowerPrefix.Length; i++)
        {
            var c = span[i];
            if (c >= (byte)'A' && c <= (byte)'Z') c += 32;
            if (c != lowerPrefix[i]) return false;
        }
        return true;
    }

    /// <summary>
    /// .md / .markdown extension gate for text/markdown uploads. Takes the
    /// basename with the same rule as SanitizeFileName, so a client cannot
    /// dodge the gate with a path like "notes.md/evil.txt" (checked as
    /// evil.txt -> rejected) or smuggle one in via "evil/x.md" (checked as
    /// x.md -> allowed, same as the browser would treat it).
    /// </summary>
    private static bool HasMarkdownExtension(string? fileName)
    {
        if (string.IsNullOrEmpty(fileName)) return false;
        var name = fileName.Split('/', '\\')[^1];
        var ext = Path.GetExtension(name);
        return ext.Equals(".md", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".markdown", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Content-Disposition filename sanitizer: the stored name is already
    /// control-char-free (SanitizeFileName), but header values must also
    /// stay quoted-string-safe and ASCII, so anything outside printable
    /// ASCII — plus quote and backslash — collapses to an underscore.
    /// </summary>
    private static string HeaderSafeFileName(string name)
    {
        var cleaned = new string(name
            .Select(c => c is < (char)0x20 or > (char)0x7E or '"' or '\\' ? '_' : c)
            .ToArray());
        return cleaned.Length == 0 ? "attachment" : cleaned;
    }

    /// <summary>
    /// Pure magic-byte matcher — separate sync method because ref-struct
    /// (Span) locals are not allowed inside async method bodies.
    /// </summary>
    private static bool MatchesMagicBytes(ReadOnlySpan<byte> header, string contentType)
    {
        switch (contentType)
        {
            // PNG signature: 0x89 followed by "PNG\r\n\x1a\n" as literal
            // ASCII bytes. (Written this way because a "\x89"u8 literal
            // would UTF-8-encode the high byte into two bytes.)
            case "image/png":
                return header.Length >= 8
                    && header[0] == 0x89
                    && header.Slice(1, 7).SequenceEqual("PNG\r\n\x1a\n"u8);
            case "image/jpeg":
                return header.Length >= 3
                    && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;
            case "application/pdf":
                return header.StartsWith("%PDF"u8);
            case "image/webp":
                return header.StartsWith("RIFF"u8)
                    && header.Length >= 12
                    && header.Slice(8, 4).SequenceEqual("WEBP"u8);
            default:
                return false;
        }
    }

    /// <summary>
    /// Display-name only: drop any directory part (browsers may send paths,
    /// hostile clients certainly will) and all control characters. The result
    /// is only ever rendered via textContent and never used for file paths —
    /// blob paths are built exclusively from Guids.
    /// </summary>
    private static string SanitizeFileName(string? rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName)) return "attachment";

        var name = rawName.Split('/', '\\')[^1];
        name = new string(name.Where(c => !char.IsControl(c)).ToArray()).Trim();

        if (name.Length == 0) name = "attachment";
        if (name.Length > 200) name = name[..200];
        return name;
    }
}
