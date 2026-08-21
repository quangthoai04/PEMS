using Moq;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Reminders;
using PEMS.Application.Emails.Common;
using PEMS.Application.Notifications.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Enums;
using PEMS.UnitTests.TestInfrastructure;

namespace PEMS.UnitTests.Delegations.Reminders;

/// <summary>
/// C-23 — the scheduled visit reminder.
///
/// <para>
/// Untested before this batch, and it showed: the job passed <c>plannedStartAt</c>, <c>DelegationName</c>
/// and <c>CampusName</c> into templates that declare <c>plannedStart</c>, <c>delegationName</c> and
/// <c>campusName</c>, never passed <c>plannedEnd</c> at all, and rendered with a private regex that
/// leaves an unmatched placeholder in place. The result is that a real recipient read the literal text
/// "{{plannedStart}}", "{{plannedEnd}}" and "{{actionBlock}}" in their reminder. These tests pin the
/// variable contract precisely so that cannot come back.
/// </para>
/// </summary>
public class VisitReminderDispatchServiceTests
{
    private const ulong ParticipantAId = 601;
    private const ulong ParticipantBId = 602;
    private const ulong InvitedOnlyId = 603;
    private const ulong ReminderId = 70;

    private static (DelegationsTestDbContext Db, VisitReminderDispatchService Service,
        DelegationsHandlerMocks Mocks, FakeDelegationsEmailDispatcher Dispatcher) CreateSut()
    {
        var db = DelegationsTestDbContext.Create();
        DelegationsTestData.SeedBase(db);

        db.Departments.Add(DelegationsTestData.CreateDepartment(60, departmentType: "IC"));
        db.Users.AddRange(
            DelegationsTestData.CreateUser(ParticipantAId, DelegationsTestData.StaffRoleId, UserSubRoles.Staff, 60),
            DelegationsTestData.CreateUser(ParticipantBId, DelegationsTestData.StaffRoleId, UserSubRoles.Staff, 60),
            DelegationsTestData.CreateUser(InvitedOnlyId, DelegationsTestData.StaffRoleId, UserSubRoles.Staff, 60));
        db.VisitParticipants.AddRange(
            DelegationsTestData.CreateParticipant(801, ParticipantAId, ParticipantRoles.IcSupport, ParticipantStatuses.Accepted),
            DelegationsTestData.CreateParticipant(802, ParticipantBId, ParticipantRoles.IcSupport, ParticipantStatuses.Assigned),
            DelegationsTestData.CreateParticipant(803, InvitedOnlyId, ParticipantRoles.IcSupport, ParticipantStatuses.Invited));
        db.SaveChanges();

        var mocks = new DelegationsHandlerMocks();
        mocks.Tokens.Setup(t => t.BuildHostVisitProcessUrl(It.IsAny<ulong>()))
            .Returns((ulong inst) => $"https://pems.test/dashboard/visit/process/{inst}");

        var dispatcher = mocks.DispatcherFor(db);
        var service = new VisitReminderDispatchService(
            db, dispatcher, mocks.Clock, mocks.Tokens.Object, mocks.Notifications.Object);

        return (db, service, mocks, dispatcher);
    }

    private static VisitInstanceReminderSetting Reminder(
        VisitReminderTargetGroup target = VisitReminderTargetGroup.HOST,
        VisitReminderChannel channel = VisitReminderChannel.EMAIL,
        VisitReminderStatus status = VisitReminderStatus.PENDING) => new()
    {
        ReminderSettingId = ReminderId,
        VisitInstanceId = DelegationsTestData.VisitInstanceId,
        Channel = channel,
        TargetGroup = target,
        DaysBefore = 1,
        ReminderTime = new TimeSpan(8, 0, 0),
        ScheduledAt = new DateTime(2026, 7, 31, 8, 0, 0),
        Status = status,
        CreatedAt = new DateTime(2026, 6, 1),
    };

    // ── The variable contract, which is where this handler was actually broken ──

    [Fact]
    public async Task The_host_reminder_supplies_exactly_the_five_variables_its_template_declares()
    {
        var (_, service, _, dispatcher) = CreateSut();

        var outcome = await service.DispatchOneAsync(Reminder(), default);

        Assert.True(outcome.Succeeded);
        var sent = dispatcher.Single(SystemEmailTemplates.VisitReminderHost);

        Assert.Equal(
            new[] { "campusName", "delegationName", "hostName", "plannedEnd", "plannedStart" },
            sent.Variables.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray());

        Assert.Equal($"User {DelegationsTestData.HostUserId}", sent.Variables["hostName"]);
        Assert.Equal("Đoàn khách kiểm thử", sent.Variables["delegationName"]);
        Assert.Equal($"Campus {DelegationsTestData.CampusId}", sent.Variables["campusName"]);
        // Both ends of the window, in one format. plannedEnd was never passed before, so the recipient
        // read the literal placeholder.
        Assert.Equal("09:00 01/08/2026", sent.Variables["plannedStart"]);
        Assert.Equal("11:00 01/08/2026", sent.Variables["plannedEnd"]);

        // The detail link is a trusted block built by the backend — never a free template variable.
        Assert.DoesNotContain("detailUrl", sent.Variables.Keys);
        Assert.Contains(EmailTrustedBlocks.ActionBlock, sent.TrustedBlocks!.Keys);
    }

    [Fact]
    public async Task The_participant_reminder_supplies_its_own_five_variables()
    {
        var (_, service, _, dispatcher) = CreateSut();

        await service.DispatchOneAsync(Reminder(VisitReminderTargetGroup.PARTICIPANTS), default);

        var messages = dispatcher.All(SystemEmailTemplates.VisitReminderParticipants);
        Assert.Equal(2, messages.Count);

        Assert.All(messages, m =>
        {
            Assert.Equal(
                new[] { "campusName", "delegationName", "plannedEnd", "plannedStart", "recipientName" },
                m.Variables.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray());
            Assert.DoesNotContain("hostName", m.Variables.Keys);
            Assert.Equal("11:00 01/08/2026", m.Variables["plannedEnd"]);
            Assert.Equal(SystemEmailContent.FromTemplate.Instance, m.Content);
        });
    }

    [Fact]
    public void Neither_reminder_template_is_treated_as_sensitive()
    {
        // A login-required detail link grants nothing on its own, so the whole body is worth keeping.
        Assert.Equal(HistoryBodyPolicy.Full,
            SensitiveEmailHistory.PolicyFor(SystemEmailTemplates.VisitReminderHost));
        Assert.Equal(HistoryBodyPolicy.Full,
            SensitiveEmailHistory.PolicyFor(SystemEmailTemplates.VisitReminderParticipants));
    }

    // ── One message per person, and nobody learns who else is coming ────────

    [Fact]
    public async Task Every_recipient_gets_their_own_message_naming_only_themselves()
    {
        var (db, service, _, dispatcher) = CreateSut();

        await service.DispatchOneAsync(Reminder(VisitReminderTargetGroup.HOST_AND_PARTICIPANTS), default);

        Assert.Equal(3, dispatcher.Requests.Count);
        Assert.All(dispatcher.Requests, r => Assert.NotNull(r.To.Email));

        // Nobody's message mentions anybody else — not a name, not an address, not a count.
        foreach (var request in dispatcher.Requests)
        {
            var mine = request.To.Email;
            var others = dispatcher.Requests.Where(r => r.To.Email != mine).Select(r => r.To.Email);
            foreach (var otherAddress in others)
                Assert.DoesNotContain(otherAddress, string.Join("|", request.Variables.Values));
        }

        // Three separate sent_emails rows, one TO each.
        Assert.Equal(3, db.SentEmails.Count());
        Assert.Equal(3, db.SentEmailRecipients.Count());
        Assert.All(db.SentEmailRecipients, r => Assert.Equal(EmailRecipientTypes.To, r.RecipientType));
        // A reminder mints nothing anybody could act on without logging in.
        Assert.Empty(db.EmailActionTokens);
    }

    [Fact]
    public async Task The_host_is_reminded_as_the_host_even_when_also_a_participant()
    {
        var (db, service, _, dispatcher) = CreateSut();
        // The Host also holds a non-host participant row — a shape the data allows.
        db.VisitParticipants.Add(DelegationsTestData.CreateParticipant(
            804, DelegationsTestData.HostUserId, ParticipantRoles.IcSupport, ParticipantStatuses.Accepted));
        db.SaveChanges();

        await service.DispatchOneAsync(Reminder(VisitReminderTargetGroup.HOST_AND_PARTICIPANTS), default);

        var hostAddress = $"user{DelegationsTestData.HostUserId}@test.local";
        Assert.Single(dispatcher.Requests.Where(r => r.To.Email == hostAddress));
        // …and it is the HOST template, because that is the role the reminder is about.
        Assert.Equal(SystemEmailTemplates.VisitReminderHost,
            dispatcher.Requests.Single(r => r.To.Email == hostAddress).TemplateCode);
    }

    [Fact]
    public async Task One_mailbox_is_never_reminded_twice_for_the_same_reminder()
    {
        var (db, service, _, dispatcher) = CreateSut();
        // Two different accounts, one real person behind them.
        var shared = db.Users.Single(u => u.UserId == ParticipantBId);
        shared.Email = $"user{ParticipantAId}@test.local";
        db.SaveChanges();

        await service.DispatchOneAsync(Reminder(VisitReminderTargetGroup.PARTICIPANTS), default);

        Assert.Single(dispatcher.Requests);
    }

    // ── Eligibility ────────────────────────────────────────────────────────

    [Fact]
    public async Task Only_the_host_is_reminded_when_the_target_group_says_host()
    {
        var (_, service, _, dispatcher) = CreateSut();

        await service.DispatchOneAsync(Reminder(VisitReminderTargetGroup.HOST), default);

        var only = Assert.Single(dispatcher.Requests);
        Assert.Equal($"user{DelegationsTestData.HostUserId}@test.local", only.To.Email);
        Assert.Equal(SystemEmailTemplates.VisitReminderHost, only.TemplateCode);
    }

    [Fact]
    public async Task The_host_is_not_reminded_when_the_target_group_says_participants()
    {
        var (_, service, _, dispatcher) = CreateSut();

        await service.DispatchOneAsync(Reminder(VisitReminderTargetGroup.PARTICIPANTS), default);

        Assert.Equal(2, dispatcher.Requests.Count);
        Assert.DoesNotContain(dispatcher.Requests,
            r => r.To.Email == $"user{DelegationsTestData.HostUserId}@test.local");
        Assert.All(dispatcher.Requests,
            r => Assert.Equal(SystemEmailTemplates.VisitReminderParticipants, r.TemplateCode));
    }

    [Fact]
    public async Task Somebody_who_only_has_an_open_invitation_is_not_reminded()
    {
        var (_, service, _, dispatcher) = CreateSut();

        await service.DispatchOneAsync(Reminder(VisitReminderTargetGroup.PARTICIPANTS), default);

        Assert.DoesNotContain(dispatcher.Requests, r => r.To.Email == $"user{InvitedOnlyId}@test.local");
    }

    [Fact]
    public async Task Participants_of_another_campus_instance_are_never_pulled_in()
    {
        var (db, service, _, dispatcher) = CreateSut();
        db.VisitRequestCampuses.Add(DelegationsTestData.CreateVisitInstance(
            visitInstanceId: 11, campusId: DelegationsTestData.OtherCampusId));
        db.Users.Add(DelegationsTestData.CreateUser(
            604, DelegationsTestData.StaffRoleId, UserSubRoles.Staff, 60,
            campusId: DelegationsTestData.OtherCampusId));
        db.VisitParticipants.Add(DelegationsTestData.CreateParticipant(
            805, 604, ParticipantRoles.IcSupport, ParticipantStatuses.Accepted, visitInstanceId: 11));
        db.SaveChanges();

        await service.DispatchOneAsync(Reminder(VisitReminderTargetGroup.PARTICIPANTS), default);

        Assert.Equal(2, dispatcher.Requests.Count);
        Assert.DoesNotContain(dispatcher.Requests, r => r.To.Email == "user604@test.local");
    }

    [Fact]
    public async Task A_recipient_with_no_address_is_skipped_rather_than_failing_the_reminder()
    {
        var (db, service, _, dispatcher) = CreateSut();
        db.Users.Single(u => u.UserId == ParticipantAId).Email = "   ";
        db.SaveChanges();

        var outcome = await service.DispatchOneAsync(Reminder(VisitReminderTargetGroup.PARTICIPANTS), default);

        Assert.True(outcome.Succeeded);
        var only = Assert.Single(dispatcher.Requests);
        Assert.Equal($"user{ParticipantBId}@test.local", only.To.Email);
    }

    [Fact]
    public async Task A_reminder_with_nobody_to_remind_sends_nothing_and_is_not_a_failure()
    {
        var (db, service, _, dispatcher) = CreateSut();
        db.VisitParticipants.RemoveRange(db.VisitParticipants);
        db.SaveChanges();

        var outcome = await service.DispatchOneAsync(Reminder(VisitReminderTargetGroup.PARTICIPANTS), default);

        // Nothing FAILED — there was no send to fail. But there was nothing to send either, which is
        // its own outcome and must not be mistaken for a delivery.
        Assert.True(outcome.Succeeded);
        Assert.True(outcome.Cancelled);
        Assert.Equal(ReminderCancelReasons.NoEligibleRecipients, outcome.CancelReasonCode);
        Assert.Equal(0, outcome.Messages);
        Assert.Empty(dispatcher.Requests);
        Assert.Empty(db.SentEmails);
    }

    // ── Nobody left to remind, per target group (§7) ────────────────────────
    //
    // These stay at the DispatchOneAsync level on purpose: the claim and the CANCELLED write are
    // ExecuteUpdate statements, which the InMemory provider does not implement, so what the ROW ends
    // up looking like is asserted against a real MySQL row in
    // PEMS.IntegrationTests VisitReminderDispatchIdempotencyTests. What belongs here is the decision —
    // that resolving nobody produces the cancel outcome and touches no provider.

    [Fact]
    public async Task A_host_without_a_usable_address_leaves_nobody_to_remind()
    {
        var (db, service, _, dispatcher) = CreateSut();
        db.Users.Single(u => u.UserId == DelegationsTestData.HostUserId).Email = "   ";
        db.SaveChanges();

        var outcome = await service.DispatchOneAsync(Reminder(VisitReminderTargetGroup.HOST), default);

        Assert.True(outcome.Cancelled);
        Assert.Equal(ReminderCancelReasons.NoEligibleRecipients, outcome.CancelReasonCode);
        Assert.Empty(dispatcher.Requests);
        Assert.Empty(db.SentEmails);
    }

    [Fact]
    public async Task A_participant_who_is_no_longer_accepted_leaves_nobody_to_remind()
    {
        var (db, service, _, dispatcher) = CreateSut();
        foreach (var p in db.VisitParticipants) p.Status = ParticipantStatuses.Declined;
        db.SaveChanges();

        var outcome = await service.DispatchOneAsync(Reminder(VisitReminderTargetGroup.PARTICIPANTS), default);

        Assert.True(outcome.Cancelled);
        Assert.Empty(dispatcher.Requests);
        Assert.Empty(db.SentEmails);
    }

    [Fact]
    public async Task A_reminder_that_still_has_somebody_is_not_cancelled()
    {
        var (_, service, _, dispatcher) = CreateSut();

        var outcome = await service.DispatchOneAsync(
            Reminder(VisitReminderTargetGroup.HOST_AND_PARTICIPANTS), default);

        Assert.False(outcome.Cancelled);
        Assert.Null(outcome.CancelReasonCode);
        Assert.Equal(3, outcome.Messages);
        Assert.Equal(3, dispatcher.Requests.Count);
    }

    /// <summary>The wording an operator and the screen read, pinned so a rename cannot quietly change it.</summary>
    [Fact]
    public void The_cancel_reason_is_recorded_as_a_code_followed_by_a_readable_sentence()
    {
        var recorded = ReminderCancelReasons.Record(ReminderCancelReasons.NoEligibleRecipients);

        Assert.StartsWith("NO_ELIGIBLE_RECIPIENTS:", recorded);
        Assert.Contains("Đã hủy nhắc lịch vì không còn người nhận đủ điều kiện.", recorded);
        Assert.Equal(
            "The reminder was cancelled because no eligible recipients remained.",
            ReminderCancelReasons.NoEligibleRecipientsMessageEn);
    }

    // ── Channels stay separate ─────────────────────────────────────────────

    [Fact]
    public async Task An_in_app_reminder_sends_no_email_and_still_notifies()
    {
        var (db, service, mocks, dispatcher) = CreateSut();

        var outcome = await service.DispatchOneAsync(
            Reminder(VisitReminderTargetGroup.HOST_AND_PARTICIPANTS, VisitReminderChannel.IN_APP), default);

        Assert.True(outcome.Succeeded);
        Assert.Empty(dispatcher.Requests);
        Assert.Empty(db.SentEmails);
        mocks.Notifications.Verify(
            n => n.CreateManyAsync(
                It.Is<IReadOnlyList<CreateNotificationRequest>>(list => list.Count == 3),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // Plan RC-05/RM-01..03: one shared process URL/actionType for every recipient silently walked a
    // non-Host participant into the Host-only operational screen. The Host must get an
    // OPEN_HOST_PROCESS/{id} destination; every other recipient must get their own
    // OPEN_CONTRIBUTION/contribution destination — never the other's.
    [Fact]
    public async Task An_in_app_reminder_routes_the_Host_to_Host_Process_and_everyone_else_to_their_own_contribution_screen()
    {
        var (db, service, mocks, _) = CreateSut();

        await service.DispatchOneAsync(
            Reminder(VisitReminderTargetGroup.HOST_AND_PARTICIPANTS, VisitReminderChannel.IN_APP), default);

        mocks.Notifications.Verify(
            n => n.CreateManyAsync(
                It.Is<IReadOnlyList<CreateNotificationRequest>>(list =>
                    list.Count == 3
                    && list.Single(r => r.RecipientUserId == DelegationsTestData.HostUserId).ActionType
                        == NotificationActionTypes.OpenHostProcess
                    && list.Single(r => r.RecipientUserId == DelegationsTestData.HostUserId).ActionUrl
                        == $"/dashboard/visit/process/{DelegationsTestData.VisitInstanceId}"
                    && list.Where(r => r.RecipientUserId != DelegationsTestData.HostUserId).All(r =>
                        r.ActionType == NotificationActionTypes.OpenContribution
                        && r.ActionUrl == $"/dashboard/visit/contribution/{DelegationsTestData.VisitInstanceId}")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task An_email_reminder_creates_no_in_app_notification()
    {
        var (_, service, mocks, _) = CreateSut();

        await service.DispatchOneAsync(Reminder(VisitReminderTargetGroup.HOST), default);

        mocks.Notifications.Verify(
            n => n.CreateManyAsync(
                It.IsAny<IReadOnlyList<CreateNotificationRequest>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── Delivery outcome ───────────────────────────────────────────────────

    [Fact]
    public async Task A_failed_send_is_reported_without_repeating_the_provider_message()
    {
        var (db, service, mocks, _) = CreateSut();
        mocks.FailEmail = true;

        var outcome = await service.DispatchOneAsync(
            Reminder(VisitReminderTargetGroup.HOST_AND_PARTICIPANTS), default);

        Assert.False(outcome.Succeeded);
        Assert.Equal("3/3 email nhắc lịch không gửi được.", outcome.SafeError);
        Assert.DoesNotContain("SMTP", outcome.SafeError!);
        Assert.All(db.SentEmails, e => Assert.Equal("FAILED", e.Status));
    }

    [Fact]
    public async Task A_missing_visit_instance_is_reported_rather_than_guessed_at()
    {
        var (_, service, _, dispatcher) = CreateSut();
        var orphan = Reminder();
        orphan.VisitInstanceId = 9999;

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.DispatchOneAsync(orphan, default));
        Assert.Empty(dispatcher.Requests);
    }
}
