using Moq;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Commands.AssignDepartmentStaff;
using PEMS.Application.Emails.Common;
using PEMS.Domain.Constants;
using PEMS.UnitTests.TestInfrastructure;

namespace PEMS.UnitTests.Delegations.AssignDepartmentStaff;

/// <summary>
/// A Department Leader assigns one of their own staff to a visit their department was invited to
/// support. Only a Leader may do it, only within their own department, and the person assigned gets a
/// message that says so — an ASSIGNMENT, not an invitation.
///
/// <para>
/// This handler had no tests before the email migration. The ones below pin what it does now, including
/// the two things that were wrong: it recorded every assignment against
/// <c>VISIT_PARTICIPANT_INVITATION</c> (the invitation template, which is a different message to a
/// different kind of recipient), and it wrote the live accept/decline links into
/// <c>sent_emails.body_snapshot</c>.
/// </para>
/// </summary>
public class AssignDepartmentStaffCommandHandlerTests
{
    private const ulong DeptId = 910;
    private const ulong LeaderId = 201;
    private const ulong StaffId = 202;
    private const ulong OtherDeptStaffId = 203;
    private const ulong LeaderParticipantId = 600;

    /// <summary>
    /// Records what the handler locked, so a test can assert the lock was taken at all.
    ///
    /// <para>
    /// This is not decoration. The merge of Dev into Cảnh-Iter1 dropped this handler's
    /// <see cref="IUserMutationLockService"/> call and its transaction while the XML documentation went
    /// on describing both; every unit test here still passed, because none of them looked. A human
    /// reviewer found it (C-1). The assertion below is what makes the next removal fail a test.
    /// </para>
    /// </summary>
    private sealed class RecordingLockService : IUserMutationLockService
    {
        public List<ulong[]> UserLockCalls { get; } = new();
        public List<ulong[]> DepartmentLockCalls { get; } = new();

        public Task LockUsersAsync(IReadOnlyCollection<ulong> userIds, CancellationToken ct = default)
        {
            UserLockCalls.Add(userIds.ToArray());
            return Task.CompletedTask;
        }

        public Task LockDepartmentsAsync(IReadOnlyCollection<ulong> departmentIds, CancellationToken ct = default)
        {
            DepartmentLockCalls.Add(departmentIds.ToArray());
            return Task.CompletedTask;
        }
    }

    private static (DelegationsTestDbContext Db, AssignDepartmentStaffCommandHandler Handler,
        FakeDelegationsCurrentUser User, DelegationsHandlerMocks Mocks,
        FakeDelegationsEmailDispatcher Dispatcher, RecordingLockService Locks) CreateSut()
    {
        var db = DelegationsTestDbContext.Create();
        DelegationsTestData.SeedBase(db);

        db.Departments.AddRange(
            DelegationsTestData.CreateDepartment(DeptId),
            DelegationsTestData.CreateDepartment(911));
        db.Users.AddRange(
            DelegationsTestData.CreateUser(LeaderId, DelegationsTestData.DepartmentRoleId, UserSubRoles.Leader, DeptId),
            DelegationsTestData.CreateUser(StaffId, DelegationsTestData.DepartmentRoleId, UserSubRoles.Staff, DeptId),
            DelegationsTestData.CreateUser(OtherDeptStaffId, DelegationsTestData.DepartmentRoleId, UserSubRoles.Staff, 911));
        db.VisitParticipants.Add(DelegationsTestData.CreateParticipant(
            LeaderParticipantId, LeaderId, ParticipantRoles.DeptSupport, ParticipantStatuses.Accepted));
        db.SaveChanges();

        var user = new FakeDelegationsCurrentUser
        {
            UserId = LeaderId,
            RoleId = DelegationsTestData.DepartmentRoleId,
            RoleCode = RoleCodes.Department,
            SubRole = UserSubRoles.Leader,
            DepartmentId = DeptId,
        };

        var mocks = new DelegationsHandlerMocks();
        var formRead = new Mock<PEMS.Application.Delegations.Services.VisitFormRead.IVisitFormReadService>();
        formRead
            .Setup(f => f.ResolveCampusFormContentAsync(
                It.IsAny<PEMS.Domain.Entities.Delegations.VisitRequest>(),
                It.IsAny<IReadOnlyList<ulong>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((PEMS.Domain.Entities.Delegations.VisitRequest _, IReadOnlyList<ulong> ids, CancellationToken _) =>
                ids.ToDictionary(
                        id => id,
                        _ => new PEMS.Application.Delegations.Services.VisitFormRead.VisitCampusFormContent
                        {
                            DelegationName = "Đoàn khách kiểm thử",
                        })
                   as IReadOnlyDictionary<ulong, PEMS.Application.Delegations.Services.VisitFormRead.VisitCampusFormContent>);

        var dispatcher = mocks.DispatcherFor(db);
        var locks = new RecordingLockService();
        var handler = new AssignDepartmentStaffCommandHandler(
            db, user, mocks.Clock, dispatcher, mocks.Tokens.Object, mocks.Sanitizer.Object,
            mocks.Storage.Object, formRead.Object, locks);

        return (db, handler, user, mocks, dispatcher, locks);
    }

    private static AssignDepartmentStaffCommand Command(
        ulong staffUserId = StaffId, EmailOverride? emailOverride = null)
        => new(LeaderParticipantId, staffUserId, "Nhờ em hỗ trợ", emailOverride);

    // ── The message is an assignment, and says which department assigned it ──

    [Fact]
    public async Task The_assignment_uses_the_assignment_template_not_the_invitation_one()
    {
        var (db, handler, _, _, dispatcher, _locks) = CreateSut();

        var participantId = await handler.Handle(Command(), default);

        var sent = dispatcher.Single(SystemEmailTemplates.VisitDepartmentStaffAssignment);
        Assert.Equal($"user{StaffId}@test.local", sent.To.Email);
        Assert.Equal(SystemEmailContent.FromTemplate.Instance, sent.Content);

        // The recipient is told who assigned them and to which visit — an assignment with neither is
        // not actionable.
        Assert.Equal($"Phòng ban {DeptId}", sent.Variables["departmentName"]);
        Assert.Equal("Đoàn khách kiểm thử", sent.Variables["delegationName"]);
        Assert.Equal($"Campus {DelegationsTestData.CampusId}", sent.Variables["campusName"]);
        Assert.Equal("09:00 01/08/2026 - 11:00 01/08/2026", sent.Variables["plannedTime"]);
        Assert.Equal($"User {StaffId}", sent.Variables["recipientName"]);

        // …and nothing was recorded against the invitation template, which is a different message.
        Assert.Empty(dispatcher.Requests.Where(
            r => r.TemplateCode == SystemEmailTemplates.VisitParticipantInvitation));

        var assigned = Assert.Single(db.VisitParticipants.Where(p => p.UserId == StaffId));
        Assert.Equal(participantId, assigned.ParticipantId);
        Assert.Equal(ParticipantStatuses.Assigned, assigned.Status);
        Assert.Equal(ParticipantRoles.DeptSupport, assigned.ParticipantRole);
        Assert.Equal(LeaderId, assigned.AssignedBy);
    }

    [Fact]
    public async Task Both_response_tokens_point_at_the_message_that_carried_them()
    {
        var (db, handler, _, _, _, _locks) = CreateSut();

        var participantId = await handler.Handle(Command(), default);

        var sentEmail = Assert.Single(db.SentEmails);
        var tokens = db.EmailActionTokens.Where(t => t.TargetId == participantId).ToList();

        Assert.Equal(2, tokens.Count);
        Assert.All(tokens, t =>
        {
            // Never null: the row is written only after the message exists.
            Assert.Equal(sentEmail.SentEmailId, t.SentEmailId);
            Assert.Equal(EmailActionResultStatuses.Pending, t.ResultStatus);
            Assert.Equal(EmailActionContexts.ParticipationResponse, t.ActionContext);
            Assert.Equal($"user{StaffId}@test.local", t.RecipientEmail);
        });
        Assert.Single(tokens.Where(t => t.IntendedAction == EmailIntendedActions.Accept));
        Assert.Single(tokens.Where(t => t.IntendedAction == EmailIntendedActions.Decline));

        // Only hashes are stored — the raw token exists solely in the link the recipient received.
        Assert.All(tokens, t => Assert.StartsWith("hash(", t.TokenHash));
    }

    [Fact]
    public async Task The_action_block_carries_the_real_links_and_the_leader_never_supplies_them()
    {
        var (_, handler, _, _, dispatcher, _locks) = CreateSut();

        await handler.Handle(Command(), default);

        var block = dispatcher.Single(SystemEmailTemplates.VisitDepartmentStaffAssignment)
            .TrustedBlocks![EmailTrustedBlocks.ActionBlock];

        Assert.Contains("https://pems.test/email-actions/raw-token-1", block);
        Assert.Contains("https://pems.test/email-actions/raw-token-2", block);
        Assert.Contains(EmailComposition.ActionBlockStart, block);
    }

    // ── The Leader may rewrite the words, and nothing else ───────────────────

    [Fact]
    public async Task A_leader_edit_is_a_named_content_mode_and_changes_only_the_words()
    {
        var (_, handler, _, _, dispatcher, _locks) = CreateSut();

        await handler.Handle(
            Command(emailOverride: new EmailOverride(
                UseEditedContent: true,
                Subject: "Em hỗ trợ đoàn Kyoto giúp anh nhé",
                BodyHtml: "<p>Em phụ trách phần đón tiếp buổi sáng.</p>")),
            default);

        var sent = dispatcher.Single(SystemEmailTemplates.VisitDepartmentStaffAssignment);
        var authored = Assert.IsType<SystemEmailContent.AuthoredByUser>(sent.Content);

        Assert.Equal("Em hỗ trợ đoàn Kyoto giúp anh nhé", authored.Subject);
        Assert.Contains("đón tiếp buổi sáng", authored.BodyHtml);

        // Same template, same recipient, same backend-built buttons.
        Assert.Equal($"user{StaffId}@test.local", sent.To.Email);
        Assert.Contains(EmailTrustedBlocks.ActionBlock, sent.TrustedBlocks!.Keys);
        Assert.DoesNotContain(EmailComposition.ActionBlockStart, authored.BodyHtml);
    }

    [Fact]
    public async Task A_leader_may_not_hand_write_the_action_block()
    {
        var (db, handler, _, _, _, _locks) = CreateSut();

        var ex = await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(
            Command(emailOverride: new EmailOverride(
                UseEditedContent: true,
                Subject: "Nhờ em",
                BodyHtml: "<p>Bấm đây</p><!-- PEMS_ACTION_BLOCK_START --><p>giả</p><!-- PEMS_ACTION_BLOCK_END -->")),
            default));

        Assert.Equal(EmailErrorCodes.AuthoredActionBlockForbidden, ex.ErrorCode);
        // Refused before anything was written: no assignment, no message, no token.
        Assert.Empty(db.VisitParticipants.Where(p => p.UserId == StaffId));
        Assert.Empty(db.SentEmails);
        Assert.Empty(db.EmailActionTokens);
    }

    [Fact]
    public async Task An_empty_edited_subject_is_refused_before_the_assignment_is_written()
    {
        var (db, handler, _, _, _, _locks) = CreateSut();

        var ex = await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(
            Command(emailOverride: new EmailOverride(
                UseEditedContent: true, Subject: "   ", BodyHtml: "<p>Nội dung</p>")),
            default));

        Assert.Equal(EmailErrorCodes.AuthoredSubjectRequired, ex.ErrorCode);
        Assert.Empty(db.SentEmails);
    }

    // ── Who is allowed to do this ────────────────────────────────────────────

    [Fact]
    public async Task Only_a_department_leader_may_assign()
    {
        var (db, handler, user, _, _, _locks) = CreateSut();
        user.SubRole = UserSubRoles.Staff;

        await Assert.ThrowsAsync<ForbiddenException>(() => handler.Handle(Command(), default));
        Assert.Empty(db.SentEmails);
    }

    [Fact]
    public async Task A_leader_may_not_assign_somebody_from_another_department()
    {
        var (db, handler, _, _, _, _locks) = CreateSut();

        await Assert.ThrowsAsync<ConflictException>(
            () => handler.Handle(Command(staffUserId: OtherDeptStaffId), default));
        Assert.Empty(db.SentEmails);
        Assert.Empty(db.EmailActionTokens);
    }

    [Theory]
    [InlineData(UserStatuses.Inactive)]
    [InlineData(UserStatuses.Locked)]
    [InlineData(UserStatuses.PendingEmailConfirmation)]
    public async Task A_leader_may_not_assign_an_account_that_is_not_active(string status)
    {
        // Role and department alone were not enough: a deactivated, locked or not-yet-confirmed
        // account holds no effective authority, so assigning it a live visit responsibility creates a
        // task nobody can act on. The sibling logistics-assignment flow already re-checked status.
        var (db, handler, _, _, _, _locks) = CreateSut();
        var staff = db.Users.Single(u => u.UserId == StaffId);
        staff.Status = status;
        db.SaveChanges();

        await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(Command(), default));

        // Refused before anything was written.
        Assert.Empty(db.VisitParticipants.Where(p => p.UserId == StaffId));
        Assert.Empty(db.SentEmails);
        Assert.Empty(db.EmailActionTokens);
    }

    // ── The account is locked before it is judged (C-1) ──────────────────────

    /// <summary>
    /// The assignment must take the shared user lock, exactly once, on the account being assigned.
    ///
    /// <para>
    /// <c>UpdateAccountRole</c> takes the same lock and relies on assignment flows contending on it; a
    /// handler that skips it makes the exclusion one-sided, so a role change can commit in the gap
    /// between this handler's eligibility checks and its commit. That is what happened when the merge
    /// dropped the call — nothing failed, because nothing asserted it.
    /// </para>
    /// <para>
    /// Ordering relative to the reads is proved against real MySQL in
    /// <c>AssignDepartmentStaffConcurrencyTests</c>; a lock with no row-level semantics cannot show it.
    /// </para>
    /// </summary>
    [Fact]
    public async Task The_assigned_account_is_locked_exactly_once_before_it_is_judged()
    {
        var (_, handler, _, _, _, locks) = CreateSut();

        await handler.Handle(Command(), default);

        var call = Assert.Single(locks.UserLockCalls);
        Assert.Equal(new[] { StaffId }, call);
        Assert.Empty(locks.DepartmentLockCalls);
    }

    /// <summary>
    /// A refusal still locks first. The check that rejects the account has to run against committed
    /// state, so the lock cannot be conditional on the outcome of the very checks it protects.
    /// </summary>
    [Fact]
    public async Task An_account_refused_for_status_was_still_locked_before_the_check()
    {
        var (db, handler, _, _, _, locks) = CreateSut();
        var staff = db.Users.Single(u => u.UserId == StaffId);
        staff.Status = UserStatuses.Inactive;
        db.SaveChanges();

        await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(Command(), default));

        var call = Assert.Single(locks.UserLockCalls);
        Assert.Equal(new[] { StaffId }, call);
    }

    // ── Reassignment supersedes the previous response links ─────────────────

    [Fact]
    public async Task Reassigning_invalidates_the_pending_tokens_of_the_previous_round()
    {
        var (db, handler, _, _, _, _locks) = CreateSut();

        var firstId = await handler.Handle(Command(), default);
        var firstRoundHashes = db.EmailActionTokens
            .Where(t => t.TargetId == firstId).Select(t => t.TokenHash).ToList();

        var secondId = await handler.Handle(Command(), default);
        Assert.Equal(firstId, secondId);   // the same person, the same slot

        // The links from the first message must stop working — otherwise two live accept links exist
        // for one assignment and the last one clicked wins.
        var superseded = db.EmailActionTokens
            .Where(t => firstRoundHashes.Contains(t.TokenHash)).ToList();
        Assert.All(superseded, t => Assert.NotEqual(EmailActionResultStatuses.Pending, t.ResultStatus));

        var latest = db.EmailActionTokens
            .Where(t => t.ResultStatus == EmailActionResultStatuses.Pending).ToList();
        Assert.Equal(2, latest.Count);
        Assert.All(latest, t => Assert.Equal(db.SentEmails.OrderBy(e => e.SentEmailId).Last().SentEmailId, t.SentEmailId));
    }

    // ── A failed send does not lose the assignment ───────────────────────────

    [Fact]
    public async Task An_email_failure_leaves_the_assignment_in_place_and_records_the_failure()
    {
        var (db, handler, _, mocks, _, _locks) = CreateSut();
        mocks.FailEmail = true;

        var participantId = await handler.Handle(Command(), default);

        var assigned = Assert.Single(db.VisitParticipants.Where(p => p.UserId == StaffId));
        Assert.Equal(participantId, assigned.ParticipantId);
        Assert.Equal(ParticipantStatuses.Assigned, assigned.Status);

        Assert.Equal("FAILED", Assert.Single(db.SentEmails).Status);
        Assert.Equal(2, db.EmailActionTokens.Count());
    }
}
