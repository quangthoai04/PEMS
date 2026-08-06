using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Application.Common.Security;
using PEMS.Domain.Constants;
using PEMS.Shared;
using PEMS.Infrastructure.Persistence;
using PEMS.IntegrationTests.TestInfrastructure;
using Xunit;
using PEMS.Application.Common;

namespace PEMS.IntegrationTests.VisitRequests;

/// <summary>
/// Actor-relation + authenticated create (POST /api/visit-requests) against the REAL API
/// pipeline and MySQL pems_test (validators, handlers, EF, DB triggers all live):
///   - role × host-arrangement matrix (an external submit never names a host; Staff may propose
///     THEMSELF on their own campus; a Staff Leader may also propose a same-campus IC Staff),
///   - registrant/contact account linkage (visit_requests.registrant_user_id +
///     visit_request_campuses.operational_contact_user_id, one per campus),
///   - aggregate status (APPROVED / PARTIALLY_APPROVED / PENDING_APPROVAL),
///   - the registrant relation staying strictly read-only (mutations → 403),
///   - the "registered" list tab returning read-only rows.
/// </summary>
public sealed class ActorRelationAuthenticatedCreateApiTests : IAsyncLifetime
{
    private const string DelegationPrefix = "[IT-ACTOR-REL] ";

    private readonly PemsWebApplicationFactory _factory = new();

    private ulong _staffId, _staffSessionId;
    private ulong _leaderId, _leaderSessionId;
    private ulong _visitorId, _visitorSessionId;
    private ulong _campus1Id, _campus2Id;
    private string _campus1Code = "", _campus2Code = "";
    private string _staffEmail = "", _leaderEmail = "", _visitorEmail = "";

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        _staffId = await DatabaseResetHelper.EnsureTestUserAsync(db, EffectiveRole.Staff);
        _leaderId = await DatabaseResetHelper.EnsureTestUserAsync(db, EffectiveRole.StaffLeader);
        _visitorId = await DatabaseResetHelper.EnsureTestUserAsync(db, EffectiveRole.Visitor);

        _staffSessionId = await DatabaseResetHelper.CreateActiveSessionAsync(db, _staffId, EffectiveRole.Staff);
        _leaderSessionId = await DatabaseResetHelper.CreateActiveSessionAsync(db, _leaderId, EffectiveRole.StaffLeader);
        _visitorSessionId = await DatabaseResetHelper.CreateActiveSessionAsync(db, _visitorId, EffectiveRole.Visitor);

        _staffEmail = await db.Users.AsNoTracking().Where(u => u.UserId == _staffId).Select(u => u.Email).FirstAsync();
        _leaderEmail = await db.Users.AsNoTracking().Where(u => u.UserId == _leaderId).Select(u => u.Email).FirstAsync();
        _visitorEmail = await db.Users.AsNoTracking().Where(u => u.UserId == _visitorId).Select(u => u.Email).FirstAsync();

        var staff = await db.Users.AsNoTracking().FirstAsync(u => u.UserId == _staffId);
        _campus1Id = staff.PrimaryCampusId!.Value;
        _campus1Code = await db.Campuses.AsNoTracking()
            .Where(c => c.CampusId == _campus1Id).Select(c => c.CampusCode).FirstAsync();

        var campus2 = await db.Campuses.AsNoTracking()
            .Where(c => c.Status == "ACTIVE" && c.CampusId != _campus1Id)
            .OrderBy(c => c.CampusId)
            .FirstAsync();
        _campus2Id = campus2.CampusId;
        _campus2Code = campus2.CampusCode;

        // Every selected campus needs an ACTIVE Staff Leader (routing rule) — ensure one on
        // campus 2 as well (campus 1 is covered by the test Staff Leader above).
        await EnsureLeaderOnCampusAsync(db, _campus2Id);
    }

    public async Task DisposeAsync()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var requestIds = await db.VisitRequests
                // Pure V2: the delegation name lives on each campus instance's detail, so a request is
                // matched through any of its instances.
                .Where(v => v.CampusInstances.Any(c => c.FormDetail!.DelegationName.StartsWith(DelegationPrefix)))
                .Select(v => v.VisitRequestId)
                .ToListAsync();

            if (requestIds.Count > 0)
            {
                var instanceIds = await db.VisitRequestCampuses
                    .Where(c => requestIds.Contains(c.VisitRequestId))
                    .Select(c => c.VisitInstanceId)
                    .ToListAsync();

                await db.Notifications.Where(n => n.VisitRequestId != null && requestIds.Contains(n.VisitRequestId.Value)).ExecuteDeleteAsync();
                await db.VisitParticipants.Where(p => instanceIds.Contains(p.VisitInstanceId)).ExecuteDeleteAsync();
                await db.VisitRequestCampuses.Where(c => requestIds.Contains(c.VisitRequestId)).ExecuteDeleteAsync();
                await db.VisitGuestMembers.Where(g => requestIds.Contains(g.VisitRequestId)).ExecuteDeleteAsync();
                await db.VisitRequests.Where(v => requestIds.Contains(v.VisitRequestId)).ExecuteDeleteAsync();
            }
        }

        await _factory.DisposeAsync();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static async Task EnsureLeaderOnCampusAsync(ApplicationDbContext db, ulong campusId)
    {
        // A campus must have EXACTLY ONE valid Staff Leader for visit-registration availability
        // (BR-86-19/20). The seed already provides one per campus, so reuse it — never add a second,
        // which would make the campus configuration-invalid and fail every create on it.
        var alreadyHasValidLeader = await db.Users.AsNoTracking().AnyAsync(u =>
            u.Role!.RoleCode == "STAFF"
            && u.SubRole == "LEADER"
            && u.Status == "ACTIVE"
            && u.PrimaryCampusId == campusId
            && u.Department != null
            && u.Department.DepartmentType == "IC"
            && u.Department.Status == "ACTIVE"
            && u.Department.CampusId == campusId);
        if (alreadyHasValidLeader)
            return;

        var email = $"it-actor-rel-leader-c{campusId}@it-uc63.pems.local";
        if (await db.Users.AsNoTracking().AnyAsync(u => u.Email == email))
            return;

        var role = await db.Roles.AsNoTracking().FirstAsync(r => r.RoleCode == "STAFF");
        var icDept = await db.Departments.AsNoTracking()
            .Where(d => d.CampusId == campusId && d.DepartmentType == "IC" && d.Status == "ACTIVE")
            .Select(d => (ulong?)d.DepartmentId)
            .FirstOrDefaultAsync();
        if (icDept is null)
            return; // seed already guarantees IC departments; nothing sensible to do otherwise

        db.Users.Add(new PEMS.Domain.Entities.Users.User
        {
            FullName = "[IT-ACTOR-REL] Leader C2",
            Email = email,
            RoleId = role.RoleId,
            SubRole = "LEADER",
            PrimaryCampusId = campusId,
            DepartmentId = icDept,
            Status = "ACTIVE",
            CreatedVia = "MANUAL_CREATED",
            CreatedAt = VietnamTime.Now(),
        });
        await db.SaveChangesAsync();
    }

    private HttpClient CreateClient(ulong userId, string roleCode, string? subRole, ulong sessionId, ulong? campusId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.RoleCodeHeader, roleCode);
        if (!string.IsNullOrEmpty(subRole))
            client.DefaultRequestHeaders.Add(TestAuthHandler.SubRoleHeader, subRole);
        client.DefaultRequestHeaders.Add(TestAuthHandler.SessionIdHeader, sessionId.ToString());
        if (campusId.HasValue)
            client.DefaultRequestHeaders.Add(TestAuthHandler.PrimaryCampusIdHeader, campusId.Value.ToString());
        return client;
    }

    private HttpClient StaffClient() => CreateClient(_staffId, "STAFF", "STAFF", _staffSessionId, _campus1Id);
    private HttpClient LeaderClient() => CreateClient(_leaderId, "STAFF", "LEADER", _leaderSessionId, _campus1Id);
    private HttpClient VisitorClient() => CreateClient(_visitorId, "VISITOR", null, _visitorSessionId, null);

    private static string UniqueContactEmail() => $"it-actor-rel-contact-{Guid.NewGuid():N}@example.com";

    private Dictionary<string, object?> CreatePayload(
        string delegationName,
        string contactEmail,
        (string CampusCode, string HostSelectionMode, ulong? ProposedHostUserId)[] campuses,
        string? registrantEmailOverride = null)
    {
        return V2TestDataBuilder.BuildCreatePayload(
            delegationName: delegationName,
            registrantEmail: registrantEmailOverride ?? _visitorEmail,
            contactEmail: contactEmail,
            campuses: campuses);
    }

    private static async Task<string?> ErrorCodeOf(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.TryGetProperty("errorCode", out var code) ? code.GetString() : null;
    }

    // ── Visitor ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Visitor_ProposingAHost_IsRejected_NoRequestCreated()
    {
        var name = DelegationPrefix + "Visitor proposes host " + Guid.NewGuid().ToString("N")[..8];
        var payload = CreatePayload(name, UniqueContactEmail(),
            new[] { (_campus1Code, "SELF", (ulong?)null) });

        var response = await VisitorClient().PostAsJsonAsync("/api/v2/visit-requests", payload);

        // Refused, not silently downgraded: a forged payload has to be distinguishable from a clean
        // one, or the client cannot tell whether the intent applied.
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(VisitRequestErrorCodes.ProposedHostNotAllowedForRole, await ErrorCodeOf(response));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await db.VisitRequests.AnyAsync(v => v.CampusInstances.Any(c => c.FormDetail!.DelegationName == name)));
        Assert.False(await db.VisitRequestCampuses.AnyAsync(c => c.FormDetail!.DelegationName == name));
        Assert.False(await db.Notifications.AnyAsync(n => n.Message.Contains(name)));
    }

    [Fact]
    public async Task Visitor_WaitForLater_Succeeds_WithRegistrantAndContactLinked()
    {
        var name = DelegationPrefix + "Visitor review " + Guid.NewGuid().ToString("N")[..8];
        var contactEmail = UniqueContactEmail();
        var payload = CreatePayload(name, contactEmail,
            new[] { (_campus1Code, "WAIT_FOR_LATER", (ulong?)null) });

        var response = await VisitorClient().PostAsJsonAsync("/api/v2/visit-requests", payload);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var vr = await db.VisitRequests.Include(v => v.CampusInstances)
            .FirstAsync(v => v.CampusInstances.Any(c => c.FormDetail!.DelegationName == name));

        Assert.Equal(_visitorId, vr.RegistrantUserId);
        Assert.Equal(_visitorEmail, vr.RegistrantEmail);
        // The contact is per campus: nobody holds any campus yet, and the invitation names the
        // campus it was sent for.
        Assert.All(vr.CampusInstances, c => Assert.Null(c.OperationalContactUserId));
        var claim = await db.VisitRequestIdentityChanges.AsNoTracking()
            .FirstOrDefaultAsync(c => c.VisitRequestId == vr.VisitRequestId
                                      && c.ChangeKind == IdentityChangeKinds.InitialConfirmation);
        Assert.NotNull(claim);
        Assert.Equal("PENDING", claim!.Status);
        Assert.Equal(contactEmail.ToLowerInvariant(), claim.NewEmailNormalized);
        Assert.Contains(vr.CampusInstances, c => c.VisitInstanceId == claim.VisitInstanceId);
        // An outstanding confirmation holds the WHOLE request at the gate, and the campus it was sent
        // for waits with it — WAITING_REQUEST_APPROVAL is where a campus lands only once its own
        // contact has confirmed.
        Assert.Equal(VisitRequestStatuses.PendingContactConfirmation, vr.Status);
        Assert.All(vr.CampusInstances, i => Assert.Equal("WAITING_CONTACT_CONFIRMATION", i.Status));
    }

    /// <summary>
    /// Vietnam-time policy (AC-01/AC-03): submitted_at/created_at persist as Vietnam
    /// wall-clock (not UTC −7h), and the planned slot round-trips verbatim with no shift.
    /// </summary>
    [Fact]
    public async Task Create_Stores_VietnamWallClock_Timestamps_And_PlannedTimes_Unshifted()
    {
        var name = DelegationPrefix + "VN time " + Guid.NewGuid().ToString("N")[..8];
        var payload = CreatePayload(name, UniqueContactEmail(),
            new[] { (_campus1Code, "WAIT_FOR_LATER", (ulong?)null) });

        var slots = (List<Dictionary<string, object?>>)payload["campusVisits"]!;
        var expectedStart = DateTime.Parse((string)slots[0]["plannedStartAt"]!);

        var before = VietnamTime.Now().AddMinutes(-1);
        var response = await VisitorClient().PostAsJsonAsync("/api/v2/visit-requests", payload);
        var after = VietnamTime.Now().AddMinutes(1);

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"expected 200, got {(int)response.StatusCode}: {body}");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var vr = await db.VisitRequests.Include(v => v.CampusInstances)
            .FirstAsync(v => v.CampusInstances.Any(c => c.FormDetail!.DelegationName == name));

        Assert.InRange(vr.SubmittedAt, before, after);
        Assert.InRange(vr.CreatedAt, before, after);

        var instance = Assert.Single(vr.CampusInstances);
        Assert.Equal(expectedStart, instance.PlannedStartAt);
    }

    // ── Regular Staff ───────────────────────────────────────────────────────

    [Fact]
    public async Task Staff_OwnCampus_ProposesSelf_StoresProposalWithNoCurrentHost()
    {
        var name = DelegationPrefix + "Staff proposes self " + Guid.NewGuid().ToString("N")[..8];
        // The contact is somebody OUTSIDE FPTU: an internal registrant may never be their own
        // campus contact, so the request legitimately sits behind the gate. That is exactly the
        // state under test — a proposal is recorded and NOBODY is hosting anything yet.
        var payload = CreatePayload(name, UniqueContactEmail(),
            new[] { (_campus1Code, "SELF", (ulong?)null) },
            registrantEmailOverride: _staffEmail);

        var response = await StaffClient().PostAsJsonAsync("/api/v2/visit-requests", payload);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"expected 200, got {(int)response.StatusCode}: {body}");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var vr = await db.VisitRequests.Include(v => v.CampusInstances)
            .FirstAsync(v => v.CampusInstances.Any(c => c.FormDetail!.DelegationName == name));

        Assert.Equal(VisitRequestStatuses.PendingContactConfirmation, vr.Status);
        Assert.Equal(_staffId, vr.RegistrantUserId);
        Assert.Equal("STAFF_CREATED", vr.CreatedSource);

        var instance = Assert.Single(vr.CampusInstances);
        // The proposal is recorded...
        Assert.Equal(HostSelectionModes.Self, instance.HostSelectionMode);
        Assert.Equal(_staffId, instance.ProposedHostUserId);
        Assert.Equal(_staffId, instance.ProposedHostByUserId);
        Assert.Equal(ProposedHostActivationStatuses.Pending, instance.ProposedHostActivationStatus);
        Assert.NotNull(instance.ProposedHostAt);

        // ...and NOTHING has been assigned. This is the whole point of the model: until somebody on
        // the guest side confirms they are coming there is no visit to host, so naming a current
        // host here would tell an FPTU staff member to start preparing for nothing.
        Assert.Equal("WAITING_CONTACT_CONFIRMATION", instance.Status);
        Assert.Null(instance.CurrentHostUserId);
        Assert.Null(instance.DecidedBy);
        Assert.Null(instance.DecisionSource);
        Assert.Null(instance.ProposedHostActivatedAt);
        Assert.NotNull(instance.CoordinatorUserId);

        // No IC_HOST participant either — being proposed is not being on the team.
        Assert.False(await db.VisitParticipants
            .AnyAsync(p => p.VisitInstanceId == instance.VisitInstanceId && p.IsHost));
    }

    [Fact]
    public async Task Staff_MultiCampus_ProposalOnOwnCampusOnly_OtherWaitsForAssignment()
    {
        var name = DelegationPrefix + "Staff multi " + Guid.NewGuid().ToString("N")[..8];
        var payload = CreatePayload(name, UniqueContactEmail(), new[]
        {
            (_campus1Code, "SELF", (ulong?)null),
            (_campus2Code, "WAIT_FOR_LATER", (ulong?)null),
        },
        registrantEmailOverride: _staffEmail);

        var response = await StaffClient().PostAsJsonAsync("/api/v2/visit-requests", payload);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"expected 200, got {(int)response.StatusCode}: {body}");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var vr = await db.VisitRequests.Include(v => v.CampusInstances)
            .FirstAsync(v => v.CampusInstances.Any(c => c.FormDetail!.DelegationName == name));

        // Both campuses are behind the gate: the gate is a property of the REQUEST, so a campus
        // with an arrangement cannot run ahead of a sibling nobody has confirmed.
        Assert.Equal(VisitRequestStatuses.PendingContactConfirmation, vr.Status);
        var own = vr.CampusInstances.First(i => i.CampusId == _campus1Id);
        var other = vr.CampusInstances.First(i => i.CampusId == _campus2Id);

        // The arrangement stays on the campus it was made for. A proposal on the creator's own
        // campus says nothing about the sibling, which waits for its own Staff Leader.
        Assert.Equal(HostSelectionModes.Self, own.HostSelectionMode);
        Assert.Equal(_staffId, own.ProposedHostUserId);
        Assert.Equal(HostSelectionModes.WaitForLater, other.HostSelectionMode);
        Assert.Null(other.ProposedHostUserId);

        Assert.Equal("WAITING_CONTACT_CONFIRMATION", own.Status);
        Assert.Equal("WAITING_CONTACT_CONFIRMATION", other.Status);
        Assert.Null(own.CurrentHostUserId);
        Assert.Null(other.CurrentHostUserId);
    }

    [Fact]
    public async Task Staff_ProposingForAnotherCampus_IsForbidden()
    {
        var name = DelegationPrefix + "Staff other campus " + Guid.NewGuid().ToString("N")[..8];
        var payload = CreatePayload(name, UniqueContactEmail(),
            new[] { (_campus2Code, "SELF", (ulong?)null) },
            registrantEmailOverride: _staffEmail);

        var response = await StaffClient().PostAsJsonAsync("/api/v2/visit-requests", payload);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await db.VisitRequests.AnyAsync(v => v.CampusInstances.Any(c => c.FormDetail!.DelegationName == name)));
        Assert.False(await db.VisitRequestCampuses.AnyAsync(c => c.FormDetail!.DelegationName == name));
        Assert.False(await db.Notifications.AnyAsync(n => n.Message.Contains(name)));
    }

    [Fact]
    public async Task Staff_ProposingSomebodyElse_IsForbidden()
    {
        var name = DelegationPrefix + "Staff assign " + Guid.NewGuid().ToString("N")[..8];
        var payload = CreatePayload(name, UniqueContactEmail(),
            new[] { (_campus1Code, "SELECTED", (ulong?)_leaderId) },
            registrantEmailOverride: _staffEmail);

        var response = await StaffClient().PostAsJsonAsync("/api/v2/visit-requests", payload);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await db.VisitRequests.AnyAsync(v => v.CampusInstances.Any(c => c.FormDetail!.DelegationName == name)));
        Assert.False(await db.VisitRequestCampuses.AnyAsync(c => c.FormDetail!.DelegationName == name));
        Assert.False(await db.Notifications.AnyAsync(n => n.Message.Contains(name)));
    }

    [Fact]
    public async Task Staff_OwnEmailAsContact_IsRejected_InternalRegistrantCannotBeContact()
    {
        var name = DelegationPrefix + "Staff self contact " + Guid.NewGuid().ToString("N")[..8];
        var payload = CreatePayload(name, _staffEmail,
            new[] { (_campus1Code, "WAIT_FOR_LATER", (ulong?)null) },
            registrantEmailOverride: _staffEmail);

        var response = await StaffClient().PostAsJsonAsync("/api/v2/visit-requests", payload);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(VisitRequestErrorCodes.InternalRegistrantCannotBeContact, await ErrorCodeOf(response));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await db.VisitRequests.AnyAsync(v => v.CampusInstances.Any(c => c.FormDetail!.DelegationName == name)));
        Assert.False(await db.VisitRequestCampuses.AnyAsync(c => c.FormDetail!.DelegationName == name));
        Assert.False(await db.Notifications.AnyAsync(n => n.Message.Contains(name)));
    }

    [Fact]
    public async Task Staff_OtherInternalEmailAsContact_IsRejected()
    {
        var name = DelegationPrefix + "Staff internal contact " + Guid.NewGuid().ToString("N")[..8];
        var payload = CreatePayload(name, _leaderEmail,
            new[] { (_campus1Code, "WAIT_FOR_LATER", (ulong?)null) },
            registrantEmailOverride: _staffEmail);

        var response = await StaffClient().PostAsJsonAsync("/api/v2/visit-requests", payload);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(VisitRequestErrorCodes.ContactEmailCannotBeUsedForVisitorAccount, await ErrorCodeOf(response));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await db.VisitRequests.AnyAsync(v => v.CampusInstances.Any(c => c.FormDetail!.DelegationName == name)));
        Assert.False(await db.VisitRequestCampuses.AnyAsync(c => c.FormDetail!.DelegationName == name));
        Assert.False(await db.Notifications.AnyAsync(n => n.Message.Contains(name)));
    }

    // ── Staff Leader ────────────────────────────────────────────────────────

    [Fact]
    public async Task Leader_OwnCampus_ProposesSameCampusIcStaff_StoresProposalWithNoCurrentHost()
    {
        var name = DelegationPrefix + "Leader proposes " + Guid.NewGuid().ToString("N")[..8];
        var payload = CreatePayload(name, UniqueContactEmail(),
            new[] { (_campus1Code, "SELECTED", (ulong?)_staffId) },
            registrantEmailOverride: _leaderEmail);

        var response = await LeaderClient().PostAsJsonAsync("/api/v2/visit-requests", payload);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"expected 200, got {(int)response.StatusCode}: {body}");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var vr = await db.VisitRequests.Include(v => v.CampusInstances)
            .FirstAsync(v => v.CampusInstances.Any(c => c.FormDetail!.DelegationName == name));

        var instance = Assert.Single(vr.CampusInstances);
        Assert.Equal(HostSelectionModes.Selected, instance.HostSelectionMode);
        Assert.Equal(_staffId, instance.ProposedHostUserId);
        // The PROPOSER is recorded separately from the person proposed: it is the proposer whose
        // authority the activation rests on, and who is recorded as having decided it later.
        Assert.Equal(_leaderId, instance.ProposedHostByUserId);
        Assert.Equal(ProposedHostActivationStatuses.Pending, instance.ProposedHostActivationStatus);

        Assert.Equal("WAITING_CONTACT_CONFIRMATION", instance.Status);
        Assert.Null(instance.CurrentHostUserId);
        Assert.Null(instance.DecidedBy);
        Assert.Equal(VisitRequestStatuses.PendingContactConfirmation, vr.Status);
    }

    [Fact]
    public async Task Leader_ProposingAVisitorAsHost_IsRejected_InvalidCandidate()
    {
        var name = DelegationPrefix + "Leader bad host " + Guid.NewGuid().ToString("N")[..8];
        var payload = CreatePayload(name, UniqueContactEmail(),
            new[] { (_campus1Code, "SELECTED", (ulong?)_visitorId) },
            registrantEmailOverride: _leaderEmail);

        var response = await LeaderClient().PostAsJsonAsync("/api/v2/visit-requests", payload);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(VisitRequestErrorCodes.InvalidHostCandidate, await ErrorCodeOf(response));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await db.VisitRequests.AnyAsync(v => v.CampusInstances.Any(c => c.FormDetail!.DelegationName == name)));
        Assert.False(await db.VisitRequestCampuses.AnyAsync(c => c.FormDetail!.DelegationName == name));
        Assert.False(await db.Notifications.AnyAsync(n => n.Message.Contains(name)));
    }

    // ── Registrant relation stays read-only ────────────────────────────────

    [Fact]
    public async Task StaffRegistrant_CannotCancel_OwnRequestWhileItWaitsForContactConfirmation()
    {
        var name = DelegationPrefix + "Staff no-cancel " + Guid.NewGuid().ToString("N")[..8];
        // What this test is about is that the registrant relation stays read-only, whatever the
        // request's gate state — an internal creator never acquires guest-side rights by creating.
        var payload = CreatePayload(name, UniqueContactEmail(),
            new[] { (_campus1Code, "WAIT_FOR_LATER", (ulong?)null) },
            registrantEmailOverride: _staffEmail);

        var client = StaffClient();
        var createResponse = await client.PostAsJsonAsync("/api/v2/visit-requests", payload);
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

        ulong requestId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            requestId = await db.VisitRequests.Where(v => v.CampusInstances.Any(c => c.FormDetail!.DelegationName == name))
                .Select(v => v.VisitRequestId).FirstAsync();
        }

        var cancelResponse = await client.PostAsJsonAsync(
            $"/api/Delegations/{requestId}/cancel",
            new Dictionary<string, object?> { ["cancellationReason"] = "IT registrant must not cancel" });

        // Refused, and the reason is the lifecycle rather than the relation: cancelling is defined
        // for a request that is pending approval or already approved, and this one is neither — it
        // is still waiting for somebody on the guest side to confirm they are coming at all.
        Assert.Equal(HttpStatusCode.UnprocessableEntity, cancelResponse.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var vr = await db.VisitRequests.Include(v => v.CampusInstances)
                .FirstAsync(v => v.VisitRequestId == requestId);
            // Nothing moved. A refused cancel must not half-cancel anything.
            Assert.Equal(VisitRequestStatuses.PendingContactConfirmation, vr.Status);
            Assert.All(vr.CampusInstances, c => Assert.NotEqual("CANCELLED", c.Status));
        }
    }

    [Fact]
    public async Task RegisteredTab_ReturnsReadOnlyRow_ForStaffRegistrant()
    {
        var name = DelegationPrefix + "Staff registered tab " + Guid.NewGuid().ToString("N")[..8];
        var payload = CreatePayload(name, UniqueContactEmail(),
            new[] { (_campus1Code, "WAIT_FOR_LATER", (ulong?)null) },
            registrantEmailOverride: _staffEmail);

        var client = StaffClient();
        var createResponse = await client.PostAsJsonAsync("/api/v2/visit-requests", payload);
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

        var listResponse = await client.GetAsync(
            $"/api/Delegations/viewguestdelegationlist?tab=registered&page=1&pageSize=50&keyword={Uri.EscapeDataString(name)}");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        var json = await listResponse.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var items = doc.RootElement.GetProperty("items");
        Assert.True(items.GetArrayLength() >= 1, $"registered tab returned no rows: {json}");

        var row = items[0];
        Assert.Equal("REGISTRANT_VIEWER", row.GetProperty("currentUserRelation").GetString());
        Assert.True(row.GetProperty("isReadOnly").GetBoolean());
        var actions = row.GetProperty("allowedActions").EnumerateArray().Select(a => a.GetString()).ToList();
        var action = Assert.Single(actions);
        Assert.Equal("VIEW_DETAIL", action);
    }
}
