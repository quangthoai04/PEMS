using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Application.Common.Security;
using PEMS.Domain.Constants;
using PEMS.Infrastructure.Persistence;
using PEMS.IntegrationTests.TestInfrastructure;
using Xunit;

namespace PEMS.IntegrationTests.VisitRequests;

/// <summary>
/// Actor-relation + authenticated create (POST /api/visit-requests) against the REAL API
/// pipeline and MySQL pems_test (validators, handlers, EF, DB triggers all live):
///   - role × campus-mode matrix (Visitor never direct; Staff self-host own campus only;
///     Staff Leader self/assign own campus only),
///   - registrant/contact account linkage (registrant_user_id + visitor_user_id),
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
    private string _staffEmail = "", _leaderEmail = "";

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
                .Where(v => v.DelegationName.StartsWith(DelegationPrefix))
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
            CreatedAt = DateTime.UtcNow,
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

    private static Dictionary<string, object?> CreatePayload(
        string delegationName,
        string contactEmail,
        (string CampusCode, string Mode, ulong? HostUserId)[] campuses)
    {
        var start = DateTime.Now.AddDays(10).Date.AddHours(9);
        var slots = new List<Dictionary<string, object?>>();
        var processing = new List<Dictionary<string, object?>>();
        var offsetDays = 0;
        foreach (var (code, mode, hostId) in campuses)
        {
            var s = start.AddDays(offsetDays++);
            slots.Add(new Dictionary<string, object?>
            {
                ["campusId"] = code,
                ["startDatetime"] = s.ToString("yyyy-MM-dd'T'HH:mm:ss"),
                ["endDatetime"] = s.AddHours(4).ToString("yyyy-MM-dd'T'HH:mm:ss"),
            });
            processing.Add(new Dictionary<string, object?>
            {
                ["campusId"] = code,
                ["mode"] = mode,
                ["hostUserId"] = hostId,
            });
        }

        return new Dictionary<string, object?>
        {
            // Identity fields are display-only (server overrides them from the JWT user).
            ["registrantFullName"] = "IT Actor Relation",
            ["registrantNationality"] = "Việt Nam",
            ["registrantOrganization"] = "FPT University (IT)",
            ["registrantPosition"] = "IC Staff",
            ["registrantPhone"] = "0912345678",
            ["registrantEmail"] = "spoofed-identity@evil.example.com",
            ["delegationName"] = delegationName,
            ["visitScope"] = campuses.Length > 1 ? "MULTI_CAMPUS" : "SINGLE_CAMPUS",
            ["visitType"] = "CAMPUS_TOUR",
            ["visitTypeOther"] = null,
            ["campusVisits"] = slots,
            ["purpose"] = "Tham quan và trao đổi hợp tác (integration test)",
            ["workingContent"] = null,
            ["visitors"] = Array.Empty<object>(),
            ["supportMembers"] = Array.Empty<object>(),
            ["contactPerson"] = new Dictionary<string, object?>
            {
                ["fullName"] = "IT Đầu Mối",
                ["organization"] = "Công ty Kiểm Thử",
                ["phone"] = "0987654321",
                ["email"] = contactEmail,
            },
            ["isContactSelf"] = false,
            ["workingLanguage"] = "VI",
            ["transportationNote"] = null,
            ["mediaConsentStatus"] = "DECLINED",
            ["mediaConsentNote"] = null,
            ["partnerId"] = null,
            ["notes"] = null,
            ["campusProcessing"] = processing,
            ["confirmedHostConflict"] = false,
            ["submissionId"] = Guid.NewGuid().ToString(),
        };
    }

    private static async Task<string?> ErrorCodeOf(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.TryGetProperty("errorCode", out var code) ? code.GetString() : null;
    }

    // ── Visitor ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Visitor_SelfHostMode_IsRejected_NoRequestCreated()
    {
        var name = DelegationPrefix + "Visitor self-host " + Guid.NewGuid().ToString("N")[..8];
        var payload = CreatePayload(name, UniqueContactEmail(),
            new[] { (_campus1Code, "SELF_HOST", (ulong?)null) });

        var response = await VisitorClient().PostAsJsonAsync("/api/visit-requests", payload);

        Assert.True(response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Forbidden or HttpStatusCode.UnprocessableEntity,
            $"expected 4xx rejection, got {(int)response.StatusCode}");
        Assert.Equal(VisitRequestErrorCodes.InvalidCampusSubmissionMode, await ErrorCodeOf(response));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await db.VisitRequests.AnyAsync(v => v.DelegationName == name));
    }

    [Fact]
    public async Task Visitor_SendForReview_Succeeds_WithRegistrantAndContactLinked()
    {
        var name = DelegationPrefix + "Visitor review " + Guid.NewGuid().ToString("N")[..8];
        var contactEmail = UniqueContactEmail();
        var payload = CreatePayload(name, contactEmail,
            new[] { (_campus1Code, "SEND_FOR_REVIEW", (ulong?)null) });

        var response = await VisitorClient().PostAsJsonAsync("/api/visit-requests", payload);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var vr = await db.VisitRequests.Include(v => v.CampusInstances)
            .FirstAsync(v => v.DelegationName == name);

        Assert.Equal(_visitorId, vr.RegistrantUserId);
        Assert.NotNull(vr.VisitorUserId);
        Assert.NotEqual(_visitorId, vr.VisitorUserId); // contact is a different (new) VISITOR account
        Assert.Equal(VisitRequestStatuses.PendingApproval, vr.Status);
        Assert.All(vr.CampusInstances, i => Assert.Equal("WAITING_REQUEST_APPROVAL", i.Status));
        // Registrant identity came from the DB user, never the payload.
        Assert.NotEqual("spoofed-identity@evil.example.com", vr.RegistrantEmail);
    }

    // ── Regular Staff ───────────────────────────────────────────────────────

    [Fact]
    public async Task Staff_OwnCampus_SelfHost_CreatesAssignedInstance_AggregateApproved()
    {
        var name = DelegationPrefix + "Staff self-host " + Guid.NewGuid().ToString("N")[..8];
        var payload = CreatePayload(name, UniqueContactEmail(),
            new[] { (_campus1Code, "SELF_HOST", (ulong?)null) });

        var response = await StaffClient().PostAsJsonAsync("/api/visit-requests", payload);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"expected 200, got {(int)response.StatusCode}: {body}");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var vr = await db.VisitRequests.Include(v => v.CampusInstances)
            .FirstAsync(v => v.DelegationName == name);

        Assert.Equal(VisitRequestStatuses.Approved, vr.Status);
        Assert.Equal(_staffId, vr.RegistrantUserId);
        Assert.Equal("STAFF_CREATED", vr.CreatedSource);

        var instance = Assert.Single(vr.CampusInstances);
        Assert.Equal("ASSIGNED", instance.Status);
        Assert.Equal(_staffId, instance.CurrentHostUserId);
        Assert.Equal(_staffId, instance.DecidedBy);
        Assert.Equal(_staffId, instance.HostAssignedBy);
        Assert.Equal("STAFF", instance.DecisionActorRole);
        Assert.Equal("INTERNAL_SELF_HOST", instance.DecisionSource);
        // Coordinator remains the campus Staff Leader so the campus keeps a monitor.
        Assert.NotNull(instance.CoordinatorUserId);

        var hostParticipant = await db.VisitParticipants
            .FirstOrDefaultAsync(p => p.VisitInstanceId == instance.VisitInstanceId && p.UserId == _staffId && p.IsHost);
        Assert.NotNull(hostParticipant);
    }

    [Fact]
    public async Task Staff_MultiCampus_SelfHostOwn_OtherPending_AggregatePartiallyApproved()
    {
        var name = DelegationPrefix + "Staff multi " + Guid.NewGuid().ToString("N")[..8];
        var payload = CreatePayload(name, UniqueContactEmail(), new[]
        {
            (_campus1Code, "SELF_HOST", (ulong?)null),
            (_campus2Code, "SEND_FOR_REVIEW", (ulong?)null),
        });

        var response = await StaffClient().PostAsJsonAsync("/api/visit-requests", payload);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"expected 200, got {(int)response.StatusCode}: {body}");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var vr = await db.VisitRequests.Include(v => v.CampusInstances)
            .FirstAsync(v => v.DelegationName == name);

        Assert.Equal(VisitRequestStatuses.PartiallyApproved, vr.Status);
        var own = vr.CampusInstances.First(i => i.CampusId == _campus1Id);
        var other = vr.CampusInstances.First(i => i.CampusId == _campus2Id);
        Assert.Equal("ASSIGNED", own.Status);
        Assert.Equal("WAITING_REQUEST_APPROVAL", other.Status);
        Assert.Null(other.CurrentHostUserId);
    }

    [Fact]
    public async Task Staff_DirectProcess_OtherCampus_IsForbidden()
    {
        var name = DelegationPrefix + "Staff other campus " + Guid.NewGuid().ToString("N")[..8];
        var payload = CreatePayload(name, UniqueContactEmail(),
            new[] { (_campus2Code, "SELF_HOST", (ulong?)null) });

        var response = await StaffClient().PostAsJsonAsync("/api/visit-requests", payload);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await db.VisitRequests.AnyAsync(v => v.DelegationName == name));
    }

    [Fact]
    public async Task Staff_AssignAnotherHost_IsForbidden()
    {
        var name = DelegationPrefix + "Staff assign " + Guid.NewGuid().ToString("N")[..8];
        var payload = CreatePayload(name, UniqueContactEmail(),
            new[] { (_campus1Code, "ASSIGN_HOST", (ulong?)_leaderId) });

        var response = await StaffClient().PostAsJsonAsync("/api/visit-requests", payload);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Staff_OwnEmailAsContact_IsRejected_InternalRegistrantCannotBeContact()
    {
        var name = DelegationPrefix + "Staff self contact " + Guid.NewGuid().ToString("N")[..8];
        var payload = CreatePayload(name, _staffEmail,
            new[] { (_campus1Code, "SEND_FOR_REVIEW", (ulong?)null) });

        var response = await StaffClient().PostAsJsonAsync("/api/visit-requests", payload);
        Assert.True(response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity or HttpStatusCode.Conflict,
            $"expected 4xx rejection, got {(int)response.StatusCode}");
        Assert.Equal(VisitRequestErrorCodes.InternalRegistrantCannotBeContact, await ErrorCodeOf(response));
    }

    [Fact]
    public async Task Staff_OtherInternalEmailAsContact_IsRejected()
    {
        var name = DelegationPrefix + "Staff internal contact " + Guid.NewGuid().ToString("N")[..8];
        var payload = CreatePayload(name, _leaderEmail, // an internal (Staff Leader) email
            new[] { (_campus1Code, "SEND_FOR_REVIEW", (ulong?)null) });

        var response = await StaffClient().PostAsJsonAsync("/api/visit-requests", payload);
        Assert.True(response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict,
            $"expected 400/409, got {(int)response.StatusCode}");
        Assert.Equal(VisitRequestErrorCodes.ContactEmailCannotBeUsedForVisitorAccount, await ErrorCodeOf(response));
    }

    // ── Staff Leader ────────────────────────────────────────────────────────

    [Fact]
    public async Task Leader_OwnCampus_AssignSameCampusIcStaff_ProcessesDirectly()
    {
        var name = DelegationPrefix + "Leader assign " + Guid.NewGuid().ToString("N")[..8];
        var payload = CreatePayload(name, UniqueContactEmail(),
            new[] { (_campus1Code, "ASSIGN_HOST", (ulong?)_staffId) });

        var response = await LeaderClient().PostAsJsonAsync("/api/visit-requests", payload);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"expected 200, got {(int)response.StatusCode}: {body}");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var vr = await db.VisitRequests.Include(v => v.CampusInstances)
            .FirstAsync(v => v.DelegationName == name);

        var instance = Assert.Single(vr.CampusInstances);
        Assert.Equal("ASSIGNED", instance.Status);
        Assert.Equal(_staffId, instance.CurrentHostUserId);
        Assert.Equal(_leaderId, instance.DecidedBy);
        Assert.Equal("STAFF_LEADER", instance.DecisionActorRole);
        Assert.Equal("INTERNAL_LEADER_ASSIGN", instance.DecisionSource);
        Assert.Equal(VisitRequestStatuses.Approved, vr.Status);
    }

    [Fact]
    public async Task Leader_AssignVisitorAsHost_IsRejected_InvalidCandidate()
    {
        var name = DelegationPrefix + "Leader bad host " + Guid.NewGuid().ToString("N")[..8];
        var payload = CreatePayload(name, UniqueContactEmail(),
            new[] { (_campus1Code, "ASSIGN_HOST", (ulong?)_visitorId) });

        var response = await LeaderClient().PostAsJsonAsync("/api/visit-requests", payload);
        Assert.True(response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity,
            $"expected 400, got {(int)response.StatusCode}");
        Assert.Equal(VisitRequestErrorCodes.InvalidHostCandidate, await ErrorCodeOf(response));
    }

    // ── Registrant relation stays read-only ────────────────────────────────

    [Fact]
    public async Task StaffRegistrant_CannotCancel_OwnRegisteredRequest()
    {
        var name = DelegationPrefix + "Staff no-cancel " + Guid.NewGuid().ToString("N")[..8];
        var payload = CreatePayload(name, UniqueContactEmail(),
            new[] { (_campus1Code, "SEND_FOR_REVIEW", (ulong?)null) });

        var client = StaffClient();
        var createResponse = await client.PostAsJsonAsync("/api/visit-requests", payload);
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

        ulong requestId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            requestId = await db.VisitRequests.Where(v => v.DelegationName == name)
                .Select(v => v.VisitRequestId).FirstAsync();
        }

        // The registrant relation never grants owner mutations: cancel → 403.
        var cancelResponse = await client.PostAsJsonAsync(
            $"/api/Delegations/{requestId}/cancel",
            new Dictionary<string, object?> { ["cancellationReason"] = "IT registrant must not cancel" });
        Assert.Equal(HttpStatusCode.Forbidden, cancelResponse.StatusCode);
    }

    [Fact]
    public async Task RegisteredTab_ReturnsReadOnlyRow_ForStaffRegistrant()
    {
        var name = DelegationPrefix + "Staff registered tab " + Guid.NewGuid().ToString("N")[..8];
        var payload = CreatePayload(name, UniqueContactEmail(),
            new[] { (_campus1Code, "SEND_FOR_REVIEW", (ulong?)null) });

        var client = StaffClient();
        var createResponse = await client.PostAsJsonAsync("/api/visit-requests", payload);
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
