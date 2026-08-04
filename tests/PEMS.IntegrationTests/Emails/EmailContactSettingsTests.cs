using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;
using PEMS.Application.Emails.Contact;
using PEMS.Domain.Entities.Emails;
using PEMS.Domain.Enums;
using PEMS.Infrastructure.Persistence;
using PEMS.IntegrationTests.TestInfrastructure;
using Xunit;

namespace PEMS.IntegrationTests.Emails;

/// <summary>
/// The template screen's "Cấu hình thông tin liên hệ" card, against a real database.
///
/// <para>
/// <b>Why these exist.</b> The card showed one sentence — "Không tìm thấy dữ liệu cần xử lý." — for
/// every possible failure, and that sentence is the toast helper's generic HTTP-404 text, reached
/// whenever the response carried no error code. It was therefore shown for a running API built before
/// the endpoint existed (nothing wrong with the data), for a database missing
/// <c>email_contact_policies</c> (patch not run), and for a template code outside the catalog (catalog
/// not aligned). Three unrelated repairs behind one message. These tests pin the distinctions the
/// backend now has to make so the screen can name the right one.
/// </para>
/// <para>
/// The other half is inheritance. The card previously derived "Đang kế thừa" from
/// <c>isDefault = !hasOverride</c> — does a TEMPLATE-scope row exist. The seed writes a row for all 31
/// catalogued templates, so that expression is <c>false</c> everywhere and the notice was unreachable.
/// Provenance is now reported per field, which is also how the cascade actually works.
/// </para>
/// </summary>
public sealed class EmailContactSettingsTests : IDisposable
{
    /// <summary>A template that is REQUIRED and carries the block — the ordinary configured case.</summary>
    private const string RequiredTemplate = SystemEmailTemplates.VisitParticipantInvitation;

    /// <summary>A template whose shipped policy is NONE: no contact block at all, deliberately.</summary>
    private const string NoContactTemplate = SystemEmailTemplates.AuthPasswordResetOtp;

    /// <summary>
    /// A template that MAY carry the block but is not obliged to — the case where all three levels are
    /// the operator's to choose, and the only one on which a NONE save is legal.
    /// </summary>
    private const string OptionalTemplate = SystemEmailTemplates.AccountRoleChanged;

    private readonly List<EmailContactPolicy> _originals = new();

    public EmailContactSettingsTests()
    {
        if (!CanUseDatabase()) return;

        using var db = EmailEvidenceHarness.NewContext();

        // Integration tests share one database and xUnit runs classes in parallel, so anything this
        // suite edits it must be able to put back exactly — including a row it deletes.
        foreach (var row in db.EmailContactPolicies.AsNoTracking().ToList())
            _originals.Add(row);
    }

    private static bool CanUseDatabase()
    {
        try { EmailEvidenceHarness.RequireDb(); return true; }
        catch { return false; }
    }

    public void Dispose()
    {
        if (_originals.Count == 0) return;

        try
        {
            using var db = EmailEvidenceHarness.NewContext();

            var live = db.EmailContactPolicies.ToList();
            var byKey = _originals.ToDictionary(r => (r.ScopeType, r.ScopeKey));

            foreach (var row in live)
            {
                if (!byKey.TryGetValue((row.ScopeType, row.ScopeKey), out var o))
                {
                    db.EmailContactPolicies.Remove(row);   // something this suite added
                    continue;
                }

                row.Requirement = o.Requirement;
                row.ContactSource = o.ContactSource;
                row.ShowEmail = o.ShowEmail;
                row.ShowPhone = o.ShowPhone;
                row.ShowDepartment = o.ShowDepartment;
                row.ShowCampus = o.ShowCampus;
                row.ShowSender = o.ShowSender;
                row.HeadingVi = o.HeadingVi;
                row.HeadingEn = o.HeadingEn;
                row.ReplyToSource = o.ReplyToSource;
            }

            var liveKeys = live.Select(r => (r.ScopeType, r.ScopeKey)).ToHashSet();
            foreach (var o in _originals.Where(o => !liveKeys.Contains((o.ScopeType, o.ScopeKey))))
                db.EmailContactPolicies.Add(new EmailContactPolicy
                {
                    ScopeType = o.ScopeType,
                    ScopeKey = o.ScopeKey,
                    Requirement = o.Requirement,
                    ContactSource = o.ContactSource,
                    ShowEmail = o.ShowEmail,
                    ShowPhone = o.ShowPhone,
                    ShowDepartment = o.ShowDepartment,
                    ShowCampus = o.ShowCampus,
                    ShowSender = o.ShowSender,
                    HeadingVi = o.HeadingVi,
                    HeadingEn = o.HeadingEn,
                    ReplyToSource = o.ReplyToSource,
                    CreatedAt = o.CreatedAt,
                    CreatedBy = o.CreatedBy,
                });

            db.SaveChanges();
        }
        catch
        {
            // Never let cleanup mask the assertion that actually failed.
        }
    }

    // ── Wiring ───────────────────────────────────────────────────────────────

    private sealed class HoOperator : ICurrentUserService
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

    /// <summary>Routes the one query the update handler re-sends, so no DI container is needed.</summary>
    private sealed class GetSettingsMediator : IMediator
    {
        private readonly GetEmailContactSettingsQueryHandler _handler;
        public GetSettingsMediator(GetEmailContactSettingsQueryHandler handler) => _handler = handler;

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
            => request is GetEmailContactSettingsQuery q
                ? (Task<TResponse>)(object)_handler.Handle(q, ct)
                : throw new NotSupportedException($"Unexpected request {request.GetType().Name}.");

        public Task<object?> Send(object request, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task Send<TRequest>(TRequest request, CancellationToken ct = default) where TRequest : IRequest
            => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> r, CancellationToken ct = default)
            => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task Publish(object notification, CancellationToken ct = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification n, CancellationToken ct = default)
            where TNotification : INotification => Task.CompletedTask;
    }

    private static GetEmailContactSettingsQueryHandler Get(ApplicationDbContext db)
        => new(db, new EmailContactPolicyStore(db));

    private static UpdateEmailContactSettingsCommandHandler Update(ApplicationDbContext db)
        => new(db, new HoOperator(), new GetSettingsMediator(Get(db)));

    private static RestoreEmailContactSettingsCommandHandler Restore(ApplicationDbContext db)
        => new(db, new HoOperator(), new GetSettingsMediator(Get(db)));

    private static Task<EmailContactSettingsDto> Load(ApplicationDbContext db, string code)
        => Get(db).Handle(new GetEmailContactSettingsQuery { TemplateCode = code }, CancellationToken.None);

    // ── The form opens ───────────────────────────────────────────────────────

    /// <summary>
    /// The headline case, as it stands after the capability split: a catalogued template that CAN carry
    /// the block answers with a complete, renderable settings object — every control the card draws has a
    /// value and a list of options. This is what "card 4 shows a form instead of an error" means at the
    /// API boundary.
    ///
    /// <para>
    /// It used to be asserted over every catalogued template. That was right while every template was
    /// treated as configurable, and it is the assumption the reported defect rested on: the four
    /// templates that can never render a block were handed the full requirement list, an operator set
    /// "Tùy chọn" on one, and the block that setting invites was then refused by the content validator.
    /// Those four are asserted separately below, on what they must NOT offer.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_catalogued_template_always_answers_with_a_complete_form()
    {
        EmailEvidenceHarness.RequireDb();
        await using var db = EmailEvidenceHarness.NewContext();

        foreach (var code in SystemEmailTemplates.AllCodes.Where(EmailContactCapabilities.Supports))
        {
            var dto = await Load(db, code);

            Assert.Equal(code, dto.TemplateCode);
            Assert.False(string.IsNullOrWhiteSpace(dto.Requirement));
            Assert.False(string.IsNullOrWhiteSpace(dto.ContactSource));
            Assert.False(string.IsNullOrWhiteSpace(dto.ReplyToSource));
            Assert.False(string.IsNullOrWhiteSpace(dto.HeadingVi));
            Assert.False(string.IsNullOrWhiteSpace(dto.HeadingEn));
            Assert.Equal("{{contactInformationBlock}}", dto.BlockPlaceholder);

            Assert.True(dto.Editable);
            Assert.NotEmpty(dto.AvailableRequirements);
            Assert.NotEmpty(dto.AvailableSources);
            Assert.NotEmpty(dto.AvailableReplyToSources);
        }
    }

    /// <summary>
    /// A template that cannot carry the block answers with a CAPABILITY and a reason, and with no levels
    /// to choose between — so the card has nothing to render a form out of, by construction rather than
    /// by the screen deciding to hide one.
    /// </summary>
    [Fact]
    public async Task An_unsupported_template_answers_with_a_reason_and_no_choices()
    {
        EmailEvidenceHarness.RequireDb();
        await using var db = EmailEvidenceHarness.NewContext();

        foreach (var code in EmailContactCapabilities.UnsupportedTemplateCodes)
        {
            var dto = await Load(db, code);

            Assert.Equal(nameof(EmailContactCapability.UNSUPPORTED), dto.Capability);
            Assert.False(dto.Editable);
            Assert.Empty(dto.AvailableRequirements);
            Assert.Equal(nameof(EmailContactRequirement.NONE), dto.Requirement);
            Assert.False(string.IsNullOrWhiteSpace(dto.CapabilityReasonCode));
            Assert.False(string.IsNullOrWhiteSpace(dto.CapabilityReasonVi));
        }
    }

    /// <summary>
    /// A template whose text tells the recipient to make contact is not offered "Không hiển thị": the
    /// instruction would be left with no address, and the save is refused anyway.
    /// </summary>
    [Fact]
    public async Task A_template_that_promises_a_contact_is_not_offered_NONE()
    {
        EmailEvidenceHarness.RequireDb();
        await using var db = EmailEvidenceHarness.NewContext();

        var dto = await Load(db, RequiredTemplate);

        Assert.Equal(nameof(EmailContactCapability.REQUIRED), dto.Capability);
        Assert.True(dto.Editable);
        Assert.DoesNotContain(nameof(EmailContactRequirement.NONE), dto.AvailableRequirements);
        Assert.Contains(nameof(EmailContactRequirement.OPTIONAL), dto.AvailableRequirements);

        var refused = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Update(db).Handle(new UpdateEmailContactSettingsCommand
            {
                TemplateCode = RequiredTemplate,
                Requirement = nameof(EmailContactRequirement.NONE),
                ContactSource = nameof(EmailContactSource.HOST),
                ShowEmail = true,
                ShowPhone = true,
                ReplyToSource = nameof(EmailReplyToSource.NONE),
            }, CancellationToken.None));

        Assert.Equal(EmailErrorCodes.ContactConfigurationInvalid, refused.ErrorCode);
    }

    /// <summary>
    /// A template with NO policy row of its own still opens the form — on the values it inherits, each
    /// labelled with the level that supplied it. This is the case the old <c>isDefault</c> flag claimed
    /// to describe and never could.
    /// </summary>
    [Fact]
    public async Task A_template_with_no_row_of_its_own_opens_the_form_on_inherited_values()
    {
        EmailEvidenceHarness.RequireDb();

        await using (var setup = EmailEvidenceHarness.NewContext())
        {
            var own = await setup.EmailContactPolicies.FirstOrDefaultAsync(
                p => p.ScopeType == EmailContactScopeType.TEMPLATE && p.ScopeKey == RequiredTemplate);

            if (own is not null) setup.EmailContactPolicies.Remove(own);
            await setup.SaveChangesAsync();
        }

        await using var db = EmailEvidenceHarness.NewContext();
        var dto = await Load(db, RequiredTemplate);

        Assert.False(dto.HasOwnPolicyRow);
        Assert.True(dto.HasInheritedField);

        // Not one field claims to be the template's own, because the template has nothing of its own.
        Assert.NotEqual(EmailContactPolicyLevels.Template, dto.RequirementSource);
        Assert.NotEqual(EmailContactPolicyLevels.Template, dto.ContactSourceSource);

        // And it is still a usable form: the shipped default for this template is REQUIRED/HOST.
        Assert.Equal(nameof(EmailContactRequirement.REQUIRED), dto.Requirement);
        Assert.Equal(nameof(EmailContactSource.HOST), dto.ContactSource);
    }

    /// <summary>
    /// Provenance is per FIELD. A row that answers some columns and leaves others NULL must produce a
    /// mixture — the whole reason a single inherited/overridden flag was the wrong shape.
    /// </summary>
    [Fact]
    public async Task Provenance_is_reported_per_field_not_per_row()
    {
        EmailEvidenceHarness.RequireDb();

        await using (var setup = EmailEvidenceHarness.NewContext())
        {
            var own = await setup.EmailContactPolicies.FirstAsync(
                p => p.ScopeType == EmailContactScopeType.TEMPLATE && p.ScopeKey == RequiredTemplate);

            // Says something about the requirement, says nothing about the telephone number.
            own.Requirement = EmailContactRequirement.OPTIONAL;
            own.ShowPhone = null;
            await setup.SaveChangesAsync();
        }

        await using var db = EmailEvidenceHarness.NewContext();
        var dto = await Load(db, RequiredTemplate);

        Assert.True(dto.HasOwnPolicyRow);
        Assert.Equal(EmailContactPolicyLevels.Template, dto.RequirementSource);
        Assert.NotEqual(EmailContactPolicyLevels.Template, dto.ShowPhoneSource);
        Assert.True(dto.HasInheritedField);
    }

    // ── NO_CONTACT ───────────────────────────────────────────────────────────

    /// <summary>
    /// A NO_CONTACT template is a state, not a missing one: it answers normally, with requirement NONE
    /// and the reason the card shows in place of a form.
    ///
    /// <para>
    /// It used to assert the levels were "still switchable". That was the defect, not the guarantee —
    /// this template's whole message is a one-time reset code, so every one of those switches led to a
    /// configuration the renderer ignores and the content validator contradicts.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_no_contact_template_answers_normally_with_requirement_none()
    {
        EmailEvidenceHarness.RequireDb();
        await using var db = EmailEvidenceHarness.NewContext();

        var dto = await Load(db, NoContactTemplate);

        Assert.Equal(nameof(EmailContactRequirement.NONE), dto.Requirement);
        Assert.Equal(nameof(EmailContactCapability.UNSUPPORTED), dto.Capability);
        Assert.Empty(dto.AvailableRequirements);             // nothing to switch between
        Assert.False(dto.BodyCarriesBlockVi);                // and correctly has no block
    }

    /// <summary>Writing contact settings to a template that cannot carry the block is refused by name.</summary>
    [Fact]
    public async Task Saving_contact_settings_for_an_unsupported_template_is_refused()
    {
        EmailEvidenceHarness.RequireDb();
        await using var db = EmailEvidenceHarness.NewContext();

        var refused = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Update(db).Handle(new UpdateEmailContactSettingsCommand
            {
                TemplateCode = NoContactTemplate,
                Requirement = nameof(EmailContactRequirement.OPTIONAL),
                ContactSource = nameof(EmailContactSource.SUPPORT_CONTACT),
                ShowEmail = true,
                ShowPhone = true,
                ReplyToSource = nameof(EmailReplyToSource.NONE),
            }, CancellationToken.None));

        // Its own code: an invalid CONFIGURATION is one an operator fixes by changing a value, and this
        // one has no value that would make it valid.
        Assert.Equal(EmailErrorCodes.ContactNotSupportedForTemplate, refused.ErrorCode);
    }

    /// <summary>
    /// The other write on this card is refused the same way.
    ///
    /// <para>
    /// The button is not rendered on a template that cannot carry the block, so a call reaching the
    /// handler is either a screen left open across a release or a caller working from an assumption the
    /// catalog does not support. Both are worth a named refusal rather than a silent no-op, which would
    /// report success for a restore that put nothing anywhere — and the backend stays the last line
    /// whatever the screen is doing.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Restoring_contact_settings_for_an_unsupported_template_is_refused()
    {
        EmailEvidenceHarness.RequireDb();
        await using var db = EmailEvidenceHarness.NewContext();

        var refused = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Restore(db).Handle(
                new RestoreEmailContactSettingsCommand { TemplateCode = NoContactTemplate },
                CancellationToken.None));

        Assert.Equal(EmailErrorCodes.ContactNotSupportedForTemplate, refused.ErrorCode);

        // And nothing moved: the template still reports the state it had.
        var after = await Load(db, NoContactTemplate);
        Assert.Equal(nameof(EmailContactRequirement.NONE), after.Requirement);
        Assert.Equal(nameof(EmailContactCapability.UNSUPPORTED), after.Capability);
    }

    // ── Saving ───────────────────────────────────────────────────────────────

    /// <summary>
    /// The round trip the operator actually performs: change toggles, save, reload, see the same values
    /// — now reported as the template's own rather than inherited.
    /// </summary>
    [Fact]
    public async Task Toggles_survive_a_save_and_reload()
    {
        EmailEvidenceHarness.RequireDb();

        EmailContactSettingsDto saved;
        await using (var db = EmailEvidenceHarness.NewContext())
        {
            var before = await Load(db, RequiredTemplate);

            saved = await Update(db).Handle(new UpdateEmailContactSettingsCommand
            {
                TemplateCode = RequiredTemplate,
                Requirement = before.Requirement,
                ContactSource = before.ContactSource,
                ShowEmail = true,
                ShowPhone = false,               // flipped
                ShowDepartment = true,           // flipped
                ShowCampus = false,              // flipped
                ShowSender = true,               // flipped
                HeadingVi = "Đầu mối liên hệ",
                HeadingEn = "Reply contact",
                ReplyToSource = nameof(EmailReplyToSource.CONTACT),
            }, CancellationToken.None);
        }

        Assert.False(saved.ShowPhone);
        Assert.True(saved.ShowDepartment);

        // Reloaded through a FRESH context, so this reads the database rather than a tracked graph.
        await using var verify = EmailEvidenceHarness.NewContext();
        var reloaded = await Load(verify, RequiredTemplate);

        Assert.True(reloaded.ShowEmail);
        Assert.False(reloaded.ShowPhone);
        Assert.True(reloaded.ShowDepartment);
        Assert.False(reloaded.ShowCampus);
        Assert.True(reloaded.ShowSender);
        Assert.Equal("Đầu mối liên hệ", reloaded.HeadingVi);
        Assert.Equal("Reply contact", reloaded.HeadingEn);
        Assert.Equal(nameof(EmailReplyToSource.CONTACT), reloaded.ReplyToSource);

        // Everything just written is the template's own now, by definition.
        Assert.True(reloaded.HasOwnPolicyRow);
        Assert.Equal(EmailContactPolicyLevels.Template, reloaded.ShowPhoneSource);
        Assert.Equal(EmailContactPolicyLevels.Template, reloaded.HeadingSource);
    }

    /// <summary>
    /// Saving a template that had no row creates one rather than failing.
    ///
    /// <para>
    /// Exercised on a template that SUPPORTS the block. It used to use the NO_CONTACT one, which the
    /// settings endpoint now refuses outright — so the row-creation path would have been asserted through
    /// the one template on which it must never run.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Saving_a_template_with_no_row_creates_one()
    {
        EmailEvidenceHarness.RequireDb();

        await using (var setup = EmailEvidenceHarness.NewContext())
        {
            var own = await setup.EmailContactPolicies.FirstOrDefaultAsync(
                p => p.ScopeType == EmailContactScopeType.TEMPLATE && p.ScopeKey == OptionalTemplate);
            if (own is not null) setup.EmailContactPolicies.Remove(own);
            await setup.SaveChangesAsync();
        }

        await using var db = EmailEvidenceHarness.NewContext();

        var result = await Update(db).Handle(new UpdateEmailContactSettingsCommand
        {
            TemplateCode = OptionalTemplate,
            Requirement = nameof(EmailContactRequirement.NONE),
            ContactSource = nameof(EmailContactSource.SUPPORT_CONTACT),
            ShowEmail = true,
            ShowPhone = true,
            ReplyToSource = nameof(EmailReplyToSource.NONE),
        }, CancellationToken.None);

        Assert.True(result.HasOwnPolicyRow);
        Assert.Equal(nameof(EmailContactRequirement.NONE), result.Requirement);
    }

    // ── Data integrity ───────────────────────────────────────────────────────

    /// <summary>
    /// At most ONE row per scope. A second SYSTEM row would make the cascade's answer depend on which
    /// one the query happened to return first — a policy that changes between reads without anybody
    /// editing it. The patch is written to be re-runnable precisely so this cannot accumulate.
    /// </summary>
    [Fact]
    public async Task No_scope_has_a_duplicate_policy_row()
    {
        EmailEvidenceHarness.RequireDb();
        await using var db = EmailEvidenceHarness.NewContext();

        var duplicates = await db.EmailContactPolicies
            .AsNoTracking()
            .GroupBy(p => new { p.ScopeType, p.ScopeKey })
            .Where(g => g.Count() > 1)
            .Select(g => new { g.Key.ScopeType, g.Key.ScopeKey, Count = g.Count() })
            .ToListAsync();

        Assert.True(duplicates.Count == 0,
            "Duplicate contact policy rows: "
            + string.Join(", ", duplicates.Select(d => $"{d.ScopeType}/{d.ScopeKey} ×{d.Count}")));

        Assert.Equal(
            1,
            await db.EmailContactPolicies.CountAsync(p => p.ScopeType == EmailContactScopeType.SYSTEM));
    }

    // ── Failure modes are told apart ─────────────────────────────────────────

    /// <summary>
    /// A code outside the catalog is NOT found — under the template code, so the screen can say "run
    /// the catalog alignment patch" instead of the generic 404 sentence.
    /// </summary>
    [Fact]
    public async Task A_template_outside_the_catalog_fails_under_the_template_not_found_code()
    {
        EmailEvidenceHarness.RequireDb();
        await using var db = EmailEvidenceHarness.NewContext();

        var ex = await Assert.ThrowsAsync<NotFoundException>(
            () => Load(db, "ACCOUNT_CREATED_INTERNAL"));   // a real pre-catalog demo code

        Assert.Equal(EmailErrorCodes.TemplateNotFound, ex.ErrorCode);
    }

    /// <summary>
    /// A database without <c>email_contact_policies</c> — the patch not run — reports its own code
    /// rather than surfacing a raw provider error or being mistaken for a missing template.
    /// </summary>
    [Fact]
    public async Task A_database_without_the_policy_table_reports_the_store_as_unavailable()
    {
        EmailEvidenceHarness.RequireDb();

        var scratch = "pems_contact_store_probe_" + Guid.NewGuid().ToString("N")[..8];

        await using (var admin = EmailEvidenceHarness.NewContext())
        {
            await admin.Database.ExecuteSqlRawAsync($"CREATE DATABASE `{scratch}`");
        }

        try
        {
            // BaseConnectionString already names a database, so the key is REPLACED — appending a
            // second `database=` makes MySqlConnector reject the whole string before any query runs.
            var connection = System.Text.RegularExpressions.Regex.Replace(
                EmailEvidenceHarness.BaseConnectionString,
                @"database=[^;]*",
                $"database={scratch}");

            await using var db = EmailEvidenceHarness.ContextFor(connection);

            var ex = await Assert.ThrowsAsync<BusinessRuleException>(
                () => new EmailContactPolicyStore(db)
                    .ResolveAsync(RequiredTemplate, null, null, CancellationToken.None));

            Assert.Equal(EmailErrorCodes.ContactPolicyStoreUnavailable, ex.ErrorCode);
            Assert.Contains("email_contact_policies", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            await using var admin = EmailEvidenceHarness.NewContext();
            await admin.Database.ExecuteSqlRawAsync($"DROP DATABASE IF EXISTS `{scratch}`");
        }
    }
}
