using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;

namespace PEMS.Application.Galleries.Public.Common;

/// <summary>
/// Shared authorization for the anonymous, gallery-scoped public file proxy (BR-PGAL-13/14). A file id
/// is only serveable when it backs public-visible content: a public gallery media (main or thumbnail),
/// an ACTIVE area/location cover (image OR MP4 video — both live in <c>cover_file_id</c>), or a READY
/// TTS narration of a public item. Everything else is rejected so the anonymous route can never fetch
/// avatars, documents, hidden media, or an arbitrary Drive file.
/// </summary>
internal static class PublicGalleryMediaAccess
{
    public static async Task<bool> IsPublicGalleryFileAsync(
        IApplicationDbContext db, ulong fileId, CancellationToken ct)
    {
        var ok = await db.GalleryItemMedia.AsNoTracking().AnyAsync(m =>
            (m.FileId == fileId || m.ThumbnailFileId == fileId) &&
            m.Status == "ACTIVE" &&
            m.DeletedAt == null &&
            m.GalleryItem.Status == "PUBLISHED" &&
            m.GalleryItem.DeletedAt == null &&
            m.GalleryItem.Location.Status == "ACTIVE" &&
            m.GalleryItem.Location.Area.Status == "ACTIVE" &&
            m.GalleryItem.Location.Area.Campus.Status == "ACTIVE",
            ct);

        // Area cover (image or MP4 video) of an ACTIVE area under an ACTIVE campus.
        if (!ok)
            ok = await db.GalleryAreas.AsNoTracking().AnyAsync(a =>
                a.CoverFileId == fileId &&
                a.Status == "ACTIVE" &&
                a.Campus.Status == "ACTIVE",
                ct);

        // Location cover of an ACTIVE location under an ACTIVE area/campus.
        if (!ok)
            ok = await db.GalleryLocations.AsNoTracking().AnyAsync(l =>
                l.CoverFileId == fileId &&
                l.Status == "ACTIVE" &&
                l.Area.Status == "ACTIVE" &&
                l.Area.Campus.Status == "ACTIVE",
                ct);

        // READY TTS narration audio of a public-visible item.
        if (!ok)
            ok = await db.GalleryItemTtsAudios.AsNoTracking().AnyAsync(t =>
                t.AudioFileId == fileId &&
                t.Status == "READY" &&
                t.GalleryItem.Status == "PUBLISHED" &&
                t.GalleryItem.DeletedAt == null &&
                t.GalleryItem.Location.Status == "ACTIVE" &&
                t.GalleryItem.Location.Area.Status == "ACTIVE" &&
                t.GalleryItem.Location.Area.Campus.Status == "ACTIVE",
                ct);

        return ok;
    }
}
