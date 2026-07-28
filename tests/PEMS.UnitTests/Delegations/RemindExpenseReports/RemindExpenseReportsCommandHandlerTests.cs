using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.VisitExpenses.Commands.RemindExpenseReports;
using PEMS.Application.Emails.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Shared;
using PEMS.UnitTests.TestInfrastructure;

namespace PEMS.UnitTests.Delegations.RemindExpenseReports;

/// <summary>
/// C-22 — after the visit, the reception owner chases whoever still owes an expense entry.
///
/// <para>
/// Untested before this batch, and the only one of the four logistics emails that is NOT sensitive: its
/// link requires a login and grants nothing on its own, which is why its body is kept in full while the
/// other three keep theirs stripped. It also mints no token — and must not start doing so.
/// </para>
/// </summary>
public class RemindExpenseReportsCommandHandlerTests
{
    private const ulong DeptId = 50;
    private const ulong LeaderId = 500;
    private const ulong AssigneeId = 501;

    private static (DelegationsTestDbContext Db, RemindExpenseReportsCommandHandler Handler,
        FakeDelegationsCurrentUser User, DelegationsHandlerMocks Mocks,
        FakeDelegationsEmailDispatcher Dispatcher) CreateSut(
            string instanceStatus = VisitInstanceStatus.AfterVisit)
    {
        var db = DelegationsTestDbContext.Create();
        DelegationsTestData.SeedBase(db, instanceStatus);

        db.Departments.Add(DelegationsTestData.CreateDepartment(DeptId));
        db.Users.AddRange(
            DelegationsTestData.CreateUser(LeaderId, DelegationsTestData.DepartmentRoleId, UserSubRoles.Leader, DeptId),
            DelegationsTestData.CreateUser(AssigneeId, DelegationsTestData.DepartmentRoleId, UserSubRoles.Staff, DeptId));
        db.SaveChanges();

        var user = new FakeDelegationsCurrentUser();   // the Host
        var mocks = new DelegationsHandlerMocks();
        var dispatcher = mocks.DispatcherFor(db);
        var handler = new RemindExpenseReportsCommandHandler(
            db, user, mocks.Clock, dispatcher, mocks.Tokens.Object, mocks.Notifications.Object);

        return (db, handler, user, mocks, dispatcher);
    }

    private static VisitLogisticsItem Item(
        ulong id, ulong? assignedTo, string status = "DONE", DateTime? dueAt = null) => new()
    {
        LogisticsItemId = id,
        VisitInstanceId = DelegationsTestData.VisitInstanceId,
        ItemType = "LED",
        Title = $"Hạng mục {id}",
        Status = status,
        CoordinationMode = "SYSTEM_REQUEST",
        RequestedToDepartmentId = DeptId,
        AssignedToUserId = assignedTo,
        DueAt = dueAt,
        CreatedAt = new DateTime(2026, 6, 1),
    };

    private static RemindExpenseReportsCommand Command()
        => new() { VisitInstanceId = DelegationsTestData.VisitInstanceId };

    [Fact]
    public async Task Each_reminder_names_its_template_and_supplies_every_declared_variable()
    {
        var (db, handler, _, _, dispatcher) = CreateSut();
        db.VisitLogisticsItems.Add(Item(900, AssigneeId, dueAt: new DateTime(2026, 8, 10, 17, 0, 0)));
        db.SaveChanges();

        var result = await handler.Handle(Command(), default);

        Assert.Equal(1, result.RemindedCount);
        var sent = dispatcher.Single(SystemEmailTemplates.LogisticsExpenseReportReminder);
        Assert.Equal($"user{AssigneeId}@test.local", sent.To.Email);

        Assert.Equal(
            new[] { "delegationName", "dueAt", "itemTitle", "recipientName" },
            sent.Variables.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray());

        Assert.Equal($"User {AssigneeId}", sent.Variables["recipientName"]);
        Assert.Equal("Hạng mục 900", sent.Variables["itemTitle"]);
        Assert.Equal("17:00 10/08/2026", sent.Variables["dueAt"]);
        // delegationName is new — the previous hard-coded body did include it, and the template
        // declares it, so it had to keep working.
        Assert.Equal("Đoàn khách kiểm thử", sent.Variables["delegationName"]);
    }

    [Fact]
    public async Task The_reminder_mints_no_token_and_carries_only_a_login_required_link()
    {
        var (db, handler, _, _, dispatcher) = CreateSut();
        db.VisitLogisticsItems.Add(Item(901, AssigneeId));
        db.SaveChanges();

        await handler.Handle(Command(), default);

        // Nothing in this message grants access, which is exactly why its history keeps the whole body.
        Assert.Empty(db.EmailActionTokens);
        Assert.Equal(
            HistoryBodyPolicy.Full,
            SensitiveEmailHistory.PolicyFor(SystemEmailTemplates.LogisticsExpenseReportReminder));

        var block = dispatcher.Single(SystemEmailTemplates.LogisticsExpenseReportReminder)
            .TrustedBlocks![EmailTrustedBlocks.ActionBlock];
        Assert.Contains("https://pems.test/logistics/901", block);
        Assert.DoesNotContain("email-actions", block);
    }

    [Fact]
    public async Task Nobody_is_added_as_a_silent_copy()
    {
        var (db, handler, _, _, dispatcher) = CreateSut();
        db.VisitLogisticsItems.AddRange(Item(902, AssigneeId), Item(903, AssigneeId));
        db.SaveChanges();

        await handler.Handle(Command(), default);

        // Two items, two separate messages to the one person responsible — not one message with the
        // department leader quietly copied in.
        Assert.Equal(2, dispatcher.Requests.Count);
        Assert.All(dispatcher.Requests, r => Assert.Equal($"user{AssigneeId}@test.local", r.To.Email));
        Assert.All(db.SentEmailRecipients, r => Assert.Equal(EmailRecipientTypes.To, r.RecipientType));
        Assert.Equal(2, db.SentEmailRecipients.Count());
    }

    [Fact]
    public async Task An_unassigned_item_falls_back_to_the_departments_leader()
    {
        var (db, handler, _, _, dispatcher) = CreateSut();
        db.VisitLogisticsItems.Add(Item(904, assignedTo: null));
        db.SaveChanges();

        var result = await handler.Handle(Command(), default);

        Assert.Equal(1, result.RemindedCount);
        Assert.Equal($"user{LeaderId}@test.local",
            dispatcher.Single(SystemEmailTemplates.LogisticsExpenseReportReminder).To.Email);
    }

    [Fact]
    public async Task An_item_that_is_not_finished_is_not_chased()
    {
        var (db, handler, _, _, dispatcher) = CreateSut();
        db.VisitLogisticsItems.Add(Item(905, AssigneeId, status: "IN_PROGRESS"));
        db.SaveChanges();

        var result = await handler.Handle(Command(), default);

        Assert.Equal(0, result.RemindedCount);
        Assert.Empty(dispatcher.Requests);
        Assert.Empty(db.SentEmails);
    }

    [Fact]
    public async Task Only_the_host_may_send_reminders_and_only_after_the_visit()
    {
        var (db, handler, user, _, _) = CreateSut();
        db.VisitLogisticsItems.Add(Item(906, AssigneeId));
        db.SaveChanges();

        user.UserId = 999;
        await Assert.ThrowsAsync<ForbiddenException>(() => handler.Handle(Command(), default));

        var (beforeDb, beforeHandler, _, _, _) = CreateSut(VisitInstanceStatus.BeforeVisit);
        beforeDb.VisitLogisticsItems.Add(Item(907, AssigneeId));
        beforeDb.SaveChanges();
        await Assert.ThrowsAsync<ForbiddenException>(() => beforeHandler.Handle(Command(), default));

        Assert.Empty(db.SentEmails);
        Assert.Empty(beforeDb.SentEmails);
    }

    [Fact]
    public async Task One_failed_delivery_does_not_stop_the_rest()
    {
        var (db, handler, _, mocks, _) = CreateSut();
        db.VisitLogisticsItems.AddRange(Item(908, AssigneeId), Item(909, AssigneeId));
        db.SaveChanges();
        mocks.FailEmail = true;

        var result = await handler.Handle(Command(), default);

        // Both were attempted and both recorded the truth; the reminder is best-effort and a bad
        // mailbox must not silence the others.
        Assert.Equal(2, result.RemindedCount);
        Assert.Equal(2, db.SentEmails.Count());
        Assert.All(db.SentEmails, e => Assert.Equal("FAILED", e.Status));
    }
}
