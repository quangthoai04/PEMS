using PEMS.Application.Common.Exceptions;
using PEMS.Application.Delegations.Commands.SaveVisitInstanceReminderSettings;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Enums;
using PEMS.UnitTests.TestInfrastructure;

namespace PEMS.UnitTests.Delegations.Reminders;

/// <summary>
/// 2026-08-27 — "Nhắc nhở chuyến thăm" redesign replaced days_before + reminder_time (a calendar-day
/// count plus an unrelated fixed clock time) with a single offsetMinutes duration, so
/// scheduled_at = planned_start_at - offsetMinutes is a real subtraction: "1 ngày trước" is always the
/// same time of day as the visit, and sub-day offsets (30 phút, 1 giờ, 2 giờ...) are representable at
/// all, which the old fields could not do. These tests pin that formula and the surrounding guards the
/// UI redesign must not silently break.
/// </summary>
public class SaveVisitInstanceReminderSettingsCommandHandlerTests
{
    // DelegationsTestData.CreateVisitInstance seeds PlannedStartAt = 2026-08-01 09:00:00 (Vietnam
    // wall-clock), and DelegationsHandlerMocks.Clock defaults VietnamNow to 2026-07-12 15:00:00 — well
    // before the visit, so every offset used below resolves to a future, valid scheduled_at.

    private static (DelegationsTestDbContext Db, SaveVisitInstanceReminderSettingsCommandHandler Handler,
        FakeDelegationsCurrentUser User, FakeDateTimeService Clock) CreateSut()
    {
        var db = DelegationsTestDbContext.Create();
        DelegationsTestData.SeedBase(db);

        var mocks = new DelegationsHandlerMocks();
        var user = new FakeDelegationsCurrentUser(); // defaults to the seeded Host
        var handler = new SaveVisitInstanceReminderSettingsCommandHandler(db, user, mocks.Clock);
        return (db, handler, user, mocks.Clock);
    }

    private static SaveVisitInstanceReminderSettingsCommand Command(
        int offsetMinutes, bool enabled = true,
        string channel = "IN_APP", string targetGroup = "HOST") =>
        new(DelegationsTestData.VisitInstanceId,
            new() { new SaveVisitReminderSettingItem(channel, targetGroup, offsetMinutes, enabled) });

    [Fact]
    public async Task Thirty_minutes_before_subtracts_exactly_thirty_minutes_from_the_visit_start()
    {
        var (db, handler, _, _) = CreateSut();

        await handler.Handle(Command(30), default);

        var row = Assert.Single(db.ReminderSettings);
        Assert.Equal(30, row.OffsetMinutes);
        Assert.Equal(new DateTime(2026, 8, 1, 8, 30, 0), row.ScheduledAt);
    }

    [Fact]
    public async Task One_day_before_lands_on_the_same_time_of_day_as_the_visit()
    {
        var (db, handler, _, _) = CreateSut();

        await handler.Handle(Command(24 * 60), default);

        var row = Assert.Single(db.ReminderSettings);
        // Same 09:00 the visit itself starts at — the old days_before + reminder_time formula could
        // only guarantee this by coincidence (reminder_time had to be hand-set to match).
        Assert.Equal(new DateTime(2026, 7, 31, 9, 0, 0), row.ScheduledAt);
    }

    [Fact]
    public async Task Rejects_an_offset_whose_computed_moment_has_already_passed()
    {
        var (_, handler, _, clock) = CreateSut();
        // Move "now" to just after the moment a 1-day-before reminder would have fired.
        clock.UtcNow = new DateTime(2026, 7, 31, 9, 1, 0).AddHours(-7);

        await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(Command(24 * 60), default));
    }

    [Fact]
    public async Task Disabling_cancels_only_the_matching_PENDING_row_and_leaves_SENT_rows_alone()
    {
        var (db, handler, _, _) = CreateSut();
        db.ReminderSettings.AddRange(
            new VisitInstanceReminderSetting
            {
                ReminderSettingId = 1, VisitInstanceId = DelegationsTestData.VisitInstanceId,
                Channel = VisitReminderChannel.IN_APP, TargetGroup = VisitReminderTargetGroup.HOST,
                OffsetMinutes = 60, ScheduledAt = new DateTime(2026, 8, 1, 8, 0, 0),
                Status = VisitReminderStatus.PENDING, CreatedAt = new DateTime(2026, 6, 1),
            },
            new VisitInstanceReminderSetting
            {
                ReminderSettingId = 2, VisitInstanceId = DelegationsTestData.VisitInstanceId,
                Channel = VisitReminderChannel.EMAIL, TargetGroup = VisitReminderTargetGroup.HOST,
                OffsetMinutes = 120, ScheduledAt = new DateTime(2026, 7, 1, 8, 0, 0),
                Status = VisitReminderStatus.SENT, CreatedAt = new DateTime(2026, 6, 1),
            });
        db.SaveChanges();

        await handler.Handle(
            new SaveVisitInstanceReminderSettingsCommand(DelegationsTestData.VisitInstanceId, new()
            {
                new SaveVisitReminderSettingItem("IN_APP", "HOST", 60, Enabled: false),
                new SaveVisitReminderSettingItem("EMAIL", "HOST", 999, Enabled: false),
            }),
            default);

        var inApp = db.ReminderSettings.Single(r => r.ReminderSettingId == 1);
        var email = db.ReminderSettings.Single(r => r.ReminderSettingId == 2);
        Assert.Equal(VisitReminderStatus.CANCELLED, inApp.Status);
        // A SENT row is never touched, regardless of what the (ignored) offset on a disabled item says.
        Assert.Equal(VisitReminderStatus.SENT, email.Status);
        Assert.Equal(120, email.OffsetMinutes);
    }

    [Fact]
    public async Task Saving_again_re_arms_the_pending_row_with_the_new_offset()
    {
        var (db, handler, _, _) = CreateSut();
        await handler.Handle(Command(60), default);

        await handler.Handle(Command(120), default);

        var row = Assert.Single(db.ReminderSettings);
        Assert.Equal(120, row.OffsetMinutes);
        Assert.Equal(new DateTime(2026, 8, 1, 7, 0, 0), row.ScheduledAt);
        Assert.Equal(VisitReminderStatus.PENDING, row.Status);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(31 * 24 * 60 + 1)]
    public void Validator_rejects_offsets_outside_1_minute_to_31_days(int offsetMinutes)
    {
        var validator = new SaveVisitInstanceReminderSettingsCommandValidator();

        var result = validator.Validate(Command(offsetMinutes));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validator_rejects_duplicate_channel_and_target_group_pairs()
    {
        var validator = new SaveVisitInstanceReminderSettingsCommandValidator();
        var command = new SaveVisitInstanceReminderSettingsCommand(DelegationsTestData.VisitInstanceId, new()
        {
            new SaveVisitReminderSettingItem("IN_APP", "HOST", 60, true),
            new SaveVisitReminderSettingItem("IN_APP", "HOST", 120, true),
        });

        var result = validator.Validate(command);

        Assert.False(result.IsValid);
    }
}
