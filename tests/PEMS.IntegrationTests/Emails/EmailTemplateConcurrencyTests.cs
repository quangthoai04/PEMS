using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Emails.Commands.RestoreEmailTemplate;
using PEMS.Application.Emails.Commands.UpdateEmailTemplate;
using PEMS.Application.Emails.Common;
using PEMS.Domain.Entities.Emails;
using PEMS.Infrastructure.Persistence;
using PEMS.IntegrationTests.TestInfrastructure;
using Xunit;

namespace PEMS.IntegrationTests.Emails;

/// <summary>
/// Optimistic concurrency on template content, against a real MySQL row.
///
/// <para>
/// <b>What changed and why.</b> The token used to be <c>updated_at</c>. That column is DATETIME with no
/// fractional part, so two saves landing inside the same second stored an identical stamp, compared
/// equal, and the second silently overwrote the first — a blind spot exactly at the resolution where
/// concurrent edits actually collide. It is now a monotonic <c>revision</c>, and the comparison happens
/// inside the UPDATE rather than in the handler, so there is no window between deciding and writing.
/// </para>
/// <para>
/// The same-second case has its own test below. It is the one the previous mechanism could not pass, and
/// it is the reason this work was not closed at the end of G11.
/// </para>
/// </summary>
public sealed class EmailTemplateConcurrencyTests : IDisposable
{
    private readonly EmailEvidenceHarness _h = new("g11-concurrency@partner.example.com");

    private readonly Dictionary<string, (string Name, string? Description, string? SubjectVi, string? BodyVi,
        string? SubjectEn, string? BodyEn, string? VariablesText)> _original = new(StringComparer.Ordinal);

    public EmailTemplateConcurrencyTests()
    {
        if (!CanUseDatabase()) return;

        using var db = EmailEvidenceHarness.NewContext();
        foreach (var t in db.EmailTemplates.ToList())
            _original[t.TemplateCode] = (t.Name, t.Description, t.SubjectVi, t.BodyVi,
                                         t.SubjectEn, t.BodyEn, t.VariablesText);
    }

    private static bool CanUseDatabase()
    {
        try { EmailEvidenceHarness.RequireDb(); return true; }
        catch { return false; }
    }

    public void Dispose()
    {
        try
        {
            if (_original.Count > 0)
            {
                using var db = EmailEvidenceHarness.NewContext();
                foreach (var t in db.EmailTemplates.ToList())
                {
                    if (!_original.TryGetValue(t.TemplateCode, out var o)) continue;
                    t.Name = o.Name; t.Description = o.Description;
                    t.SubjectVi = o.SubjectVi; t.BodyVi = o.BodyVi;
                    t.SubjectEn = o.SubjectEn; t.BodyEn = o.BodyEn;
                    t.VariablesText = o.VariablesText;
                }
                db.SaveChanges();
            }
        }
        catch { /* a failed restore must not mask the test result */ }

        _h.Dispose();
    }

    // ── Rig ─────────────────────────────────────────────────────────────────

    private sealed class HoOperator : PEMS.Application.Common.Interfaces.ICurrentUserService
    {
        public bool IsAuthenticated => true;
        public ulong? UserId => 1;
        public string? Email => "ho-operator@pems.test";
        public ulong? RoleId => null;
        public string? RoleCode => "HO";
        public string? SubRole => null;
        public ulong? PrimaryCampusId => null;
        public ulong? SessionId => null;
        public ulong? DepartmentId => null;
        public string? LoginPortal => null;
    }

    private static UpdateEmailTemplateCommandHandler Update(ApplicationDbContext db) => new(db, new HoOperator());
    private static RestoreEmailTemplateCommandHandler Restore(ApplicationDbContext db) => new(db, new HoOperator());

    private static async Task<EmailTemplate> LoadAsync(ApplicationDbContext db, string code)
        => await db.EmailTemplates.AsNoTracking().FirstAsync(t => t.TemplateCode == code);

    /// <summary>
    /// A content edit an operator could actually save: any trusted block the template's contract
    /// requires is kept, because the handler refuses a body that drops one. These tests are about the
    /// concurrency token, not about the contact block.
    /// </summary>
    private static UpdateEmailTemplateCommand Edit(EmailTemplate t, string bodyVi, uint expected) => new()
    {
        EmailTemplateId = t.EmailTemplateId,
        Name = t.Name,
        Description = t.Description,
        SubjectVi = t.SubjectVi ?? "Tiêu đề",
        BodyVi = EmailContractFixture.BodyWithRequiredBlocks(t.TemplateCode, bodyVi),
        SubjectEn = t.SubjectEn,
        BodyEn = t.BodyEn,
        ExpectedRevision = expected,
    };

    // ── The happy paths ─────────────────────────────────────────────────────

    [Fact]
    public async Task An_update_with_the_current_revision_succeeds_and_bumps_it_by_one()
    {
        EmailEvidenceHarness.RequireDb();
        await using var db = EmailEvidenceHarness.NewContext();

        var t = await LoadAsync(db, SystemEmailTemplates.AccountActivated);
        var before = t.Revision;

        var result = await Update(db).Handle(
            Edit(t, "<p>Bản mới — {{fullName}}</p>", before), CancellationToken.None);

        Assert.Equal(before + 1, result.Revision);

        await using var verify = EmailEvidenceHarness.NewContext();
        Assert.Equal(before + 1, (await LoadAsync(verify, SystemEmailTemplates.AccountActivated)).Revision);
    }

    [Fact]
    public async Task A_restore_with_the_current_revision_succeeds_and_bumps_it_by_one()
    {
        EmailEvidenceHarness.RequireDb();
        await using var db = EmailEvidenceHarness.NewContext();

        var t = await LoadAsync(db, SystemEmailTemplates.AccountRoleChanged);
        var before = t.Revision;

        var result = await Restore(db).Handle(new RestoreEmailTemplateCommand
        {
            EmailTemplateId = t.EmailTemplateId, ExpectedRevision = before,
        }, CancellationToken.None);

        Assert.Equal(before + 1, result.Revision);
    }

    // ── The refusals ────────────────────────────────────────────────────────

    [Fact]
    public async Task A_stale_revision_is_refused_and_changes_nothing()
    {
        EmailEvidenceHarness.RequireDb();
        await using var db = EmailEvidenceHarness.NewContext();

        var t = await LoadAsync(db, SystemEmailTemplates.AccountStaffLeaderAssigned);
        var stale = t.Revision;

        var winner = "<p>Người thứ nhất — {{fullName}}</p>";
        await Update(db).Handle(Edit(t, winner, stale), CancellationToken.None);

        await using var second = EmailEvidenceHarness.NewContext();
        var reloaded = await LoadAsync(second, SystemEmailTemplates.AccountStaffLeaderAssigned);

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            Update(second).Handle(
                Edit(reloaded, "<p>Người thứ hai — {{fullName}}</p>", stale), CancellationToken.None));

        Assert.Equal(EmailErrorCodes.TemplateConcurrencyConflict, ex.ErrorCode);

        // Refused means NOTHING was written — not the body, and not the revision.
        await using var verify = EmailEvidenceHarness.NewContext();
        var after = await LoadAsync(verify, SystemEmailTemplates.AccountStaffLeaderAssigned);
        Assert.Equal(winner, after.BodyVi);
        Assert.Equal(stale + 1, after.Revision);
    }

    /// <summary>
    /// The case the timestamp token could not see: two saves inside the same second.
    ///
    /// <para>
    /// Both editors load the same revision and save immediately, one after the other, well within one
    /// second. Under <c>updated_at</c> both stored the same stamp, the second compared equal, and one
    /// person's wording disappeared with no error. Here the second is refused.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Two_saves_inside_the_same_second_are_still_distinguished()
    {
        EmailEvidenceHarness.RequireDb();
        await using var db = EmailEvidenceHarness.NewContext();

        var t = await LoadAsync(db, SystemEmailTemplates.AccountEmailChangedNewNotice);
        var shared = t.Revision;

        var started = DateTime.Now;

        var first = await Update(db).Handle(
            Edit(t, "<p>Trong cùng một giây — một</p>", shared), CancellationToken.None);

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            Update(db).Handle(
                Edit(t, "<p>Trong cùng một giây — hai</p>", shared), CancellationToken.None));

        var elapsed = DateTime.Now - started;

        Assert.Equal(EmailErrorCodes.TemplateConcurrencyConflict, ex.ErrorCode);
        Assert.Equal(shared + 1, first.Revision);

        // If the two saves happened to straddle a second boundary this test would still pass, but it
        // would no longer be testing what it claims to. Said out loud rather than assumed.
        Assert.True(elapsed < TimeSpan.FromSeconds(1),
            $"Both saves must land inside one second for this test to mean anything; took {elapsed}.");
    }

    [Fact]
    public async Task A_restore_carrying_a_stale_revision_is_refused()
    {
        EmailEvidenceHarness.RequireDb();
        await using var db = EmailEvidenceHarness.NewContext();

        var t = await LoadAsync(db, SystemEmailTemplates.AccountEmailChangedOldNotice);
        var stale = t.Revision;

        // Appended rather than replaced: this template does not declare {{fullName}}, and a body written
        // from scratch would be refused by the content contract before concurrency was ever reached —
        // which would make the test pass for the wrong reason. Keeping the shipped body and adding a
        // paragraph is a valid edit for every template in the catalog.
        var winner = (t.BodyVi ?? "") + "<p>Sửa trước khi phục hồi</p>";
        await Update(db).Handle(Edit(t, winner, stale), CancellationToken.None);

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            Restore(db).Handle(new RestoreEmailTemplateCommand
            {
                EmailTemplateId = t.EmailTemplateId, ExpectedRevision = stale,
            }, CancellationToken.None));

        Assert.Equal(EmailErrorCodes.TemplateConcurrencyConflict, ex.ErrorCode);

        await using var verify = EmailEvidenceHarness.NewContext();
        Assert.Equal(winner, (await LoadAsync(verify, SystemEmailTemplates.AccountEmailChangedOldNotice)).BodyVi);
    }

    /// <summary>An update and a restore competing for the same revision: exactly one may win.</summary>
    [Fact]
    public async Task An_update_and_a_restore_racing_the_same_revision_produce_exactly_one_winner()
    {
        EmailEvidenceHarness.RequireDb();

        const string code = SystemEmailTemplates.DeptLeadershipGranted;

        await using var setup = EmailEvidenceHarness.NewContext();
        var t = await LoadAsync(setup, code);
        var shared = t.Revision;

        await using var dbA = EmailEvidenceHarness.NewContext();
        await using var dbB = EmailEvidenceHarness.NewContext();

        var outcomes = await Task.WhenAll(
            Attempt(() => Update(dbA).Handle(
                Edit(t, "<p>Bản sửa tay — {{fullName}}</p>", shared), CancellationToken.None)),
            Attempt(() => Restore(dbB).Handle(new RestoreEmailTemplateCommand
            {
                EmailTemplateId = t.EmailTemplateId, ExpectedRevision = shared,
            }, CancellationToken.None)));

        Assert.Equal(1, outcomes.Count(o => o));

        await using var verify = EmailEvidenceHarness.NewContext();
        Assert.Equal(shared + 1, (await LoadAsync(verify, code)).Revision);
    }

    /// <summary>Two concurrent updates on the same revision: exactly one may win.</summary>
    [Fact]
    public async Task Two_updates_racing_the_same_revision_produce_exactly_one_winner()
    {
        EmailEvidenceHarness.RequireDb();

        const string code = SystemEmailTemplates.DeptLeadershipHandedOver;

        await using var setup = EmailEvidenceHarness.NewContext();
        var t = await LoadAsync(setup, code);
        var shared = t.Revision;

        await using var dbA = EmailEvidenceHarness.NewContext();
        await using var dbB = EmailEvidenceHarness.NewContext();

        var outcomes = await Task.WhenAll(
            Attempt(() => Update(dbA).Handle(Edit(t, "<p>A — {{fullName}}</p>", shared), CancellationToken.None)),
            Attempt(() => Update(dbB).Handle(Edit(t, "<p>B — {{fullName}}</p>", shared), CancellationToken.None)));

        Assert.Equal(1, outcomes.Count(o => o));

        await using var verify = EmailEvidenceHarness.NewContext();
        Assert.Equal(shared + 1, (await LoadAsync(verify, code)).Revision);
    }

    // ── The revision must not move when the write is refused ────────────────

    [Fact]
    public async Task A_content_validation_failure_leaves_the_revision_untouched()
    {
        EmailEvidenceHarness.RequireDb();
        await using var db = EmailEvidenceHarness.NewContext();

        var t = await LoadAsync(db, SystemEmailTemplates.AccountEmailConfirmation);
        var before = t.Revision;

        await Assert.ThrowsAsync<EmailTemplateContentException>(() =>
            Update(db).Handle(
                Edit(t, "<p>{{khongPhaiBienCuaMauNay}}</p>", before), CancellationToken.None));

        await using var verify = EmailEvidenceHarness.NewContext();
        Assert.Equal(before, (await LoadAsync(verify, SystemEmailTemplates.AccountEmailConfirmation)).Revision);
    }

    [Fact]
    public async Task An_update_without_a_revision_is_refused_and_writes_nothing()
    {
        EmailEvidenceHarness.RequireDb();
        await using var db = EmailEvidenceHarness.NewContext();

        var t = await LoadAsync(db, SystemEmailTemplates.AccountActivated);
        var before = t.Revision;

        var command = Edit(t, "<p>{{fullName}}</p>", before);
        command.ExpectedRevision = null;

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            Update(db).Handle(command, CancellationToken.None));

        Assert.Equal(EmailErrorCodes.TemplateConcurrencyConflict, ex.ErrorCode);

        await using var verify = EmailEvidenceHarness.NewContext();
        Assert.Equal(before, (await LoadAsync(verify, SystemEmailTemplates.AccountActivated)).Revision);
    }

    /// <summary>A revision that never existed cannot match, so nothing is written.</summary>
    [Fact]
    public async Task A_revision_from_the_future_is_refused()
    {
        EmailEvidenceHarness.RequireDb();
        await using var db = EmailEvidenceHarness.NewContext();

        var t = await LoadAsync(db, SystemEmailTemplates.AccountActivated);
        var before = t.Revision;

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            Update(db).Handle(Edit(t, "<p>{{fullName}}</p>", before + 500), CancellationToken.None));

        Assert.Equal(EmailErrorCodes.TemplateConcurrencyConflict, ex.ErrorCode);

        await using var verify = EmailEvidenceHarness.NewContext();
        Assert.Equal(before, (await LoadAsync(verify, SystemEmailTemplates.AccountActivated)).Revision);
    }

    private static async Task<bool> Attempt(Func<Task> action)
    {
        try { await action(); return true; }
        catch (ConflictException) { return false; }
    }
}
