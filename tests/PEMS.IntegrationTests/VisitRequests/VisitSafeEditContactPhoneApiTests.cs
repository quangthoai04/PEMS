using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PEMS.Application.Common.DTOs;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Options;
using PEMS.Application.Common.Security;
using PEMS.Application.Delegations.Commands.CreateVisitRequestV2;
using PEMS.Application.Delegations.Services;
using PEMS.Application.Notifications.Common;
using PEMS.Domain.Constants;
using PEMS.Infrastructure.Persistence;
using PEMS.Infrastructure.Services;
using PEMS.IntegrationTests.TestInfrastructure;
using Xunit;

namespace PEMS.IntegrationTests.VisitRequests;

/// <summary>
/// GitHub bug report (CanhIter3FixBug live-UI repro): PATCH /api/v2/visit-requests/{id}/safe-details
/// with an Operational Contact block whose Phone is null/blank returned "The Phone field is required."
/// Every OTHER Safe Edit test in this suite calls <see cref="PEMS.Infrastructure.Services.VisitSafeEditService"/>
/// directly, in-process — none of them ever go through real ASP.NET Core JSON model binding, so none of
/// them could have caught a model-binding-layer defect. This file exercises the REAL HTTP pipeline
/// (<see cref="PemsWebApplicationFactory"/>: real routing, real <c>[ApiController]</c> automatic
/// model validation, real System.Text.Json body binding) specifically to close that gap.
/// </summary>
public sealed class VisitSafeEditContactPhoneApiTests : IAsyncLifetime
{
    private const string DelegationPrefix = "[IT-SE-PHONE] ";
    private readonly PemsWebApplicationFactory _factory = new();
    private ulong _registrantId, _registrantSessionId;
    private string _registrantEmail = "";

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        _registrantId = await DatabaseResetHelper.EnsureTestUserAsync(db, EffectiveRole.Visitor);
        _registrantSessionId = await DatabaseResetHelper.CreateActiveSessionAsync(db, _registrantId, EffectiveRole.Visitor);
        _registrantEmail = await db.Users.AsNoTracking()
            .Where(u => u.UserId == _registrantId).Select(u => u.Email).FirstAsync();
    }

    public async Task DisposeAsync()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var requestIds = await db.VisitRequests
                .Where(v => v.CampusInstances.Any(c => c.FormDetail!.DelegationName.StartsWith(DelegationPrefix)))
                .Select(v => v.VisitRequestId).ToListAsync();
            foreach (var id in requestIds)
            {
                async Task Del(string sql) => await db.Database.ExecuteSqlRawAsync(sql, id);
                await Del("DELETE FROM visit_instance_form_revision_history WHERE visit_request_id = {0}");
                await Del("DELETE FROM visit_request_revision_history WHERE visit_request_id = {0}");
                await Del("DELETE FROM visit_instance_guest_members WHERE visit_request_id = {0}");
                await Del("DELETE FROM visit_guest_members WHERE visit_request_id = {0}");
                await Del("DELETE FROM visit_instance_form_details WHERE visit_instance_id IN (SELECT visit_instance_id FROM visit_request_campuses WHERE visit_request_id = {0})");
                await Del("DELETE FROM visit_request_campuses WHERE visit_request_id = {0}");
                await Del("DELETE alc FROM audit_log_changes alc JOIN audit_logs al ON al.audit_log_id = alc.audit_log_id WHERE al.visit_request_id = {0}");
                await Del("DELETE FROM audit_logs WHERE visit_request_id = {0}");
                await Del("DELETE FROM visit_requests WHERE visit_request_id = {0}");
            }
        }
        await _factory.DisposeAsync();
    }

    private HttpClient RegistrantClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, _registrantId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.RoleCodeHeader, "VISITOR");
        client.DefaultRequestHeaders.Add(TestAuthHandler.SessionIdHeader, _registrantSessionId.ToString());
        return client;
    }

    /// <summary>Builds + approves (to ASSIGNED) a one-campus request with a self-matched contact, via the
    /// real create handler + direct DB transition — identical shape to VisitSafeEditV2Tests' own fixture,
    /// just against the factory's scoped context instead of a bare connection string.</summary>
    private async Task<(ulong RequestId, ulong InstanceId, int InstanceRowVersion, int RequestRowVersion)> SeedAssignedRequestAsync(
        string? phone)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var readOn = new PerCampusFormV2Options { Enabled = true };
        var writeOn = new PerCampusFormV2WriteOptions { Enabled = true };
        var now = DateTime.Now;
        var start = now.AddDays(20);

        var handler = new CreateVisitRequestV2CommandHandler(
            db, new StaticUser(_registrantId), new StaticClock(now), new VisitRequestV2CreateService(db),
            new NoopNotifications(), new NoopInvitationService(), new UserProvisionService(db),
            NullLogger<CreateVisitRequestV2CommandHandler>.Instance, readOn, writeOn,
            new VisitRequestAggregateStatusService(db),
            new ProposedHostActivationService(db, new MySqlUserMutationLockService(db)),
            new MySqlUserMutationLockService(db));

        var campus = new CampusVisitFormDto(
            await FirstCampusCodeAsync(db), start, start.AddMinutes(120),
            DelegationPrefix + Guid.NewGuid().ToString("N")[..8], "MEETING", null, "Thăm", "Nội dung",
            new List<VisitorDto> { new("Guest A", "VN", "Guest", "GuestOrg") },
            new List<SupportTeamMemberDto>(),
            new ContactPointDto("Kim Min Jae", "SeoulTech Global Engagement Center",
                "International Partnerships Manager", phone, _registrantEmail),
            "EN", "Xe 16 chỗ", "AGREED", null, null);

        var form = new VisitRequestFormDataV2(
            "SEP" + Guid.NewGuid().ToString("N"),
            new RegistrantInputV2("Registrant", "VN", "Org", "Job", "+8491", _registrantEmail),
            null, new List<CampusVisitFormDto> { campus });
        var created = await handler.Handle(new CreateVisitRequestV2Command(form), CancellationToken.None);

        var visit = await db.VisitRequests.Include(v => v.CampusInstances)
            .SingleAsync(v => v.VisitRequestId == created.VisitRequestId);
        foreach (var instance in visit.CampusInstances)
        {
            instance.Status = VisitInstanceStatuses.Assigned;
            instance.CurrentHostUserId = instance.CoordinatorUserId;
            instance.HostAssignedBy = instance.CoordinatorUserId;
            instance.HostAssignedAt = now;
            instance.DecidedBy = instance.CoordinatorUserId;
            instance.DecidedAt = now;
            instance.DecisionActorRole = "STAFF_LEADER";
            instance.DecisionSource = "STANDARD_CAMPUS_REVIEW";
            instance.RowVersion += 1;
        }
        await db.SaveChangesAsync();
        visit.Status = VisitRequestStatuses.Approved;
        visit.RowVersion += 1;
        await db.SaveChangesAsync();

        var instanceId = visit.CampusInstances.Single().VisitInstanceId;
        return (visit.VisitRequestId, instanceId, visit.CampusInstances.Single().RowVersion, visit.RowVersion);
    }

    private static async Task<string> FirstCampusCodeAsync(ApplicationDbContext db)
        => await db.Campuses.AsNoTracking().OrderBy(c => c.CampusId).Select(c => c.CampusCode).FirstAsync();

    private sealed class StaticUser : ICurrentUserService
    {
        private readonly ulong _id;
        public StaticUser(ulong id) => _id = id;
        public bool IsAuthenticated => true;
        public ulong? UserId => _id;
        public string? Email => null;
        public ulong? RoleId => null;
        public string? RoleCode => RoleCodes.Visitor;
        public string? SubRole => null;
        public ulong? PrimaryCampusId => null;
        public ulong? DepartmentId => null;
        public ulong? SessionId => null;
        public string? LoginPortal => null;
    }

    private sealed class StaticClock : IDateTimeService
    {
        private readonly DateTime _now;
        public StaticClock(DateTime now) => _now = now;
        public DateTime UtcNow => DateTime.UtcNow;
        public DateTime VietnamNow => _now;
    }

    private sealed class NoopNotifications : INotificationService
    {
        public Task CreateManyAsync(IEnumerable<CreateNotificationRequest> r, CancellationToken ct) => Task.CompletedTask;
        public Task CreateManyAsync(IEnumerable<CreateNotificationItem> i, CancellationToken ct) => Task.CompletedTask;
        public Task CreateAsync(ulong r, string t, string? m, string nt, string? rt, ulong? ri, CancellationToken ct) => Task.CompletedTask;
        public Task CreateAsync(CreateNotificationRequest r, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class NoopInvitationService : IOperationalContactInvitationService
    {
        public Task<OperationalContactInvitationTokens?> MintInvitationTokensAsync(
            ulong identityChangeId, CancellationToken ct) => Task.FromResult<OperationalContactInvitationTokens?>(null);
        public Task DispatchInvitationEmailAsync(
            ulong identityChangeId, OperationalContactInvitationTokens tokens, CancellationToken ct) => Task.CompletedTask;
        public Task<PEMS.Domain.Entities.Delegations.VisitRequestIdentityChange?> LockChangeAsync(
            ulong identityChangeId, CancellationToken ct)
            => Task.FromResult<PEMS.Domain.Entities.Delegations.VisitRequestIdentityChange?>(null);
        public Task<PEMS.Domain.Entities.Delegations.VisitRequestIdentityChange?> LockPendingChangeForInstanceAsync(
            ulong visitInstanceId, CancellationToken ct) => Task.FromResult<PEMS.Domain.Entities.Delegations.VisitRequestIdentityChange?>(null);
    }

    // ── The actual repro: raw JSON through the REAL HTTP pipeline ──────────────────────────────

    private object RawInstance(ulong instanceId, int instV, string? phone)
        => new
        {
            visitInstanceId = instanceId,
            expectedRowVersion = instV,
            operationalContact = new
            {
                fullName = "Kim Min Jae",
                organization = "SeoulTech Global Engagement Center",
                jobTitle = "International Partnerships Manager (đã sửa)",
                phone,
                email = _registrantEmail,
                memberLink = (object?)null,
            },
            transportationNote = (string?)null,
            mediaConsentStatus = (string?)null,
            notes = (string?)null,
        };

    /// <summary>
    /// B7 — EXACT shape from the bug report: FullName/Organization/JobTitle/Email present, Phone
    /// explicitly <c>null</c>, MemberLink explicitly null (picking "— Không nằm trong danh sách đoàn —").
    /// This is the literal payload a relation-only or name-only edit on a contact with NO phone on file
    /// produces. Proves model binding accepts it (no "The Phone field is required."), and that the call
    /// reaches the real business handler (200 OK, contact profile actually applied).
    /// </summary>
    [Fact]
    public async Task B7_Raw_json_with_explicit_null_phone_is_accepted_end_to_end()
    {
        VisitRequestTestGate.RequireDb();
        var (requestId, instanceId, instV, reqV) = await SeedAssignedRequestAsync(phone: null);

        var rawJson = JsonSerializer.Serialize(new
        {
            expectedRequestRowVersion = reqV,
            registrant = (object?)null,
            instances = new[] { RawInstance(instanceId, instV, null) },
        });

        using var content = new StringContent(rawJson, System.Text.Encoding.UTF8, "application/json");
        var response = await RegistrantClient().PatchAsync(
            $"/api/v2/visit-requests/{requestId}/safe-details", content);
        var body = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("Phone field is required", body, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"changeClass\":\"CONTACT\"", body);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var detail = await db.VisitInstanceFormDetails.AsNoTracking().SingleAsync(d => d.VisitInstanceId == instanceId);
        Assert.Equal("Kim Min Jae", detail.OperationalContactFullName);
        Assert.Null(detail.OperationalContactPhone);
    }

    /// <summary>
    /// B7 variant: a NON-blank, well-formed phone through the same real HTTP round trip — proves the
    /// null case above isn't passing merely because null happens to skip validation; a real value is
    /// bound, validated, and persisted too.
    /// </summary>
    [Fact]
    public async Task B7b_Raw_json_with_a_wellformed_phone_is_accepted_and_persisted()
    {
        VisitRequestTestGate.RequireDb();
        var (requestId, instanceId, instV, reqV) = await SeedAssignedRequestAsync(phone: null);

        var rawJson = JsonSerializer.Serialize(new
        {
            expectedRequestRowVersion = reqV,
            registrant = (object?)null,
            instances = new[] { RawInstance(instanceId, instV, "+821012340001") },
        });

        using var content = new StringContent(rawJson, System.Text.Encoding.UTF8, "application/json");
        var response = await RegistrantClient().PatchAsync(
            $"/api/v2/visit-requests/{requestId}/safe-details", content);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var detail = await db.VisitInstanceFormDetails.AsNoTracking().SingleAsync(d => d.VisitInstanceId == instanceId);
        Assert.Equal("+821012340001", detail.OperationalContactPhone);
    }

    /// <summary>
    /// B7 variant: a malformed non-blank phone through the same real HTTP round trip — proves the
    /// backend still enforces the FORMAT rule end-to-end; optional never meant "unvalidated".
    /// </summary>
    [Fact]
    public async Task B7c_Raw_json_with_a_malformed_phone_is_rejected_not_silently_accepted()
    {
        VisitRequestTestGate.RequireDb();
        var (requestId, instanceId, instV, reqV) = await SeedAssignedRequestAsync(phone: null);

        var rawJson = JsonSerializer.Serialize(new
        {
            expectedRequestRowVersion = reqV,
            registrant = (object?)null,
            instances = new[] { RawInstance(instanceId, instV, "123-not-a-phone") },
        });

        using var content = new StringContent(rawJson, System.Text.Encoding.UTF8, "application/json");
        var response = await RegistrantClient().PatchAsync(
            $"/api/v2/visit-requests/{requestId}/safe-details", content);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain("Phone field is required", body, StringComparison.OrdinalIgnoreCase);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var detail = await db.VisitInstanceFormDetails.AsNoTracking().SingleAsync(d => d.VisitInstanceId == instanceId);
        Assert.Null(detail.OperationalContactPhone); // zero mutation
    }

    /// <summary>
    /// B8 — Phone OMITTED from the JSON entirely (not even <c>"phone": null</c>), documenting the actual
    /// transport behavior of this DTO contract rather than assuming it. A positional record's
    /// constructor parameter with no default has no JSON-level "optional" concept of its own: System.Text.Json
    /// substitutes the CLR default (null for a reference type) for a missing property, which is
    /// indistinguishable from an explicit null for this nullable field — proven here rather than assumed.
    /// </summary>
    [Fact]
    public async Task B8_Contact_patch_omitting_phone_entirely_documents_actual_transport_behavior()
    {
        VisitRequestTestGate.RequireDb();
        var (requestId, instanceId, instV, reqV) = await SeedAssignedRequestAsync(phone: null);

        var rawJson = JsonSerializer.Serialize(new
        {
            expectedRequestRowVersion = reqV,
            registrant = (object?)null,
            instances = new object[]
            {
                new
                {
                    visitInstanceId = instanceId,
                    expectedRowVersion = instV,
                    operationalContact = new
                    {
                        // Genuinely different from the seeded FullName ("Kim Min Jae") — otherwise this
                        // payload is a true no-op (every field identical to what's stored) and the
                        // pre-existing "Không có thay đổi nào để áp dụng." guard fires for THAT reason,
                        // unrelated to Phone. That masked this exact scenario on the first attempt here.
                        fullName = "Kim Min Jae (đã sửa)",
                        organization = "SeoulTech Global Engagement Center",
                        jobTitle = "International Partnerships Manager",
                        // phone: DELIBERATELY ABSENT — not even present as a JSON key.
                        email = _registrantEmail,
                        memberLink = (object?)null,
                    },
                    transportationNote = (string?)null,
                    mediaConsentStatus = (string?)null,
                    notes = (string?)null,
                },
            },
        });

        using var content = new StringContent(rawJson, System.Text.Encoding.UTF8, "application/json");
        var response = await RegistrantClient().PatchAsync(
            $"/api/v2/visit-requests/{requestId}/safe-details", content);
        var body = await response.Content.ReadAsStringAsync();

        // Documented, not assumed: an omitted Phone key binds to null (CLR default), same as an explicit
        // null — it does NOT trigger "The Phone field is required." either, and does not block an
        // otherwise-genuine change from applying.
        Assert.DoesNotContain("Phone field is required", body, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var detail = await db.VisitInstanceFormDetails.AsNoTracking().SingleAsync(d => d.VisitInstanceId == instanceId);
        Assert.Equal("Kim Min Jae (đã sửa)", detail.OperationalContactFullName);
        Assert.Null(detail.OperationalContactPhone);
    }
}

/// <summary>Tiny shared DB-reachability gate so this file does not depend on VisitSafeEditV2Tests.</summary>
internal static class VisitRequestTestGate
{
    private static bool? _dbUp;
    public static void RequireDb()
    {
        if (_dbUp is null)
        {
            try
            {
                using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                    .UseMySql(
                        "server=localhost;port=3306;database=pems_pr3_test;user=root;password=123456;AllowUserVariables=True;GuidFormat=None",
                        ServerVersion.AutoDetect(
                            "server=localhost;port=3306;database=pems_pr3_test;user=root;password=123456;AllowUserVariables=True;GuidFormat=None"))
                    .Options);
                _dbUp = db.Database.CanConnect();
            }
            catch { _dbUp = false; }
        }
        Assert.True(_dbUp!.Value, "pems_pr3_test is not reachable.");
    }
}
