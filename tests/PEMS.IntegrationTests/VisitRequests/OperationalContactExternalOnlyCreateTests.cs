using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.DTOs;
using PEMS.Application.Common.Exceptions;
using PEMS.Domain.Constants;
using PEMS.Infrastructure.Persistence;
using PEMS.Infrastructure.Services;
using Xunit;

namespace PEMS.IntegrationTests.VisitRequests;

/// <summary>
/// A visit request cannot be created with an INTERNAL account as any campus's operational contact —
/// proved against the real schema, through the service every create path funnels into.
///
/// <para>
/// The command handlers check the same thing first, so the ordinary user gets a red message on the
/// exact campus card rather than a banner. This suite is about the layer UNDERNEATH them: three callers
/// reach the create service (authenticated create, verify-and-create, the delegated OTP flow), the OTP
/// path validates a form at INITIATE and creates from the payload posted at VERIFY, and an in-process
/// caller reaches it with no handler in front at all.
/// </para>
/// <para>
/// The self-match case is the one worth stating plainly: a registrant whose own address is also the
/// campus contact is linked immediately, with no invitation and nobody confirming anything. That is the
/// shortcut the rule exists to close for internal accounts — being the registrant is not an exception
/// to being internal.
/// </para>
/// </summary>
public sealed class OperationalContactExternalOnlyCreateTests
{
    private static string ConnString => TestInfrastructure.DisposableDatabaseManager.GetDisposableConnectionString(
        "server=localhost;port=3306;database=pems_pr3_test;user=root;password=123456;AllowUserVariables=True;GuidFormat=None");

    // Canonical seed. Campus 1 = HN.
    private const ulong LeaderHn = 3;        // STAFF / LEADER — internal
    private const ulong IcStaffHn = 101;     // STAFF / STAFF — internal
    private const ulong Visitor = 8;         // VISITOR — external
    private const ulong OtherVisitor = 22;   // VISITOR — external

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

    private static CampusVisitFormDto Campus(string code, string contactEmail, DateTime start)
        => new(
            code, start, start.AddMinutes(120), "Đoàn kiểm tra đầu mối", "MEETING", null, "Thăm", "Nội dung",
            new List<VisitorDto> { new("Guest A", "VN", "Guest", "GuestOrg") },
            new List<SupportTeamMemberDto>(),
            new ContactPointDto("Op Contact", "OpOrg", "Trưởng phòng Hợp tác", "+8410", contactEmail),
            "EN", null, "DECLINED", null, null);

    private static VisitRequestFormDataV2 Form(string registrantEmail, string contactEmail)
        => new(
            Guid.NewGuid().ToString("N"),
            new RegistrantInputV2("Registrant", "VN", "Org", "Job", "+8491", registrantEmail),
            null,
            new List<CampusVisitFormDto> { Campus("HN", contactEmail, Now.AddDays(20)) });

    private static async Task<int> RequestCountAsync(string submissionFingerprintFree = "")
    {
        _ = submissionFingerprintFree;
        using var db = NewContext();
        return await db.VisitRequests.AsNoTracking().CountAsync();
    }

    private static async Task CleanupAsync(ulong id)
    {
        if (id == 0) return;
        using var db = NewContext();
        async Task Del(string sql) => await db.Database.ExecuteSqlRawAsync(sql, id);
        await Del("DELETE FROM visit_request_identity_change_events WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_request_identity_changes WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_instance_form_revision_history WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_request_revision_history WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_instance_guest_members WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_guest_members WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_instance_form_details WHERE visit_instance_id IN (SELECT visit_instance_id FROM visit_request_campuses WHERE visit_request_id = {0})");
        await Del("DELETE FROM visit_request_campuses WHERE visit_request_id = {0}");
        await Del("DELETE FROM audit_log_changes WHERE audit_log_id IN (SELECT audit_log_id FROM audit_logs WHERE visit_request_id = {0})");
        await Del("DELETE FROM audit_logs WHERE visit_request_id = {0}");
        await Del("DELETE FROM notifications WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_requests WHERE visit_request_id = {0}");
    }

    /// <summary>
    /// A guest files the request and names an FPTU staff member as the campus's contact. Refused, and
    /// nothing at all is written — not the request, not the campus, not an invitation.
    /// </summary>
    [Fact]
    public async Task A_request_naming_an_internal_account_as_the_campus_contact_is_refused()
    {
        RequireDb();
        var before = await RequestCountAsync();

        using var db = NewContext();
        await using var tx = await db.Database.BeginTransactionAsync();
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            new VisitRequestV2CreateService(db).CreateV2Async(
                Form(V2SeedActor.Email(Visitor), V2SeedActor.Email(IcStaffHn)),
                Visitor, "VISITOR_SUBMITTED", Now, CancellationToken.None));
        await tx.RollbackAsync();

        Assert.Equal(VisitRequestErrorCodes.ContactEmailCannotBeUsedForVisitorAccount, ex.ErrorCode);
        Assert.Equal(before, await RequestCountAsync());
    }

    /// <summary>
    /// The registrant IS the internal account, naming their own address — the self-match shortcut, which
    /// would have linked them as the contact on the spot with no invitation and no confirmation.
    /// Refused for the same reason: the rule is about the account, not about how it got there.
    /// </summary>
    [Fact]
    public async Task An_internal_registrant_may_not_name_their_own_address_as_the_contact()
    {
        RequireDb();
        var leaderEmail = V2SeedActor.Email(LeaderHn);
        var before = await RequestCountAsync();

        using var db = NewContext();
        await using var tx = await db.Database.BeginTransactionAsync();
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            new VisitRequestV2CreateService(db).CreateV2Async(
                Form(leaderEmail, leaderEmail),
                LeaderHn, "STAFF_CREATED", Now, CancellationToken.None));
        await tx.RollbackAsync();

        Assert.Equal(VisitRequestErrorCodes.ContactEmailCannotBeUsedForVisitorAccount, ex.ErrorCode);
        Assert.Equal(before, await RequestCountAsync());
    }

    /// <summary>
    /// The control, and the case that must keep working: an internal registrant filing for a delegation
    /// whose contact is an external guest. Being STAFF is not the problem — appointing yourself is.
    /// </summary>
    [Fact]
    public async Task An_internal_registrant_may_file_a_request_whose_contact_is_a_guest()
    {
        RequireDb();
        var requestId = 0UL;
        try
        {
            using (var db = NewContext())
            {
                await using var tx = await db.Database.BeginTransactionAsync();
                var created = await new VisitRequestV2CreateService(db).CreateV2Async(
                    Form(V2SeedActor.Email(LeaderHn), V2SeedActor.Email(OtherVisitor)),
                    LeaderHn, "STAFF_CREATED", Now, CancellationToken.None);
                await tx.CommitAsync();
                requestId = created.VisitRequestId;
            }

            using var check = NewContext();
            var instance = await check.VisitRequestCampuses.AsNoTracking()
                .SingleAsync(c => c.VisitRequestId == requestId);

            // Nobody is linked yet: the guest has been INVITED, and the campus waits behind the gate
            // until they answer. That waiting is the whole point of an external contact.
            Assert.Null(instance.OperationalContactUserId);
            Assert.Equal(VisitInstanceStatuses.WaitingContactConfirmation, instance.Status);
            Assert.True(await check.VisitRequestIdentityChanges.AsNoTracking()
                .AnyAsync(c => c.VisitRequestId == requestId && c.Status == IdentityChangeStatuses.Pending));
        }
        finally { await CleanupAsync(requestId); }
    }
}
