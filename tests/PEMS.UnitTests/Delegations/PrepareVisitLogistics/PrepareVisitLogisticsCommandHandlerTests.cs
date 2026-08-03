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
            "2026-08-01T08:00", "2026-08-01T12:00");

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

        // Exactly the eight the template declares — no more, no fewer. A missing one is a fail-closed
        // render error now, not a silent "Chưa có thông tin" in front of the department leader.
        Assert.Equal(
            new[]
            {
                "departmentLeaderName", "logisticsDescription", "logisticsItemType", "logisticsTitle",
                "quantity", "requesterName", "usageEndAt", "usageStartAt",
            },
            sent.Variables.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray());

        Assert.Equal($"User {LeaderId}", sent.Variables["departmentLeaderName"]);
        Assert.Equal("Welcome LED", sent.Variables["logisticsTitle"]);
        Assert.Equal("Màn hình LED", sent.Variables["logisticsItemType"]);
        Assert.Equal("1", sent.Variables["quantity"]);
        Assert.Equal("08:00 01/08/2026", sent.Variables["usageStartAt"]);
        Assert.Equal("12:00 01/08/2026", sent.Variables["usageEndAt"]);
    }

    /// <summary>
    /// The two variables this message must NOT carry, named individually so a reinstatement fails with
    /// the reason rather than as an off-by-one on the key count.
    /// </summary>
    [Fact]
    public async Task The_request_carries_no_response_deadline_and_no_coordination_note()
    {
        var (db, handler, _, _, dispatcher) = CreateSut();

        var response = await handler.Handle(SystemRequest(), default);

        var vars = dispatcher.Single(SystemEmailTemplates.LogisticsRequestToDepartment).Variables;
        // The Host sets no deadline, so the department is told of none. "dueAt" reappearing here means
        // somebody put "Hạn phản hồi" back into a message whose form has no such field.
        Assert.DoesNotContain("dueAt", vars.Keys);
        // A different column (offline_coordination_note) that this flow can never populate: an
        // OFFLINE_COORDINATED item is recorded DONE and sends no email at all.
        Assert.DoesNotContain("coordinationNote", vars.Keys);

        // The COLUMN is untouched — the server still derives it for its own scheduling (usage start
        // minus 24h). Not showing a value and not having one are different things.
        var item = db.VisitLogisticsItems.Single(l => l.LogisticsItemId == response.LogisticsItemId);
        Assert.Equal(new DateTime(2026, 7, 31, 8, 0, 0), item.DueAt);
    }

    [Fact]
    public async Task The_caller_decides_what_a_missing_value_reads_as_not_the_renderer()
    {
        var (_, handler, _, _, dispatcher) = CreateSut();

        // No description, no quantity, no usage window.
        await handler.Handle(
            new PrepareVisitLogisticsCommand(
                DelegationsTestData.VisitInstanceId, DeptId, "OTHER", "Việc khác",
                Description: null, Quantity: null, UsageStartAt: null, UsageEndAt: null),
            default);

        var vars = dispatcher.Single(SystemEmailTemplates.LogisticsRequestToDepartment).Variables;
        Assert.Equal("Chưa có mô tả chi tiết.", vars["logisticsDescription"]);
        Assert.Equal("Chưa nhập", vars["quantity"]);
        Assert.Equal("Chưa chọn thời gian", vars["usageStartAt"]);
    }

    /// <summary>
    /// A description of only whitespace is the same thing as none — it must reach the empty wording,
    /// not print a heading over a blank line.
    /// </summary>
    [Theory]
    [InlineData("   ")]
    [InlineData("\n\n")]
    [InlineData("\t \r\n ")]
    public async Task A_whitespace_only_description_is_treated_as_absent(string blank)
    {
        var (db, handler, _, _, dispatcher) = CreateSut();

        var response = await handler.Handle(
            new PrepareVisitLogisticsCommand(
                DelegationsTestData.VisitInstanceId, DeptId, "OTHER", "Việc khác",
                Description: blank, Quantity: null, UsageStartAt: null, UsageEndAt: null),
            default);

        Assert.Null(db.VisitLogisticsItems.Single(l => l.LogisticsItemId == response.LogisticsItemId).Description);
        Assert.Equal(
            "Chưa có mô tả chi tiết.",
            dispatcher.Single(SystemEmailTemplates.LogisticsRequestToDepartment).Variables["logisticsDescription"]);
    }

    [Fact]
    public async Task The_hosts_detailed_description_reaches_the_department_under_its_own_name()
    {
        var (db, handler, _, _, dispatcher) = CreateSut();

        const string description = "Cần bật từ 7h30, nội dung do IC gửi sau.";
        var response = await handler.Handle(
            new PrepareVisitLogisticsCommand(
                DelegationsTestData.VisitInstanceId, DeptId, "LED", "Màn LED sảnh",
                Description: description, Quantity: 2,
                UsageStartAt: "2026-08-01T08:00", UsageEndAt: "2026-08-01T12:00"),
            default);

        // Stored verbatim, Vietnamese diacritics intact...
        Assert.Equal(
            description,
            db.VisitLogisticsItems.Single(l => l.LogisticsItemId == response.LogisticsItemId).Description);
        // ...and mailed under logisticsDescription, not smuggled through coordinationNote.
        var vars = dispatcher.Single(SystemEmailTemplates.LogisticsRequestToDepartment).Variables;
        Assert.Equal(description, vars["logisticsDescription"]);
    }

    /// <summary>
    /// A description written as several instructions stays several instructions: the handler trims the
    /// ends and touches nothing in between. (Turning the interior newlines into line breaks is the HTML
    /// renderer's job and is covered by its own tests.)
    /// </summary>
    [Fact]
    public async Task A_multi_line_description_keeps_its_line_breaks_and_is_trimmed_only_at_the_ends()
    {
        var (db, handler, _, _, dispatcher) = CreateSut();

        const string body = "Chuẩn bị teabreak cho 20 khách, gồm trà, cà phê,\nnước suối và bánh ngọt.\n\nBố trí trước giờ họp 15 phút.";
        var response = await handler.Handle(
            new PrepareVisitLogisticsCommand(
                DelegationsTestData.VisitInstanceId, DeptId, "MEAL", "Teabreak",
                Description: "  \n" + body + "  \n ", Quantity: 20,
                UsageStartAt: "2026-08-01T08:00", UsageEndAt: "2026-08-01T12:00"),
            default);

        Assert.Equal(
            body,
            db.VisitLogisticsItems.Single(l => l.LogisticsItemId == response.LogisticsItemId).Description);
        Assert.Equal(
            body,
            dispatcher.Single(SystemEmailTemplates.LogisticsRequestToDepartment).Variables["logisticsDescription"]);
    }

    /// <summary>
    /// Two items on one visit are two separate requests. This is the failure mode the "one active item
    /// per category" guard makes easy to miss: the second send must describe the SECOND item, not
    /// re-send the first one's content under a new title.
    /// </summary>
    [Fact]
    public async Task Two_items_on_the_same_visit_never_borrow_each_others_description()
    {
        var (db, handler, _, _, dispatcher) = CreateSut();

        await handler.Handle(
            new PrepareVisitLogisticsCommand(
                DelegationsTestData.VisitInstanceId, DeptId, "MEAL", "Teabreak",
                Description: "Teabreak cho 20 khách, có suất chay.", Quantity: 20,
                UsageStartAt: "2026-08-01T08:00", UsageEndAt: "2026-08-01T09:00"),
            default);
        await handler.Handle(
            new PrepareVisitLogisticsCommand(
                DelegationsTestData.VisitInstanceId, DeptId, "ROOM", "Phòng họp",
                Description: "Phòng 30 chỗ, hai micro, máy chiếu.", Quantity: 1,
                UsageStartAt: "2026-08-01T09:00", UsageEndAt: "2026-08-01T12:00"),
            default);

        var sends = dispatcher.All(SystemEmailTemplates.LogisticsRequestToDepartment).ToList();
        Assert.Equal(2, sends.Count);

        var byTitle = sends.ToDictionary(s => s.Variables["logisticsTitle"], s => s.Variables);
        Assert.Equal("Teabreak cho 20 khách, có suất chay.", byTitle["Teabreak"]["logisticsDescription"]);
        Assert.Equal("Suất ăn / Teabreak", byTitle["Teabreak"]["logisticsItemType"]);
        Assert.Equal("Phòng 30 chỗ, hai micro, máy chiếu.", byTitle["Phòng họp"]["logisticsDescription"]);
        Assert.Equal("Phòng / Hội trường", byTitle["Phòng họp"]["logisticsItemType"]);

        // And the same separation in the rows themselves. Materialised first: the ordinal comparer has
        // no provider translation, and sorting is this assertion's business, not the query's.
        Assert.Equal(
            new[] { "Phòng 30 chỗ, hai micro, máy chiếu.", "Teabreak cho 20 khách, có suất chay." },
            db.VisitLogisticsItems.Select(l => l.Description).ToList()
                .OrderBy(d => d, StringComparer.Ordinal).ToArray());
    }

    /// <summary>
    /// The department reads a category, not a column value. "Loại: MEAL" is what this replaced — the
    /// screen the request was created on has always said "Suất ăn / Teabreak".
    /// </summary>
    [Theory]
    [InlineData("ROOM", "Phòng / Hội trường")]
    [InlineData("TRANSPORT", "Xe / Di chuyển")]
    [InlineData("MEAL", "Suất ăn / Teabreak")]
    [InlineData("EQUIPMENT", "Thiết bị")]
    [InlineData("BANNER", "Banner / Standee")]
    [InlineData("LED", "Màn hình LED")]
    [InlineData("OTHER", "Yêu cầu khác")]
    public async Task Every_item_type_is_mailed_as_a_label_never_as_its_code(string itemType, string expectedLabel)
    {
        var (_, handler, _, _, dispatcher) = CreateSut();

        await handler.Handle(
            new PrepareVisitLogisticsCommand(
                DelegationsTestData.VisitInstanceId, DeptId, itemType, $"Hạng mục {itemType}",
                Description: "Nội dung công việc.", Quantity: 1,
                UsageStartAt: "2026-08-01T08:00", UsageEndAt: "2026-08-01T12:00"),
            default);

        var vars = dispatcher.Single(SystemEmailTemplates.LogisticsRequestToDepartment).Variables;
        Assert.Equal(expectedLabel, vars["logisticsItemType"]);
        Assert.NotEqual(itemType, vars["logisticsItemType"]);
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
                null, null, null, null,
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
