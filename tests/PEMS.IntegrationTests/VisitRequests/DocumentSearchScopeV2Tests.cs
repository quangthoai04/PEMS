using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Documents.Queries.SearchDocuments;
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
}
