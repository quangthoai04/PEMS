using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using PEMS.Application.Accounts.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.DepartmentLeaderPersonnel.Commands.UpdateDepartmentPersonnel;
using PEMS.Application.DepartmentLeaderPersonnel.Common;
using PEMS.Application.Emails.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Enums;
using Xunit;

namespace PEMS.UnitTests.DepartmentLeaderPersonnel;

/// <summary>
/// Profile + login-identity editing (spec §12) — the heart of this feature.
///
/// The rule every test here defends: <b>the email is editable in EVERY account status, and changing
/// it never changes that status.</b> PENDING stays PENDING, ACTIVE stays ACTIVE, INACTIVE stays
/// INACTIVE, LOCKED stays LOCKED. What differs per status is only what the new address COSTS —
/// a re-issued confirmation for PENDING, an identity reset plus session revocation for the rest.
/// </summary>
public class UpdateDepartmentPersonnelCommandTests
{
    private const ulong TargetId = 901;
    private const string OldEmail = "cu@fpt.edu.vn";
    private const string NewEmail = "moi@fpt.edu.vn";

    private static UpdateDepartmentPersonnelCommandHandler Handler(DepartmentLeaderTestHarness h)
        => new(h.Db, h.Scope, h.Locks, h.Confirmations.Object, h.Sessions, h.Dispatcher, h.Clock);

    private static UpdateDepartmentPersonnelCommand Command(
        string email = NewEmail,
        string fullName = "Nhan Vien Moi",
        string phone = "0912345678",
        string gender = "MALE") => new()
    {
        UserId = TargetId,
        FullName = fullName,
        Email = email,
        Phone = phone,
        Gender = gender,
    };

    private static Task<UpdateDepartmentPersonnelResponse> Run(
        DepartmentLeaderTestHarness h, UpdateDepartmentPersonnelCommand command)
        => Handler(h).Handle(command, CancellationToken.None);

    /// <summary>Target in the caller's department, with the given status and one active session.</summary>
    private static DepartmentLeaderTestHarness WithTarget(string status)
    {
        var h = DepartmentLeaderTestHarness.Create();
        h.AddStaff(TargetId, status: status, email: OldEmail, fullName: "Nhan Vien Cu",
            phone: "0900000000", gender: Gender.Male);
        h.AddActiveSession(sessionId: 5001, userId: TargetId);
        return h;
    }

    // ── Profile-only edits ───────────────────────────────────────────────────

    [Fact]
    public async Task Updates_name_phone_and_gender_without_touching_identity()
    {
        var h = WithTarget(UserStatuses.Active);
        h.AddAuthProvider(1, TargetId, ProviderTypes.GoogleSso, OldEmail);

        var result = await Run(h, Command(email: OldEmail, fullName: "Ten Moi", phone: "0987654321", gender: "FEMALE"));

        var target = h.GetUser(TargetId);
        Assert.Equal("Ten Moi", target.FullName);
        Assert.Equal("0987654321", target.Phone);
        Assert.Equal(Gender.Female, target.Gender);
        Assert.True(result.Changed);
        Assert.False(result.EmailChanged);

        // Identity untouched: the SSO link survives and nobody is signed out.
        Assert.Single(h.Db.UserAuthProviders.Where(p => p.UserId == TargetId));
        Assert.Empty(h.Sessions.RevokeAllCalls);
        Assert.Equal(DepartmentPersonnelEmails.StatusNotRequired, result.EmailNotificationStatus);
    }

    /// <summary>
    /// The bug this guards: opening the modal and pressing save must not rewrite a stored gender.
    /// A round-trip of the same values is a no-op, not a silent Male → Other conversion.
    /// </summary>
    [Fact]
    public async Task Round_tripping_unchanged_values_is_a_no_op_and_preserves_gender()
    {
        var h = WithTarget(UserStatuses.Active);

        var result = await Run(h, Command(
            email: OldEmail, fullName: "Nhan Vien Cu", phone: "0900000000", gender: "MALE"));

        Assert.False(result.Changed);
        Assert.False(result.EmailChanged);
        Assert.Equal("Không có thông tin nào thay đổi.", result.Message);
        Assert.Equal(Gender.Male, h.GetUser(TargetId).Gender);

        // A true no-op writes nothing at all: no audit row, no updated_at bump, no revoke.
        Assert.Empty(h.Db.AuditLogs);
        Assert.Empty(h.Sessions.RevokeAllCalls);
    }

    [Fact]
    public async Task Whitespace_and_case_differences_alone_do_not_count_as_a_change()
    {
        var h = WithTarget(UserStatuses.Active);

        var result = await Run(h, Command(
            email: "  CU@FPT.EDU.VN ", fullName: "  Nhan   Vien  Cu ", phone: " 0900000000 ", gender: "MALE"));

        Assert.False(result.Changed);
        Assert.False(result.EmailChanged);
        Assert.Empty(h.Sessions.RevokeAllCalls);
    }

    [Fact]
    public async Task Role_department_and_campus_are_never_modified()
    {
        var h = WithTarget(UserStatuses.Active);
        var before = h.GetUser(TargetId);
        var (roleId, subRole, departmentId, campusId) =
            (before.RoleId, before.SubRole, before.DepartmentId, before.PrimaryCampusId);

        await Run(h, Command());

        var after = h.GetUser(TargetId);
        Assert.Equal(roleId, after.RoleId);
        Assert.Equal(subRole, after.SubRole);
        Assert.Equal(departmentId, after.DepartmentId);
        Assert.Equal(campusId, after.PrimaryCampusId);
    }

    // ── Email change: status is preserved in ALL FOUR states ─────────────────

    [Theory]
    [InlineData(UserStatuses.PendingEmailConfirmation)]
    [InlineData(UserStatuses.Active)]
    [InlineData(UserStatuses.Inactive)]
    [InlineData(UserStatuses.Locked)]
    public async Task Email_is_editable_in_every_status_and_the_status_never_changes(string status)
    {
        var h = WithTarget(status);

        var result = await Run(h, Command());

        Assert.True(result.EmailChanged);
        Assert.Equal(NewEmail, h.GetUser(TargetId).Email);
        // The invariant: identity changed, status did not.
        Assert.Equal(status, h.GetUser(TargetId).Status);
        Assert.Equal(status, result.Status);
    }

    [Theory]
    [InlineData(UserStatuses.PendingEmailConfirmation)]
    [InlineData(UserStatuses.Active)]
    [InlineData(UserStatuses.Inactive)]
    [InlineData(UserStatuses.Locked)]
    public async Task Every_email_change_revokes_all_sessions(string status)
    {
        var h = WithTarget(status);

        var result = await Run(h, Command());

        var call = Assert.Single(h.Sessions.RevokeAllCalls);
        Assert.Equal(TargetId, call.UserId);
        Assert.Equal(SessionRevokeReasons.AccountEmailChanged, call.Reason);
        Assert.Equal(1, result.RevokedSessions);
    }

    [Theory]
    [InlineData(UserStatuses.PendingEmailConfirmation)]
    [InlineData(UserStatuses.Active)]
    [InlineData(UserStatuses.Inactive)]
    [InlineData(UserStatuses.Locked)]
    public async Task Old_address_notice_never_leaks_the_new_address(string status)
    {
        var h = WithTarget(status);

        await Run(h, Command());

        var oldNotice = h.MessageTo(OldEmail);

        // The unlinked address may belong to a stranger reached by a typo. It gets one of the two
        // deliberately variable-free ACCOUNT templates, so there is nothing to leak: no new address,
        // no holder name, no department, no campus. Asserting the variable set is empty proves that
        // for every future edit of the body, which searching the rendered HTML never could.
        Assert.Contains(oldNotice.TemplateCode, new[]
        {
            SystemEmailTemplates.AccountEmailChangedOldNotice,
            SystemEmailTemplates.AccountPendingEmailChangedOldNotice,
        });
        Assert.Empty(oldNotice.Variables);

        // The display name is part of the To header, so it must be absent there too.
        Assert.True(string.IsNullOrEmpty(oldNotice.To.DisplayName));
    }

    // ── PENDING specifics ────────────────────────────────────────────────────

    [Fact]
    public async Task Pending_account_gets_a_fresh_confirmation_bound_to_the_new_address()
    {
        var h = WithTarget(UserStatuses.PendingEmailConfirmation);
        h.AddPendingConfirmation(7001, TargetId, OldEmail);

        var result = await Run(h, Command());

        Assert.True(result.ConfirmationReissued);
        Assert.False(result.AuthenticationRelinkRequired);
        Assert.Equal(UserStatuses.PendingEmailConfirmation, h.GetUser(TargetId).Status);

        // isResend:false restarts the resend counter for the new address; the service supersedes
        // the previous live token, which is what kills the old link.
        h.Confirmations.Verify(c => c.IssuePendingAsync(
            TargetId, NewEmail, false, It.IsAny<CancellationToken>()), Times.Once);

        // The new address receives a confirmation LINK, not a "your login changed" notice.
        var toNew = h.MessageTo(NewEmail);
        Assert.Equal(SystemEmailTemplates.AccountEmailConfirmation, toNew.TemplateCode);
        Assert.Contains(
            "confirm-email?token=",
            toNew.TrustedBlocks![EmailTrustedBlocks.ActionBlock]);
    }

    [Fact]
    public async Task Pending_account_keeps_its_sso_providers_untouched()
    {
        var h = WithTarget(UserStatuses.PendingEmailConfirmation);
        h.AddAuthProvider(1, TargetId, ProviderTypes.GoogleSso, OldEmail);

        var result = await Run(h, Command());

        // Nothing has been proven about this account's identity yet, so there is nothing to reset.
        Assert.False(result.AuthenticationRelinkRequired);
        Assert.Single(h.Db.UserAuthProviders.Where(p => p.UserId == TargetId));
    }

    // ── ACTIVE / INACTIVE / LOCKED identity reset ────────────────────────────

    [Theory]
    [InlineData(UserStatuses.Active)]
    [InlineData(UserStatuses.Inactive)]
    [InlineData(UserStatuses.Locked)]
    public async Task Provisioned_account_drops_sso_rows_and_keeps_local_password(string status)
    {
        var h = WithTarget(status);
        h.AddAuthProvider(1, TargetId, ProviderTypes.GoogleSso, OldEmail);
        h.AddAuthProvider(3, TargetId, ProviderTypes.LocalPassword, OldEmail);
        var target = h.GetUser(TargetId);
        target.PasswordHash = "bcrypt-hash";
        h.Db.SaveChanges();
        h.Detach();

        var result = await Run(h, Command());

        var providers = h.Db.UserAuthProviders.Where(p => p.UserId == TargetId).ToList();
        // The external link is DELETED (the subject identifies the old identity and cannot be rewritten).
        Assert.DoesNotContain(providers, p => p.ProviderType == ProviderTypes.GoogleSso);

        // Local password survives untouched, hash intact — changing an email is not a password
        // reset, and the row carries no address of its own to re-point.
        Assert.Single(providers, p => p.ProviderType == ProviderTypes.LocalPassword);
        Assert.Equal("bcrypt-hash", h.GetUser(TargetId).PasswordHash);
        Assert.Equal(NewEmail, h.GetUser(TargetId).Email);

        Assert.True(result.AuthenticationRelinkRequired);
        Assert.False(result.ConfirmationReissued);
    }

    [Fact]
    public async Task Locked_account_keeps_its_security_lock_metadata()
    {
        var h = WithTarget(UserStatuses.Locked);
        var target = h.GetUser(TargetId);
        target.FailedLoginCount = 7;
        target.LockedUntil = h.Clock.VietnamNow.AddHours(3);
        h.Db.SaveChanges();
        var lockedUntil = target.LockedUntil;
        h.Detach();

        await Run(h, Command());

        var after = h.GetUser(TargetId);
        Assert.Equal(UserStatuses.Locked, after.Status);
        // Fixing a typo in an address must never quietly clear a security lock.
        Assert.Equal(7, after.FailedLoginCount);
        Assert.Equal(lockedUntil, after.LockedUntil);
    }

    [Fact]
    public async Task Inactive_account_stays_inactive_after_an_email_change()
    {
        var h = WithTarget(UserStatuses.Inactive);

        var result = await Run(h, Command());

        Assert.Equal(UserStatuses.Inactive, h.GetUser(TargetId).Status);
        Assert.Contains("vô hiệu hóa", result.Message);
    }

    [Fact]
    public async Task Stray_pending_confirmation_on_a_provisioned_account_is_superseded()
    {
        var h = WithTarget(UserStatuses.Active);
        h.AddPendingConfirmation(7002, TargetId, OldEmail);

        await Run(h, Command());

        var row = h.Db.AccountEmailConfirmations.Single(c => c.ConfirmationId == 7002);
        Assert.Equal(AccountEmailConfirmationStatuses.Superseded, row.Status);
    }

    // ── Uniqueness / conflicts ───────────────────────────────────────────────

    [Fact]
    public async Task Email_taken_by_another_user_is_a_409()
    {
        var h = WithTarget(UserStatuses.Active);
        h.AddStaff(902, email: NewEmail);

        var ex = await Assert.ThrowsAsync<ConflictException>(() => Run(h, Command()));
        Assert.Equal(DepartmentLeaderErrorCodes.AccountEmailAlreadyExists, ex.ErrorCode);
        Assert.Equal(OldEmail, h.GetUser(TargetId).Email);   // nothing was written
    }

    /// <summary>
    /// There is only ONE identity surface left to collide on. An auth-provider row no longer stores
    /// an address of its own (provider_email is gone) — it authenticates against its account's
    /// <c>users.email</c> — so another account's Google binding cannot "own" this address, and
    /// <c>uq_users_email</c> is the whole rule. The edit therefore succeeds.
    /// </summary>
    [Fact]
    public async Task Email_is_free_when_only_another_accounts_provider_row_existed()
    {
        var h = WithTarget(UserStatuses.Active);
        h.AddStaff(902, email: "other@fpt.edu.vn");
        h.AddAuthProvider(9, 902, ProviderTypes.GoogleSso, "other@fpt.edu.vn");

        var result = await Run(h, Command());

        Assert.True(result.EmailChanged);
        Assert.Equal(NewEmail, h.GetUser(TargetId).Email);
        // The other account keeps its own binding untouched.
        Assert.Single(h.Db.UserAuthProviders, p => p.UserId == 902);
    }

    [Fact]
    public async Task Invalid_email_is_rejected_and_nothing_is_written()
    {
        var h = WithTarget(UserStatuses.Active);

        await Assert.ThrowsAsync<ValidationException>(() => Run(h, Command(email: "khong-phai-email")));
        Assert.Equal(OldEmail, h.GetUser(TargetId).Email);
        Assert.Empty(h.Sessions.RevokeAllCalls);
    }

    [Fact]
    public async Task Disallowed_email_domain_is_rejected()
    {
        var h = WithTarget(UserStatuses.Active);

        await Assert.ThrowsAsync<ValidationException>(() => Run(h, Command(email: "moi@evil.com")));
        Assert.Equal(OldEmail, h.GetUser(TargetId).Email);
    }

    // ── Login-email domain whitelist, across every status (spec §12, §18.2) ──

    /// <summary>
    /// Every account status the feature supports. The list is spelled out rather than derived so a
    /// new status cannot silently join the product without someone deciding what this rule means
    /// for it.
    /// </summary>
    public static TheoryData<string> AllStatuses => new()
    {
        UserStatuses.Active,
        UserStatuses.Inactive,
        UserStatuses.PendingEmailConfirmation,
        UserStatuses.Locked,
    };

    public static TheoryData<string, string> AllStatusesWithDisallowedEmail()
    {
        var data = new TheoryData<string, string>();
        foreach (var status in new[]
                 {
                     UserStatuses.Active, UserStatuses.Inactive,
                     UserStatuses.PendingEmailConfirmation, UserStatuses.Locked,
                 })
        {
            foreach (var email in new[]
                     {
                         "moi@fe.edu.vn",              // the domain this rule removed
                         "moi@yahoo.com",
                         "moi@student.fpt.edu.vn",     // subdomain
                         "moi@fpt.edu.vn.evil.com",    // wrapped look-alike
                         "moi@fake-fpt.edu.vn",        // prefixed look-alike
                         "moi+tag@gmail.com",          // plus addressing
                     })
            {
                data.Add(status, email);
            }
        }

        return data;
    }

    /// <summary>
    /// The property this whole feature rests on: <b>the status decides what an email change costs,
    /// never which addresses are legal.</b> A refused address is refused identically whether the
    /// account is ACTIVE, INACTIVE, PENDING or LOCKED.
    /// </summary>
    [Theory]
    [MemberData(nameof(AllStatusesWithDisallowedEmail))]
    public async Task Disallowed_domains_are_refused_in_every_status(string status, string email)
    {
        var h = WithTarget(status);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => Run(h, Command(email: email)));

        // Same wording in every status — the operator must not have to guess why one screen differs.
        Assert.False(string.IsNullOrWhiteSpace(ex.Message));
        Assert.Equal(OldEmail, h.GetUser(TargetId).Email);
        Assert.Equal(status, h.GetUser(TargetId).Status);
    }

    /// <summary>
    /// A refused address must cost the account NOTHING. Not the identity, not the status, not the
    /// live sessions, not the pending confirmation token, and no audit row claiming a change that
    /// never happened — the whole point of validating before the transaction.
    /// </summary>
    [Theory]
    [MemberData(nameof(AllStatuses))]
    public async Task A_refused_domain_leaves_the_account_completely_untouched(string status)
    {
        var h = WithTarget(status);
        h.AddAuthProvider(1, TargetId, ProviderTypes.GoogleSso, OldEmail);
        var confirmation = h.AddPendingConfirmation(7001, TargetId, OldEmail);
        var beforeName = h.GetUser(TargetId).FullName;

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => Run(h, Command(email: "moi@fe.edu.vn")));
        Assert.Equal("Chỉ chấp nhận @gmail.com và @fpt.edu.vn.", ex.Message);

        h.Detach();
        var target = h.GetUser(TargetId);
        Assert.Equal(OldEmail, target.Email);
        Assert.Equal(status, target.Status);
        // The name is submitted in the same request; a rejected email must not let it through either.
        Assert.Equal(beforeName, target.FullName);

        Assert.Empty(h.Sessions.RevokeAllCalls);
        Assert.Single(h.Db.UserAuthProviders, p => p.UserId == TargetId);
        Assert.Equal(
            AccountEmailConfirmationStatuses.Pending,
            h.Db.AccountEmailConfirmations.Single(c => c.ConfirmationId == confirmation.ConfirmationId).Status);
        h.Confirmations.Verify(c => c.IssuePendingAsync(
            It.IsAny<ulong>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
        Assert.Empty(h.Dispatcher.Sent);
        Assert.Empty(h.Db.AuditLogs);
    }

    [Theory]
    [MemberData(nameof(AllStatuses))]
    public async Task Allowed_domains_are_accepted_in_every_status(string status)
    {
        foreach (var email in new[] { "moi@gmail.com", "moi@fpt.edu.vn", "  MOI@GMAIL.COM  " })
        {
            var h = WithTarget(status);

            var result = await Run(h, Command(email: email));

            Assert.True(result.EmailChanged);
            Assert.Equal(AccountIdentityRules.NormalizeEmail(email), result.Email);
            // Changing the address never changes the status — that is the invariant, in all four.
            Assert.Equal(status, result.Status);
            Assert.Equal(status, h.GetUser(TargetId).Status);
        }
    }

    /// <summary>
    /// Calls the handler directly, as a mis-wired MediatR registration would: the refusal must not
    /// depend on the FluentValidation pipeline having run first.
    /// </summary>
    [Theory]
    [MemberData(nameof(AllStatuses))]
    public async Task The_handler_refuses_on_its_own_without_the_validator_pipeline(string status)
    {
        var h = WithTarget(status);

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => Handler(h).Handle(Command(email: "moi@fe.edu.vn"), CancellationToken.None));

        Assert.Equal(AccountIdentityRules.EmailDomainNotAllowedMessage, ex.Message);
        Assert.Equal(OldEmail, h.GetUser(TargetId).Email);
    }

    [Fact]
    public void The_validator_reports_the_same_refusal_as_the_handler()
    {
        var result = new UpdateDepartmentPersonnelCommandValidator()
            .Validate(Command(email: "moi@fe.edu.vn"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(UpdateDepartmentPersonnelCommand.Email)
            && e.ErrorMessage == AccountIdentityRules.EmailDomainNotAllowedMessage);
    }

    // ── Scope + side-effect ordering ─────────────────────────────────────────

    [Fact]
    public async Task Target_in_another_department_answers_404_and_is_not_modified()
    {
        var h = DepartmentLeaderTestHarness.Create();
        h.AddOtherDepartment();
        h.AddStaff(
            TargetId,
            departmentId: DepartmentLeaderTestHarness.OtherDepartmentId,
            campusId: DepartmentLeaderTestHarness.OtherCampusId,
            email: OldEmail);

        var ex = await Assert.ThrowsAsync<AuthBusinessException>(() => Run(h, Command()));
        Assert.Equal(404, ex.StatusCode);
        Assert.Equal(OldEmail, h.GetUser(TargetId).Email);
    }

    [Fact]
    public async Task Target_and_department_rows_are_locked_before_the_write()
    {
        var h = WithTarget(UserStatuses.Active);

        await Run(h, Command());

        Assert.Contains(h.Locks.AllLockedUserIds, id => id == TargetId);
        Assert.Contains(
            h.Locks.LockedDepartmentBatches,
            batch => batch.Contains(DepartmentLeaderTestHarness.DepartmentId));
    }

    /// <summary>
    /// The identity change is already committed when the mails go out, so a delivery failure is
    /// REPORTED, not rolled back (spec §12.12).
    /// </summary>
    [Fact]
    public async Task Email_delivery_failure_does_not_roll_back_the_identity_change()
    {
        var h = WithTarget(UserStatuses.Active);
        h.MakeEmailFail();

        var result = await Run(h, Command());

        Assert.Equal(NewEmail, h.GetUser(TargetId).Email);
        Assert.True(result.Success);
        Assert.True(result.EmailChanged);
        Assert.Equal(DepartmentPersonnelEmails.StatusFailed, result.EmailNotificationStatus);
    }

    [Fact]
    public async Task Partial_delivery_is_reported_as_partial()
    {
        var h = WithTarget(UserStatuses.Active);
        // The notice to the OLD address fails; the message to the NEW one succeeds.
        h.MakeEmailFailFor(OldEmail);

        var result = await Run(h, Command());

        Assert.Equal(DepartmentPersonnelEmails.StatusPartial, result.EmailNotificationStatus);
    }

    [Fact]
    public async Task Identity_edit_is_audited_under_its_own_action_with_a_masked_email()
    {
        var h = WithTarget(UserStatuses.Active);

        await Run(h, Command());

        var audit = h.Db.AuditLogs.Single();
        Assert.Equal(DepartmentPersonnelAuditActions.UpdatePersonnelIdentity, audit.Action);

        var change = audit.Changes.Single();
        Assert.DoesNotContain(NewEmail, change.NewValueText!);
        Assert.DoesNotContain(OldEmail, change.OldValueText!);
        Assert.Contains("@fpt.edu.vn", change.NewValueText!);
    }

    [Fact]
    public async Task Pending_email_correction_uses_its_own_audit_action()
    {
        var h = WithTarget(UserStatuses.PendingEmailConfirmation);

        await Run(h, Command());

        Assert.Equal(
            DepartmentPersonnelAuditActions.CorrectPendingPersonnelEmail,
            h.Db.AuditLogs.Single().Action);
    }

    [Fact]
    public async Task Profile_only_edit_uses_the_plain_update_audit_action()
    {
        var h = WithTarget(UserStatuses.Active);

        await Run(h, Command(email: OldEmail, fullName: "Ten Khac"));

        Assert.Equal(DepartmentPersonnelAuditActions.UpdatePersonnel, h.Db.AuditLogs.Single().Action);
    }
}
