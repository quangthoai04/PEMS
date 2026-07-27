using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Commands.PrepareVisitLogistics;
using PEMS.Application.Emails.Common;
using PEMS.Domain.Constants;
using PEMS.Shared;
using PEMS.UnitTests.TestInfrastructure;

namespace PEMS.UnitTests.Delegations.PrepareVisitLogistics;

/// <summary>
/// Regression guard for the invitation/logistics capability split: a system logistics request MUST
/// still be created when the department's active leader already holds a participant slot
/// (INVITED/ACCEPTED/ASSIGNED) — the two businesses are independent. Department scope + active
/// leader remain revalidated server-side.
/// </summary>
public class PrepareVisitLogisticsCommandHandlerTests
{
    private const ulong DeptId = 20;
    private const ulong LeaderId = 200;

    private static (DelegationsTestDbContext Db, PrepareVisitLogisticsCommandHandler Handler,
        FakeDelegationsCurrentUser User, DelegationsHandlerMocks Mocks,
        FakeDelegationsEmailDispatcher Dispatcher) CreateSut()
    {
        var db = DelegationsTestDbContext.Create();
        DelegationsTestData.SeedBase(db);
        db.Departments.Add(DelegationsTestData.CreateDepartment(DeptId, headUserId: LeaderId));
        db.Users.Add(DelegationsTestData.CreateUser(LeaderId, DelegationsTestData.DepartmentRoleId, UserSubRoles.Leader, DeptId));
        db.SaveChanges();

        var user = new FakeDelegationsCurrentUser();
        var mocks = new DelegationsHandlerMocks();
        var dispatcher = mocks.DispatcherFor(db);
        var handler = new PrepareVisitLogisticsCommandHandler(
            db, user, mocks.Clock, dispatcher, mocks.Tokens.Object, mocks.Sanitizer.Object,
            mocks.Storage.Object, mocks.Normalizer.Object, mocks.Notifications.Object);
        return (db, handler, user, mocks, dispatcher);
    }

    private static PrepareVisitLogisticsCommand SystemRequest(ulong? departmentId = DeptId, string title = "Welcome LED") =>
        new(DelegationsTestData.VisitInstanceId, departmentId, "LED", title, null, 1,
            "2026-08-01T08:00", "2026-08-01T12:00", "MEDIUM", null);

    [Theory]
    [InlineData(ParticipantStatuses.Invited)]
    [InlineData(ParticipantStatuses.Accepted)]
    [InlineData(ParticipantStatuses.Assigned)]
    public async Task LeaderAlreadyAParticipant_NeverBlocksTheSystemLogisticsRequest(string participantStatus)
    {
        var (db, handler, _, mocks, _) = CreateSut();
        db.VisitParticipants.Add(DelegationsTestData.CreateParticipant(500, LeaderId, ParticipantRoles.DeptSupport, participantStatus));
        db.SaveChanges();

        var response = await handler.Handle(SystemRequest(), default);

        Assert.True(response.BusinessCreated);
        var item = Assert.Single(db.VisitLogisticsItems);
        Assert.Equal(LogisticsItemStatus.Requested, item.Status);
        Assert.Equal(DeptId, item.RequestedToDepartmentId);
        Assert.Equal(new DateTime(2026, 8, 1, 8, 0, 0), item.UsageStartAt);
        Assert.Equal(new DateTime(2026, 8, 1, 12, 0, 0), item.UsageEndAt);
        // The email still goes to the department's active leader.
        var mail = Assert.Single(mocks.SentEmails);
        Assert.Equal($"user{LeaderId}@test.local", Assert.Single(mail.To).Email);
    }

    [Fact]
    public async Task NoActiveLeader_RejectsTheSystemRequest()
    {
        var (db, handler, _, _, _) = CreateSut();
        db.Departments.Add(DelegationsTestData.CreateDepartment(21)); // GENERAL, no leader
        db.SaveChanges();

        await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(SystemRequest(departmentId: 21), default));
        Assert.Empty(db.VisitLogisticsItems);
    }

    [Fact]
    public async Task DepartmentOutsideScope_IsRejected()
    {
        var (db, handler, _, _, _) = CreateSut();
        db.Departments.AddRange(
            DelegationsTestData.CreateDepartment(22, campusId: DelegationsTestData.OtherCampusId),
            DelegationsTestData.CreateDepartment(23, departmentType: "IC"),
            DelegationsTestData.CreateDepartment(24, status: EntityStatuses.Inactive));
        db.SaveChanges();

        foreach (var invalidDeptId in new ulong[] { 22, 23, 24 })
        {
            await Assert.ThrowsAsync<ConflictException>(() =>
                handler.Handle(SystemRequest(departmentId: invalidDeptId, title: $"LED {invalidDeptId}"), default));
        }
        Assert.Empty(db.VisitLogisticsItems);
    }

    [Fact]
    public async Task NonHostActor_IsForbidden()
    {
        var (_, handler, user, _, _) = CreateSut();
        user.UserId = 999;

        await Assert.ThrowsAsync<ForbiddenException>(() => handler.Handle(SystemRequest(), default));
    }

    // ── Batch 7: the request email now comes from the template, in full ──────

    [Fact]
    public async Task The_request_names_its_template_and_supplies_every_declared_variable()
    {
        var (db, handler, _, _, dispatcher) = CreateSut();

        await handler.Handle(SystemRequest(), default);

        var sent = dispatcher.Single(SystemEmailTemplates.LogisticsRequestToDepartment);
        Assert.Equal(SystemEmailContent.FromTemplate.Instance, sent.Content);
        Assert.Equal($"user{LeaderId}@test.local", sent.To.Email);

        // Exactly the nine the template declares — no more, no fewer. A missing one is a fail-closed
        // render error now, not a silent "Chưa có thông tin" in front of the department leader.
        Assert.Equal(
            new[]
            {
                "coordinationNote", "departmentLeaderName", "dueAt", "logisticsItemType", "logisticsTitle",
                "quantity", "requesterName", "usageEndAt", "usageStartAt",
            },
            sent.Variables.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray());

        Assert.Equal($"User {LeaderId}", sent.Variables["departmentLeaderName"]);
        Assert.Equal("Welcome LED", sent.Variables["logisticsTitle"]);
        Assert.Equal("LED", sent.Variables["logisticsItemType"]);
        Assert.Equal("1", sent.Variables["quantity"]);
        Assert.Equal("08:00 01/08/2026", sent.Variables["usageStartAt"]);
        Assert.Equal("12:00 01/08/2026", sent.Variables["usageEndAt"]);
    }

    [Fact]
    public async Task The_caller_decides_what_a_missing_value_reads_as_not_the_renderer()
    {
        var (_, handler, _, _, dispatcher) = CreateSut();

        // No description, no quantity, no usage window, no due date.
        await handler.Handle(
            new PrepareVisitLogisticsCommand(
                DelegationsTestData.VisitInstanceId, DeptId, "OTHER", "Việc khác",
                Description: null, Quantity: null, UsageStartAt: null, UsageEndAt: null,
                Priority: "MEDIUM", DueAt: null),
            default);

        var vars = dispatcher.Single(SystemEmailTemplates.LogisticsRequestToDepartment).Variables;
        Assert.Equal("Không có ghi chú phối hợp.", vars["coordinationNote"]);
        Assert.Equal("Chưa nhập", vars["quantity"]);
        Assert.Equal("Chưa chọn thời gian", vars["usageStartAt"]);
        Assert.Equal("Chưa đặt hạn", vars["dueAt"]);
    }

    [Fact]
    public async Task The_hosts_description_is_what_the_department_reads_as_the_coordination_note()
    {
        var (_, handler, _, _, dispatcher) = CreateSut();

        await handler.Handle(
            new PrepareVisitLogisticsCommand(
                DelegationsTestData.VisitInstanceId, DeptId, "LED", "Màn LED sảnh",
                Description: "Cần bật từ 7h30, nội dung do IC gửi sau.", Quantity: 2,
                UsageStartAt: "2026-08-01T08:00", UsageEndAt: "2026-08-01T12:00",
                Priority: "HIGH", DueAt: "2026-07-30T17:00"),
            default);

        var vars = dispatcher.Single(SystemEmailTemplates.LogisticsRequestToDepartment).Variables;
        Assert.Equal("Cần bật từ 7h30, nội dung do IC gửi sau.", vars["coordinationNote"]);
        Assert.Equal("17:00 30/07/2026", vars["dueAt"]);
    }

    [Fact]
    public async Task The_three_response_buttons_are_built_by_the_backend_and_bound_to_the_message()
    {
        var (db, handler, _, _, dispatcher) = CreateSut();

        var response = await handler.Handle(SystemRequest(), default);

        var block = dispatcher.Single(SystemEmailTemplates.LogisticsRequestToDepartment)
            .TrustedBlocks![EmailTrustedBlocks.ActionBlock];
        Assert.Contains("https://pems.test/email-actions/raw-token-1", block);
        Assert.Contains("https://pems.test/email-actions/raw-token-2", block);
        Assert.Contains($"https://pems.test/logistics/{response.LogisticsItemId}", block);

        var sentEmail = Assert.Single(db.SentEmails);
        var tokens = db.EmailActionTokens.Where(t => t.TargetId == response.LogisticsItemId).ToList();
        Assert.Equal(2, tokens.Count);
        Assert.All(tokens, t =>
        {
            Assert.Equal(sentEmail.SentEmailId, t.SentEmailId);
            Assert.Equal(EmailActionContexts.LogisticsRequestResponse, t.ActionContext);
            Assert.Equal(EmailActionTargetTypes.LogisticsItem, t.TargetType);
            Assert.Equal($"user{LeaderId}@test.local", t.RecipientEmail);
        });
    }

    [Fact]
    public async Task An_offline_coordinated_request_still_sends_nothing()
    {
        var (db, handler, _, mocks, dispatcher) = CreateSut();

        var response = await handler.Handle(
            new PrepareVisitLogisticsCommand(
                DelegationsTestData.VisitInstanceId, DeptId, "LED", "Đã trao đổi ngoài",
                null, null, null, null, "MEDIUM", null,
                CoordinationMode: LogisticsCoordinationModes.OfflineCoordinated,
                OfflineCoordinationNote: "Đã gọi điện thống nhất với phòng."),
            default);

        Assert.Equal("SKIPPED", response.EmailStatus);
        Assert.Empty(dispatcher.Requests);
        Assert.Empty(mocks.SentEmails);
        Assert.Empty(db.SentEmails);
        Assert.Empty(db.EmailActionTokens);
        Assert.Equal(LogisticsItemStatus.Done, Assert.Single(db.VisitLogisticsItems).Status);
    }

    [Fact]
    public async Task A_host_edit_is_a_named_content_mode_and_keeps_the_buttons()
    {
        var (_, handler, _, _, dispatcher) = CreateSut();

        await handler.Handle(
            new PrepareVisitLogisticsCommand(
                DelegationsTestData.VisitInstanceId, DeptId, "LED", "Welcome LED",
                null, 1, "2026-08-01T08:00", "2026-08-01T12:00", "MEDIUM", null,
                EmailOverride: new EmailOverride(
                    UseEditedContent: true,
                    Subject: "Nhờ phòng hỗ trợ màn LED sảnh A",
                    BodyHtml: "<p>Nhờ phòng chuẩn bị giúp màn LED sảnh A trước 7h30.</p>")),
            default);

        var sent = dispatcher.Single(SystemEmailTemplates.LogisticsRequestToDepartment);
        var authored = Assert.IsType<SystemEmailContent.AuthoredByUser>(sent.Content);
        Assert.Equal("Nhờ phòng hỗ trợ màn LED sảnh A", authored.Subject);
        Assert.DoesNotContain(EmailComposition.ActionBlockStart, authored.BodyHtml);
        Assert.Contains(EmailTrustedBlocks.ActionBlock, sent.TrustedBlocks!.Keys);
        // Editing the words does not change which template this is, nor who receives it.
        Assert.Equal($"user{LeaderId}@test.local", sent.To.Email);
    }

    [Fact]
    public async Task A_host_may_not_hand_write_the_action_block()
    {
        var (db, handler, _, _, _) = CreateSut();

        var ex = await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(
            new PrepareVisitLogisticsCommand(
                DelegationsTestData.VisitInstanceId, DeptId, "LED", "Welcome LED",
                null, 1, null, null, "MEDIUM", null,
                EmailOverride: new EmailOverride(
                    UseEditedContent: true, Subject: "Nhờ phòng",
                    BodyHtml: "<!-- PEMS_ACTION_BLOCK_START --><p>giả</p><!-- PEMS_ACTION_BLOCK_END -->")),
            default));

        Assert.Equal(EmailErrorCodes.AuthoredActionBlockForbidden, ex.ErrorCode);
        // Refused before the logistics item is created: nothing at all was written.
        Assert.Empty(db.VisitLogisticsItems);
        Assert.Empty(db.SentEmails);
    }

    [Fact]
    public async Task An_email_failure_leaves_the_request_in_place()
    {
        var (db, handler, _, mocks, _) = CreateSut();
        mocks.FailEmail = true;

        var response = await handler.Handle(SystemRequest(), default);

        Assert.Equal("FAILED", response.EmailStatus);
        Assert.Equal(LogisticsItemStatus.Requested, Assert.Single(db.VisitLogisticsItems).Status);
        Assert.Equal("FAILED", Assert.Single(db.SentEmails).Status);
        Assert.Equal(2, db.EmailActionTokens.Count());
    }
}
