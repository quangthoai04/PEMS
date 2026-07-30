using Moq;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.DepartmentReceptionTasks.Common;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.DepartmentReceptionTasks.Commands.AssignRequestAssignee;
using PEMS.Application.Emails.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.UnitTests.TestInfrastructure;

namespace PEMS.UnitTests.DepartmentReceptionTasks;

/// <summary>
/// C-20 — a Department Leader gives one of their staff a logistics task.
///
/// <para>
/// This handler had no tests. What it used to do, and no longer does, was compose the whole message in
/// C# (<c>DefaultContentHtml</c>) while separately looking up the template id purely to write it into
/// <c>sent_emails</c> — so the history claimed content came from a template that had never been read,
/// and editing that template changed nothing a recipient saw.
/// </para>
/// </summary>
public class AssignRequestAssigneeCommandHandlerTests
{
    private const ulong DeptId = 30;
    private const ulong LeaderId = 300;
    private const ulong StaffId = 301;
    private const ulong OutsiderId = 302;
    private const ulong ItemId = 700;

    private static (DelegationsTestDbContext Db, AssignRequestAssigneeCommandHandler Handler,
        FakeDelegationsCurrentUser User, DelegationsHandlerMocks Mocks,
        FakeDelegationsEmailDispatcher Dispatcher) CreateSut(string itemStatus = "REQUESTED")
    {
        var db = DelegationsTestDbContext.Create();
        DelegationsTestData.SeedBase(db);

        db.Departments.AddRange(
            DelegationsTestData.CreateDepartment(DeptId),
            DelegationsTestData.CreateDepartment(31));
        db.Users.AddRange(
            DelegationsTestData.CreateUser(LeaderId, DelegationsTestData.DepartmentRoleId, UserSubRoles.Leader, DeptId),
            DelegationsTestData.CreateUser(StaffId, DelegationsTestData.DepartmentRoleId, UserSubRoles.Staff, DeptId),
            DelegationsTestData.CreateUser(OutsiderId, DelegationsTestData.DepartmentRoleId, UserSubRoles.Staff, 31));
        db.VisitLogisticsItems.Add(new VisitLogisticsItem
        {
            LogisticsItemId = ItemId,
            VisitInstanceId = DelegationsTestData.VisitInstanceId,
            ItemType = "LED",
            Title = "Màn LED sảnh A",
            Status = itemStatus,
            CoordinationMode = "SYSTEM_REQUEST",
            RequestedToDepartmentId = DeptId,
            RequestedBy = DelegationsTestData.HostUserId,
            DueAt = new DateTime(2026, 7, 30, 17, 0, 0),
            CreatedAt = new DateTime(2026, 6, 1),
        });
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
        var dispatcher = mocks.DispatcherFor(db);
        var handler = new AssignRequestAssigneeCommandHandler(
            db, user, mocks.Clock, dispatcher, mocks.Tokens.Object, mocks.Sanitizer.Object,
            mocks.Storage.Object, mocks.Normalizer.Object, mocks.Notifications.Object,
            new PEMS.UnitTests.TestInfrastructure.RecordingUserMutationLockService());

        return (db, handler, user, mocks, dispatcher);
    }

    private static AssignRequestAssigneeCommand Command(
        ulong assigneeId = StaffId, EmailOverride? emailOverride = null)
        => new() { LogisticsItemId = ItemId, AssigneeUserId = assigneeId, EmailOverride = emailOverride };

    [Fact]
    public async Task The_assignment_uses_its_own_template_and_supplies_every_declared_variable()
    {
        var (db, handler, _, _, dispatcher) = CreateSut();

        Assert.True(await handler.Handle(Command(), default));

        var sent = dispatcher.Single(SystemEmailTemplates.LogisticsAssigneeAssignment);
        Assert.Equal(SystemEmailContent.FromTemplate.Instance, sent.Content);
        Assert.Equal($"user{StaffId}@test.local", sent.To.Email);

        Assert.Equal(
            new[] { "assigneeName", "campusName", "delegationName", "dueAt", "logisticsTitle" },
            sent.Variables.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray());

        Assert.Equal($"User {StaffId}", sent.Variables["assigneeName"]);
        Assert.Equal("Màn LED sảnh A", sent.Variables["logisticsTitle"]);
        Assert.Equal("17:00 30/07/2026", sent.Variables["dueAt"]);
        // campusName and delegationName are new: the previous hard-coded body never said where the
        // visit was, so an assignee working across campuses could not tell which one this was.
        Assert.Equal($"Campus {DelegationsTestData.CampusId}", sent.Variables["campusName"]);
        Assert.Equal("Đoàn khách kiểm thử", sent.Variables["delegationName"]);

        Assert.Equal("ASSIGNED", db.VisitLogisticsItems.Single().Status);
        Assert.Equal(StaffId, db.VisitLogisticsItems.Single().AssignedToUserId);
    }

    [Fact]
    public async Task Both_response_tokens_belong_to_the_message_and_the_assignee()
    {
        var (db, handler, _, _, dispatcher) = CreateSut();

        await handler.Handle(Command(), default);

        var sentEmail = Assert.Single(db.SentEmails);
        var tokens = db.EmailActionTokens.Where(t => t.TargetId == ItemId).ToList();

        Assert.Equal(2, tokens.Count);
        Assert.All(tokens, t =>
        {
            Assert.Equal(sentEmail.SentEmailId, t.SentEmailId);
            Assert.Equal(EmailActionContexts.LogisticsAssigneeResponse, t.ActionContext);
            Assert.Equal(EmailActionTargetTypes.LogisticsItem, t.TargetType);
            Assert.Equal(StaffId, t.RecipientUserId);
            Assert.Equal($"user{StaffId}@test.local", t.RecipientEmail);
        });

        var block = dispatcher.Single(SystemEmailTemplates.LogisticsAssigneeAssignment)
            .TrustedBlocks![EmailTrustedBlocks.ActionBlock];
        Assert.Contains("https://pems.test/email-actions/raw-token-1", block);
        Assert.Contains("https://pems.test/email-actions/raw-token-2", block);
        Assert.Contains($"https://pems.test/logistics/{ItemId}", block);
    }

    [Fact]
    public async Task A_leader_edit_replaces_the_words_and_nothing_else()
    {
        var (_, handler, _, _, dispatcher) = CreateSut();

        await handler.Handle(
            Command(emailOverride: new EmailOverride(
                UseEditedContent: true,
                Subject: "Em phụ trách màn LED giúp anh",
                BodyHtml: null,
                BodyText: "Em nhận giúp anh phần màn LED sảnh A nhé.")),
            default);

        var sent = dispatcher.Single(SystemEmailTemplates.LogisticsAssigneeAssignment);
        var authored = Assert.IsType<SystemEmailContent.AuthoredByUser>(sent.Content);
        Assert.Equal("Em phụ trách màn LED giúp anh", authored.Subject);
        // Plain text is accepted here now — the old code read only bodyHtml and called this empty.
        Assert.Contains(System.Net.WebUtility.HtmlEncode("sảnh A"), authored.BodyHtml);
        Assert.Equal($"user{StaffId}@test.local", sent.To.Email);
        Assert.Contains(EmailTrustedBlocks.ActionBlock, sent.TrustedBlocks!.Keys);
    }

    [Fact]
    public async Task A_leader_may_not_hand_write_the_action_block()
    {
        var (db, handler, _, _, _) = CreateSut();

        var ex = await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(
            Command(emailOverride: new EmailOverride(
                UseEditedContent: true, Subject: "Giao việc",
                BodyHtml: "<p>a</p><!-- PEMS_ACTION_BLOCK_START --><p>giả</p><!-- PEMS_ACTION_BLOCK_END -->")),
            default));

        Assert.Equal(EmailErrorCodes.AuthoredActionBlockForbidden, ex.ErrorCode);
        // Refused before the item is touched.
        Assert.Equal("REQUESTED", db.VisitLogisticsItems.Single().Status);
        Assert.Empty(db.SentEmails);
    }

    [Fact]
    public async Task Somebody_from_another_department_cannot_be_assigned()
    {
        var (db, handler, _, _, _) = CreateSut();

        // A refusal, not a fault: the caller is told WHY with a stable code, and the middleware turns
        // this into a 409 rather than a 500.
        var outsider = await Assert.ThrowsAsync<ConflictException>(
            () => handler.Handle(Command(assigneeId: OutsiderId), default));
        Assert.Equal(LogisticsTaskErrorCodes.AssigneeNotEligible, outsider.ErrorCode);
        Assert.Equal("REQUESTED", db.VisitLogisticsItems.Single().Status);
        Assert.Empty(db.SentEmails);
    }

    [Theory]
    [InlineData("ASSIGNED")]
    [InlineData("ACCEPTED")]
    [InlineData("DONE")]
    [InlineData("CANCELLED")]
    public async Task An_item_already_in_flight_or_closed_cannot_be_reassigned(string status)
    {
        var (db, handler, _, _, _) = CreateSut(itemStatus: status);

        var inFlight = await Assert.ThrowsAsync<ConflictException>(
            () => handler.Handle(Command(), default));
        Assert.Equal(LogisticsTaskErrorCodes.AssignmentStatusNotAssignable, inFlight.ErrorCode);
        Assert.Empty(db.SentEmails);
        Assert.Empty(db.EmailActionTokens);
    }

    [Fact]
    public async Task An_email_failure_leaves_the_assignment_in_place()
    {
        var (db, handler, _, mocks, _) = CreateSut();
        mocks.FailEmail = true;

        Assert.True(await handler.Handle(Command(), default));

        Assert.Equal("ASSIGNED", db.VisitLogisticsItems.Single().Status);
        Assert.Equal("FAILED", Assert.Single(db.SentEmails).Status);
        Assert.Equal(2, db.EmailActionTokens.Count());
    }
}
