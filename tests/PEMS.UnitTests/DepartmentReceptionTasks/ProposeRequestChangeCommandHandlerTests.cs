using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.DepartmentReceptionTasks.Common;
using PEMS.Application.DepartmentReceptionTasks.Commands.ProposeRequestChange;
using PEMS.Application.Emails.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.UnitTests.TestInfrastructure;

namespace PEMS.UnitTests.DepartmentReceptionTasks;

/// <summary>
/// C-21 — the handling department asks the reception owner to change a logistics request.
///
/// <para>
/// This handler had no tests, and its email was the worst-recorded of the four: the whole message was
/// built in C# and the row was written with <c>EmailTemplateId = null</c>, so the email history could
/// not say what kind of message it was at all. It now names
/// <c>LOGISTICS_CHANGE_PROPOSAL_TO_HOST</c> and, for the first time, tells the Host WHICH department
/// is asking.
/// </para>
/// </summary>
public class ProposeRequestChangeCommandHandlerTests
{
    private const ulong DeptId = 40;
    private const ulong LeaderId = 400;
    private const ulong ItemId = 800;

    private static (DelegationsTestDbContext Db, ProposeRequestChangeCommandHandler Handler,
        FakeDelegationsCurrentUser User, DelegationsHandlerMocks Mocks,
        FakeDelegationsEmailDispatcher Dispatcher) CreateSut(ulong? hostUserId = DelegationsTestData.HostUserId)
    {
        var db = DelegationsTestDbContext.Create();
        DelegationsTestData.SeedBase(db);

        db.Departments.Add(DelegationsTestData.CreateDepartment(DeptId));
        db.Users.Add(DelegationsTestData.CreateUser(
            LeaderId, DelegationsTestData.DepartmentRoleId, UserSubRoles.Leader, DeptId));
        db.VisitLogisticsItems.Add(new VisitLogisticsItem
        {
            LogisticsItemId = ItemId,
            VisitInstanceId = DelegationsTestData.VisitInstanceId,
            ItemType = "LED",
            Title = "Màn LED sảnh A",
            Status = "ACCEPTED",
            CoordinationMode = "SYSTEM_REQUEST",
            RequestedToDepartmentId = DeptId,
            RequestedBy = hostUserId,
            Quantity = 2,
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
        var handler = new ProposeRequestChangeCommandHandler(
            db, user, dispatcher, mocks.Tokens.Object, mocks.Normalizer.Object, mocks.Notifications.Object);

        return (db, handler, user, mocks, dispatcher);
    }

    private static ProposeRequestChangeCommand Command(string? note = "Kho chỉ còn 1 màn, xin giảm số lượng.")
        => new() { LogisticsItemId = ItemId, ProposedQuantity = 1, ProposalNote = note };

    [Fact]
    public async Task The_proposal_email_names_a_template_instead_of_none()
    {
        var (db, handler, _, _, dispatcher) = CreateSut();

        Assert.True(await handler.Handle(Command(), default));

        var sent = dispatcher.Single(SystemEmailTemplates.LogisticsChangeProposalToHost);
        Assert.Equal(SystemEmailContent.FromTemplate.Instance, sent.Content);
        Assert.Equal($"user{DelegationsTestData.HostUserId}@test.local", sent.To.Email);

        // A proposal is a counter-offer, so the mail carries WHAT is proposed, not only why. Sending
        // the rationale alone made the Host open the portal to find the numbers they were being asked
        // to approve.
        Assert.Equal(
            new[]
            {
                "delegationName", "departmentName", "hostName", "logisticsTitle", "originalQuantity",
                "proposalNote", "proposedDescription", "proposedQuantity",
                "proposedUsageEndAt", "proposedUsageStartAt",
            },
            sent.Variables.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray());

        Assert.Equal("1", sent.Variables["proposedQuantity"]);

        // The Host now learns who is asking. "The handling department proposes a change" was
        // unverifiable — this names it.
        Assert.Equal($"Phòng ban {DeptId}", sent.Variables["departmentName"]);
        Assert.Equal("Màn LED sảnh A", sent.Variables["logisticsTitle"]);
        Assert.Equal("Kho chỉ còn 1 màn, xin giảm số lượng.", sent.Variables["proposalNote"]);

        var item = db.VisitLogisticsItems.Single();
        Assert.Equal("CHANGE_PROPOSED", item.Status);
        Assert.Equal(1, item.ProposedQuantity);
        // The planned figure is never overwritten by a proposal.
        Assert.Equal(2, item.Quantity);
    }

    [Fact]
    public async Task The_approve_and_reject_tokens_belong_to_the_message_and_the_host()
    {
        var (db, handler, _, _, dispatcher) = CreateSut();

        await handler.Handle(Command(), default);

        var sentEmail = Assert.Single(db.SentEmails);
        var tokens = db.EmailActionTokens
            .Where(t => t.ResultStatus == EmailActionResultStatuses.Pending).ToList();

        Assert.Equal(2, tokens.Count);
        Assert.All(tokens, t =>
        {
            Assert.Equal(sentEmail.SentEmailId, t.SentEmailId);
            Assert.Equal(EmailActionContexts.LogisticsProposalResponse, t.ActionContext);
            Assert.Equal(EmailActionTargetTypes.LogisticsItem, t.TargetType);
            Assert.Equal(ItemId, t.TargetId);
            Assert.Equal(DelegationsTestData.HostUserId, t.RecipientUserId);
        });
        Assert.Single(tokens.Where(t => t.IntendedAction == EmailIntendedActions.ApproveProposal));
        Assert.Single(tokens.Where(t => t.IntendedAction == EmailIntendedActions.RejectProposal));

        var block = dispatcher.Single(SystemEmailTemplates.LogisticsChangeProposalToHost)
            .TrustedBlocks![EmailTrustedBlocks.ActionBlock];
        Assert.Contains("https://pems.test/email-actions/raw-token-1", block);
        Assert.Contains("https://pems.test/email-actions/raw-token-2", block);
        Assert.Contains($"https://pems.test/logistics/{ItemId}", block);
    }

    [Fact]
    public async Task A_proposal_with_no_host_on_record_changes_the_item_but_sends_nothing()
    {
        var (db, handler, _, _, dispatcher) = CreateSut(hostUserId: null);

        Assert.True(await handler.Handle(Command(), default));

        // Portal-only proposal: the status still moves, because the Host can respond in the system.
        Assert.Equal("CHANGE_PROPOSED", db.VisitLogisticsItems.Single().Status);
        Assert.Empty(dispatcher.Requests);
        Assert.Empty(db.SentEmails);
        Assert.Empty(db.EmailActionTokens);
    }

    [Fact]
    public async Task A_proposal_with_no_rationale_is_refused()
    {
        var (db, handler, _, _, _) = CreateSut();

        var missing = await Assert.ThrowsAsync<ValidationException>(
            () => handler.Handle(Command(note: "   "), default));
        Assert.Equal(LogisticsTaskErrorCodes.ProposalNoteRequired, missing.ErrorCode);

        Assert.Equal("ACCEPTED", db.VisitLogisticsItems.Single().Status);
        Assert.Empty(db.SentEmails);
    }

    [Fact]
    public async Task An_email_failure_never_rolls_back_the_proposal()
    {
        var (db, handler, _, mocks, _) = CreateSut();
        mocks.FailEmail = true;

        Assert.True(await handler.Handle(Command(), default));

        Assert.Equal("CHANGE_PROPOSED", db.VisitLogisticsItems.Single().Status);
        Assert.Equal("FAILED", Assert.Single(db.SentEmails).Status);
        Assert.Equal(2, db.EmailActionTokens.Count());
    }
}
