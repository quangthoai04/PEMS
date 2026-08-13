using Microsoft.EntityFrameworkCore;
using Moq;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Options;
using PEMS.Application.Delegations.Commands.OperationalContact;
using PEMS.Application.Delegations.Common;
using PEMS.Application.Delegations.Services;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Entities.Users;
using PEMS.UnitTests.TestInfrastructure;

namespace PEMS.UnitTests.Delegations.OperationalContact;

/// <summary>
/// The operational contact is EXTERNAL, and no internal FPTU account may hold it.
///
/// <para>
/// The role exists so somebody outside FPTU confirms in their own name that they are bringing this
/// delegation — that confirmation is the whole content of the contact gate. An internal account
/// answering it confirms nothing: the university would be assuring itself a guest is coming, and the
/// campus's Staff Leader would then be reviewing a request whose only "external" party was a colleague.
/// </para>
/// <para>
/// The suite is written around the ways in, because the rule is only worth as much as its weakest door:
/// naming the address on a form, replacing a campus's contact, proposing a handover, re-inviting an
/// address that was accepted long ago, and answering an invitation with an internal account. Being the
/// registrant, owning the matching mailbox, leading the campus or POSTing straight at the API are not
/// exceptions to any of them.
/// </para>
/// </summary>
public class OperationalContactMustBeExternalTests
{
    private static readonly DateTime Now = new(2026, 8, 13, 10, 0, 0);

    private const ulong RegistrantId = 500;
    private const ulong RequestId = DelegationsTestData.VisitRequestId;
    private const ulong InstanceId = DelegationsTestData.VisitInstanceId;

    private const string InternalEmail = "staff.leader.hn@fpt.edu.vn";
    private const string GuestEmail = "guest@partner.example";

    // ── Fixtures ────────────────────────────────────────────────────────────────────────────────

    private static async Task<OperationalContactTestDbContext> WithAccountAsync(
        string email, string roleCode, string? subRole = null, string status = UserStatuses.Active)
    {
        var db = OperationalContactTestDbContext.Create();
        db.Roles.Add(new Role { RoleId = 90, RoleCode = roleCode, Name = roleCode });
        db.Users.Add(new User
        {
            UserId = 900,
            FullName = "Account under test",
            Email = email,
            RoleId = 90,
            SubRole = subRole,
            Status = status,
            CreatedAt = Now,
        });
        await db.SaveChangesAsync(CancellationToken.None);
        db.ChangeTracker.Clear();
        return db;
    }

    /// <summary>Every internal shape the rule names, one case each — including both STAFF/DEPARTMENT sub-roles.</summary>
    public static TheoryData<string, string?> InternalAccounts => new()
    {
        { RoleCodes.Admin, null },
        { RoleCodes.Ho, null },
        { RoleCodes.Staff, UserSubRoles.Leader },
        { RoleCodes.Staff, UserSubRoles.Staff },
        { RoleCodes.Department, UserSubRoles.Leader },
        { RoleCodes.Department, UserSubRoles.Staff },
        { RoleCodes.Student, null },
    };

    // ── 1. The address, wherever it is named ────────────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(InternalAccounts))]
    public async Task An_address_owned_by_an_internal_account_may_not_be_named_as_the_contact(
        string roleCode, string? subRole)
    {
        var db = await WithAccountAsync(InternalEmail, roleCode, subRole);

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            OperationalContactEligibility.EnsureEmailMayHoldContactRoleAsync(
                db, InternalEmail, CancellationToken.None));

        Assert.Equal(VisitRequestErrorCodes.ContactEmailCannotBeUsedForVisitorAccount, ex.ErrorCode);
        // ONE shared sentence for every internal role, which is how the refusal avoids telling the
        // reader what kind of account owns the address — otherwise the contact field would answer
        // questions about the staff directory.
        Assert.Equal(VisitRequestErrorMessages.ContactEmailNotEligible, ex.Message);
    }

    [Fact]
    public async Task A_visitor_address_may_be_named_as_the_contact()
    {
        var db = await WithAccountAsync(GuestEmail, RoleCodes.Visitor);

        await OperationalContactEligibility.EnsureEmailMayHoldContactRoleAsync(
            db, GuestEmail, CancellationToken.None);
    }

    /// <summary>
    /// No account at all is the ordinary case, not a violation: nobody is being repurposed, and the
    /// invitation provisions the same VISITOR account the public form would create. The check asks "is
    /// there an internal account here", never "does an account exist".
    /// </summary>
    [Fact]
    public async Task An_address_with_no_account_is_accepted()
    {
        var db = await WithAccountAsync(InternalEmail, RoleCodes.Staff, UserSubRoles.Leader);

        await OperationalContactEligibility.EnsureEmailMayHoldContactRoleAsync(
            db, "nobody@partner.example", CancellationToken.None);
    }

    /// <summary>
    /// A deactivated VISITOR is a DIFFERENT answer from an internal one, and the codes stay apart: the
    /// address is the right kind of account and the way forward is support, not another address.
    /// </summary>
    [Fact]
    public async Task A_deactivated_visitor_address_is_refused_under_its_own_code()
    {
        var db = await WithAccountAsync(GuestEmail, RoleCodes.Visitor, status: "INACTIVE");

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            OperationalContactEligibility.EnsureEmailMayHoldContactRoleAsync(
                db, GuestEmail, CancellationToken.None));

        Assert.Equal(VisitRequestErrorCodes.VisitorAccountInactive, ex.ErrorCode);
    }

    /// <summary>
    /// Case and surrounding whitespace are not a way past the check — the stored address is normalized,
    /// and so is the one being tested.
    /// </summary>
    [Fact]
    public async Task Casing_and_padding_do_not_get_an_internal_address_through()
    {
        var db = await WithAccountAsync(InternalEmail, RoleCodes.Staff, UserSubRoles.Leader);

        await Assert.ThrowsAsync<ConflictException>(() =>
            OperationalContactEligibility.EnsureEmailMayHoldContactRoleAsync(
                db, "  Staff.Leader.HN@FPT.edu.VN  ", CancellationToken.None));
    }

    // ── 2. The account, at the moment it answers ────────────────────────────────────────────────

    /// <summary>
    /// Not redundant with the address check: an address can be clean when the invitation is written and
    /// belong to an internal account by the time the link is clicked — a guest who has since joined
    /// FPTU, an account whose role was reassigned. This is the last point before the campus changes
    /// hands.
    /// </summary>
    [Theory]
    [MemberData(nameof(InternalAccounts))]
    public void An_internal_account_may_not_take_the_contact_role(string roleCode, string? subRole)
    {
        _ = subRole; // the bar is the ROLE; a sub-role never softens it

        var ex = Assert.Throws<ConflictException>(() =>
            OperationalContactEligibility.EnsureAccountMayHoldContactRole(roleCode, UserStatuses.Active));

        Assert.Equal(OperationalContactErrorCodes.MustBeExternal, ex.ErrorCode);
    }

    [Fact]
    public void A_visitor_account_may_take_the_contact_role()
        => OperationalContactEligibility.EnsureAccountMayHoldContactRole(
            RoleCodes.Visitor, UserStatuses.Active);

    /// <summary>
    /// An account whose role cannot be read is refused, not admitted. The predicate is written as "is it
    /// VISITOR" rather than "is it one of the internal ones" precisely so the failure mode of a missing
    /// or newly-added role is a refusal.
    /// </summary>
    [Fact]
    public void An_account_with_no_readable_role_is_refused()
        => Assert.Throws<ConflictException>(() =>
            OperationalContactEligibility.EnsureAccountMayHoldContactRole(null, UserStatuses.Active));

    /// <summary>The guard the accept handlers call reaches the same verdict, from a loaded account.</summary>
    [Fact]
    public void The_accept_guard_refuses_an_internal_account_before_it_compares_the_address()
    {
        var actor = new User
        {
            UserId = 900,
            FullName = "Staff Leader",
            Email = InternalEmail,
            Status = UserStatuses.Active,
            Role = new Role { RoleId = 3, RoleCode = RoleCodes.Staff, Name = "Staff" },
        };
        var change = new VisitRequestIdentityChange { NewEmailNormalized = InternalEmail };

        // The addresses MATCH — this account really was the one invited — and it is still refused. The
        // invitation being addressed to them is exactly the "loophole" the rule closes.
        var ex = Assert.Throws<ConflictException>(() =>
            OperationalContactGuards.EnsureActorMayTakeContactRole(actor, change));

        Assert.Equal(OperationalContactErrorCodes.MustBeExternal, ex.ErrorCode);
    }

    // ── 3. The doors, wired ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Replacing a campus's contact with an internal address is refused, and refused BEFORE anything is
    /// written: the campus keeps the contact it had, so a rejected replacement cannot leave it ownerless
    /// behind a re-closed gate.
    /// </summary>
    [Fact]
    public async Task Replacing_the_contact_with_an_internal_address_is_refused_and_changes_nothing()
    {
        var db = await ArrangeUndecidedCampusAsync(withInternalAccount: true);

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            ReplaceHandler(db).Handle(
                new ReplaceOperationalContactCommand(
                    RequestId, InstanceId, "Anh Staff", "FPTU", "Trưởng phòng", "+8490", InternalEmail),
                CancellationToken.None));

        Assert.Equal(VisitRequestErrorCodes.ContactEmailCannotBeUsedForVisitorAccount, ex.ErrorCode);

        var detail = await db.VisitInstanceFormDetails.AsNoTracking()
            .SingleAsync(d => d.VisitInstanceId == InstanceId);
        Assert.Equal(GuestEmail, detail.OperationalContactEmail);
        Assert.Empty(await db.VisitRequestIdentityChanges.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task Replacing_the_contact_with_an_external_address_still_works()
    {
        var db = await ArrangeUndecidedCampusAsync(withInternalAccount: true);

        await ReplaceHandler(db).Handle(
            new ReplaceOperationalContactCommand(
                RequestId, InstanceId, "Chị Khách", "Partner", "Giám đốc", "+8491", "new.guest@partner.example"),
            CancellationToken.None);

        var detail = await db.VisitInstanceFormDetails.AsNoTracking()
            .SingleAsync(d => d.VisitInstanceId == InstanceId);
        Assert.Equal("new.guest@partner.example", detail.OperationalContactEmail);
    }

    /// <summary>
    /// A re-invitation is a NEW invitation, so the address is re-checked rather than trusted because it
    /// was accepted once. This is the case that motivates it: a campus whose stored contact address now
    /// belongs to an FPTU account must not be re-opened to it.
    /// </summary>
    [Fact]
    public async Task Re_inviting_an_address_that_now_belongs_to_an_internal_account_is_refused()
    {
        var db = await ArrangeUndecidedCampusAsync(withInternalAccount: true, contactEmail: InternalEmail);

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            ReinviteHandler(db).Handle(
                new ReinviteOperationalContactConfirmationCommand(RequestId, InstanceId),
                CancellationToken.None));

        Assert.Equal(VisitRequestErrorCodes.ContactEmailCannotBeUsedForVisitorAccount, ex.ErrorCode);
        Assert.Empty(await db.VisitRequestIdentityChanges.AsNoTracking().ToListAsync());
    }

    // ── Arrange helpers ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A campus nobody has decided, held by an external guest, on a request filed by
    /// <see cref="RegistrantId"/> — the shape both the replace and the re-invite commands act on.
    /// </summary>
    private static async Task<OperationalContactTestDbContext> ArrangeUndecidedCampusAsync(
        bool withInternalAccount, string contactEmail = GuestEmail)
    {
        var db = OperationalContactTestDbContext.Create();
        db.Campuses.Add(DelegationsTestData.CreateCampus());

        if (withInternalAccount)
        {
            db.Roles.Add(new Role { RoleId = 3, RoleCode = RoleCodes.Staff, Name = "Staff" });
            db.Users.Add(new User
            {
                UserId = 900, FullName = "Staff Leader HN", Email = InternalEmail,
                RoleId = 3, SubRole = UserSubRoles.Leader, Status = UserStatuses.Active, CreatedAt = Now,
            });
        }

        var visit = DelegationsTestData.CreateVisitRequest();
        visit.Status = VisitRequestStatuses.PendingApproval;
        visit.RegistrantUserId = RegistrantId;
        visit.RegistrantEmail = "registrant@partner.example";
        visit.EmailVerifiedAt = Now.AddDays(-1);
        visit.ContactGateRevision = 1;

        var instance = DelegationsTestData.CreateVisitInstance(
            status: VisitInstanceStatuses.WaitingRequestApproval, currentHostUserId: null);
        instance.OperationalContactUserId = null;
        if (instance.FormDetail is not null)
            instance.FormDetail.OperationalContactEmail = contactEmail;
        visit.CampusInstances.Add(instance);
        db.VisitRequests.Add(visit);

        await db.SaveChangesAsync(CancellationToken.None);

        // The re-invite path reads the stored address off the form detail, which the shared fixture
        // seeds with its own default — set it here too, for whichever of the two rows actually exists.
        var detail = await db.VisitInstanceFormDetails
            .SingleOrDefaultAsync(d => d.VisitInstanceId == InstanceId);
        if (detail is null)
        {
            db.VisitInstanceFormDetails.Add(new VisitInstanceFormDetail
            {
                VisitInstanceId = InstanceId,
                DelegationName = "Đoàn thử", VisitType = "MEETING", Purpose = "Thăm",
                WorkingContent = "Nội dung", WorkingLanguage = "VI", MediaConsentStatus = "DECLINED",
                OperationalContactFullName = "Khách", OperationalContactJobTitle = "Giám đốc",
                OperationalContactEmail = contactEmail,
                FormRevision = 1, ApprovalRevision = 1, CreatedAt = Now,
            });
        }
        else
        {
            detail.OperationalContactEmail = contactEmail;
        }
        await db.SaveChangesAsync(CancellationToken.None);

        db.Journal.Clear();
        db.ChangeTracker.Clear();
        return db;
    }

    private static Mock<ICurrentUserService> Registrant()
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(c => c.IsAuthenticated).Returns(true);
        currentUser.SetupGet(c => c.UserId).Returns(RegistrantId);
        return currentUser;
    }

    private static Mock<IDateTimeService> Clock()
    {
        var clock = new Mock<IDateTimeService>();
        clock.SetupGet(c => c.VietnamNow).Returns(Now);
        clock.SetupGet(c => c.UtcNow).Returns(Now.AddHours(-7));
        return clock;
    }

    private static ReplaceOperationalContactCommandHandler ReplaceHandler(OperationalContactTestDbContext db)
        => new(db, Registrant().Object, Clock().Object,
            new RecordingOperationalContactInvitationService(db, Now),
            new VisitRequestAggregateStatusService(db),
            new Mock<PEMS.Application.Notifications.Common.INotificationService>().Object,
            new Mock<Microsoft.Extensions.Logging.ILogger<ReplaceOperationalContactCommandHandler>>().Object,
            new PerCampusFormV2WriteOptions());

    private static ReinviteOperationalContactConfirmationCommandHandler ReinviteHandler(
        OperationalContactTestDbContext db)
        => new(db, Registrant().Object, Clock().Object,
            new RecordingOperationalContactInvitationService(db, Now),
            new VisitRequestAggregateStatusService(db),
            new PerCampusFormV2WriteOptions());
}
