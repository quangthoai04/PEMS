using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Documents.Queries.SearchDocuments;
using PEMS.Application.Documents.Queries.ViewDocumentDetail;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Documents;
using PEMS.Infrastructure.Persistence;
using Xunit;

namespace PEMS.IntegrationTests.VisitRequests;

/// <summary>
/// Phase 3A — document search campus scope.
///
/// A Staff Leader is scoped to their own campus's documents (documents.campus_id), and that scope is
/// applied to the base query before the keyword. A title that exists only on another campus's document
/// must therefore move neither the rows nor the total. HO carries no fixed campus and may self-filter.
/// </summary>
public sealed class DocumentSearchScopeV2Tests
{
    private static string ConnString => TestInfrastructure.DisposableDatabaseManager.GetDisposableConnectionString(
        "server=localhost;port=3306;database=pems_pr3_test;user=root;password=123456;AllowUserVariables=True;GuidFormat=None");

    private const ulong LeaderHn = 3;
    private const ulong CampusHn = 1;
    private const ulong CampusHcm = 2;

    private static bool? _dbUp;
    private static readonly DateTime Now = DateTime.Now;

    private static ApplicationDbContext NewContext()
        => new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseMySql(ConnString, ServerVersion.AutoDetect(ConnString)).Options);

    private static void RequireDb()
    {
        if (_dbUp is null)
        {
            try { using var db = NewContext(); _dbUp = db.Database.CanConnect(); }
            catch { _dbUp = false; }
        }
        Assert.True(_dbUp!.Value, "pems_pr3_test is not reachable.");
    }

    private sealed class FakeUser : ICurrentUserService
    {
        public FakeUser(ulong id, string roleCode, string? subRole = null, ulong? campusId = null)
        { UserId = id; RoleCode = roleCode; SubRole = subRole; PrimaryCampusId = campusId; }
        public bool IsAuthenticated => true;
        public ulong? UserId { get; }
        public string? Email => null;
        public ulong? RoleId => null;
        public string? RoleCode { get; }
        public string? SubRole { get; }
        public ulong? PrimaryCampusId { get; }
        public ulong? DepartmentId => null;
        public ulong? SessionId => null;
        public string? LoginPortal => null;
    }

    private static async Task<(ulong DocumentId, ulong FileId)> AddDocumentAsync(ulong campusId, string title)
    {
        using var db = NewContext();
        var file = new UploadedFile
        {
            StorageProvider = "LOCAL",
            ObjectKey = $"it/{Guid.NewGuid():N}.pdf",
            OriginalFilename = "tai-lieu.pdf",
            MimeType = "application/pdf",
            FileSize = 512,
            UploadedBy = LeaderHn,
            UploadedAt = Now,
        };
        db.Files.Add(file);
        await db.SaveChangesAsync();

        var document = new Document
        {
            FileId = file.FileId,
            OwnerType = "GENERAL",
            CampusId = campusId,
            Title = title,
            Status = "PUBLISHED",
            CreatedAt = Now,
            CreatedBy = LeaderHn,
        };
        db.Documents.Add(document);
        await db.SaveChangesAsync();
        return (document.DocumentId, file.FileId);
    }

    private static async Task CleanupAsync(List<(ulong DocumentId, ulong FileId)> docs)
    {
        using var db = NewContext();
        foreach (var (documentId, fileId) in docs)
        {
            await db.Database.ExecuteSqlRawAsync("DELETE FROM documents WHERE document_id = {0}", documentId);
            await db.Database.ExecuteSqlRawAsync("DELETE FROM files WHERE file_id = {0}", fileId);
        }
    }

    [Fact]
    public async Task A_staff_leader_never_finds_another_campus_document_even_by_its_exact_title()
    {
        RequireDb();
        var docs = new List<(ulong, ulong)>();
        try
        {
            var tag = Guid.NewGuid().ToString("N")[..6];
            docs.Add(await AddDocumentAsync(CampusHn, $"KếHoạchHN{tag}"));
            docs.Add(await AddDocumentAsync(CampusHcm, $"KếHoạchHCM{tag}"));

            using var db = NewContext();
            var handler = new SearchDocumentsQueryHandler(
                db, new FakeUser(LeaderHn, RoleCodes.Staff, UserSubRoles.Leader, CampusHn));

            // The HCM document's exact title must not surface for the HN leader...
            var hidden = await handler.Handle(
                new SearchDocumentsQuery { Q = $"KếHoạchHCM{tag}", Page = 1, PageSize = 200 }, CancellationToken.None);
            Assert.DoesNotContain(hidden.Items, i => i.Title == $"KếHoạchHCM{tag}");
            var nowhere = await handler.Handle(
                new SearchDocumentsQuery { Q = $"zz{tag}nowhere", Page = 1, PageSize = 200 }, CancellationToken.None);
            Assert.Equal(nowhere.TotalItems, hidden.TotalItems); // scope before keyword → count unchanged

            // ...but the HN leader's own campus document does.
            var own = await handler.Handle(
                new SearchDocumentsQuery { Q = $"KếHoạchHN{tag}", Page = 1, PageSize = 200 }, CancellationToken.None);
            Assert.Single(own.Items, i => i.Title == $"KếHoạchHN{tag}");
        }
        finally { await CleanupAsync(docs); }
    }

    [Fact]
    public async Task HO_sees_both_campuses_and_can_self_filter_to_one()
    {
        RequireDb();
        var docs = new List<(ulong, ulong)>();
        try
        {
            var tag = Guid.NewGuid().ToString("N")[..6];
            docs.Add(await AddDocumentAsync(CampusHn, $"BáoCáoHN{tag}"));
            docs.Add(await AddDocumentAsync(CampusHcm, $"BáoCáoHCM{tag}"));

            using var db = NewContext();
            var handler = new SearchDocumentsQueryHandler(db, new FakeUser(500, RoleCodes.Ho));

            // No campus filter → HO sees both campuses' documents.
            var both = await handler.Handle(
                new SearchDocumentsQuery { Q = tag, Page = 1, PageSize = 200 }, CancellationToken.None);
            Assert.Contains(both.Items, i => i.Title == $"BáoCáoHN{tag}");
            Assert.Contains(both.Items, i => i.Title == $"BáoCáoHCM{tag}");

            // HO's optional self-filter narrows to one campus.
            var hnOnly = await handler.Handle(
                new SearchDocumentsQuery { CampusId = CampusHn, Page = 1, PageSize = 200 }, CancellationToken.None);
            Assert.Contains(hnOnly.Items, i => i.Title == $"BáoCáoHN{tag}");
            Assert.DoesNotContain(hnOnly.Items, i => i.Title == $"BáoCáoHCM{tag}");
        }
        finally { await CleanupAsync(docs); }
    }

    /// <summary>
    /// Security fix: the campus-scope check (both here and in ViewDocumentDetail) used to gate on
    /// `RoleCode == "STAFF" && SubRole == "LEADER"` only, so a plain Staff account (no Leader subrole)
    /// had no scope check at all — it could search every campus's documents, and even self-select a
    /// campus via the CampusId param, a widening path meant only for HO. Plain Staff must be scoped to
    /// their own campus exactly like Staff Leader already was.
    /// </summary>
    [Fact]
    public async Task A_plain_staff_account_never_finds_another_campus_document_either()
    {
        RequireDb();
        var docs = new List<(ulong, ulong)>();
        try
        {
            var tag = Guid.NewGuid().ToString("N")[..6];
            docs.Add(await AddDocumentAsync(CampusHn, $"NhanVienHN{tag}"));
            docs.Add(await AddDocumentAsync(CampusHcm, $"NhanVienHCM{tag}"));

            using var db = NewContext();
            var handler = new SearchDocumentsQueryHandler(
                db, new FakeUser(LeaderHn, RoleCodes.Staff, UserSubRoles.Staff, CampusHn));

            // A plain Staff (not Leader) at CampusHn still cannot find the HCM document by exact title...
            var hidden = await handler.Handle(
                new SearchDocumentsQuery { Q = $"NhanVienHCM{tag}", Page = 1, PageSize = 200 }, CancellationToken.None);
            Assert.DoesNotContain(hidden.Items, i => i.Title == $"NhanVienHCM{tag}");

            // ...nor by explicitly asking for CampusHcm — the client-supplied CampusId must not widen
            // a non-HO caller's scope.
            var widened = await handler.Handle(
                new SearchDocumentsQuery { CampusId = CampusHcm, Q = tag, Page = 1, PageSize = 200 }, CancellationToken.None);
            Assert.DoesNotContain(widened.Items, i => i.Title == $"NhanVienHCM{tag}");

            // ...but their own campus's document still surfaces.
            var own = await handler.Handle(
                new SearchDocumentsQuery { Q = $"NhanVienHN{tag}", Page = 1, PageSize = 200 }, CancellationToken.None);
            Assert.Single(own.Items, i => i.Title == $"NhanVienHN{tag}");
        }
        finally { await CleanupAsync(docs); }
    }

    /// <summary>A non-HO caller with no PrimaryCampusId at all (there is no campus to scope to) is
    /// refused outright — the pre-existing behavior for Staff Leader (`isStaffLeader &amp;&amp; campusId
    /// == null -> Forbidden`), now extended to plain Staff too instead of falling through unscoped.</summary>
    [Fact]
    public async Task A_staff_account_with_no_primary_campus_is_refused_not_shown_every_campus()
    {
        RequireDb();
        using var db = NewContext();
        var handler = new SearchDocumentsQueryHandler(
            db, new FakeUser(LeaderHn, RoleCodes.Staff, UserSubRoles.Staff, campusId: null));

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            handler.Handle(new SearchDocumentsQuery { Page = 1, PageSize = 200 }, CancellationToken.None));
    }

    // ── ViewDocumentDetailQueryHandler: same bug, same fix, GET-by-id shape ────────────────────────
    // The isStaffLeader-only check on document.CampusId used to let a plain Staff account view ANY
    // document by id regardless of campus.

    [Fact]
    public async Task A_plain_staff_account_cannot_view_another_campus_document_by_id()
    {
        RequireDb();
        var docs = new List<(ulong, ulong)>();
        try
        {
            var tag = Guid.NewGuid().ToString("N")[..6];
            docs.Add(await AddDocumentAsync(CampusHcm, $"DetailHCM{tag}"));
            var hcmDocId = docs[0].Item1;

            using var db = NewContext();
            var handler = new ViewDocumentDetailQueryHandler(
                db, new FakeUser(LeaderHn, RoleCodes.Staff, UserSubRoles.Staff, CampusHn));

            await Assert.ThrowsAsync<ForbiddenException>(() =>
                handler.Handle(new ViewDocumentDetailQuery { DocumentId = hcmDocId }, CancellationToken.None));
        }
        finally { await CleanupAsync(docs); }
    }

    [Fact]
    public async Task A_staff_leader_still_views_their_own_campus_document_by_id()
    {
        RequireDb();
        var docs = new List<(ulong, ulong)>();
        try
        {
            var tag = Guid.NewGuid().ToString("N")[..6];
            var (hnDocId, hnFileId) = await AddDocumentAsync(CampusHn, $"DetailHN{tag}");
            docs.Add((hnDocId, hnFileId));

            using var db = NewContext();
            var handler = new ViewDocumentDetailQueryHandler(
                db, new FakeUser(LeaderHn, RoleCodes.Staff, UserSubRoles.Leader, CampusHn));

            var result = await handler.Handle(
                new ViewDocumentDetailQuery { DocumentId = hnDocId }, CancellationToken.None);

            Assert.Equal($"DetailHN{tag}", result.Document.Title);
        }
        finally { await CleanupAsync(docs); }
    }

    [Fact]
    public async Task HO_can_view_any_campus_document_by_id()
    {
        RequireDb();
        var docs = new List<(ulong, ulong)>();
        try
        {
            var tag = Guid.NewGuid().ToString("N")[..6];
            var (hcmDocId, hcmFileId) = await AddDocumentAsync(CampusHcm, $"DetailHOView{tag}");
            docs.Add((hcmDocId, hcmFileId));

            using var db = NewContext();
            var handler = new ViewDocumentDetailQueryHandler(db, new FakeUser(500, RoleCodes.Ho));

            var result = await handler.Handle(
                new ViewDocumentDetailQuery { DocumentId = hcmDocId }, CancellationToken.None);

            Assert.Equal($"DetailHOView{tag}", result.Document.Title);
        }
        finally { await CleanupAsync(docs); }
    }
}
