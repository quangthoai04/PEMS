using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Files;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Common;
using PEMS.Application.Emails.Common;
using PEMS.Application.Galleries.Public.Common;
using PEMS.Domain.Entities.Documents;

namespace PEMS.Application.Files.Common;

/// <summary>
/// Whether the current user may download a stored file.
///
/// <para>
/// <b>The hole this closes.</b> <c>GET /api/files/{id}/download</c> (and <c>/content</c>) checked only
/// that someone was logged in, then streamed the bytes. Every internal account could therefore read any
/// file in the system by guessing a numeric id — and because email attachments live in the same table,
/// that walked straight around the recipient rules Giai đoạn 5 had just built: a colleague who could not
/// open an email could still download what was attached to it, and a draft nobody had sent yet was
/// readable by anyone who tried the right number.
/// </para>
/// <para>
/// <b>A file has no permissions of its own.</b> It is readable because of what REFERENCES it — an email,
/// a draft, a visit photo, a published news post. So this service resolves the referencing objects and
/// asks each module's own rule; it does not invent a second opinion about who may see a visit or a
/// partner. Where a module already has an access helper (<see cref="SentEmailAccess"/>,
/// <see cref="VisitInstanceAccess"/>, <see cref="PublicGalleryMediaAccess"/>) that helper is called
/// directly, so a file and the screen that shows it can never disagree.
/// </para>
/// <para>
/// <b>Any one valid reference is enough</b> (decision G5-13). A file can legitimately be referenced
/// twice — the same image attached to an email and used as the cover of a published news post — and in
/// that case the public reference is what it is: the picture is already published to the world, so
/// refusing it to a logged-in colleague would protect nothing. The reverse (requiring EVERY reference to
/// grant) would make a file less readable each time someone re-used it, which is not a rule anyone could
/// predict from the UI.
/// </para>
/// <para>
/// <b>No role is a master key.</b> There is no HO or Admin branch anywhere below. Seniority reaches a
/// file the same way everyone else does — by having access to something that references it.
/// </para>
/// </summary>
public interface IFileAccessAuthorizationService
{
    /// <summary>
    /// True when the current user may read this file's bytes. Called BEFORE any storage path is resolved
    /// or any stream is opened.
    /// </summary>
    Task<bool> CanDownloadAsync(UploadedFile file, CancellationToken cancellationToken = default);
}

public sealed class FileAccessAuthorizationService : IFileAccessAuthorizationService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ISentEmailObjectScope _emailObjectScope;

    public FileAccessAuthorizationService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        ISentEmailObjectScope emailObjectScope)
    {
        _db = db;
        _currentUser = currentUser;
        _emailObjectScope = emailObjectScope;
    }

    public async Task<bool> CanDownloadAsync(UploadedFile file, CancellationToken cancellationToken = default)
    {
        if (file is null) return false;
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is not { } userId) return false;

        var fileId = file.FileId;

        // Cheapest and most permissive references first: a hit here means the object is already visible
        // to this user through its own module, so the more expensive lookups never run.
        if (await IsPubliclyPublishedAsync(fileId, cancellationToken)) return true;
        if (await IsInternallyBrowsableAsync(fileId, cancellationToken)) return true;

        if (await CanReadEmailAttachmentAsync(fileId, cancellationToken)) return true;
        // A rule for "my own draft's attachment" used to sit here. With no drafts, a file picked in the
        // composer is referenced by nothing until the message is sent, so the uploader fallback below is
        // what lets the author see their own attachment back — which is the same answer, reached by the
        // branch that already existed for exactly this situation.
        if (await CanReadVisitFileAsync(fileId, cancellationToken)) return true;

        // Last: a file nothing references yet. The person who uploaded it is mid-flow (composing an
        // email, filling a form) and must be able to see their own upload back; nobody else may.
        if (await IsUnreferencedAsync(fileId, cancellationToken))
            return file.UploadedBy == userId;

        return false;
    }

    // ── Public content ───────────────────────────────────────────────────────

    /// <summary>
    /// Files that back content already published to anonymous visitors. The published-ness test is the
    /// SAME one the anonymous routes use — <see cref="PublicGalleryMediaAccess"/> for gallery media, and
    /// the news rule that a file counts only while the post carrying it is PUBLISHED. A draft or hidden
    /// post therefore grants nothing here; it falls through to the internal check below.
    /// </summary>
    private async Task<bool> IsPubliclyPublishedAsync(ulong fileId, CancellationToken ct)
    {
        if (await PublicGalleryMediaAccess.IsPublicGalleryFileAsync(_db, fileId, ct)) return true;

        var publishedCover = await _db.News.AsNoTracking()
            .AnyAsync(n => n.CoverFileId == fileId
                        && n.Status == PEMS.Domain.Constants.NewsConstants.Status.Published, ct);
        if (publishedCover) return true;

        return await _db.NewsSectionFiles.AsNoTracking()
            .AnyAsync(sf => sf.FileId == fileId
                && _db.NewsContentSections.Any(s => s.SectionId == sf.SectionId
                    && _db.NewsTranslations.Any(t => t.NewsTranslationId == s.NewsTranslationId
                        && _db.News.Any(n => n.NewsId == t.NewsId
                            && n.Status == PEMS.Domain.Constants.NewsConstants.Status.Published))), ct);
    }

    // ── Internally browsable modules ─────────────────────────────────────────

    /// <summary>
    /// References whose owning module already shows the object to any signed-in user, so scoping the file
    /// tighter than the screen would only break the screen.
    ///
    /// <list type="bullet">
    /// <item><b>Partner</b> — <c>PartnersController</c> is <c>[Authorize]</c> with no further role gate:
    /// every internal account can open a partner and see its logo, cover, contact avatar and scanned
    /// card.</item>
    /// <item><b>Document</b> — <c>DocumentsController</c> applies no role filter of its own either.</item>
    /// <item><b>News/gallery in draft</b> — the management screens are internal; the modules gate who may
    /// EDIT, not who may look.</item>
    /// <item><b>Avatars</b> — a person's photo appears beside their name all over the product.</item>
    /// </list>
    ///
    /// This is deliberately a mirror, not a policy: tightening any of these is a decision for the module
    /// that owns the screen, and doing it here would leave the file and the screen disagreeing.
    /// </summary>
    private async Task<bool> IsInternallyBrowsableAsync(ulong fileId, CancellationToken ct)
    {
        if (await _db.Partners.AsNoTracking()
                .AnyAsync(p => p.LogoFileId == fileId || p.CoverFileId == fileId, ct)) return true;

        if (await _db.PartnerContacts.AsNoTracking()
                .AnyAsync(c => c.AvatarFileId == fileId || c.ScannedCardFileId == fileId, ct)) return true;

        if (await _db.BusinessCardOcrJobs.AsNoTracking()
                .AnyAsync(j => j.ScannedCardFileId == fileId, ct)) return true;

        if (await _db.Documents.AsNoTracking().AnyAsync(d => d.FileId == fileId, ct)) return true;

        if (await _db.News.AsNoTracking().AnyAsync(n => n.CoverFileId == fileId, ct)) return true;
        if (await _db.NewsSectionFiles.AsNoTracking().AnyAsync(sf => sf.FileId == fileId, ct)) return true;

        if (await _db.GalleryAreas.AsNoTracking().AnyAsync(a => a.CoverFileId == fileId, ct)) return true;
        if (await _db.GalleryLocations.AsNoTracking().AnyAsync(l => l.CoverFileId == fileId, ct)) return true;
        if (await _db.GalleryItemMedia.AsNoTracking()
                .AnyAsync(m => m.FileId == fileId || m.ThumbnailFileId == fileId, ct)) return true;
        if (await _db.GalleryItemContents.AsNoTracking()
                .AnyAsync(c => c.AudioViFileId == fileId || c.AudioEnFileId == fileId, ct)) return true;

        return string.Equals(
            await _db.Files.AsNoTracking().Where(f => f.FileId == fileId).Select(f => f.FilePurpose)
                .FirstOrDefaultAsync(ct),
            FilePurposeDbValues.UserAvatar, StringComparison.OrdinalIgnoreCase);
    }

    // ── Email ────────────────────────────────────────────────────────────────

    /// <summary>
    /// An attachment is readable by exactly the people who may read the message carrying it — the same
    /// <see cref="SentEmailAccess"/> decision the detail screen makes, including the linked-object branch.
    ///
    /// <para>
    /// Note what is NOT needed to answer this: the BCC list. A blind recipient is recognised by matching
    /// their own address, and nothing about the others is returned or implied. Asking "may I download
    /// this?" can never be turned into "who else received it?".
    /// </para>
    /// </summary>
    private async Task<bool> CanReadEmailAttachmentAsync(ulong fileId, CancellationToken ct)
    {
        var emailIds = await _db.SentEmailAttachments.AsNoTracking()
            .Where(a => a.FileId == fileId)
            .Select(a => a.SentEmailId)
            .Distinct()
            .ToListAsync(ct);
        if (emailIds.Count == 0) return false;

        var viewerEmail = await ResolveViewerEmailAsync(ct);

        foreach (var emailId in emailIds)
        {
            var email = await _db.SentEmails.AsNoTracking()
                .Where(e => e.SentEmailId == emailId)
                .Select(e => new { e.SentBy, e.RelatedType, e.RelatedId })
                .FirstOrDefaultAsync(ct);
            if (email is null) continue;

            var recipients = await _db.SentEmailRecipients.AsNoTracking()
                .Where(r => r.SentEmailId == emailId)
                .Select(r => new { r.RecipientEmail, r.RecipientType })
                .ToListAsync(ct);

            var relation = SentEmailAccess.Resolve(
                _currentUser.UserId, viewerEmail, email.SentBy, recipients,
                r => r.RecipientEmail, r => r.RecipientType);

            if (relation == SentEmailAccess.Relation.None
                && await _emailObjectScope.CanViewLinkedObjectAsync(email.RelatedType, email.RelatedId, ct))
                relation = SentEmailAccess.Relation.LinkedObject;

            if (SentEmailAccess.CanView(relation)) return true;
        }

        return false;
    }

    // ── Visit ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Photos, face-scan artefacts and logistics handover attachments all belong to a visit instance, so
    /// they use the visit's own relation rule: host, campus Staff Leader, HO, or an ACCEPTED participant.
    /// The visitor/guest relation is excluded exactly as <see cref="VisitInstanceAccess.CanViewInternal"/>
    /// excludes it elsewhere — these are internal working documents.
    /// </summary>
    private async Task<bool> CanReadVisitFileAsync(ulong fileId, CancellationToken ct)
    {
        var instanceId =
            await _db.VisitPhotos.AsNoTracking()
                .Where(p => p.FileId == fileId).Select(p => (ulong?)p.VisitInstanceId).FirstOrDefaultAsync(ct)
            ?? await _db.PhotoFaceTags.AsNoTracking()
                .Where(t => t.FileId == fileId)
                .Select(t => _db.VisitPhotos.Where(p => p.FileId == t.FileId)
                    .Select(p => (ulong?)p.VisitInstanceId).FirstOrDefault())
                .FirstOrDefaultAsync(ct)
            ?? await _db.VisitPhotoFaceScans.AsNoTracking()
                .Where(s => s.FileId == fileId).Select(s => (ulong?)s.VisitInstanceId).FirstOrDefaultAsync(ct)
            ?? await _db.VisitLogisticsItemHandovers.AsNoTracking()
                .Where(h => h.AttachmentFileId == fileId)
                .Select(h => _db.VisitLogisticsItems
                    .Where(i => i.LogisticsItemId == h.LogisticsItemId)
                    .Select(i => (ulong?)i.VisitInstanceId).FirstOrDefault())
                .FirstOrDefaultAsync(ct);

        if (instanceId is not { } visitInstanceId) return false;

        var instance = await _db.VisitRequestCampuses.AsNoTracking()
            .FirstOrDefaultAsync(c => c.VisitInstanceId == visitInstanceId, ct);
        if (instance is null) return false;

        var visit = await _db.VisitRequests.AsNoTracking()
            .FirstOrDefaultAsync(v => v.VisitRequestId == instance.VisitRequestId, ct);
        if (visit is null) return false;

        var relation = await VisitInstanceAccess.ResolveRelationAsync(_db, _currentUser, instance, visit, ct);
        return VisitInstanceAccess.CanViewInternal(relation);
    }

    // ── Unreferenced ─────────────────────────────────────────────────────────

    /// <summary>
    /// True when no table points at this file. Checked LAST and only to decide the uploader fallback —
    /// never as a shortcut to "allow", because an unreferenced file is usually one that is about to be
    /// attached to something, not one that is free for everyone.
    /// </summary>
    private async Task<bool> IsUnreferencedAsync(ulong fileId, CancellationToken ct)
        => !await _db.SentEmailAttachments.AsNoTracking().AnyAsync(a => a.FileId == fileId, ct)
        && !await _db.VisitPhotos.AsNoTracking().AnyAsync(p => p.FileId == fileId, ct)
        && !await _db.PhotoFaceTags.AsNoTracking().AnyAsync(t => t.FileId == fileId, ct)
        && !await _db.VisitPhotoFaceScans.AsNoTracking().AnyAsync(s => s.FileId == fileId, ct)
        && !await _db.VisitPhotoFaceDetections.AsNoTracking().AnyAsync(d => d.FileId == fileId, ct)
        && !await _db.VisitLogisticsItemHandovers.AsNoTracking().AnyAsync(h => h.AttachmentFileId == fileId, ct);

    private async Task<string?> ResolveViewerEmailAsync(CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(_currentUser.Email)) return _currentUser.Email;

        return await _db.Users.AsNoTracking()
            .Where(u => u.UserId == _currentUser.UserId)
            .Select(u => u.Email)
            .FirstOrDefaultAsync(ct);
    }
}
