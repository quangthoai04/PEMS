using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
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
/// Restore-to-default (G11-I), proved against a real database.
///
/// <para>
/// The catalog is fixed: an operator who edits a template into an unusable state cannot create a
/// replacement and cannot delete it. Until this existed, the only way back was for somebody with
/// database access to re-run a SQL script — which is not a feature, and is why this was left open at the
/// end of G11. What is asserted below is that the restored content comes from the SHIPPED defaults and
/// not from anything the operator could have influenced.
/// </para>
/// </summary>
public sealed class EmailTemplateRestoreDefaultTests : IDisposable
{
    private readonly EmailEvidenceHarness _h = new("g11i-restore@partner.example.com");

    /// <summary>
    /// Template content as it stood before this class ran, restored on the way out — the shared-database
    /// discipline every suite that edits canonical templates has to follow.
    /// </summary>
    private readonly Dictionary<string, (string Name, string? Description, string? SubjectVi, string? BodyVi,
        string? SubjectEn, string? BodyEn, string? VariablesText)> _original = new(StringComparer.Ordinal);

    private readonly List<ulong> _auditLogIds = new();

    public EmailTemplateRestoreDefaultTests()
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

                if (_auditLogIds.Count > 0)
                {
                    var ids = _auditLogIds.ToList();
                    db.AuditLogs.Where(a => ids.Contains(a.AuditLogId)).ExecuteDelete();
                }

                db.SaveChanges();
            }
        }
        catch { /* a failed restore must not mask the test result; the next fresh import repairs it */ }

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

    private static RestoreEmailTemplateCommandHandler Restore(ApplicationDbContext db)
        => new(db, new HoOperator());

    private static UpdateEmailTemplateCommandHandler Update(ApplicationDbContext db)
        => new(db, new HoOperator());

    private static async Task<EmailTemplate> LoadAsync(ApplicationDbContext db, string code)
        => await db.EmailTemplates.AsNoTracking().FirstAsync(t => t.TemplateCode == code);

    /// <summary>Damages a template's content the way an operator could, and returns the new revision.</summary>
    private static async Task<uint> BreakAsync(ApplicationDbContext db, string code, string marker)
    {
        var t = await LoadAsync(db, code);
        var result = await Update(db).Handle(new UpdateEmailTemplateCommand
        {
            EmailTemplateId = t.EmailTemplateId,
            Name = "ĐÃ SỬA " + marker,
            Description = "mô tả bị sửa " + marker,
            SubjectVi = "Tiêu đề bị sửa " + marker,
            BodyVi = "<p>Nội dung bị sửa " + marker + "</p>",
            SubjectEn = "Broken subject " + marker,
            BodyEn = "<p>Broken body " + marker + "</p>",
            ExpectedRevision = t.Revision,
        }, CancellationToken.None);

        return result.Revision;
    }

    private void RememberAudit(ApplicationDbContext db, ulong templateId)
    {
        var id = db.AuditLogs.AsNoTracking()
            .Where(a => a.EntityType == "EmailTemplate" && a.EntityId == templateId)
            .OrderByDescending(a => a.AuditLogId)
            .Select(a => a.AuditLogId)
            .FirstOrDefault();

        if (id != 0) _auditLogIds.Add(id);
    }

    // ── The core behaviour ──────────────────────────────────────────────────

    /// <summary>
    /// An ordinary template: broken, then restored, and every one of the six editable fields is back to
    /// the shipped wording.
    /// </summary>
    [Fact]
    public async Task Restoring_an_edited_template_returns_all_six_fields_to_the_shipped_default()
    {
        EmailEvidenceHarness.RequireDb();
        await using var db = EmailEvidenceHarness.NewContext();

        const string code = SystemEmailTemplates.AccountActivated;
        var shipped = EmailTemplateDefaults.For(code);
        Assert.NotNull(shipped);

        var revision = await BreakAsync(db, code, "R1");

        var t = await LoadAsync(db, code);
        var restored = await Restore(db).Handle(new RestoreEmailTemplateCommand
        {
            EmailTemplateId = t.EmailTemplateId,
            ExpectedRevision = revision,
        }, CancellationToken.None);

        RememberAudit(db, t.EmailTemplateId);

        Assert.True(restored.Success);

        await using var verify = EmailEvidenceHarness.NewContext();
        var after = await LoadAsync(verify, code);

        Assert.Equal(shipped!.Name, after.Name);
        Assert.Equal(shipped.Description, after.Description);
        Assert.Equal(shipped.SubjectVi, after.SubjectVi);
        Assert.Equal(shipped.BodyVi, after.BodyVi);
        Assert.Equal(shipped.SubjectEn, after.SubjectEn);
        Assert.Equal(shipped.BodyEn, after.BodyEn);
    }

    /// <summary>Both languages, explicitly — a restore that fixed only Vietnamese would pass a laxer test.</summary>
    [Fact]
    public async Task Restoring_returns_both_the_vietnamese_and_the_english_content()
    {
        EmailEvidenceHarness.RequireDb();
        await using var db = EmailEvidenceHarness.NewContext();

        const string code = SystemEmailTemplates.AccountRoleChanged;
        var shipped = EmailTemplateDefaults.For(code)!;

        var revision = await BreakAsync(db, code, "R2");
        var t = await LoadAsync(db, code);

        await Restore(db).Handle(new RestoreEmailTemplateCommand
        {
            EmailTemplateId = t.EmailTemplateId, ExpectedRevision = revision,
        }, CancellationToken.None);

        RememberAudit(db, t.EmailTemplateId);

        await using var verify = EmailEvidenceHarness.NewContext();
        var after = await LoadAsync(verify, code);

        Assert.Equal(shipped.SubjectVi, after.SubjectVi);
        Assert.Equal(shipped.BodyVi, after.BodyVi);
        Assert.Equal(shipped.SubjectEn, after.SubjectEn);
        Assert.Equal(shipped.BodyEn, after.BodyEn);
        Assert.DoesNotContain("R2", after.BodyVi ?? "");
        Assert.DoesNotContain("R2", after.BodyEn ?? "");
    }

    /// <summary>
    /// A template carrying a one-time action link restores like any other. Worth its own case: these are
    /// the templates whose breakage matters most, and the ones whose contract is strictest.
    /// </summary>
    [Fact]
    public async Task A_sensitive_template_restores_to_its_shipped_content()
    {
        EmailEvidenceHarness.RequireDb();
        await using var db = EmailEvidenceHarness.NewContext();

        const string code = SystemEmailTemplates.AccountEmailConfirmation;
        var shipped = EmailTemplateDefaults.For(code)!;

        var t0 = await LoadAsync(db, code);
        var contract = EmailTemplateContracts.For(code)!;
        Assert.True(contract.CarriesSecret || contract.RequiredVariables.Count > 0);

        // Edited, but still legal for this template — the point is that restore reverses a VALID edit.
        var edited = await Update(db).Handle(new UpdateEmailTemplateCommand
        {
            EmailTemplateId = t0.EmailTemplateId,
            Name = t0.Name,
            Description = t0.Description,
            SubjectVi = t0.SubjectVi,
            BodyVi = (t0.BodyVi ?? "") + "<p>Ghi chú thêm của người vận hành</p>",
            SubjectEn = t0.SubjectEn,
            BodyEn = t0.BodyEn,
            ExpectedRevision = t0.Revision,
        }, CancellationToken.None);

        await Restore(db).Handle(new RestoreEmailTemplateCommand
        {
            EmailTemplateId = t0.EmailTemplateId, ExpectedRevision = edited.Revision,
        }, CancellationToken.None);

        RememberAudit(db, t0.EmailTemplateId);

        await using var verify = EmailEvidenceHarness.NewContext();
        var after = await LoadAsync(verify, code);

        Assert.Equal(shipped.BodyVi, after.BodyVi);
        Assert.DoesNotContain("Ghi chú thêm của người vận hành", after.BodyVi ?? "");
    }

    /// <summary>
    /// The default does not come from the database row. Proved by breaking the row and restoring: if the
    /// "default" were read back from what is stored, the damage would survive.
    /// </summary>
    [Fact]
    public async Task The_default_does_not_come_from_the_current_database_content()
    {
        EmailEvidenceHarness.RequireDb();
        await using var db = EmailEvidenceHarness.NewContext();

        const string code = SystemEmailTemplates.AccountStaffLeaderAssigned;
        var revision = await BreakAsync(db, code, "NOT-A-DEFAULT");

        await using (var dirty = EmailEvidenceHarness.NewContext())
            Assert.Contains("NOT-A-DEFAULT", (await LoadAsync(dirty, code)).BodyVi ?? "");

        var t = await LoadAsync(db, code);
        await Restore(db).Handle(new RestoreEmailTemplateCommand
        {
            EmailTemplateId = t.EmailTemplateId, ExpectedRevision = revision,
        }, CancellationToken.None);

        RememberAudit(db, t.EmailTemplateId);

        await using var verify = EmailEvidenceHarness.NewContext();
        Assert.DoesNotContain("NOT-A-DEFAULT", (await LoadAsync(verify, code)).BodyVi ?? "");
    }

    /// <summary>Restore after a hot edit brings the canonical wording back, byte for byte.</summary>
    [Fact]
    public async Task Restore_after_a_hot_edit_reinstates_the_canonical_wording_exactly()
    {
        EmailEvidenceHarness.RequireDb();
        await using var db = EmailEvidenceHarness.NewContext();

        const string code = SystemEmailTemplates.AccountEmailChangedNewNotice;
        var shipped = EmailTemplateDefaults.For(code)!;

        var revision = await BreakAsync(db, code, "HOT");
        var t = await LoadAsync(db, code);

        await Restore(db).Handle(new RestoreEmailTemplateCommand
        {
            EmailTemplateId = t.EmailTemplateId, ExpectedRevision = revision,
        }, CancellationToken.None);

        RememberAudit(db, t.EmailTemplateId);

        await using var verify = EmailEvidenceHarness.NewContext();
        var after = await LoadAsync(verify, code);

        Assert.Equal(shipped.BodyVi, after.BodyVi);
        Assert.Equal(shipped.BodyEn, after.BodyEn);
    }

    // ── What restore must not touch or accept ───────────────────────────────

    /// <summary>Registry-owned metadata is not the operator's, and restore does not move it either.</summary>
    [Fact]
    public async Task Restoring_does_not_move_any_registry_owned_field()
    {
        EmailEvidenceHarness.RequireDb();
        await using var db = EmailEvidenceHarness.NewContext();

        const string code = SystemEmailTemplates.AccountEmailChangedOldNotice;
        var before = await LoadAsync(db, code);
        var fingerprint = new
        {
            before.TemplateCode, before.Purpose, before.CampusId, before.Status, before.BodyFormat,
        };

        await Restore(db).Handle(new RestoreEmailTemplateCommand
        {
            EmailTemplateId = before.EmailTemplateId, ExpectedRevision = before.Revision,
        }, CancellationToken.None);

        RememberAudit(db, before.EmailTemplateId);

        await using var verify = EmailEvidenceHarness.NewContext();
        var after = await LoadAsync(verify, code);

        Assert.Equal(
            JsonSerializer.Serialize(fingerprint),
            JsonSerializer.Serialize(new
            {
                after.TemplateCode, after.Purpose, after.CampusId, after.Status, after.BodyFormat,
            }));
    }

    /// <summary>A code the registry does not know has no default to restore to, and says so.</summary>
    [Fact]
    public async Task An_unknown_template_is_refused_with_a_stable_code()
    {
        EmailEvidenceHarness.RequireDb();
        await using var db = EmailEvidenceHarness.NewContext();

        var historical = new EmailTemplate
        {
            TemplateCode = "HISTORICAL_RESTORE_PROBE_" + Guid.NewGuid().ToString("N")[..8],
            Name = "Bản ghi lịch sử",
            Purpose = "ACCOUNT",
            Status = "ACTIVE",
            SubjectVi = "x", BodyVi = "<p>x</p>", SubjectEn = "x", BodyEn = "<p>x</p>",
            CreatedAt = DateTime.Now,
        };
        db.EmailTemplates.Add(historical);
        await db.SaveChangesAsync();

        try
        {
            var ex = await Assert.ThrowsAsync<ConflictException>(() =>
                Restore(db).Handle(new RestoreEmailTemplateCommand
                {
                    EmailTemplateId = historical.EmailTemplateId, ExpectedRevision = historical.Revision,
                }, CancellationToken.None));

            Assert.Equal(EmailErrorCodes.TemplateCatalogFixed, ex.ErrorCode);
        }
        finally
        {
            await db.EmailTemplates.Where(t => t.EmailTemplateId == historical.EmailTemplateId)
                .ExecuteDeleteAsync();
        }
    }

    /// <summary>A missing row is a 404, not a silent success.</summary>
    [Fact]
    public async Task Restoring_a_template_that_does_not_exist_is_a_not_found()
    {
        EmailEvidenceHarness.RequireDb();
        await using var db = EmailEvidenceHarness.NewContext();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            Restore(db).Handle(new RestoreEmailTemplateCommand
            {
                EmailTemplateId = 99_999_999, ExpectedRevision = 1,
            }, CancellationToken.None));
    }

    // ── Audit ───────────────────────────────────────────────────────────────

    /// <summary>
    /// A restore is recorded with the actor, the template and both sides of the change. The replaced text
    /// matters most: an operator who restores by mistake needs their wording to still exist somewhere.
    /// </summary>
    [Fact]
    public async Task A_restore_writes_an_audit_row_carrying_the_replaced_content()
    {
        EmailEvidenceHarness.RequireDb();
        await using var db = EmailEvidenceHarness.NewContext();

        const string code = SystemEmailTemplates.AccountPendingEmailChangedOldNotice;
        var revision = await BreakAsync(db, code, "AUDIT-ME");
        var t = await LoadAsync(db, code);

        await Restore(db).Handle(new RestoreEmailTemplateCommand
        {
            EmailTemplateId = t.EmailTemplateId, ExpectedRevision = revision,
        }, CancellationToken.None);

        RememberAudit(db, t.EmailTemplateId);

        await using var verify = EmailEvidenceHarness.NewContext();
        var audit = await verify.AuditLogs.AsNoTracking()
            .Include(a => a.Changes)
            .Where(a => a.EntityType == "EmailTemplate" && a.EntityId == t.EmailTemplateId
                        && a.Action == RestoreEmailTemplateCommandHandler.AuditAction)
            .OrderByDescending(a => a.AuditLogId)
            .FirstOrDefaultAsync();

        Assert.NotNull(audit);
        Assert.Equal(1UL, audit!.ActorUserId);
        Assert.NotEmpty(audit.Changes);

        var change = audit.Changes.First();
        Assert.Contains("AUDIT-ME", change.OldValueText ?? "");
        Assert.DoesNotContain("AUDIT-ME", change.NewValueText ?? "");
    }

    // ── Catalog-wide ────────────────────────────────────────────────────────

    /// <summary>
    /// Every one of the thirty has a usable default. A restore feature that covered twenty-nine would
    /// leave exactly one template permanently unrecoverable, and nothing on the screen would say which.
    /// </summary>
    [Fact]
    public void Every_system_template_has_a_shipped_default()
    {
        var missing = SystemEmailTemplates.All
            .Where(t => EmailTemplateDefaults.For(t.TemplateCode) is null)
            .Select(t => t.TemplateCode)
            .ToList();

        Assert.Empty(missing);
        Assert.Equal(SystemEmailTemplates.All.Count, EmailTemplateDefaults.ByCode.Count);
    }

    /// <summary>
    /// Every shipped default satisfies its own template's variable contract. If one did not, restoring it
    /// would hand the operator a template that saves cleanly and then fails every send.
    /// </summary>
    [Fact]
    public void Every_shipped_default_satisfies_its_own_contract()
    {
        var offenders = new List<string>();

        foreach (var template in SystemEmailTemplates.All)
        {
            var contract = EmailTemplateContracts.For(template.TemplateCode);
            var shipped = EmailTemplateDefaults.For(template.TemplateCode);
            if (contract is null || shipped is null) { offenders.Add(template.TemplateCode); continue; }

            var issues = EmailTemplateContentValidator.Validate(
                contract, shipped.SubjectVi, shipped.BodyVi, shipped.SubjectEn, shipped.BodyEn);

            if (issues.Any(i => i.IsError))
                offenders.Add($"{template.TemplateCode}: {string.Join("; ", issues.Where(i => i.IsError).Select(i => i.Code))}");
        }

        Assert.Empty(offenders);
    }
}
