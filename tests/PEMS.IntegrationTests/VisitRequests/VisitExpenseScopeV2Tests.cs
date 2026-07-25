using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PEMS.Application.Common.DTOs;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Options;
using PEMS.Application.Delegations.Commands.ApproveCampusInstance;
using PEMS.Application.Delegations.Commands.CreateVisitRequestV2;
using PEMS.Application.Delegations.Services;
using PEMS.Application.Delegations.Services.VisitFormRead;
using PEMS.Application.Delegations.VisitExpenses.Commands.GetOrCreateGeneralExpenseReport;
using PEMS.Application.Delegations.VisitExpenses.Commands.SaveExpenseReport;
using PEMS.Application.Delegations.VisitExpenses.Queries.GetVisitInstanceExpenseSummary;
using PEMS.Application.Notifications.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Infrastructure.Persistence;
using PEMS.Infrastructure.Services;
using Xunit;

namespace PEMS.IntegrationTests.VisitRequests;

/// <summary>
/// Phase 5B — the general (Host) expense report is owned by ONE campus instance.
///
/// The report's total is a database-generated column: the application never writes it, so it must always
/// equal the sum of quantity × unit_price of the surviving items as they are added, edited and removed.
/// And a Host may only touch the report of the instance they host — a Host of a sibling campus of the same
/// request must be refused, never allowed to edit costs that are not theirs.
/// </summary>
public sealed class VisitExpenseScopeV2Tests
{
    private static string ConnString => TestInfrastructure.DisposableDatabaseManager.GetDisposableConnectionString(
        "server=localhost;port=3306;database=pems_pr3_test;user=root;password=123456;AllowUserVariables=True;GuidFormat=None");

    private const ulong Registrant = 8;
    private const ulong LeaderHn = 3;
    private const ulong LeaderHcm = 9;
    private const ulong HostHn = 101;
    private const ulong HostHcm = 103;
    private const ulong CampusHn = 1;
    private const ulong CampusHcm = 2;

    private static bool? _dbUp;
    private static readonly DateTime Now = DateTime.Now;

    private static ApplicationDbContext NewContext()
        => new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseMySql(ConnString, ServerVersion.AutoDetect(ConnString)).Options);

    private static void RequireDb()
    {
        if (_dbUp is null)
        {
            try { using var db = NewContext(); _dbUp = db.Database.CanConnect(); }
            catch { _dbUp = false; }
        }
        Assert.True(_dbUp!.Value, "pems_pr3_test is not reachable.");
    }

    private sealed class FakeUser : ICurrentUserService
    {
        public FakeUser(ulong id, string roleCode, string? subRole = null, ulong? campusId = null)
        { UserId = id; RoleCode = roleCode; SubRole = subRole; PrimaryCampusId = campusId; }
        public bool IsAuthenticated => true;
        public ulong? UserId { get; }
        public string? Email => null;
        public ulong? RoleId => null;
        public string? RoleCode { get; }
        public string? SubRole { get; }
        public ulong? PrimaryCampusId { get; }
        public ulong? DepartmentId => null;
        public ulong? SessionId => null;
        public string? LoginPortal => null;
    }

    private sealed class FixedClock : IDateTimeService
    {
        public DateTime UtcNow => DateTime.UtcNow;
        public DateTime VietnamNow => Now;
    }

    private sealed class SilentNotifications : INotificationService
    {
        public Task CreateManyAsync(IEnumerable<CreateNotificationRequest> r, CancellationToken ct) => Task.CompletedTask;
        public Task CreateManyAsync(IEnumerable<CreateNotificationItem> i, CancellationToken ct) => Task.CompletedTask;
        public Task CreateAsync(ulong u, string t, string? m, string n, string? rt, ulong? ri, CancellationToken ct) => Task.CompletedTask;
        public Task CreateAsync(CreateNotificationRequest r, CancellationToken ct) => Task.CompletedTask;
    }

    private static readonly PerCampusFormV2Options ReadOn = new() { Enabled = true };
    private static readonly PerCampusFormV2WriteOptions WriteOn = new() { Enabled = true };

    private static CampusVisitFormDto Campus(string code, DateTime start, string delegationName)
        => new(code, start, start.AddMinutes(120), delegationName, "MEETING", null,
            $"Mục đích {delegationName}", $"Nội dung {delegationName}",
            new List<VisitorDto> { new($"Khách {delegationName}", "VN", "Guest", "GuestOrg") },
            new List<SupportTeamMemberDto>(),
            new ContactPointDto($"Đầu mối {delegationName}", "OpOrg", "+8410", "op@example.com"),
            "VI", null, "DECLINED", null, null, null);

    private static async Task<ulong> CreateAsync(params CampusVisitFormDto[] campuses)
    {
        using var db = NewContext();
        var actor = new FakeUser(Registrant, RoleCodes.Visitor);
        var handler = new CreateVisitRequestV2CommandHandler(
            db, actor, new FixedClock(), new VisitRequestV2CreateService(db),
            new SilentNotifications(), new CreateVisitRequestV2CommandTests.RecordingClaimService(),
            new UserProvisionService(db),
            NullLogger<CreateVisitRequestV2CommandHandler>.Instance, ReadOn, WriteOn,
            new VisitRequestAggregateStatusService(db));
        var form = new VisitRequestFormDataV2(
            "EX" + Guid.NewGuid().ToString("N"),
            new RegistrantInputV2("Registrant", "VN", "Org", "Job", "+8491", V2SeedActor.Email(Registrant)),
            new ContactPointDto("Registrant", "Org", "+8491", V2SeedActor.Email(Registrant)),
            null, campuses.ToList());
        return (await handler.Handle(new CreateVisitRequestV2Command(form), CancellationToken.None)).VisitRequestId;
    }

    private static async Task ApproveAsync(ulong requestId, ulong instanceId, ulong leaderId, ulong campusId, ulong hostId)
    {
        using var db = NewContext();
        var actor = new FakeUser(leaderId, RoleCodes.Staff, UserSubRoles.Leader, campusId);
        await new ApproveCampusInstanceCommandHandler(
                db, actor, new FixedClock(), new VisitRequestAggregateStatusService(db), new SilentNotifications(),
                new VisitFormReadService(db, actor, NullLogger<VisitFormReadService>.Instance, new FixedClock()))
            .Handle(new ApproveCampusInstanceCommand(requestId, instanceId, hostId, null), CancellationToken.None);
    }

    private static async Task<Dictionary<ulong, ulong>> InstanceIdsAsync(ulong requestId)
    {
        using var db = NewContext();
        return await db.VisitRequestCampuses.AsNoTracking()
            .Where(c => c.VisitRequestId == requestId)
            .ToDictionaryAsync(c => c.CampusId, c => c.VisitInstanceId);
    }

    /// <summary>Seeds an agenda item then drags an approved instance to AFTER_VISIT (respecting both the
    /// agenda-required trigger and the "no past schedule on create" rule).</summary>
    private static async Task MoveToAfterVisitAsync(ulong instanceId)
    {
        using var db = NewContext();
        db.VisitAgendas.Add(new VisitAgenda
        {
            VisitInstanceId = instanceId, Title = "[IT] Mục nghị trình",
            StartTime = Now.AddDays(-3), EndTime = Now.AddDays(-3).AddHours(1),
            SequenceOrder = 1, CreatedAt = Now, CreatedBy = LeaderHn,
        });
        await db.SaveChangesAsync();
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE visit_request_campuses SET status = {0}, planned_start_at = {1}, planned_end_at = {2} WHERE visit_instance_id = {3}",
            VisitInstanceStatuses.AfterVisit, Now.AddDays(-3), Now.AddDays(-3).AddHours(2), instanceId);
    }

    private static GetOrCreateGeneralExpenseReportCommandHandler CreateHandler(ApplicationDbContext db, ulong actor)
        => new(db, new FakeUser(actor, RoleCodes.Staff, UserSubRoles.Staff, CampusHn));

    private static SaveExpenseReportCommandHandler SaveHandler(ApplicationDbContext db, ulong actor)
        => new(db, new FakeUser(actor, RoleCodes.Staff, UserSubRoles.Staff, CampusHn));

    private static GetVisitInstanceExpenseSummaryCommandHandler SummaryHandler(ApplicationDbContext db, FakeUser actor)
        => new(db, actor);

    private static SaveExpenseItemDto Item(string name, decimal qty, decimal price, int order, ulong? id = null)
        => new()
        {
            ExpenseItemId = id, ItemOrigin = "MANUAL", ItemName = name,
            Quantity = qty, UnitName = "cái", UnitPrice = price, DisplayOrder = order,
        };

    private static async Task<decimal> DbTotalAsync(ulong reportId)
    {
        // Read the DB-generated column directly, not the DTO's in-memory computation.
        using var db = NewContext();
        return await db.VisitExpenseItems.AsNoTracking()
            .Where(i => i.ExpenseReportId == reportId)
            .SumAsync(i => i.TotalAmount);
    }

    private static async Task CleanupAsync(ulong requestId)
    {
        if (requestId == 0) return;
        using var db = NewContext();
        var id = requestId;
        async Task Del(string sql) => await db.Database.ExecuteSqlRawAsync(sql, id);
        await Del("DELETE ei FROM visit_expense_items ei JOIN visit_expense_reports er ON er.expense_report_id = ei.expense_report_id JOIN visit_request_campuses c ON c.visit_instance_id = er.visit_instance_id WHERE c.visit_request_id = {0}");
        await Del("DELETE ee FROM visit_expense_report_events ee JOIN visit_expense_reports er ON er.expense_report_id = ee.expense_report_id JOIN visit_request_campuses c ON c.visit_instance_id = er.visit_instance_id WHERE c.visit_request_id = {0}");
        await Del("DELETE er FROM visit_expense_reports er JOIN visit_request_campuses c ON c.visit_instance_id = er.visit_instance_id WHERE c.visit_request_id = {0}");
        await Del("DELETE FROM visit_agendas WHERE visit_instance_id IN (SELECT visit_instance_id FROM visit_request_campuses WHERE visit_request_id = {0})");
        await Del("DELETE FROM notifications WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_participants WHERE visit_instance_id IN (SELECT visit_instance_id FROM visit_request_campuses WHERE visit_request_id = {0})");
        await Del("DELETE FROM email_action_tokens WHERE target_type='VISIT_REQUEST_IDENTITY_CHANGE' AND target_id IN (SELECT identity_change_id FROM visit_request_identity_changes WHERE visit_request_id = {0})");
        await Del("DELETE FROM visit_request_identity_change_events WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_request_identity_changes WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_instance_form_revision_history WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_request_revision_history WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_instance_guest_members WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_guest_members WHERE visit_request_id = {0}");
        await Del("DELETE alc FROM audit_log_changes alc JOIN audit_logs al ON al.audit_log_id = alc.audit_log_id WHERE al.visit_instance_id IN (SELECT visit_instance_id FROM visit_request_campuses WHERE visit_request_id = {0})");
        await Del("DELETE FROM audit_logs WHERE visit_instance_id IN (SELECT visit_instance_id FROM visit_request_campuses WHERE visit_request_id = {0})");
        await Del("DELETE FROM visit_instance_form_details WHERE visit_instance_id IN (SELECT visit_instance_id FROM visit_request_campuses WHERE visit_request_id = {0})");
        await Del("DELETE FROM visit_request_campuses WHERE visit_request_id = {0}");
        await Del("DELETE alc FROM audit_log_changes alc JOIN audit_logs al ON al.audit_log_id = alc.audit_log_id WHERE al.visit_request_id = {0}");
        await Del("DELETE FROM audit_logs WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_requests WHERE visit_request_id = {0}");
    }

    [Fact]
    public async Task The_generated_total_tracks_item_add_update_and_delete()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Campus("HN", Now.AddDays(80), "Đoàn chi phí"));
            var instances = await InstanceIdsAsync(requestId);
            await ApproveAsync(requestId, instances[CampusHn], LeaderHn, CampusHn, HostHn);
            await MoveToAfterVisitAsync(instances[CampusHn]);

            ulong reportId;
            int rowVersion;
            using (var db = NewContext())
            {
                var report = await CreateHandler(db, HostHn).Handle(
                    new GetOrCreateGeneralExpenseReportCommand { VisitInstanceId = instances[CampusHn] }, CancellationToken.None);
                reportId = report.ExpenseReportId;
                rowVersion = report.RowVersion;
            }

            // Two items: 3 × 10000 + 2 × 25000 = 80000.
            using (var db = NewContext())
            {
                var dto = await SaveHandler(db, HostHn).Handle(new SaveExpenseReportCommand
                {
                    ExpenseReportId = reportId, RowVersion = rowVersion, NoExpense = false,
                    Items = new List<SaveExpenseItemDto> { Item("Nước", 3, 10000, 1), Item("Hoa", 2, 25000, 2) },
                }, CancellationToken.None);
                Assert.Equal(80000m, dto.TotalAmount);
                Assert.All(dto.Items, i => Assert.Equal(i.Quantity * i.UnitPrice, i.TotalAmount));
                rowVersion = dto.RowVersion;
            }
            // The DB-generated column, read straight from the table, agrees with the DTO.
            Assert.Equal(80000m, await DbTotalAsync(reportId));

            // Update the first item's quantity (3 → 5) and drop the second: total = 5 × 10000 = 50000.
            ulong keepItemId;
            using (var db = NewContext())
                keepItemId = await db.VisitExpenseItems.AsNoTracking()
                    .Where(i => i.ExpenseReportId == reportId && i.ItemName == "Nước")
                    .Select(i => i.ExpenseItemId).SingleAsync();

            using (var db = NewContext())
            {
                var dto = await SaveHandler(db, HostHn).Handle(new SaveExpenseReportCommand
                {
                    ExpenseReportId = reportId, RowVersion = rowVersion, NoExpense = false,
                    Items = new List<SaveExpenseItemDto> { Item("Nước", 5, 10000, 1, keepItemId) },
                }, CancellationToken.None);
                Assert.Equal(50000m, dto.TotalAmount);
                Assert.Single(dto.Items);
            }
            Assert.Equal(50000m, await DbTotalAsync(reportId));

            // The application never writes total_amount — it is store-generated, so it exactly equals
            // quantity × unit_price for the surviving row and nothing the handler set.
            using (var db = NewContext())
            {
                var item = await db.VisitExpenseItems.AsNoTracking().SingleAsync(i => i.ExpenseItemId == keepItemId);
                Assert.Equal(50000m, item.TotalAmount);
                Assert.Equal(item.Quantity * item.UnitPrice, item.TotalAmount);
            }
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task A_host_cannot_edit_the_expense_report_of_a_sibling_campus()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var start = Now.AddDays(81);
            requestId = await CreateAsync(
                Campus("HN", start, "Đoàn HN"),
                Campus("HCM", start.AddDays(1), "Đoàn HCM"));
            var instances = await InstanceIdsAsync(requestId);
            await ApproveAsync(requestId, instances[CampusHn], LeaderHn, CampusHn, HostHn);
            await ApproveAsync(requestId, instances[CampusHcm], LeaderHcm, CampusHcm, HostHcm);
            await MoveToAfterVisitAsync(instances[CampusHn]);
            await MoveToAfterVisitAsync(instances[CampusHcm]);

            // The HCM host creates and populates HCM's report.
            ulong hcmReportId; int hcmRowVersion;
            using (var db = NewContext())
            {
                var r = await CreateHandler(db, HostHcm).Handle(
                    new GetOrCreateGeneralExpenseReportCommand { VisitInstanceId = instances[CampusHcm] }, CancellationToken.None);
                hcmReportId = r.ExpenseReportId; hcmRowVersion = r.RowVersion;
            }
            using (var db = NewContext())
            {
                var r = await SaveHandler(db, HostHcm).Handle(new SaveExpenseReportCommand
                {
                    ExpenseReportId = hcmReportId, RowVersion = hcmRowVersion, NoExpense = false,
                    Items = new List<SaveExpenseItemDto> { Item("Chi phí HCM", 1, 99000, 1) },
                }, CancellationToken.None);
                hcmRowVersion = r.RowVersion;
            }

            // The write path is the enforced isolation: the HN host cannot save changes onto HCM's report
            // (GetOrCreate's get-branch returns an already-created report without an owner check, so the
            // mutation guard in SaveExpenseReport is what actually protects the sibling's costs).
            using (var db = NewContext())
                await Assert.ThrowsAsync<ForbiddenException>(() =>
                    SaveHandler(db, HostHn).Handle(new SaveExpenseReportCommand
                    {
                        ExpenseReportId = hcmReportId, RowVersion = hcmRowVersion, NoExpense = false,
                        Items = new List<SaveExpenseItemDto> { Item("Chi phí lạ", 1, 1, 1) },
                    }, CancellationToken.None));

            // HCM's report is exactly what its own host left it — the sibling host changed nothing.
            Assert.Equal(99000m, await DbTotalAsync(hcmReportId));
            using (var db = NewContext())
                Assert.Single(await db.VisitExpenseItems.AsNoTracking().Where(i => i.ExpenseReportId == hcmReportId).ToListAsync());
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task Reading_expense_is_scoped_to_the_instances_own_campus()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var start = Now.AddDays(82);
            requestId = await CreateAsync(
                Campus("HN", start, "Đoàn HN"),
                Campus("HCM", start.AddDays(1), "Đoàn HCM"));
            var instances = await InstanceIdsAsync(requestId);
            await ApproveAsync(requestId, instances[CampusHn], LeaderHn, CampusHn, HostHn);
            await ApproveAsync(requestId, instances[CampusHcm], LeaderHcm, CampusHcm, HostHcm);
            await MoveToAfterVisitAsync(instances[CampusHn]);

            // The HN host creates and populates HN's general report.
            ulong hnReportId; int hnRowVersion;
            using (var db = NewContext())
            {
                var r = await CreateHandler(db, HostHn).Handle(
                    new GetOrCreateGeneralExpenseReportCommand { VisitInstanceId = instances[CampusHn] }, CancellationToken.None);
                hnReportId = r.ExpenseReportId; hnRowVersion = r.RowVersion;
            }
            using (var db = NewContext())
                await SaveHandler(db, HostHn).Handle(new SaveExpenseReportCommand
                {
                    ExpenseReportId = hnReportId, RowVersion = hnRowVersion, NoExpense = false,
                    Items = new List<SaveExpenseItemDto> { Item("Chi phí HN", 1, 42000, 1) },
                }, CancellationToken.None);

            var hnInstance = instances[CampusHn];

            // A Staff Leader of a DIFFERENT campus cannot read HN's expense — neither the general
            // report get-endpoint nor the summary.
            var hcmLeader = new FakeUser(LeaderHcm, RoleCodes.Staff, UserSubRoles.Leader, CampusHcm);
            using (var db = NewContext())
                await Assert.ThrowsAsync<ForbiddenException>(() =>
                    new GetOrCreateGeneralExpenseReportCommandHandler(db, hcmLeader)
                        .Handle(new GetOrCreateGeneralExpenseReportCommand { VisitInstanceId = hnInstance }, CancellationToken.None));
            using (var db = NewContext())
                await Assert.ThrowsAsync<ForbiddenException>(() =>
                    SummaryHandler(db, hcmLeader).Handle(
                        new GetVisitInstanceExpenseSummaryQuery { VisitInstanceId = hnInstance }, CancellationToken.None));

            // An unrelated user with no campus and no relation cannot read it either.
            var stranger = new FakeUser(202, RoleCodes.Staff, UserSubRoles.Staff, null);
            using (var db = NewContext())
                await Assert.ThrowsAsync<ForbiddenException>(() =>
                    SummaryHandler(db, stranger).Handle(
                        new GetVisitInstanceExpenseSummaryQuery { VisitInstanceId = hnInstance }, CancellationToken.None));

            // The HN Staff Leader (own campus) and the HN Host both read it, and see HN's amount.
            var hnLeader = new FakeUser(LeaderHn, RoleCodes.Staff, UserSubRoles.Leader, CampusHn);
            using (var db = NewContext())
            {
                var summary = await SummaryHandler(db, hnLeader).Handle(
                    new GetVisitInstanceExpenseSummaryQuery { VisitInstanceId = hnInstance }, CancellationToken.None);
                Assert.Equal(42000m, summary.TotalAmount);
            }
            using (var db = NewContext())
            {
                var summary = await SummaryHandler(db, new FakeUser(HostHn, RoleCodes.Staff, UserSubRoles.Staff, CampusHn))
                    .Handle(new GetVisitInstanceExpenseSummaryQuery { VisitInstanceId = hnInstance }, CancellationToken.None);
                Assert.Equal(42000m, summary.TotalAmount);
            }

            // HO oversees every campus → also allowed.
            using (var db = NewContext())
            {
                var summary = await SummaryHandler(db, new FakeUser(500, RoleCodes.Ho))
                    .Handle(new GetVisitInstanceExpenseSummaryQuery { VisitInstanceId = hnInstance }, CancellationToken.None);
                Assert.Equal(42000m, summary.TotalAmount);
            }
        }
        finally { await CleanupAsync(requestId); }
    }
}
