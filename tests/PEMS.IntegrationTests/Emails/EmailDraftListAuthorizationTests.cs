using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;
using PEMS.Application.Emails.Queries.GetEmailDraft;
using PEMS.Application.Emails.Queries.ListEmailDrafts;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Emails;
using PEMS.Domain.Enums;
using PEMS.Infrastructure.Persistence;
using PEMS.IntegrationTests.TestInfrastructure;
using Xunit;

namespace PEMS.IntegrationTests.Emails;

/// <summary>
/// The "Nháp" list is Own-scope, and this is where that is proved against a real database.
///
/// <para>
/// The controller's <c>[RoleAuthorize]</c> only says who may use the mailbox at all — every role on
/// that attribute is held by many people. What keeps one person's drafts out of another's list is the
/// predicate in the handler, so these tests drive the handler directly with two different identities
/// rather than trusting the attribute.
/// </para>
/// </summary>
public sealed class EmailDraftListAuthorizationTests : IDisposable
{
    /// <summary>
    /// Two FK-valid users, plus a clean slate for them. The suite shares one database and the helper
    /// returns the same user per role, so leftover drafts from an earlier test would otherwise be
    /// counted in this one's results.
    /// </summary>
    private static async Task<(ulong Owner, ulong Other)> UsersAsync(ApplicationDbContext db)
    {
        var owner = await DatabaseResetHelper.EnsureTestUserAsync(db, EffectiveRole.Staff);
        var other = await DatabaseResetHelper.EnsureTestUserAsync(db, EffectiveRole.StaffLeader);

        var stale = await db.EmailDrafts
            .Where(d => d.CreatedBy == owner || d.CreatedBy == other).ToListAsync();
        if (stale.Count > 0) { db.EmailDrafts.RemoveRange(stale); await db.SaveChangesAsync(); }

        return (owner, other);
    }

    private readonly EmailEvidenceHarness _h = new("g6-draft-list@partner.example.com");

    public void Dispose() => _h.Dispose();

    private sealed class FakeCurrentUser : ICurrentUserService
    {
        public bool IsAuthenticated => true;
        public ulong? UserId { get; init; }
        public string? Email => $"draft-user-{UserId}@fpt.edu.vn";
        public ulong? RoleId => null;
        public string? RoleCode => RoleCodes.Staff;
        public string? SubRole => null;
        public ulong? PrimaryCampusId => null;
        public ulong? DepartmentId => null;
        public ulong? SessionId => null;
        public string? LoginPortal => null;
    }

    private static ICurrentUserService As(ulong id) => new FakeCurrentUser { UserId = id };

    private static ListEmailDraftsQueryHandler List(ApplicationDbContext db, ulong userId)
        => new(db, As(userId));

    private static async Task<ulong> SeedDraftAsync(
        ApplicationDbContext db,
        ulong owner,
        EmailDraftStatus status,
        string subject,
        DateTime? updatedAt = null,
        int recipients = 0)
    {
        var draft = new EmailDraft
        {
            Subject = subject,
            BodyContent = "<p>nội dung</p>",
            BodyFormat = EmailBodyFormat.HTML,
            Status = status,
            CreatedBy = owner,
            CreatedAt = new DateTime(2026, 7, 1, 8, 0, 0),
            UpdatedAt = updatedAt,
        };

        for (var i = 0; i < recipients; i++)
        {
            draft.Recipients.Add(new EmailDraftRecipient
            {
                RecipientEmail = $"r{i}@fpt.edu.vn",
                RecipientType = "TO",
                DisplayOrder = (uint)i,
            });
        }

        db.EmailDrafts.Add(draft);
        await db.SaveChangesAsync();
        return draft.EmailDraftId;
    }

    // ── Own-scope ───────────────────────────────────────────────────────────

    [Fact]
    public async Task The_list_contains_only_the_callers_own_drafts()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();
        var (owner, other) = await UsersAsync(db);
        await SeedDraftAsync(db, owner, EmailDraftStatus.DRAFT, "của tôi");
        await SeedDraftAsync(db, other, EmailDraftStatus.DRAFT, "của người khác");

        var result = await List(db, owner).Handle(new ListEmailDraftsQuery(), CancellationToken.None);

        Assert.All(result.Items, item => Assert.Equal("của tôi", item.Subject));
        Assert.DoesNotContain(result.Items, item => item.Subject == "của người khác");
    }

    [Fact]
    public async Task Another_users_draft_is_not_even_counted()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();
        var (owner, other) = await UsersAsync(db);
        await SeedDraftAsync(db, other, EmailDraftStatus.DRAFT, "của người khác");

        var result = await List(db, owner).Handle(new ListEmailDraftsQuery(), CancellationToken.None);

        // TotalCount drives the pager; leaking it would reveal that other drafts exist.
        Assert.Equal(0, result.TotalCount);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task Reading_another_users_draft_by_id_is_refused()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();
        var (owner, other) = await UsersAsync(db);
        var foreignDraftId = await SeedDraftAsync(db, other, EmailDraftStatus.DRAFT, "của người khác");

        var handler = new GetEmailDraftQueryHandler(db, As(owner));

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            handler.Handle(new GetEmailDraftQuery(foreignDraftId), CancellationToken.None));
    }

    [Fact]
    public async Task An_unauthenticated_caller_gets_nothing()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();
        var (owner, other) = await UsersAsync(db);
        await SeedDraftAsync(db, owner, EmailDraftStatus.DRAFT, "của tôi");

        var anonymous = new ListEmailDraftsQueryHandler(db, new FakeCurrentUser { UserId = null });

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            anonymous.Handle(new ListEmailDraftsQuery(), CancellationToken.None));
    }

    // ── Status ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Sent_and_discarded_drafts_are_excluded()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();
        var (owner, other) = await UsersAsync(db);
        await SeedDraftAsync(db, owner, EmailDraftStatus.DRAFT, "còn nháp");
        await SeedDraftAsync(db, owner, EmailDraftStatus.SENT, "đã gửi");
        await SeedDraftAsync(db, owner, EmailDraftStatus.DISCARDED, "đã hủy");

        var result = await List(db, owner).Handle(new ListEmailDraftsQuery(), CancellationToken.None);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("còn nháp", Assert.Single(result.Items).Subject);
    }

    // ── Ordering and paging ─────────────────────────────────────────────────

    [Fact]
    public async Task Most_recently_edited_comes_first_and_an_unedited_draft_falls_back_to_created_at()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();
        var (owner, other) = await UsersAsync(db);
        await SeedDraftAsync(db, owner, EmailDraftStatus.DRAFT, "sửa lâu rồi",
            updatedAt: new DateTime(2026, 7, 2, 9, 0, 0));
        await SeedDraftAsync(db, owner, EmailDraftStatus.DRAFT, "sửa gần đây",
            updatedAt: new DateTime(2026, 7, 5, 9, 0, 0));
        await SeedDraftAsync(db, owner, EmailDraftStatus.DRAFT, "chưa sửa lần nào", updatedAt: null);

        var result = await List(db, owner).Handle(new ListEmailDraftsQuery(), CancellationToken.None);

        Assert.Equal(
            new[] { "sửa gần đây", "sửa lâu rồi", "chưa sửa lần nào" },
            result.Items.Select(i => i.Subject).ToArray());

        // The never-edited draft reports its creation time rather than a null.
        Assert.Equal(new DateTime(2026, 7, 1, 8, 0, 0), result.Items.Last().UpdatedAt);
    }

    [Fact]
    public async Task Drafts_edited_at_the_same_instant_keep_a_stable_order_across_pages()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();
        var (owner, other) = await UsersAsync(db);

        // Identical UpdatedAt on every row: ordering by that column alone is not a total order, so
        // without the ID tie-break the same draft could appear on page 1 and page 2, or on neither.
        var sameInstant = new DateTime(2026, 7, 6, 10, 0, 0);
        for (var i = 0; i < 6; i++)
            await SeedDraftAsync(db, owner, EmailDraftStatus.DRAFT, $"đồng thời {i}", updatedAt: sameInstant);

        var page1 = await List(db, owner)
            .Handle(new ListEmailDraftsQuery { Page = 1, PageSize = 3 }, CancellationToken.None);
        var page2 = await List(db, owner)
            .Handle(new ListEmailDraftsQuery { Page = 2, PageSize = 3 }, CancellationToken.None);

        var ids = page1.Items.Select(i => i.EmailDraftId).Concat(page2.Items.Select(i => i.EmailDraftId)).ToArray();

        Assert.Equal(6, ids.Length);
        Assert.Equal(6, ids.Distinct().Count());          // nothing duplicated across pages
        Assert.Equal(ids.OrderByDescending(id => id).ToArray(), ids);   // newest id first, deterministic

        // Re-reading page 1 gives the same slice: the order does not shift between requests.
        var page1Again = await List(db, owner)
            .Handle(new ListEmailDraftsQuery { Page = 1, PageSize = 3 }, CancellationToken.None);
        Assert.Equal(
            page1.Items.Select(i => i.EmailDraftId).ToArray(),
            page1Again.Items.Select(i => i.EmailDraftId).ToArray());
    }

    [Fact]
    public async Task Paging_returns_the_requested_slice_and_the_full_total()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();
        var (owner, other) = await UsersAsync(db);
        for (var i = 0; i < 5; i++)
        {
            await SeedDraftAsync(db, owner, EmailDraftStatus.DRAFT, $"nháp {i}",
                updatedAt: new DateTime(2026, 7, 1, 8, 0, 0).AddMinutes(i));
        }

        var page2 = await List(db, owner)
            .Handle(new ListEmailDraftsQuery { Page = 2, PageSize = 2 }, CancellationToken.None);

        Assert.Equal(5, page2.TotalCount);
        Assert.Equal(2, page2.Items.Count);
        Assert.Equal(new[] { "nháp 2", "nháp 1" }, page2.Items.Select(i => i.Subject).ToArray());
    }

    // ── Summary shape ───────────────────────────────────────────────────────

    [Fact]
    public async Task The_summary_counts_recipients_without_returning_their_addresses()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = EmailEvidenceHarness.NewContext();
        var (owner, other) = await UsersAsync(db);
        await SeedDraftAsync(db, owner, EmailDraftStatus.DRAFT, "có người nhận", recipients: 3);

        var item = Assert.Single(
            (await List(db, owner).Handle(new ListEmailDraftsQuery(), CancellationToken.None)).Items);

        Assert.Equal(3, item.RecipientCount);

        // The DTO has no address or body member at all — a collection response must not carry the BCC
        // list. Asserted on the type so adding one later fails here.
        var members = typeof(EmailDraftSummaryDto).GetProperties().Select(p => p.Name).ToArray();
        Assert.DoesNotContain("Recipients", members);
        Assert.DoesNotContain("BodyContent", members);
    }
}
