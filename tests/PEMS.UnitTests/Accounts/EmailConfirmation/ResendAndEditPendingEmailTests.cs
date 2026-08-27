using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Moq;
using PEMS.Application.Accounts.Commands.ConfirmAccountEmail;
using PEMS.Application.Accounts.Commands.EditPendingAccountEmail;
using PEMS.Application.Accounts.Commands.ResendAccountEmailConfirmation;
using PEMS.Application.Accounts.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Users;
using PEMS.UnitTests.TestInfrastructure;

namespace PEMS.UnitTests.Accounts.EmailConfirmation;

/// <summary>
/// P0 #1 resend + edit-email for pending accounts: both are admin-authorized (HO / the account's Staff
/// Leader), refuse non-pending accounts, and re-issue a fresh token (which supersedes the old one — no
/// duplicate account). Resend is rate-limited (cooldown + max). Edit validates/normalizes the new email,
/// rejects duplicates, updates the address and re-confirms.
/// </summary>
public class ResendAndEditPendingEmailTests
{
    private const ulong CampusA = 1;               // == FakeCurrentUserService default campus (Staff Leader)
    private const ulong TargetUserId = 700;
    private const string OwnerEmail = "owner@fpt.edu.vn";

    private sealed class Harness
    {
        public TestApplicationDbContext Db { get; } = TestApplicationDbContext.Create();
        public FakeCurrentUserService Actor { get; } = new();   // Staff Leader, campus 1
        public FakeDateTimeService Clock { get; } = new();
        public Mock<IAccountEmailConfirmationService> Confirmations { get; } = new();

        public FakeSystemEmailDispatcher Dispatcher { get; } = new();

        public Harness()
        {
            Confirmations.Setup(c => c.IssuePendingAsync(
                    It.IsAny<ulong>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("raw");
            Confirmations.Setup(c => c.BuildConfirmUrl(It.IsAny<string>())).Returns("http://x/confirm-email?token=raw");
            Confirmations.Setup(c => c.ExpiryHours).Returns(24);

            // Every account has a role row in production, and the edit handler loads it with the user
            // (the confirmation email names the role). Seeded here so the fixture matches.
            Db.Roles.AddRange(
                Uc106TestData.CreateRole(Uc106TestData.StudentRoleId, RoleCodes.Student),
                Uc106TestData.CreateRole(Uc106TestData.StaffRoleId, RoleCodes.Staff));
            Db.SaveChanges();
        }

        public ResendAccountEmailConfirmationCommandHandler Resend() => new(Db, Actor, Clock, Confirmations.Object, Dispatcher);
        public EditPendingAccountEmailCommandHandler Edit() => new(
            Db, Actor, Clock, Confirmations.Object, Dispatcher,
            new PendingAccountEmailChangeService(Db, Confirmations.Object));
    }

    private static User SeedUser(Harness h, string? status = null, string email = OwnerEmail, ulong campus = CampusA)
    {
        var user = Uc106TestData.CreateUser(TargetUserId, Uc106TestData.StudentRoleId, null, campus);
        user.Email = email;
        user.Status = status ?? UserStatuses.PendingEmailConfirmation;
        h.Db.Users.Add(user);
        h.Db.SaveChanges();
        return user;
    }

    /// <summary>Adds an auth provider row to the target account (the edit re-points or removes these).</summary>
    private static UserAuthProvider SeedProvider(Harness h, string providerType, string? subject = null)
    {
        var provider = new UserAuthProvider
        {
            UserId = TargetUserId,
            ProviderType = providerType,
            ProviderSubject = subject,
            ProviderEmail = OwnerEmail,
            IsEnabled = true,
            LinkedAt = h.Clock.VietnamNow,
        };
        h.Db.UserAuthProviders.Add(provider);
        h.Db.SaveChanges();
        return provider;
    }

    private static void SeedPendingConfirmation(Harness h, int resendCount, int createdSecondsAgo)
    {
        h.Db.AccountEmailConfirmations.Add(new AccountEmailConfirmation
        {
            UserId = TargetUserId,
            TargetEmail = OwnerEmail,
            TokenHash = new string('a', 64),
            Status = AccountEmailConfirmationStatuses.Pending,
            ExpiresAt = h.Clock.VietnamNow.AddDays(1),
            ResendCount = resendCount,
            CreatedAt = h.Clock.VietnamNow.AddSeconds(-createdSecondsAgo),
        });
        h.Db.SaveChanges();
    }

    // ── Resend ─────────────────────────────────────────────────────────────
    [Fact]
    public async Task Unauthorized_actor_cannot_resend()
    {
        var h = new Harness();
        SeedUser(h);
        h.Actor.RoleCode = RoleCodes.Department;   // not HO, not the campus Staff Leader
        h.Actor.SubRole = UserSubRoles.Staff;

        await Assert.ThrowsAsync<ForbiddenException>(
            () => h.Resend().Handle(new ResendAccountEmailConfirmationCommand { UserId = TargetUserId }, CancellationToken.None));
    }

    [Fact]
    public async Task Non_pending_account_cannot_resend()
    {
        var h = new Harness();
        SeedUser(h, status: UserStatuses.Active);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => h.Resend().Handle(new ResendAccountEmailConfirmationCommand { UserId = TargetUserId }, CancellationToken.None));
        Assert.Equal("ACCOUNT_NOT_PENDING", ex.ErrorCode);
    }

    [Fact]
    public async Task Resend_within_cooldown_is_rejected()
    {
        var h = new Harness();
        SeedUser(h);
        SeedPendingConfirmation(h, resendCount: 0, createdSecondsAgo: 5);   // just issued

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => h.Resend().Handle(new ResendAccountEmailConfirmationCommand { UserId = TargetUserId }, CancellationToken.None));
        Assert.Equal("RESEND_TOO_SOON", ex.ErrorCode);
        h.Confirmations.Verify(c => c.IssuePendingAsync(It.IsAny<ulong>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Resend_over_max_limit_is_rejected()
    {
        var h = new Harness();
        SeedUser(h);
        SeedPendingConfirmation(h, resendCount: 5, createdSecondsAgo: 120);   // past cooldown, at max

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => h.Resend().Handle(new ResendAccountEmailConfirmationCommand { UserId = TargetUserId }, CancellationToken.None));
        Assert.Equal("RESEND_LIMIT_REACHED", ex.ErrorCode);
    }

    [Fact]
    public async Task Resend_success_reissues_token_and_reports_truthful_status()
    {
        var h = new Harness();
        SeedUser(h);
        SeedPendingConfirmation(h, resendCount: 1, createdSecondsAgo: 120);   // past cooldown, below max

        var res = await h.Resend().Handle(new ResendAccountEmailConfirmationCommand { UserId = TargetUserId }, CancellationToken.None);

        Assert.True(res.Success);
        Assert.Equal("SENT", res.EmailNotificationStatus);
        h.Confirmations.Verify(c => c.IssuePendingAsync(TargetUserId, OwnerEmail, true, It.IsAny<CancellationToken>()), Times.Once);

        // The content is the template's, not the handler's.
        var sent = Assert.Single(h.Dispatcher.Sent);
        Assert.Equal(SystemEmailTemplates.AccountEmailConfirmation, sent.TemplateCode);
        Assert.Equal(OwnerEmail, sent.To.Email);
        Assert.Equal("24", sent.Variables["expiresInHours"]);
        // The one-time link reaches the body only as a trusted block.
        Assert.Contains("confirm-email?token=raw", sent.TrustedBlocks![EmailTrustedBlocks.ActionBlock]);
        Assert.DoesNotContain(sent.Variables.Values, v => v.Contains("token=raw"));
    }

    /// <summary>
    /// HO is not campus-bound: the same actor campus that would refuse a Staff Leader (see
    /// <see cref="Unauthorized_actor_cannot_edit_email"/>) is accepted for HO.
    /// </summary>
    [Fact]
    public async Task Ho_of_any_campus_may_resend()
    {
        var h = new Harness();
        SeedUser(h);                        // account on campus 1
        h.Actor.RoleCode = RoleCodes.Ho;
        h.Actor.SubRole = null;
        h.Actor.PrimaryCampusId = 2;        // a different campus — irrelevant for HO

        var res = await h.Resend().Handle(
            new ResendAccountEmailConfirmationCommand { UserId = TargetUserId }, CancellationToken.None);

        Assert.True(res.Success);
        Assert.Equal(1, res.ResendCount);   // no live pending row ⇒ series starts at 1
    }

    [Fact]
    public async Task Unknown_user_is_a_404()
    {
        var h = new Harness();   // nothing seeded

        await Assert.ThrowsAsync<NotFoundException>(
            () => h.Resend().Handle(new ResendAccountEmailConfirmationCommand { UserId = TargetUserId }, CancellationToken.None));
    }

    /// <summary>
    /// Every non-pending state is refused with the SAME stable code, and — the part that matters —
    /// without sending anything. A resend on an account that is already active/disabled/locked would
    /// mail a live activation link to an address the flow no longer vouches for.
    /// </summary>
    [Theory]
    [InlineData(UserStatuses.Active)]
    [InlineData(UserStatuses.Inactive)]
    [InlineData(UserStatuses.Locked)]
    public async Task Non_pending_statuses_are_refused_and_send_nothing(string status)
    {
        var h = new Harness();
        SeedUser(h, status: status);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => h.Resend().Handle(new ResendAccountEmailConfirmationCommand { UserId = TargetUserId }, CancellationToken.None));

        Assert.Equal("ACCOUNT_NOT_PENDING", ex.ErrorCode);
        Assert.Empty(h.Dispatcher.Sent);
        h.Confirmations.Verify(c => c.IssuePendingAsync(
            It.IsAny<ulong>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// A resend re-mails a link; it provisions nothing. The account keeps its identity and its
    /// pending status — only the holder clicking the new link may change that.
    /// </summary>
    [Fact]
    public async Task Resend_changes_nothing_about_the_account()
    {
        var h = new Harness();
        var before = SeedUser(h);
        var (roleId, subRole, campus, department) = (before.RoleId, before.SubRole, before.PrimaryCampusId, before.DepartmentId);
        SeedPendingConfirmation(h, resendCount: 0, createdSecondsAgo: 120);

        await h.Resend().Handle(new ResendAccountEmailConfirmationCommand { UserId = TargetUserId }, CancellationToken.None);

        var after = await h.Db.Users.SingleAsync();          // Single ⇒ no second account was created
        Assert.Equal(TargetUserId, after.UserId);
        Assert.Equal(UserStatuses.PendingEmailConfirmation, after.Status);   // never auto-activated
        Assert.Equal(OwnerEmail, after.Email);
        Assert.Equal(roleId, after.RoleId);
        Assert.Equal(subRole, after.SubRole);
        Assert.Equal(campus, after.PrimaryCampusId);
        Assert.Equal(department, after.DepartmentId);
    }

    /// <summary>The counter continues the pending row's series rather than restarting at 1.</summary>
    [Fact]
    public async Task Resend_count_continues_from_the_live_pending_row()
    {
        var h = new Harness();
        SeedUser(h);
        SeedPendingConfirmation(h, resendCount: 3, createdSecondsAgo: 120);

        var res = await h.Resend().Handle(
            new ResendAccountEmailConfirmationCommand { UserId = TargetUserId }, CancellationToken.None);

        Assert.Equal(4, res.ResendCount);
    }

    /// <summary>
    /// Delivery is reported as it happened. A caller that treated Success as "the mail arrived"
    /// would tell HO the holder has a link when SMTP was off or the send failed outright.
    /// </summary>
    [Theory]
    [InlineData("SKIPPED")]
    [InlineData("FAILED")]
    public async Task Failed_or_skipped_delivery_is_reported_truthfully(string notificationStatus)
    {
        var h = new Harness();
        SeedUser(h);
        SeedPendingConfirmation(h, resendCount: 0, createdSecondsAgo: 120);
        h.Dispatcher.Outcome = notificationStatus == "SKIPPED"
            ? EmailDeliveryResult.Skipped("SMTP_DISABLED", "SMTP disabled")
            : EmailDeliveryResult.Failed("SMTP_SEND_FAILED", "SMTP down");

        var res = await h.Resend().Handle(
            new ResendAccountEmailConfirmationCommand { UserId = TargetUserId }, CancellationToken.None);

        Assert.Equal(notificationStatus, res.EmailNotificationStatus);
        // A fresh token was still issued, so the account stays actionable via a later resend.
        Assert.Equal(UserStatuses.PendingEmailConfirmation,
            (await h.Db.Users.SingleAsync()).Status);
    }

    // ── Edit email ─────────────────────────────────────────────────────────
    [Fact]
    public async Task Unauthorized_actor_cannot_edit_email()
    {
        var h = new Harness();
        SeedUser(h);
        h.Actor.PrimaryCampusId = 2;   // Staff Leader of a different campus

        await Assert.ThrowsAsync<ForbiddenException>(
            () => h.Edit().Handle(new EditPendingAccountEmailCommand { UserId = TargetUserId, NewEmail = "new@fpt.edu.vn" }, CancellationToken.None));
    }

    [Fact]
    public async Task Edit_non_pending_account_is_rejected()
    {
        var h = new Harness();
        SeedUser(h, status: UserStatuses.Active);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => h.Edit().Handle(new EditPendingAccountEmailCommand { UserId = TargetUserId, NewEmail = "new@fpt.edu.vn" }, CancellationToken.None));
        Assert.Equal("ACCOUNT_NOT_PENDING", ex.ErrorCode);
    }

    [Fact]
    public async Task Edit_invalid_email_is_rejected()
    {
        var h = new Harness();
        SeedUser(h);

        await Assert.ThrowsAsync<ValidationException>(
            () => h.Edit().Handle(new EditPendingAccountEmailCommand { UserId = TargetUserId, NewEmail = "not-an-email" }, CancellationToken.None));
    }

    [Fact]
    public async Task Edit_duplicate_email_conflicts()
    {
        var h = new Harness();
        SeedUser(h);
        var other = Uc106TestData.CreateUser(701, Uc106TestData.StudentRoleId, null, CampusA);
        other.Email = "taken@fpt.edu.vn";
        h.Db.Users.Add(other);
        h.Db.SaveChanges();

        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => h.Edit().Handle(new EditPendingAccountEmailCommand { UserId = TargetUserId, NewEmail = "taken@fpt.edu.vn" }, CancellationToken.None));
        Assert.Equal(AccountErrorCodes.EmailAlreadyExists, ex.ErrorCode);
    }

    [Fact]
    public async Task Edit_success_updates_email_and_reissues_confirmation_for_new_address()
    {
        var h = new Harness();
        SeedUser(h);

        var res = await h.Edit().Handle(
            new EditPendingAccountEmailCommand { UserId = TargetUserId, NewEmail = "  New.Owner@FPT.EDU.VN " }, CancellationToken.None);

        Assert.True(res.Success);
        Assert.Equal("new.owner@fpt.edu.vn", res.Email);   // normalized
        Assert.Equal("SENT", res.EmailNotificationStatus);
        var user = await h.Db.Users.SingleAsync(u => u.UserId == TargetUserId);
        Assert.Equal("new.owner@fpt.edu.vn", user.Email);
        // A fresh token bound to the NEW email is issued (the old one is superseded / no longer matches).
        h.Confirmations.Verify(c => c.IssuePendingAsync(TargetUserId, "new.owner@fpt.edu.vn", false, It.IsAny<CancellationToken>()), Times.Once);

        Assert.Equal(2, h.Dispatcher.Sent.Count);

        var confirmation = h.Dispatcher.Sent[0];
        Assert.Equal(SystemEmailTemplates.AccountEmailConfirmation, confirmation.TemplateCode);
        Assert.Equal("new.owner@fpt.edu.vn", confirmation.To.Email);   // the link goes to the NEW address only

        // The notice to the address being unlinked says nothing about whose account it was: no variables,
        // and no display name in the To header either. That address may belong to an uninvolved stranger.
        var notice = h.Dispatcher.Sent[1];
        Assert.Equal(SystemEmailTemplates.AccountPendingEmailChangedOldNotice, notice.TemplateCode);
        Assert.Equal(OwnerEmail, notice.To.Email);
        Assert.Null(notice.To.DisplayName);
        Assert.Empty(notice.Variables);
        Assert.Null(notice.TrustedBlocks);
    }

    /// <summary>
    /// Correcting the address does NOT activate anything. The account has still never proven it owns
    /// an address — only the holder clicking the new link may change that.
    /// </summary>
    [Fact]
    public async Task Edit_leaves_the_account_pending_and_creates_no_second_account()
    {
        var h = new Harness();
        SeedUser(h);

        await h.Edit().Handle(
            new EditPendingAccountEmailCommand { UserId = TargetUserId, NewEmail = "new@fpt.edu.vn" },
            CancellationToken.None);

        var user = await h.Db.Users.SingleAsync();   // Single ⇒ no second account was provisioned
        Assert.Equal(UserStatuses.PendingEmailConfirmation, user.Status);
        Assert.Equal("new@fpt.edu.vn", user.Email);
    }

    /// <summary>
    /// Name and address commit together. Two separate calls could half-succeed, leaving the account
    /// renamed at an address that was never changed — and the confirmation email must carry the NEW
    /// name, since that is the person it addresses.
    /// </summary>
    [Fact]
    public async Task Edit_updates_the_full_name_in_the_same_request()
    {
        var h = new Harness();
        SeedUser(h);

        await h.Edit().Handle(new EditPendingAccountEmailCommand
        {
            UserId = TargetUserId,
            NewEmail = "new@fpt.edu.vn",
            FullName = "  Nguyễn   Văn A ",
        }, CancellationToken.None);

        var user = await h.Db.Users.SingleAsync();
        Assert.Equal("Nguyễn Văn A", user.FullName);   // normalized
        Assert.Equal("new@fpt.edu.vn", user.Email);

        var confirmation = h.Dispatcher.Single(SystemEmailTemplates.AccountEmailConfirmation);
        Assert.Equal("Nguyễn Văn A", confirmation.To.DisplayName);
        Assert.Equal("Nguyễn Văn A", confirmation.Variables["fullName"]);
    }

    /// <summary>Omitting the name leaves it alone rather than blanking it.</summary>
    [Fact]
    public async Task Edit_without_a_full_name_keeps_the_current_one()
    {
        var h = new Harness();
        var seeded = SeedUser(h);
        var originalName = seeded.FullName;

        await h.Edit().Handle(
            new EditPendingAccountEmailCommand { UserId = TargetUserId, NewEmail = "new@fpt.edu.vn" },
            CancellationToken.None);

        Assert.Equal(originalName, (await h.Db.Users.SingleAsync()).FullName);
    }

    /// <summary>
    /// A name sent straight to the API is held to the same shared rules as the modal — and rejecting
    /// it must abandon the whole request, address included.
    /// </summary>
    [Fact]
    public async Task Edit_with_an_invalid_full_name_is_rejected_and_changes_nothing()
    {
        var h = new Harness();
        SeedUser(h);

        await Assert.ThrowsAsync<ValidationException>(() => h.Edit().Handle(new EditPendingAccountEmailCommand
        {
            UserId = TargetUserId,
            NewEmail = "new@fpt.edu.vn",
            FullName = "Tr4n Th1 B <script>",
        }, CancellationToken.None));

        Assert.Equal(OwnerEmail, (await h.Db.Users.SingleAsync()).Email);
        Assert.Empty(h.Dispatcher.Sent);
        h.Confirmations.Verify(c => c.IssuePendingAsync(
            It.IsAny<ulong>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Local-password logins stay linked; only the address they point at moves.</summary>
    [Fact]
    public async Task Edit_repoints_the_local_password_provider_at_the_new_address()
    {
        var h = new Harness();
        SeedUser(h);
        SeedProvider(h, ProviderTypes.LocalPassword);

        await h.Edit().Handle(
            new EditPendingAccountEmailCommand { UserId = TargetUserId, NewEmail = "new@fpt.edu.vn" },
            CancellationToken.None);

        var provider = await h.Db.UserAuthProviders.SingleAsync();
        Assert.Equal(ProviderTypes.LocalPassword, provider.ProviderType);
        Assert.Equal("new@fpt.edu.vn", provider.ProviderEmail);
    }

    /// <summary>
    /// The SSO/FEID rows are DELETED, not blanked: provider_subject identifies the OLD external
    /// identity, proven against an address this account no longer has. The login flow re-links on the
    /// next sign-in, so the account keeps working — bound to the identity that matches its new email.
    /// </summary>
    [Theory]
    [InlineData(ProviderTypes.GoogleSso)]
    [InlineData(ProviderTypes.FeId)]
    public async Task Edit_unlinks_the_external_identity_provider(string providerType)
    {
        var h = new Harness();
        SeedUser(h);
        SeedProvider(h, providerType, subject: "external-subject-1");
        SeedProvider(h, ProviderTypes.LocalPassword);

        await h.Edit().Handle(
            new EditPendingAccountEmailCommand { UserId = TargetUserId, NewEmail = "new@fpt.edu.vn" },
            CancellationToken.None);

        var providers = await h.Db.UserAuthProviders.ToListAsync();
        Assert.Equal(ProviderTypes.LocalPassword, Assert.Single(providers).ProviderType);
    }

    /// <summary>A pending account has verified nothing; after the address moves that is doubly true.</summary>
    [Fact]
    public async Task Edit_clears_email_verification()
    {
        var h = new Harness();
        var user = SeedUser(h);
        user.EmailVerifiedAt = h.Clock.VietnamNow;
        h.Db.SaveChanges();

        await h.Edit().Handle(
            new EditPendingAccountEmailCommand { UserId = TargetUserId, NewEmail = "new@fpt.edu.vn" },
            CancellationToken.None);

        Assert.Null((await h.Db.Users.SingleAsync()).EmailVerifiedAt);
    }

    /// <summary>Re-saving the same address is refused rather than pointlessly re-issuing a token.</summary>
    [Fact]
    public async Task Edit_to_the_same_email_is_rejected()
    {
        var h = new Harness();
        SeedUser(h);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => h.Edit().Handle(
            new EditPendingAccountEmailCommand { UserId = TargetUserId, NewEmail = "  OWNER@FPT.EDU.VN " },
            CancellationToken.None));

        Assert.Equal("EMAIL_UNCHANGED", ex.ErrorCode);
        Assert.Empty(h.Dispatcher.Sent);
        h.Confirmations.Verify(c => c.IssuePendingAsync(
            It.IsAny<ulong>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Every non-pending state is refused with the SAME stable code and sends nothing. Re-targeting a
    /// provisioned account's address here would bypass the re-verification the ordinary flow performs.
    /// </summary>
    [Theory]
    [InlineData(UserStatuses.Active)]
    [InlineData(UserStatuses.Inactive)]
    [InlineData(UserStatuses.Locked)]
    public async Task Edit_on_a_non_pending_status_is_refused_and_sends_nothing(string status)
    {
        var h = new Harness();
        SeedUser(h, status: status);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => h.Edit().Handle(
            new EditPendingAccountEmailCommand { UserId = TargetUserId, NewEmail = "new@fpt.edu.vn" },
            CancellationToken.None));

        Assert.Equal("ACCOUNT_NOT_PENDING", ex.ErrorCode);
        Assert.Equal(OwnerEmail, (await h.Db.Users.SingleAsync()).Email);
        Assert.Empty(h.Dispatcher.Sent);
    }

    [Fact]
    public async Task Edit_of_an_unknown_user_is_a_404()
    {
        var h = new Harness();   // nothing seeded

        await Assert.ThrowsAsync<NotFoundException>(() => h.Edit().Handle(
            new EditPendingAccountEmailCommand { UserId = TargetUserId, NewEmail = "new@fpt.edu.vn" },
            CancellationToken.None));
    }

    /// <summary>
    /// The mail to the NEW address is the create flow's activation email, not a change notice: same
    /// template, same variables, and a one-time link that reaches the body only as a trusted block.
    /// A notice without a button would leave this account with no way to ever go live.
    /// </summary>
    [Fact]
    public async Task Edit_sends_the_create_flow_confirmation_email_to_the_new_address()
    {
        var h = new Harness();
        SeedUser(h);
        h.Db.Campuses.Add(Uc106TestData.CreateCampus(CampusA));
        h.Db.SaveChanges();

        await h.Edit().Handle(
            new EditPendingAccountEmailCommand { UserId = TargetUserId, NewEmail = "new@fpt.edu.vn" },
            CancellationToken.None);

        var confirmation = h.Dispatcher.Single(SystemEmailTemplates.AccountEmailConfirmation);
        Assert.Equal("new@fpt.edu.vn", confirmation.To.Email);
        Assert.NotEqual(SystemEmailTemplates.AccountEmailChangedNewNotice, confirmation.TemplateCode);

        // Same variable set the create flow supplies — role, campus and the link's lifetime.
        Assert.Equal(
            AccountRoleDisplayNames.Resolve(RoleCodes.Student, null), confirmation.Variables["roleName"]);
        Assert.Equal($"Campus {CampusA}", confirmation.Variables["campusName"]);
        Assert.Equal("24", confirmation.Variables["expiresInHours"]);

        // The activation button, carrying the NEW token.
        Assert.Contains("confirm-email?token=raw", confirmation.TrustedBlocks![EmailTrustedBlocks.ActionBlock]);
        Assert.DoesNotContain(confirmation.Variables.Values, v => v.Contains("token=raw"));
    }

    /// <summary>
    /// The change-notice template belongs to a provisioned account changing its login address. Sending
    /// it here would tell the new holder their address changed and give them nothing to click.
    /// </summary>
    [Fact]
    public async Task Edit_never_sends_the_account_email_changed_notice()
    {
        var h = new Harness();
        SeedUser(h);

        await h.Edit().Handle(
            new EditPendingAccountEmailCommand { UserId = TargetUserId, NewEmail = "new@fpt.edu.vn" },
            CancellationToken.None);

        Assert.Empty(h.Dispatcher.All(SystemEmailTemplates.AccountEmailChangedNewNotice));
    }

    /// <summary>
    /// The reported status describes the CONFIRMATION email — the one that decides whether this
    /// account can be activated — never the neutral notice to the old address.
    /// </summary>
    [Theory]
    [InlineData("SKIPPED")]
    [InlineData("FAILED")]
    public async Task Edit_reports_the_confirmation_delivery_truthfully(string notificationStatus)
    {
        var h = new Harness();
        SeedUser(h);
        h.Dispatcher.OutcomeFor = req =>
            req.TemplateCode == SystemEmailTemplates.AccountEmailConfirmation
                ? (notificationStatus == "SKIPPED"
                    ? EmailDeliveryResult.Skipped("SMTP_DISABLED", "SMTP disabled")
                    : EmailDeliveryResult.Failed("SMTP_SEND_FAILED", "SMTP down"))
                : EmailDeliveryResult.Sent();   // the old-address notice went out fine

        var res = await h.Edit().Handle(
            new EditPendingAccountEmailCommand { UserId = TargetUserId, NewEmail = "new@fpt.edu.vn" },
            CancellationToken.None);

        Assert.Equal(notificationStatus, res.EmailNotificationStatus);
    }

    /// <summary>
    /// Delivery is not a transaction participant. The address is corrected and a live token exists, so
    /// the way forward is a resend — rolling the account back would throw that away and leave the
    /// holder at the wrong address.
    /// </summary>
    [Fact]
    public async Task Edit_survives_an_email_provider_failure_without_rolling_back()
    {
        var h = new Harness();
        SeedUser(h);
        h.Dispatcher.ThrowOnSend = new InvalidOperationException("SMTP exploded");

        var res = await h.Edit().Handle(
            new EditPendingAccountEmailCommand { UserId = TargetUserId, NewEmail = "new@fpt.edu.vn" },
            CancellationToken.None);

        Assert.True(res.Success);
        Assert.Equal("FAILED", res.EmailNotificationStatus);
        var user = await h.Db.Users.SingleAsync();
        Assert.Equal("new@fpt.edu.vn", user.Email);
        Assert.Equal(UserStatuses.PendingEmailConfirmation, user.Status);
        h.Confirmations.Verify(c => c.IssuePendingAsync(
            TargetUserId, "new@fpt.edu.vn", false, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// A failure to notify the OLD address is not allowed to shadow the mail that matters. The status
    /// stays SENT because the activation link did reach the new holder.
    /// </summary>
    [Fact]
    public async Task A_failed_notice_to_the_old_address_does_not_affect_the_reported_status()
    {
        var h = new Harness();
        SeedUser(h);
        h.Dispatcher.OutcomeFor = req =>
            req.TemplateCode == SystemEmailTemplates.AccountPendingEmailChangedOldNotice
                ? EmailDeliveryResult.Failed("SMTP_SEND_FAILED", "SMTP down")
                : EmailDeliveryResult.Sent();

        var res = await h.Edit().Handle(
            new EditPendingAccountEmailCommand { UserId = TargetUserId, NewEmail = "new@fpt.edu.vn" },
            CancellationToken.None);

        Assert.Equal("SENT", res.EmailNotificationStatus);
    }

    /// <summary>
    /// An audit row is read by more people than may activate the account, so it records addresses and
    /// names — never the token or the confirmation URL, either of which would BE the activation.
    /// </summary>
    [Fact]
    public async Task Edit_audits_the_change_without_recording_the_token_or_the_link()
    {
        var h = new Harness();
        SeedUser(h);

        await h.Edit().Handle(new EditPendingAccountEmailCommand
        {
            UserId = TargetUserId,
            NewEmail = "new@fpt.edu.vn",
            FullName = "Nguyễn Văn A",
        }, CancellationToken.None);

        var audit = await h.Db.AuditLogs.Include(a => a.Changes).SingleAsync();
        Assert.Equal("EDIT_PENDING_ACCOUNT_EMAIL", audit.Action);
        Assert.Equal("User", audit.EntityType);
        Assert.Equal(TargetUserId, audit.EntityId);

        var change = Assert.Single(audit.Changes);
        // Parsed rather than string-matched: JSON escapes non-ASCII, so a substring check on a
        // Vietnamese name would fail for reasons that have nothing to do with what was recorded.
        using var before = JsonDocument.Parse(change.OldValueText!);
        using var after = JsonDocument.Parse(change.NewValueText!);
        Assert.Equal(OwnerEmail, before.RootElement.GetProperty("email").GetString());
        Assert.Equal("User 700", before.RootElement.GetProperty("fullName").GetString());
        Assert.Equal("new@fpt.edu.vn", after.RootElement.GetProperty("email").GetString());
        Assert.Equal("Nguyễn Văn A", after.RootElement.GetProperty("fullName").GetString());
        Assert.True(after.RootElement.GetProperty("oldConfirmationSuperseded").GetBoolean());
        Assert.True(after.RootElement.GetProperty("newConfirmationIssued").GetBoolean());

        var recorded = $"{change.OldValueText}{change.NewValueText}";
        Assert.DoesNotContain("raw", recorded);                    // the raw token
        Assert.DoesNotContain("confirm-email?token", recorded);    // the confirmation URL
        Assert.DoesNotContain("http", recorded);
    }
}

/// <summary>
/// The token half of the edit, exercised end to end: what HO does to a pending account's address must
/// leave EXACTLY one link that works, and it must be the one mailed to the new address.
///
/// <para>
/// These drive the real <see cref="ConfirmAccountEmailCommandHandler"/> against a confirmation service
/// that behaves like the production one (supersede every live row, store only a SHA-256 hash) rather
/// than a mock that records calls — the questions here are about what the two handlers do to the rows
/// TOGETHER, which a verify-was-called assertion cannot answer.
/// </para>
/// </summary>
public class PendingEmailEditTokenLifecycleTests
{
    private const ulong CampusA = 1;
    private const ulong TargetUserId = 700;
    private const string OwnerEmail = "owner@fpt.edu.vn";
    private const string NewEmail = "new.owner@fpt.edu.vn";

    /// <summary>SHA-256 hex, the same scheme the production token service uses.</summary>
    private sealed class Sha256TokenService : IEmailActionTokenService
    {
        private int _counter;
        public string GenerateRawToken() => $"raw-token-{++_counter}";
        public string Hash(string rawToken)
            => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken))).ToLowerInvariant();
        public string BuildPublicActionUrl(string rawToken) => $"http://x/action?token={rawToken}";
        public string BuildVisitParticipantAssignmentUrl(ulong participantId) => "http://x/assign";
        public string BuildDepartmentStaffLogisticsTaskUrl(ulong logisticsItemId) => "http://x/logistics-staff";
        public string BuildDepartmentLeaderLogisticsTaskUrl(ulong logisticsItemId) => "http://x/logistics-leader";
        public string BuildHostVisitProcessUrl(ulong visitInstanceId) => "http://x/visit";
        public string BuildVisitContributionUrl(ulong visitInstanceId) => "http://x/contribution";
    }

    /// <summary>
    /// Mirrors the production <c>AccountEmailConfirmationService</c>: supersede every live PENDING row
    /// for the user, then insert one new PENDING row holding only the token's hash.
    /// </summary>
    private sealed class IssuingConfirmationService : IAccountEmailConfirmationService
    {
        private readonly IApplicationDbContext _db;
        private readonly IEmailActionTokenService _tokens;
        private readonly IDateTimeService _clock;

        public IssuingConfirmationService(
            IApplicationDbContext db, IEmailActionTokenService tokens, IDateTimeService clock)
        {
            _db = db;
            _tokens = tokens;
            _clock = clock;
        }

        public int ExpiryHours => 24;

        public async Task<string> IssuePendingAsync(
            ulong userId, string normalizedTargetEmail, bool isResend, CancellationToken cancellationToken)
        {
            var now = _clock.VietnamNow;
            var live = await _db.AccountEmailConfirmations
                .Where(c => c.UserId == userId && c.Status == AccountEmailConfirmationStatuses.Pending)
                .ToListAsync(cancellationToken);

            var priorResend = 0;
            foreach (var row in live)
            {
                row.Status = AccountEmailConfirmationStatuses.Superseded;
                row.UpdatedAt = now;
                priorResend = Math.Max(priorResend, row.ResendCount);
            }

            var raw = _tokens.GenerateRawToken();
            _db.AccountEmailConfirmations.Add(new AccountEmailConfirmation
            {
                UserId = userId,
                TargetEmail = normalizedTargetEmail,
                TokenHash = _tokens.Hash(raw),
                Status = AccountEmailConfirmationStatuses.Pending,
                ExpiresAt = now.AddHours(ExpiryHours),
                ResendCount = isResend ? priorResend + 1 : 0,
                CreatedAt = now,
            });
            return raw;
        }

        public string BuildConfirmUrl(string rawToken) => $"http://x/confirm-email?token={rawToken}";
        public string BuildLoginUrl() => "http://x/login";
    }

    private sealed class Harness
    {
        public TestApplicationDbContext Db { get; } = TestApplicationDbContext.Create();
        public FakeCurrentUserService Actor { get; } = new() { RoleCode = RoleCodes.Ho, SubRole = null };
        public FakeDateTimeService Clock { get; } = new();
        public FakeSystemEmailDispatcher Dispatcher { get; } = new();
        public Sha256TokenService Tokens { get; } = new();
        public IAccountEmailConfirmationService Confirmations { get; }

        public Harness()
        {
            Confirmations = new IssuingConfirmationService(Db, Tokens, Clock);
            Db.Roles.Add(Uc106TestData.CreateRole(Uc106TestData.StudentRoleId, RoleCodes.Student));
            Db.SaveChanges();
        }

        public EditPendingAccountEmailCommandHandler Edit()
            => new(Db, Actor, Clock, Confirmations, Dispatcher,
                new PendingAccountEmailChangeService(Db, Confirmations));

        public ConfirmAccountEmailCommandHandler Confirm()
            => new(Db, Tokens, Clock, Dispatcher, Confirmations);

        /// <summary>The raw token from the activation link in the last confirmation email sent.</summary>
        public string LastIssuedToken()
        {
            var block = Dispatcher.All(SystemEmailTemplates.AccountEmailConfirmation)
                .Last().TrustedBlocks![EmailTrustedBlocks.ActionBlock];
            var marker = "confirm-email?token=";
            var start = block.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
            var end = block.IndexOfAny(new[] { '"', '\'', '<', ' ' }, start);
            return end < 0 ? block[start..] : block[start..end];
        }
    }

    /// <summary>Seeds a pending account that already holds a live token for its original address.</summary>
    private static async Task<string> SeedPendingAccountWithLiveTokenAsync(Harness h)
    {
        var user = Uc106TestData.CreateUser(TargetUserId, Uc106TestData.StudentRoleId, null, CampusA);
        user.Email = OwnerEmail;
        user.Status = UserStatuses.PendingEmailConfirmation;
        h.Db.Users.Add(user);
        await h.Db.SaveChangesAsync();

        var original = await h.Confirmations.IssuePendingAsync(
            TargetUserId, OwnerEmail, isResend: false, CancellationToken.None);
        await h.Db.SaveChangesAsync();
        return original;
    }

    private static Task<EditPendingAccountEmailResponse> EditAsync(Harness h)
        => h.Edit().Handle(
            new EditPendingAccountEmailCommand { UserId = TargetUserId, NewEmail = NewEmail },
            CancellationToken.None);

    [Fact]
    public async Task The_previous_token_is_superseded_and_only_the_new_one_stays_pending()
    {
        var h = new Harness();
        var originalToken = await SeedPendingAccountWithLiveTokenAsync(h);

        await EditAsync(h);

        var rows = await h.Db.AccountEmailConfirmations.OrderBy(c => c.ConfirmationId).ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.Equal(AccountEmailConfirmationStatuses.Superseded, rows[0].Status);
        Assert.Equal(OwnerEmail, rows[0].TargetEmail);

        // Exactly one live token, bound to the NEW address.
        var live = Assert.Single(rows, r => r.Status == AccountEmailConfirmationStatuses.Pending);
        Assert.Equal(NewEmail, live.TargetEmail);
        Assert.Equal(h.Tokens.Hash(h.LastIssuedToken()), live.TokenHash);
        Assert.NotEqual(h.Tokens.Hash(originalToken), live.TokenHash);
    }

    /// <summary>Only the hash is persisted — the row cannot be replayed into a working link.</summary>
    [Fact]
    public async Task Only_the_token_hash_is_stored()
    {
        var h = new Harness();
        await SeedPendingAccountWithLiveTokenAsync(h);

        await EditAsync(h);

        var issued = h.LastIssuedToken();
        var rows = await h.Db.AccountEmailConfirmations.ToListAsync();
        Assert.DoesNotContain(rows, r => r.TokenHash == issued);
        Assert.All(rows, r => Assert.Equal(64, r.TokenHash.Length));   // SHA-256 hex
        Assert.Contains(rows, r => r.TokenHash == h.Tokens.Hash(issued));
    }

    /// <summary>
    /// The whole point of the change: the link that was mailed to the mistyped address is dead, so
    /// whoever holds it cannot activate an account that is not theirs.
    /// </summary>
    [Fact]
    public async Task The_old_link_can_no_longer_activate_the_account()
    {
        var h = new Harness();
        var originalToken = await SeedPendingAccountWithLiveTokenAsync(h);

        await EditAsync(h);
        var result = await h.Confirm().Handle(
            new ConfirmAccountEmailCommand { Token = originalToken }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ConfirmAccountEmailStatuses.Invalid, result.Status);
        Assert.Equal(UserStatuses.PendingEmailConfirmation, (await h.Db.Users.SingleAsync()).Status);
    }

    /// <summary>And the link mailed to the corrected address does activate it.</summary>
    [Fact]
    public async Task The_new_link_activates_the_account()
    {
        var h = new Harness();
        await SeedPendingAccountWithLiveTokenAsync(h);

        await EditAsync(h);
        var result = await h.Confirm().Handle(
            new ConfirmAccountEmailCommand { Token = h.LastIssuedToken() }, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(ConfirmAccountEmailStatuses.Confirmed, result.Status);
        var user = await h.Db.Users.SingleAsync();
        Assert.Equal(UserStatuses.Active, user.Status);
        Assert.Equal(NewEmail, user.Email);
    }

    /// <summary>
    /// Correcting an address is not a resend attempt: the new address starts its own series, so the
    /// holder is not handed a counter that is already close to the cap.
    /// </summary>
    [Fact]
    public async Task The_new_token_starts_a_fresh_resend_series()
    {
        var h = new Harness();
        await SeedPendingAccountWithLiveTokenAsync(h);
        var live = await h.Db.AccountEmailConfirmations.SingleAsync();
        live.ResendCount = 4;
        await h.Db.SaveChangesAsync();

        await EditAsync(h);

        var current = await h.Db.AccountEmailConfirmations
            .SingleAsync(c => c.Status == AccountEmailConfirmationStatuses.Pending);
        Assert.Equal(0, current.ResendCount);
    }
}
