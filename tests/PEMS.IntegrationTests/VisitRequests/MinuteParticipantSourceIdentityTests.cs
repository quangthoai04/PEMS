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
using PEMS.Application.Delegations.Minutes;
using PEMS.Application.Delegations.Services;
using PEMS.Application.Delegations.Services.VisitFormRead;
using PEMS.Application.Notifications.Common;
using PEMS.Domain.Constants;
using PEMS.Infrastructure.Persistence;
using PEMS.Infrastructure.Services;
using PEMS.Shared;
using Xunit;

namespace PEMS.IntegrationTests.VisitRequests;

/// <summary>
/// MIN-01..MIN-04 end to end: what the biên bản remembers about where each person came from.
///
/// <list type="bullet">
///   <item><b>MIN-01</b> — one delegation member, one row, however many times sync runs.</item>
///   <item><b>MIN-02</b> — a travelling support member stays EXTERNAL_SUPPORT instead of being
///   recorded as a guest, and the campus's contact carries an ADDITIONAL badge rather than a
///   different kind.</item>
///   <item><b>MIN-03</b> — taking somebody out survives the save, and the next sync does not put
///   them back; restoring them works.</item>
///   <item><b>MIN-04</b> — identity is editable only where it LIVES: a manual row here, everybody
///   else at their source. An edit to a source row is refused rather than silently dropped.</item>
/// </list>
/// </summary>
public sealed class MinuteParticipantSourceIdentityTests
{
    private static string ConnString => TestInfrastructure.DisposableDatabaseManager.GetDisposableConnectionString(
        "server=localhost;port=3306;database=pems_pr3_test;user=root;password=123456;AllowUserVariables=True;GuidFormat=None");

    private const ulong Registrant = 8;
    private const ulong LeaderHn = 3;
    private const ulong HostHn = 101;
    private const ulong CampusHn = 1;

    private const string GuestKey = "k-guest";
    private const string SupportKey = "k-support";

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

    private sealed class SilentEmail : IEmailService
    {
        public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendAsync(OutboundEmail message, CancellationToken ct = default) => Task.CompletedTask;
        public Task<EmailDeliveryResult> TrySendAsync(OutboundEmail message, CancellationToken ct = default) => Task.FromResult(EmailDeliveryResult.Sent());
        public Task<EmailDeliveryResult> TrySendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default) => Task.FromResult(EmailDeliveryResult.Sent());
        public Task SendPasswordResetAsync(string toEmail, string fullName, string code, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendVisitRequestOtpAsync(string toEmail, string fullName, string code, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendVisitorAccountCreatedOrLinkedEmailAsync(string toEmail, string contactFullName, string delegationName, string requestCode, string visitScope, string plannedTime, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendRegistrantConfirmationAsync(string toEmail, string registrantFullName, string contactFullName, string contactEmail, string delegationName, string requestCode, CancellationToken ct = default) => Task.CompletedTask;
    }

    private static readonly PerCampusFormV2Options ReadOn = new() { Enabled = true };
    private static readonly PerCampusFormV2WriteOptions WriteOn = new() { Enabled = true };

    /// <summary>
    /// One campus whose contact is the SUPPORT member — the case the old code got wrong twice over:
    /// the row was labelled "Khách", and the contact could turn up a second time as a snapshot.
    /// </summary>
    private static CampusVisitFormDto Campus(string delegationName) =>
        new("HN", Now.AddDays(20), Now.AddDays(20).AddMinutes(120), delegationName, "MEETING", null,
            $"Mục đích {delegationName}", $"Nội dung {delegationName}",
            new List<VisitorDto> { new("Khách Chính", "VN", "Trưởng đoàn", "GuestOrg", null, GuestKey) },
            new List<SupportTeamMemberDto> { new("Phiên Dịch Viên", "Phiên dịch", "GuestOrg", "VN", null, SupportKey) },
            // The contact is the REGISTRANT'S own address so the campus self-confirms at submit; the
            // NAME/JOB/ORG are rewritten from the picked member by the create path, which is exactly
            // what makes "đầu mối là Phiên Dịch Viên" one fact instead of two.
            new ContactPointDto("Phiên Dịch Viên", "GuestOrg", "Phiên dịch", "+8410", V2SeedActor.Email(Registrant)),
            "VI", null, "DECLINED", null, null,
            OperationalContactClientMemberKey: SupportKey);

    /// <summary>
    /// A campus whose đầu mối is NOT in the delegation — the other half of the contact rules, and the
    /// one with no <c>guest_member_id</c> to be recognised by. The address is the registrant's so the
    /// campus self-confirms at submit, which is what gives the contact an ACCOUNT: that combination
    /// (an account, no member row) is the shape the save path used to mishandle.
    /// </summary>
    private static CampusVisitFormDto CampusWithOutsideContact(string delegationName) =>
        new("HN", Now.AddDays(20), Now.AddDays(20).AddMinutes(120), delegationName, "MEETING", null,
            $"Mục đích {delegationName}", $"Nội dung {delegationName}",
            new List<VisitorDto> { new("Khách Chính", "VN", "Trưởng đoàn", "GuestOrg", null, GuestKey) },
            new List<SupportTeamMemberDto>(),
            new ContactPointDto("Điều Phối Viên", "OrgNgoài", "Điều phối", "+8410",
                V2SeedActor.Email(Registrant)),
            "VI", null, "DECLINED", null, null,
            OperationalContactClientMemberKey: null);

    private static async Task<ulong> CreateAsync(CampusVisitFormDto campus)
    {
        using var db = NewContext();
        var actor = new FakeUser(Registrant, RoleCodes.Visitor);
        var handler = new CreateVisitRequestV2CommandHandler(
            db, actor, new FixedClock(), new VisitRequestV2CreateService(db),
            new SilentNotifications(), new CreateVisitRequestV2CommandTests.RecordingInvitationService(),
            new UserProvisionService(db),
            NullLogger<CreateVisitRequestV2CommandHandler>.Instance, ReadOn, WriteOn,
            new VisitRequestAggregateStatusService(db),
            new ProposedHostActivationService(db, new MySqlUserMutationLockService(db)), new MySqlUserMutationLockService(db));
        var form = new VisitRequestFormDataV2(
            "MS" + Guid.NewGuid().ToString("N"),
            new RegistrantInputV2("Registrant", "VN", "Org", "Job", "+8491", V2SeedActor.Email(Registrant)),
            null, new List<CampusVisitFormDto> { campus });
        return (await handler.Handle(new CreateVisitRequestV2Command(form), CancellationToken.None)).VisitRequestId;
    }

    private static async Task ApproveAsync(ulong requestId, ulong instanceId)
    {
        using var db = NewContext();
        var actor = new FakeUser(LeaderHn, RoleCodes.Staff, UserSubRoles.Leader, CampusHn);
        var rowVersion = await db.VisitRequestCampuses.AsNoTracking()
            .Where(c => c.VisitInstanceId == instanceId).Select(c => c.RowVersion).SingleAsync();
        await new ApproveCampusInstanceCommandHandler(
                db, actor, new FixedClock(),
                new CampusApprovalExecutor(
                    db, new VisitRequestAggregateStatusService(db), new MySqlUserMutationLockService(db), new SilentNotifications(),
                    new VisitFormReadService(db, actor, NullLogger<VisitFormReadService>.Instance, new FixedClock()),
                    NullLogger<CampusApprovalExecutor>.Instance))
            .Handle(new ApproveCampusInstanceCommand(requestId, instanceId, HostHn, null, rowVersion), CancellationToken.None);
    }

    /// <summary>
    /// Moves the campus to DURING_VISIT, which is where a biên bản may first be opened.
    ///
    /// <para>Written straight to the column rather than driven through the Host's two "hoàn thành
    /// giai đoạn" commands: those belong to a test about stage transitions, and putting them here
    /// would make every minutes test depend on the preparation flow staying exactly as it is.</para>
    /// </summary>
    private static async Task StartVisitAsync(ulong instanceId)
    {
        using var db = NewContext();
        // A campus cannot be DURING_VISIT with no agenda — a trigger says so, because from here on
        // the delegation is actually being received.
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO visit_agendas (visit_instance_id, sequence_order, title, start_time) "
            + "SELECT {0}, 1, 'Tiếp đoàn', planned_start_at FROM visit_request_campuses "
            + "WHERE visit_instance_id = {0}",
            instanceId);
        // One step at a time: a second trigger enforces the stage machine, and DURING_VISIT is only
        // reachable from BEFORE_VISIT. Jumping straight there is refused by the database itself —
        // the same rule the create-minutes gate now states in the handler.
        foreach (var status in new[] { "BEFORE_VISIT", "DURING_VISIT" })
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE visit_request_campuses SET status = {0} WHERE visit_instance_id = {1}",
                status, instanceId);
    }

    private static async Task<ulong> InstanceIdAsync(ulong requestId)
    {
        using var db = NewContext();
        return await db.VisitRequestCampuses.AsNoTracking()
            .Where(c => c.VisitRequestId == requestId).Select(c => c.VisitInstanceId).SingleAsync();
    }

    private static SaveMinutesCommandHandler SaveHandler(ApplicationDbContext db)
        => new(db, new FakeUser(HostHn, RoleCodes.Staff, UserSubRoles.Staff, CampusHn),
            new FixedClock(), new SilentEmail(), new SilentNotifications());

    private static CreateOrLockMinutesCommandHandler OpenHandler(ApplicationDbContext db)
        => new(db, new FakeUser(HostHn, RoleCodes.Staff, UserSubRoles.Staff, CampusHn), new FixedClock());

    private static AcquireMinutesLockCommandHandler LockHandler(ApplicationDbContext db)
        => new(db, new FakeUser(HostHn, RoleCodes.Staff, UserSubRoles.Staff, CampusHn), new FixedClock());

    private static GetNewMinuteParticipantsQueryHandler SyncHandler(ApplicationDbContext db)
        => new(db, new FakeUser(HostHn, RoleCodes.Staff, UserSubRoles.Staff, CampusHn), new FixedClock());

    /// <summary>Creates + approves a campus and opens its minutes. Returns the ids the tests need.</summary>
    private static Task<(ulong RequestId, ulong InstanceId, ulong MinutesId, string Token, uint RowVersion)>
        OpenMinutesAsync(string delegationName)
        => OpenMinutesForAsync(Campus(delegationName), delegationName);

    private static async Task<(ulong RequestId, ulong InstanceId, ulong MinutesId, string Token, uint RowVersion)>
        OpenMinutesForAsync(CampusVisitFormDto campus, string delegationName)
    {
        var requestId = await CreateAsync(campus);
        var instanceId = await InstanceIdAsync(requestId);
        await ApproveAsync(requestId, instanceId);
        await StartVisitAsync(instanceId);

        using var db = NewContext();
        var opened = await OpenHandler(db).Handle(
            new CreateOrLockMinutesCommand(instanceId, $"Biên bản {delegationName}"), CancellationToken.None);
        return (requestId, instanceId, opened.MinutesId!.Value, opened.EditLockToken!, opened.RowVersion);
    }

    /// <summary>The rows as they are in the database, in display order.</summary>
    private static async Task<List<PEMS.Domain.Entities.Minutes.MinuteParticipant>> RowsAsync(ulong minutesId)
    {
        using var db = NewContext();
        return await db.MinuteParticipants.AsNoTracking()
            .Where(p => p.MinutesId == minutesId)
            .OrderBy(p => p.DisplayOrder).ToListAsync();
    }

    private static SaveMinuteParticipantInput Echo(
        PEMS.Domain.Entities.Minutes.MinuteParticipant row, string? syncState = null)
        => new(row.MinuteParticipantId, row.UserId, row.GuestMemberId, row.FullNameSnapshot,
            row.RoleSnapshot, row.OrganizationSnapshot, row.EmailSnapshot, row.AttendanceStatus,
            row.AttendanceNote, syncState);

    private static async Task<(string Token, uint RowVersion)> ReopenAsync(ulong minutesId)
    {
        using var db = NewContext();
        var reopened = await LockHandler(db).Handle(
            new AcquireMinutesLockCommand(minutesId), CancellationToken.None);
        return (reopened.EditLockToken!, reopened.RowVersion);
    }

    private static async Task CleanupAsync(ulong requestId)
    {
        if (requestId == 0) return;
        using var db = NewContext();
        var id = requestId;
        async Task Del(string sql) => await db.Database.ExecuteSqlRawAsync(sql, id);
        await Del("DELETE ai FROM minute_action_items ai JOIN minutes m ON m.minutes_id = ai.minutes_id WHERE m.visit_instance_id IN (SELECT visit_instance_id FROM visit_request_campuses WHERE visit_request_id = {0})");
        await Del("DELETE mp FROM minute_participants mp JOIN minutes m ON m.minutes_id = mp.minutes_id WHERE m.visit_instance_id IN (SELECT visit_instance_id FROM visit_request_campuses WHERE visit_request_id = {0})");
        await Del("DELETE FROM minutes WHERE visit_instance_id IN (SELECT visit_instance_id FROM visit_request_campuses WHERE visit_request_id = {0})");
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

    // ── MIN-02: the biên bản says which list each person came from ──────────────────────────────

    [Fact]
    public async Task A_travelling_support_member_is_recorded_as_support_and_carries_the_contact_badge()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var opened = await OpenMinutesAsync("Đoàn loại nguồn");
            requestId = opened.RequestId;

            var rows = await RowsAsync(opened.MinutesId);
            var guest = Assert.Single(rows.Where(r => r.FullNameSnapshot == "Khách Chính"));
            var support = Assert.Single(rows.Where(r => r.FullNameSnapshot == "Phiên Dịch Viên"));

            // The interpreter is NOT a member of the delegation, and the record of the meeting no
            // longer says they were.
            Assert.Equal(GuestMemberType.Guest, guest.SourceMemberType);
            Assert.Equal(GuestMemberType.ExternalSupport, support.SourceMemberType);

            // "Đầu mối" is an ADDITIONAL role: the interpreter is still support staff.
            Assert.True(support.IsOperationalContact);
            Assert.False(guest.IsOperationalContact);

            // …and what the UI is told follows from the stored rows, rather than collapsing every
            // guest-side row into "Khách" as the old three-value kind did.
            Assert.Equal(
                GuestMemberType.ExternalSupport,
                MinuteParticipantDto.KindOf(support.UserId, support.GuestMemberId, support.SourceMemberType));
            Assert.Equal(
                GuestMemberType.Guest,
                MinuteParticipantDto.KindOf(guest.UserId, guest.GuestMemberId, guest.SourceMemberType));
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task The_contact_badge_MOVES_when_the_role_changes_after_the_minutes_exist()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var opened = await OpenMinutesAsync("Đoàn đổi đầu mối");
            requestId = opened.RequestId;

            var before = await RowsAsync(opened.MinutesId);
            var guestRow = Assert.Single(before, r => r.FullNameSnapshot == "Khách Chính");
            Assert.True(Assert.Single(before, r => r.FullNameSnapshot == "Phiên Dịch Viên").IsOperationalContact);

            // The Host may open the biên bản as soon as the campus is approved, while the request
            // stays editable until six hours before the visit — so the campus can be handed to a
            // different đầu mối with the biên bản already sitting there. Re-pointing the link is the
            // whole of what such an edit does to this table.
            using (var db = NewContext())
            {
                await db.Database.ExecuteSqlRawAsync(
                    "UPDATE visit_instance_form_details SET operational_contact_guest_member_id = {0} "
                    + "WHERE visit_instance_id = {1}",
                    guestRow.GuestMemberId!.Value, opened.InstanceId);
            }

            // An ordinary save that echoes the rows back unchanged. The badge is RE-DERIVED here, so
            // nothing about the client's payload can decide who the đầu mối is.
            var reopened = await ReopenAsync(opened.MinutesId);
            using (var db = NewContext())
            {
                await SaveHandler(db).Handle(new SaveMinutesCommand(
                    opened.MinutesId, "Biên bản", null, reopened.Token, reopened.RowVersion,
                    Participants: before.Select(r => Echo(r)).ToList(),
                    ActionItems: null), CancellationToken.None);
            }

            var after = await RowsAsync(opened.MinutesId);
            Assert.True(Assert.Single(after, r => r.FullNameSnapshot == "Khách Chính").IsOperationalContact);
            // …and it LEFT the person who used to hold it. Writing the flag only on INSERT left the
            // biên bản naming the previous đầu mối — and naming two of them once sync brought the new
            // one in with a correct badge of their own.
            Assert.False(Assert.Single(after, r => r.FullNameSnapshot == "Phiên Dịch Viên").IsOperationalContact);
            Assert.Equal(1, after.Count(r => r.IsOperationalContact));
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task A_contact_who_is_NOT_a_member_keeps_the_badge_and_the_unit_through_a_save()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var opened = await OpenMinutesForAsync(
                CampusWithOutsideContact("Đoàn đầu mối ngoài"), "Đoàn đầu mối ngoài");
            requestId = opened.RequestId;

            // Such a contact has an ACCOUNT (they confirmed) but no delegation row, so nothing about
            // them carries a guest_member_id.
            var created = await RowsAsync(opened.MinutesId);
            var contactRow = Assert.Single(created, r => r.IsOperationalContact);
            Assert.Null(contactRow.GuestMemberId);
            Assert.NotNull(contactRow.UserId);

            // Take the row out of the biên bản entirely, so the person comes back through the SYNC —
            // the second of the two doors into a biên bản, and the one whose save path is under test.
            using (var db = NewContext())
                await db.Database.ExecuteSqlRawAsync(
                    "DELETE FROM minute_participants WHERE minute_participant_id = {0}",
                    contactRow.MinuteParticipantId);

            var reopened = await ReopenAsync(opened.MinutesId);
            List<MinuteParticipantDto> candidates;
            using (var db = NewContext())
                candidates = await SyncHandler(db).Handle(
                    new GetNewMinuteParticipantsQuery(opened.MinutesId, null), CancellationToken.None);

            var offered = Assert.Single(candidates, c => c.IsOperationalContact);

            // Saving what the sync offered used to LOSE the badge: the row has no guest_member_id, and
            // the badge was set on the guest branch only. The unit went with it — the account is an
            // external one with neither a department nor a campus, so reading the organisation off it
            // wrote NULL over what the sync had just supplied.
            var kept = await RowsAsync(opened.MinutesId);
            using (var db = NewContext())
                await SaveHandler(db).Handle(new SaveMinutesCommand(
                    opened.MinutesId, "Biên bản", null, reopened.Token, reopened.RowVersion,
                    Participants: kept.Select(r => Echo(r)).Append(new SaveMinuteParticipantInput(
                        null, offered.UserId, offered.GuestMemberId, offered.FullNameSnapshot,
                        offered.RoleSnapshot, offered.OrganizationSnapshot, offered.EmailSnapshot,
                        offered.AttendanceStatus, offered.AttendanceNote)).ToList(),
                    ActionItems: null), CancellationToken.None);

            var saved = Assert.Single(await RowsAsync(opened.MinutesId), r => r.IsOperationalContact);
            Assert.Null(saved.GuestMemberId);
            Assert.Equal("OrgNgoài", saved.OrganizationSnapshot);
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task The_contact_reads_as_a_GUEST_even_though_confirming_gave_them_an_account()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var opened = await OpenMinutesForAsync(
                CampusWithOutsideContact("Đoàn nhãn đầu mối"), "Đoàn nhãn đầu mối");
            requestId = opened.RequestId;

            MinuteDto dto;
            using (var db = NewContext())
                dto = await new GetVisitInstanceMinutesQueryHandler(
                        db, new FakeUser(HostHn, RoleCodes.Staff, UserSubRoles.Staff, CampusHn), new FixedClock())
                    .Handle(new GetVisitInstanceMinutesQuery(opened.InstanceId), CancellationToken.None);

            var contact = Assert.Single(dto.Participants, p => p.IsOperationalContact);
            // The row carries a user_id — confirming the invitation is what gave them one — and that
            // used to be read as "nhân sự nội bộ". The screen then showed the head of the visiting
            // delegation as "Nội bộ", directly beside the "Đầu mối đoàn khách" badge.
            Assert.NotNull(contact.UserId);
            Assert.Equal(GuestMemberType.Guest, contact.ParticipantKind);

            // The Host, who really is internal, is untouched by the rule.
            Assert.Equal("INTERNAL",
                Assert.Single(dto.Participants, p => p.RoleSnapshot == "Host").ParticipantKind);
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task Reading_the_minutes_shows_the_badge_on_the_CURRENT_contact_without_a_save()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var opened = await OpenMinutesAsync("Đoàn đọc lại");
            requestId = opened.RequestId;

            var rows = await RowsAsync(opened.MinutesId);
            var guestRow = Assert.Single(rows, r => r.FullNameSnapshot == "Khách Chính");

            // The role moves. Nobody re-saves the biên bản — a Staff Leader simply opens it, which is
            // exactly the case a save-time fix alone cannot reach.
            using (var db = NewContext())
                await db.Database.ExecuteSqlRawAsync(
                    "UPDATE visit_instance_form_details SET operational_contact_guest_member_id = {0} "
                    + "WHERE visit_instance_id = {1}",
                    guestRow.GuestMemberId!.Value, opened.InstanceId);

            MinuteDto dto;
            using (var db = NewContext())
                dto = await new GetVisitInstanceMinutesQueryHandler(
                        db, new FakeUser(HostHn, RoleCodes.Staff, UserSubRoles.Staff, CampusHn), new FixedClock())
                    .Handle(new GetVisitInstanceMinutesQuery(opened.InstanceId), CancellationToken.None);

            var badged = Assert.Single(dto.Participants, p => p.IsOperationalContact);
            Assert.Equal("Khách Chính", badged.FullNameSnapshot);
            // The stored column is untouched by a read — the correction belongs to the next save.
            Assert.True(Assert.Single(await RowsAsync(opened.MinutesId),
                r => r.FullNameSnapshot == "Phiên Dịch Viên").IsOperationalContact);
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── MIN-01: the contact who IS a member appears once, whatever sync does ─────────────────────

    [Fact]
    public async Task The_contact_who_is_a_delegation_member_appears_exactly_once_after_repeated_syncs()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var opened = await OpenMinutesAsync("Đoàn đồng bộ");
            requestId = opened.RequestId;

            // Sync twice in a row; neither run may offer somebody already in the biên bản.
            for (var i = 0; i < 2; i++)
            {
                using var db = NewContext();
                var candidates = await SyncHandler(db).Handle(
                    new GetNewMinuteParticipantsQuery(opened.MinutesId, null), CancellationToken.None);
                Assert.Empty(candidates);
            }

            var rows = await RowsAsync(opened.MinutesId);
            Assert.Single(rows.Where(r => r.FullNameSnapshot == "Phiên Dịch Viên"));
            // One row per delegation member — which the unique index now guarantees even against a
            // concurrent save, not only against this code path.
            Assert.Equal(
                rows.Count(r => r.GuestMemberId != null),
                rows.Where(r => r.GuestMemberId != null).Select(r => r.GuestMemberId).Distinct().Count());
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── MIN-03: taking somebody out survives the save AND the next sync ──────────────────────────

    [Fact]
    public async Task A_person_excluded_from_the_minutes_is_not_added_back_by_the_next_sync()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var opened = await OpenMinutesAsync("Đoàn loại bỏ");
            requestId = opened.RequestId;

            var before = await RowsAsync(opened.MinutesId);
            var guestRow = before.Single(r => r.FullNameSnapshot == "Khách Chính");

            // The client simply leaves the row out — the shape a "delete" has always had.
            using (var db = NewContext())
            {
                await SaveHandler(db).Handle(new SaveMinutesCommand(
                    opened.MinutesId, "Biên bản", null, opened.Token, opened.RowVersion,
                    Participants: before.Where(r => r.MinuteParticipantId != guestRow.MinuteParticipantId)
                        .Select(r => Echo(r)).ToList(),
                    ActionItems: null), CancellationToken.None);
            }

            var afterSave = await RowsAsync(opened.MinutesId);
            var excluded = Assert.Single(afterSave.Where(r => r.MinuteParticipantId == guestRow.MinuteParticipantId));
            // Kept, not deleted — which is the only reason the decision can outlive the save.
            Assert.Equal(MinuteParticipantSyncStates.Excluded, excluded.SyncState);

            // …and the next sync does not helpfully put them back.
            var reopened = await ReopenAsync(opened.MinutesId);
            using (var db = NewContext())
            {
                var candidates = await SyncHandler(db).Handle(
                    new GetNewMinuteParticipantsQuery(opened.MinutesId, null), CancellationToken.None);
                Assert.DoesNotContain(candidates, c => c.GuestMemberId == guestRow.GuestMemberId);
            }

            // Restoring is an explicit act, and reuses the SAME row rather than minting a second one.
            using (var db = NewContext())
            {
                var rows = await db.MinuteParticipants.AsNoTracking()
                    .Where(p => p.MinutesId == opened.MinutesId).ToListAsync();
                await SaveHandler(db).Handle(new SaveMinutesCommand(
                    opened.MinutesId, "Biên bản", null, reopened.Token, reopened.RowVersion,
                    Participants: rows.Select(r => Echo(r, MinuteParticipantSyncStates.Active)).ToList(),
                    ActionItems: null), CancellationToken.None);
            }

            var afterRestore = await RowsAsync(opened.MinutesId);
            var restored = Assert.Single(afterRestore.Where(r => r.MinuteParticipantId == guestRow.MinuteParticipantId));
            Assert.Equal(MinuteParticipantSyncStates.Active, restored.SyncState);
            Assert.Equal(before.Count, afterRestore.Count);
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── MIN-04: identity is editable where it lives, and only there ──────────────────────────────

    [Fact]
    public async Task Editing_a_source_participants_identity_is_refused_rather_than_silently_dropped()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var opened = await OpenMinutesAsync("Đoàn sửa tên");
            requestId = opened.RequestId;

            var rows = await RowsAsync(opened.MinutesId);
            var guestRow = rows.Single(r => r.FullNameSnapshot == "Khách Chính");

            using var db = NewContext();
            var edited = rows.Select(r => r.MinuteParticipantId == guestRow.MinuteParticipantId
                ? new SaveMinuteParticipantInput(
                    r.MinuteParticipantId, r.UserId, r.GuestMemberId,
                    "Tên Đã Sửa", r.RoleSnapshot, r.OrganizationSnapshot, r.EmailSnapshot,
                    r.AttendanceStatus, r.AttendanceNote, null)
                : Echo(r)).ToList();

            var refusal = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                SaveHandler(db).Handle(new SaveMinutesCommand(
                    opened.MinutesId, "Biên bản", null, opened.Token, opened.RowVersion,
                    Participants: edited, ActionItems: null), CancellationToken.None));

            Assert.Equal(SaveMinutesCommandHandler.SourceParticipantIdentityReadonly, refusal.ErrorCode);
            // Says WHO and where the edit does belong — the old behaviour accepted this payload,
            // dropped the field, and reported success.
            Assert.Contains("Khách Chính", refusal.Message);

            // Nothing was written: the whole save is refused, so attendance did not sneak through.
            var after = await RowsAsync(opened.MinutesId);
            Assert.Equal("Khách Chính", after.Single(r => r.MinuteParticipantId == guestRow.MinuteParticipantId).FullNameSnapshot);
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task Attendance_on_a_source_row_and_the_identity_of_a_manual_row_both_persist()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var opened = await OpenMinutesAsync("Đoàn điểm danh");
            requestId = opened.RequestId;

            var rows = await RowsAsync(opened.MinutesId);
            var guestRow = rows.Single(r => r.FullNameSnapshot == "Khách Chính");

            using (var db = NewContext())
            {
                var payload = rows.Select(r => r.MinuteParticipantId == guestRow.MinuteParticipantId
                    ? new SaveMinuteParticipantInput(
                        r.MinuteParticipantId, r.UserId, r.GuestMemberId, r.FullNameSnapshot,
                        r.RoleSnapshot, r.OrganizationSnapshot, r.EmailSnapshot,
                        "PRESENT", "Đến đúng giờ.", null)
                    : Echo(r)).ToList();
                // …plus somebody who exists only in this biên bản.
                payload.Add(new SaveMinuteParticipantInput(
                    null, null, null, "Người Thêm Tay", "Quan sát viên", "Đơn vị ngoài",
                    "quansat@example.com", "PRESENT", null, null));

                await SaveHandler(db).Handle(new SaveMinutesCommand(
                    opened.MinutesId, "Biên bản", null, opened.Token, opened.RowVersion,
                    Participants: payload, ActionItems: null), CancellationToken.None);
            }

            var after = await RowsAsync(opened.MinutesId);
            var savedGuest = after.Single(r => r.MinuteParticipantId == guestRow.MinuteParticipantId);
            Assert.Equal("PRESENT", savedGuest.AttendanceStatus);
            Assert.Equal("Đến đúng giờ.", savedGuest.AttendanceNote);

            var manual = Assert.Single(after.Where(r => r.UserId == null && r.GuestMemberId == null));
            Assert.Equal("Người Thêm Tay", manual.FullNameSnapshot);
            Assert.Equal("Quan sát viên", manual.RoleSnapshot);

            // The manual row's identity is editable, because this is where it lives.
            var reopened = await ReopenAsync(opened.MinutesId);
            using (var db = NewContext())
            {
                var current = await db.MinuteParticipants.AsNoTracking()
                    .Where(p => p.MinutesId == opened.MinutesId).ToListAsync();
                await SaveHandler(db).Handle(new SaveMinutesCommand(
                    opened.MinutesId, "Biên bản", null, reopened.Token, reopened.RowVersion,
                    Participants: current.Select(r => r.MinuteParticipantId == manual.MinuteParticipantId
                        ? new SaveMinuteParticipantInput(
                            r.MinuteParticipantId, null, null, "Người Thêm Tay (đã sửa)", "Khách mời",
                            r.OrganizationSnapshot, r.EmailSnapshot, r.AttendanceStatus, r.AttendanceNote, null)
                        : Echo(r)).ToList(),
                    ActionItems: null), CancellationToken.None);
            }

            var final = await RowsAsync(opened.MinutesId);
            var editedManual = final.Single(r => r.MinuteParticipantId == manual.MinuteParticipantId);
            Assert.Equal("Người Thêm Tay (đã sửa)", editedManual.FullNameSnapshot);
            Assert.Equal("Khách mời", editedManual.RoleSnapshot);
        }
        finally { await CleanupAsync(requestId); }
    }
}
