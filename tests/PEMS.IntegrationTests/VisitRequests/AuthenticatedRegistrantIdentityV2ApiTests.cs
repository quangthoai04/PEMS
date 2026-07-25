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
/// Registrant identity on the authenticated create (POST /api/v2/visit-requests) against the REAL API
/// pipeline and MySQL (plan §5.2–§5.4, §8.1).
///
/// The rule under test: this endpoint is SELF-registration only. The JWT stands in for the OTP, so it can
/// only vouch for the caller's own mailbox — a form naming somebody else as registrant must be refused with
/// a stable code and must write NOTHING, so the client can route to the delegated OTP flow instead.
/// Matching is trim + lower-case; anything else (Gmail dots, +alias) is a different mailbox.
/// </summary>
public sealed class AuthenticatedRegistrantIdentityV2ApiTests : IAsyncLifetime
{
    private const string DelegationPrefix = "[IT-REG-IDENTITY] ";

    private readonly PemsWebApplicationFactory _factory = new();

    private ulong _staffId, _staffSessionId;
    private ulong _visitorId, _visitorSessionId;
    private ulong _campus1Id;
    private string _campus1Code = "";
    private string _staffEmail = "", _visitorEmail = "";

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        _staffId = await DatabaseResetHelper.EnsureTestUserAsync(db, EffectiveRole.Staff);
        _visitorId = await DatabaseResetHelper.EnsureTestUserAsync(db, EffectiveRole.Visitor);

        _staffSessionId = await DatabaseResetHelper.CreateActiveSessionAsync(db, _staffId, EffectiveRole.Staff);
        _visitorSessionId = await DatabaseResetHelper.CreateActiveSessionAsync(db, _visitorId, EffectiveRole.Visitor);

        _staffEmail = await db.Users.AsNoTracking().Where(u => u.UserId == _staffId).Select(u => u.Email).FirstAsync();
        _visitorEmail = await db.Users.AsNoTracking().Where(u => u.UserId == _visitorId).Select(u => u.Email).FirstAsync();

        var staff = await db.Users.AsNoTracking().FirstAsync(u => u.UserId == _staffId);
        _campus1Id = staff.PrimaryCampusId!.Value;
        _campus1Code = await db.Campuses.AsNoTracking()
            .Where(c => c.CampusId == _campus1Id).Select(c => c.CampusCode).FirstAsync();
    }

    public async Task DisposeAsync()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var requestIds = await db.VisitRequests
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
    private HttpClient VisitorClient() => CreateClient(_visitorId, "VISITOR", null, _visitorSessionId, null);

    private static string UniqueContactEmail() => $"it-reg-identity-contact-{Guid.NewGuid():N}@example.com";
    private static string UniqueForeignEmail() => $"it-reg-identity-other-{Guid.NewGuid():N}@partner.example.com";
    private string NewDelegationName(string what) => DelegationPrefix + what + " " + Guid.NewGuid().ToString("N")[..8];

    private static async Task<string?> ErrorCodeOf(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.TryGetProperty("errorCode", out var code) ? code.GetString() : null;
    }

    private async Task AssertNothingWrittenAsync(string delegationName)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await db.VisitRequestCampuses.AnyAsync(c => c.FormDetail!.DelegationName == delegationName));
        Assert.False(await db.VisitRequests.AnyAsync(
            v => v.CampusInstances.Any(c => c.FormDetail!.DelegationName == delegationName)));
        Assert.False(await db.Notifications.AnyAsync(n => n.Message.Contains(delegationName)));
    }

    // ── Self-registration is accepted, however the caller typed their own address ────

    [Fact]
    public async Task Visitor_ExactSameEmail_CreatesDirectly()
    {
        var name = NewDelegationName("exact");
        var payload = V2TestDataBuilder.BuildCreatePayload(
            delegationName: name, registrantEmail: _visitorEmail, contactEmail: UniqueContactEmail(),
            campuses: new[] { (_campus1Code, "SEND_FOR_REVIEW", (ulong?)null) });

        var response = await VisitorClient().PostAsJsonAsync("/api/v2/visit-requests", payload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Visitor_SameEmailDifferentCase_CreatesDirectly()
    {
        var name = NewDelegationName("case");
        var payload = V2TestDataBuilder.BuildCreatePayload(
            delegationName: name, registrantEmail: _visitorEmail.ToUpperInvariant(),
            contactEmail: UniqueContactEmail(),
            campuses: new[] { (_campus1Code, "SEND_FOR_REVIEW", (ulong?)null) });

        var response = await VisitorClient().PostAsJsonAsync("/api/v2/visit-requests", payload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Visitor_SameEmailSurroundedByWhitespace_CreatesDirectly()
    {
        var name = NewDelegationName("whitespace");
        var payload = V2TestDataBuilder.BuildCreatePayload(
            delegationName: name, registrantEmail: "  " + _visitorEmail + "  ",
            contactEmail: UniqueContactEmail(),
            campuses: new[] { (_campus1Code, "SEND_FOR_REVIEW", (ulong?)null) });

        var response = await VisitorClient().PostAsJsonAsync("/api/v2/visit-requests", payload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ── Somebody else's identity is refused, and nothing is written ─────────────────

    [Fact]
    public async Task Staff_DifferentRegistrantEmail_IsRejected_AndWritesNothing()
    {
        var name = NewDelegationName("staff delegated");
        var payload = V2TestDataBuilder.BuildCreatePayload(
            delegationName: name, registrantEmail: UniqueForeignEmail(), contactEmail: UniqueContactEmail(),
            campuses: new[] { (_campus1Code, "SEND_FOR_REVIEW", (ulong?)null) });

        var response = await StaffClient().PostAsJsonAsync("/api/v2/visit-requests", payload);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(VisitRequestErrorCodes.RegistrantEmailVerificationRequired, await ErrorCodeOf(response));
        await AssertNothingWrittenAsync(name);
    }

    [Fact]
    public async Task Visitor_DifferentRegistrantEmail_IsRejected_AndWritesNothing()
    {
        // The rule is about identity, not role: a Visitor may not register somebody else either.
        var name = NewDelegationName("visitor delegated");
        var payload = V2TestDataBuilder.BuildCreatePayload(
            delegationName: name, registrantEmail: UniqueForeignEmail(), contactEmail: UniqueContactEmail(),
            campuses: new[] { (_campus1Code, "SEND_FOR_REVIEW", (ulong?)null) });

        var response = await VisitorClient().PostAsJsonAsync("/api/v2/visit-requests", payload);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(VisitRequestErrorCodes.RegistrantEmailVerificationRequired, await ErrorCodeOf(response));
        await AssertNothingWrittenAsync(name);
    }

    [Fact]
    public async Task Staff_DifferentRegistrantEmail_WithForgedSelfHost_IsRejected_AndWritesNothing()
    {
        // Journey E: identity is checked BEFORE the processing matrix, so a forged SELF_HOST attached to a
        // delegated submission can never create a half-written request with the caller already the host.
        var name = NewDelegationName("forged self-host");
        var payload = V2TestDataBuilder.BuildCreatePayload(
            delegationName: name, registrantEmail: UniqueForeignEmail(), contactEmail: UniqueContactEmail(),
            campuses: new[] { (_campus1Code, "SELF_HOST", (ulong?)null) });

        var response = await StaffClient().PostAsJsonAsync("/api/v2/visit-requests", payload);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(VisitRequestErrorCodes.RegistrantEmailVerificationRequired, await ErrorCodeOf(response));
        await AssertNothingWrittenAsync(name);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await db.VisitParticipants.AnyAsync(p => p.UserId == _staffId && p.IsHost
            && db.VisitRequestCampuses.Any(c => c.VisitInstanceId == p.VisitInstanceId
                && c.FormDetail!.DelegationName == name)));
    }

    [Fact]
    public async Task Staff_AliasOfOwnEmail_IsTreatedAsSomebodyElse()
    {
        // "+alias" and dot-folding are NOT applied: user+x@ is a different mailbox from user@, and treating
        // them as the same would let one account submit under an address it has not proven it controls.
        var at = _staffEmail.IndexOf('@');
        var aliased = _staffEmail[..at] + "+delegated" + _staffEmail[at..];
        var name = NewDelegationName("alias");
        var payload = V2TestDataBuilder.BuildCreatePayload(
            delegationName: name, registrantEmail: aliased, contactEmail: UniqueContactEmail(),
            campuses: new[] { (_campus1Code, "SEND_FOR_REVIEW", (ulong?)null) });

        var response = await StaffClient().PostAsJsonAsync("/api/v2/visit-requests", payload);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(VisitRequestErrorCodes.RegistrantEmailVerificationRequired, await ErrorCodeOf(response));
        await AssertNothingWrittenAsync(name);
    }
}
