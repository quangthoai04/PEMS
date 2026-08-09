using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Application.Common.DTOs;
using PEMS.Application.Common.Security;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Infrastructure.Persistence;
using PEMS.IntegrationTests.TestInfrastructure;
using Xunit;
using PEMS.Application.Common;

namespace PEMS.IntegrationTests.VisitRequests;

public sealed class VisitorEditResubmitApiTests : IAsyncLifetime
{
    private readonly PemsWebApplicationFactory _factory = new();

    private ulong _visitorId, _visitorSessionId;
    private ulong _staffId, _staffSessionId;
    private ulong _leaderId;
    private ulong _campusId;
    private string _campusCode = "";
    private string _visitorEmail = "";

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        _visitorId = await DatabaseResetHelper.EnsureTestUserAsync(db, EffectiveRole.Visitor);
        _visitorSessionId = await DatabaseResetHelper.CreateActiveSessionAsync(db, _visitorId, EffectiveRole.Visitor);
        _visitorEmail = await db.Users.AsNoTracking().Where(u => u.UserId == _visitorId).Select(u => u.Email).FirstAsync();

        _staffId = await DatabaseResetHelper.EnsureTestUserAsync(db, EffectiveRole.Staff);
        _staffSessionId = await DatabaseResetHelper.CreateActiveSessionAsync(db, _staffId, EffectiveRole.Staff);
        _leaderId = await DatabaseResetHelper.EnsureTestUserAsync(db, EffectiveRole.StaffLeader);
        
        var staff = await db.Users.AsNoTracking().FirstAsync(u => u.UserId == _staffId);
        _campusId = staff.PrimaryCampusId!.Value;
        
        var leader = await db.Users.FirstAsync(u => u.UserId == _leaderId);
        leader.PrimaryCampusId = _campusId; // Make sure leader is on the same campus
        await db.SaveChangesAsync();
        
        _campusCode = await db.Campuses.AsNoTracking().Where(c => c.CampusId == _campusId).Select(c => c.CampusCode).FirstAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private System.Net.Http.HttpClient CreateClient(ulong userId, string roleCode, ulong sessionId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.RoleCodeHeader, roleCode);
        client.DefaultRequestHeaders.Add(TestAuthHandler.SessionIdHeader, sessionId.ToString());
        return client;
    }
    
    private System.Net.Http.HttpClient VisitorClient() => CreateClient(_visitorId, "VISITOR", _visitorSessionId);
    private System.Net.Http.HttpClient StaffClient() => CreateClient(_staffId, "STAFF", _staffSessionId);

    private async Task<ulong> SeedVisitRequestAsync(string status, string instanceStatus)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var visit = new VisitRequest
        {
            RequestCode = $"UT-RESUBMIT-{Guid.NewGuid().ToString()[..4]}",
            RegistrantUserId = _visitorId,
            RegistrantFullName = "Integration Registrant",
            RegistrantNationality = "VN",
            RegistrantOrganization = "FPT",
            RegistrantJobTitle = "Staff",
            RegistrantPhone = "0999999999",
            RegistrantEmail = _visitorEmail,
            VisitScope = "SINGLE_CAMPUS",
            // Pure V2: delegation name / visit type / purpose live in the campus FormDetail below.
            Status = status,
            ResubmissionCount = 0,
            CreatedAt = VietnamTime.Now(),
            SubmittedAt = VietnamTime.Now(),
            CampusInstances = new List<VisitRequestCampus>
            {
                new()
                {
                    CampusId = _campusId,
                    Status = instanceStatus,
                    // Self-matched: the visitor who submitted is also this campus's operational contact,
                    // so the campus is past the confirmation gate. Every status this fixture seeds is
                    // beyond WAITING_CONTACT_CONFIRMATION, and those may not carry a NULL contact.
                    OperationalContactUserId = _visitorId,
                    OperationalContactConfirmedAt = VietnamTime.Now(),
                    OperationalContactConfirmationSource = "REGISTRANT_SELF_MATCH",
                    PlannedStartAt = VietnamTime.Now().AddDays(7),
                    PlannedEndAt = VietnamTime.Now().AddDays(7).AddHours(4),
                    CreatedAt = VietnamTime.Now(),
                    CoordinatorUserId = instanceStatus == VisitInstanceStatuses.Rejected ? _leaderId : null,
                    CoordinatorAssignedBy = instanceStatus == VisitInstanceStatuses.Rejected ? _leaderId : null,
                    CoordinatorAssignedAt = instanceStatus == VisitInstanceStatuses.Rejected ? VietnamTime.Now() : null,
                    DecidedBy = instanceStatus == VisitInstanceStatuses.Rejected ? _leaderId : null,
                    DecidedAt = instanceStatus == VisitInstanceStatuses.Rejected ? VietnamTime.Now() : null,
                    DecisionActorRole = instanceStatus == VisitInstanceStatuses.Rejected ? "STAFF_LEADER" : null,
                    DecisionSource = instanceStatus == VisitInstanceStatuses.Rejected ? "STANDARD_CAMPUS_REVIEW" : null,
                    DecisionNote = instanceStatus == VisitInstanceStatuses.Rejected ? "Test Rejection Note" : null,
                    FormDetail = new PEMS.Domain.Entities.Delegations.VisitInstanceFormDetail
                    {
                        DelegationName = "Integration Delegation",
                        VisitType = "CAMPUS_TOUR",
                        Purpose = "Integration Purpose",
                        WorkingContent = "Integration Content",
                        OperationalContactFullName = "Integration Contact",
                        OperationalContactOrganization = "FPT", OperationalContactJobTitle = "Trưởng phòng Hợp tác",
                        OperationalContactPhone = "0999999999",
                        OperationalContactEmail = "contact.integration@example.com",
                        WorkingLanguage = "VI",
                        MediaConsentStatus = "DECLINED"
                    },
                    Agendas = new List<VisitAgenda>
                    {
                        new()
                        {
                            Title = "Original Agenda",
                            Description = "Original Description",
                            StartTime = VietnamTime.Now().AddDays(7).AddHours(1),
                            EndTime = VietnamTime.Now().AddDays(7).AddHours(2),
                            Location = "Room 1",
                            SequenceOrder = 1,
                            CreatedAt = VietnamTime.Now(),
                            CreatedBy = _staffId
                        }
                    }
                }
            }
        };

        db.VisitRequests.Add(visit);
        await db.SaveChangesAsync();

        return visit.VisitRequestId;
    }

    private System.Text.Json.Nodes.JsonObject ClonePayload(System.Collections.Generic.Dictionary<string, object?> payload)
    {
        return System.Text.Json.Nodes.JsonNode.Parse(
            System.Text.Json.JsonSerializer.Serialize(payload))!.AsObject();
    }

    private async System.Threading.Tasks.Task<System.Collections.Generic.Dictionary<string, object?>> CreateValidEditPayloadAsync(ulong visitId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PEMS.Infrastructure.Persistence.ApplicationDbContext>();
        var req = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstAsync(
            System.Linq.Queryable.Where(db.VisitRequests, r => r.VisitRequestId == visitId));
        var inst = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstAsync(
            System.Linq.Queryable.Where(db.VisitRequestCampuses, c => c.VisitRequestId == visitId));
        return V2TestDataBuilder.BuildEditPayload(req.RowVersion, _visitorEmail, "contact.integration@example.com", (inst.VisitInstanceId, inst.RowVersion, _campusCode));
    }



    [Fact]
    public async Task Resubmit_Anonymous_ReturnsUnauthorized()
    {
        var visitId = await SeedVisitRequestAsync(VisitRequestStatuses.Rejected, VisitInstanceStatuses.Rejected);
        var client = _factory.CreateClient(); // No authentication

        var response = await client.PostAsJsonAsync($"/api/v2/visit-requests/{visitId}/resubmit", await CreateValidEditPayloadAsync(visitId));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Resubmit_StaffUser_ReturnsForbidden()
    {
        var visitId = await SeedVisitRequestAsync(VisitRequestStatuses.Rejected, VisitInstanceStatuses.Rejected);
        var client = StaffClient(); // Role Staff

        var response = await client.PostAsJsonAsync($"/api/v2/visit-requests/{visitId}/resubmit", await CreateValidEditPayloadAsync(visitId));

        Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Resubmit_ValidData_UpdatesDatabase_AndBumpsCounter()
    {
        var visitId = await SeedVisitRequestAsync(VisitRequestStatuses.Rejected, VisitInstanceStatuses.Rejected);
        var client = VisitorClient();
        var command = await CreateValidEditPayloadAsync(visitId);

        var response = await client.PostAsJsonAsync($"/api/v2/visit-requests/{visitId}/resubmit", command);

        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            throw new Exception($"Failed: {response.StatusCode} - {content} | JSON: " + System.Text.Json.JsonSerializer.Serialize(command));
        }

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var visit = await db.VisitRequests
            .Include(v => v.CampusInstances).ThenInclude(c => c.FormDetail)
            .FirstAsync(v => v.VisitRequestId == visitId);

        // Pure V2: the edited name lands on the campus instance's own detail, not on the request row.
        Assert.Equal("Edited Delegation Name", visit.CampusInstances.Single().FormDetail!.DelegationName);
        // The contact snapshot lives on the campus, and the edit must not have touched it.
        Assert.Equal("Integration Contact", visit.CampusInstances.Single().FormDetail!.OperationalContactFullName);
        Assert.Equal(1u, visit.ResubmissionCount);
        Assert.Equal(VisitRequestStatuses.PendingApproval, visit.Status);
    }

    /// <summary>
    /// The resubmit half of the same rule (see the pending-edit twin): a changed registrant EMAIL is
    /// still refused with IMMUTABLE_REGISTRANT_INFO and moves nothing — not the content, not the
    /// resubmission counter, not the status. Changing the descriptive name is no longer tampering and
    /// is covered by its own assertion above.
    /// </summary>
    [Fact]
    public async Task Resubmit_TamperedRegistrantEmail_ReturnsUnprocessableEntity_AndDatabaseUnchanged()
    {
        var visitId = await SeedVisitRequestAsync(VisitRequestStatuses.Rejected, VisitInstanceStatuses.Rejected);
        var client = VisitorClient();

        var command = ClonePayload(await CreateValidEditPayloadAsync(visitId));
        command["registrant"]!["email"] = "someone.else@example.com";

        var response = await client.PostAsJsonAsync($"/api/v2/visit-requests/{visitId}/resubmit", command);

        Assert.Equal(System.Net.HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        // Narrower than the retired IMMUTABLE_REGISTRANT_INFO: only the address is frozen now, and the
        // code says so, which is what lets the UI point at the right field.
        Assert.Equal("IMMUTABLE_REGISTRANT_EMAIL", content.GetProperty("errorCode").GetString());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var visit = await db.VisitRequests
            .Include(v => v.CampusInstances).ThenInclude(c => c.FormDetail)
            .FirstAsync(v => v.VisitRequestId == visitId);

        // Assert no changes made
        Assert.Equal("Integration Registrant", visit.RegistrantFullName);
        Assert.Equal("Integration Delegation", visit.CampusInstances.Single().FormDetail!.DelegationName);
        Assert.Equal(0u, visit.ResubmissionCount);
        Assert.Equal(VisitRequestStatuses.Rejected, visit.Status);
    }

    [Fact]
    public async Task Resubmit_RegistrantNotContact_ReturnsOk()
    {
        var visitId = await SeedVisitRequestAsync(VisitRequestStatuses.Rejected, VisitInstanceStatuses.Rejected);
        
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var visit = await db.VisitRequests
            .Include(v => v.CampusInstances).ThenInclude(c => c.FormDetail)
            .FirstAsync(v => v.VisitRequestId == visitId);
        
        // Make the registrant a different visitor account, but leave contact (VisitorUserId) as _visitorId
        var visitorRoleId = await db.Roles.Where(r => r.RoleCode == "VISITOR").Select(r => r.RoleId).FirstAsync();
        var newUser = new PEMS.Domain.Entities.Users.User
        {
            Email = $"registrant_{Guid.NewGuid()}@test.com",
            FullName = "New Registrant",
            RoleId = visitorRoleId,
            CreatedVia = "MANUAL_CREATED",
            CreatedAt = VietnamTime.Now(),
            Status = "ACTIVE",
            FirstLoginAt = VietnamTime.Now(),
            LastLoginAt = VietnamTime.Now(),
            FailedLoginCount = 0
        };
        db.Users.Add(newUser);
        await db.SaveChangesAsync();

        var sessionId = BitConverter.ToUInt64(Guid.NewGuid().ToByteArray());
        var registrantId = newUser.UserId;
        db.UserSessions.Add(new PEMS.Domain.Entities.Users.UserSession
        {
            SessionId = sessionId,
            UserId = registrantId,
            CreatedAt = VietnamTime.Now(),
            ExpiresAt = VietnamTime.Now().AddDays(1),
            LoginPortal = "VISITOR"
        });
        visit.RegistrantUserId = registrantId;
        await db.SaveChangesAsync();

        var client = CreateClient(registrantId, "VISITOR", sessionId);
        var command = await CreateValidEditPayloadAsync(visitId);

        var response = await client.PostAsJsonAsync($"/api/v2/visit-requests/{visitId}/resubmit", command);

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Resubmit_UnrelatedUser_ReturnsForbidden()
    {
        var visitId = await SeedVisitRequestAsync(VisitRequestStatuses.Rejected, VisitInstanceStatuses.Rejected);
        
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var unrelatedUser = new PEMS.Domain.Entities.Users.User
        {
            Email = $"unrelated_{Guid.NewGuid()}@test.com",
            FullName = "Unrelated User",
            RoleId = await db.Roles.Where(r => r.RoleCode == "VISITOR").Select(r => r.RoleId).FirstAsync(),
            CreatedVia = "MANUAL_CREATED",
            CreatedAt = VietnamTime.Now(),
            Status = "ACTIVE",
            FirstLoginAt = VietnamTime.Now(),
            LastLoginAt = VietnamTime.Now(),
            FailedLoginCount = 0
        };
        db.Users.Add(unrelatedUser);
        await db.SaveChangesAsync();

        var sessionId = BitConverter.ToUInt64(Guid.NewGuid().ToByteArray());
        var unrelatedId = unrelatedUser.UserId;
        db.UserSessions.Add(new PEMS.Domain.Entities.Users.UserSession
        {
            SessionId = sessionId,
            UserId = unrelatedId,
            CreatedAt = VietnamTime.Now(),
            ExpiresAt = VietnamTime.Now().AddDays(1),
            LoginPortal = "VISITOR"
        });
        await db.SaveChangesAsync();
        var client = CreateClient(unrelatedId, "VISITOR", sessionId);
        var command = await CreateValidEditPayloadAsync(visitId);

        var response = await client.PostAsJsonAsync($"/api/v2/visit-requests/{visitId}/resubmit", command);

        Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Resubmit_PayloadWithForgedAgenda_IgnoresAgenda_AndDoesNotAlterDatabaseAgenda()
    {
        var visitId = await SeedVisitRequestAsync(VisitRequestStatuses.Rejected, VisitInstanceStatuses.Rejected);
        var client = VisitorClient();

        // Create a valid payload, then inject "agendas"
        var command = await CreateValidEditPayloadAsync(visitId);
        var jsonNode = System.Text.Json.Nodes.JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(command))!.AsObject();
        jsonNode.Add("agendas", System.Text.Json.Nodes.JsonNode.Parse("""
            [
                {
                    "title": "Hacked Agenda",
                    "description": "I should not be able to do this",
                    "startDatetime": "2026-10-10T10:00:00Z",
                    "endDatetime": "2026-10-10T12:00:00Z",
                    "location": "Hacked Location"
                }
            ]
        """));

        var content = new System.Net.Http.StringContent(jsonNode.ToJsonString(), System.Text.Encoding.UTF8, "application/json");
        var response = await client.PostAsync($"/api/v2/visit-requests/{visitId}/resubmit", content);

        // Surface the error body on failure (EnsureSuccessStatusCode hides it) — this test
        // flaked once in a full-suite run and the missing body blocked diagnosis.
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"expected 2xx, got {(int)response.StatusCode}: {body}");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        var visit = await db.VisitRequests
            .Include(v => v.CampusInstances)
            .ThenInclude(c => c.Agendas)
            .FirstAsync(v => v.VisitRequestId == visitId);

        var agendas = visit.CampusInstances.First().Agendas.ToList();
        
        // Assert the agenda was NOT modified or added to
        Assert.Single(agendas);
        Assert.Equal("Original Agenda", agendas[0].Title);
        Assert.Equal("Room 1", agendas[0].Location);
    }

    [Fact]
    public async Task UpdatePending_ValidData_UpdatesDatabase()
    {
        var visitId = await SeedVisitRequestAsync(VisitRequestStatuses.PendingApproval, VisitInstanceStatuses.WaitingRequestApproval);
        var client = VisitorClient();
        var command = await CreateValidEditPayloadAsync(visitId);

        var response = await client.PutAsJsonAsync($"/api/v2/visit-requests/{visitId}/pending-edit", command);

        response.EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var visit = await db.VisitRequests
            .Include(v => v.CampusInstances).ThenInclude(c => c.FormDetail)
            .FirstAsync(v => v.VisitRequestId == visitId);

        // Pure V2: the edited name lands on the campus instance's own detail, not on the request row.
        Assert.Equal("Edited Delegation Name", visit.CampusInstances.Single().FormDetail!.DelegationName);
        Assert.Equal("Integration Contact", visit.CampusInstances.Single().FormDetail!.OperationalContactFullName);
        Assert.Equal(VisitRequestStatuses.PendingApproval, visit.Status);
    }

    /// <summary>
    /// The registrant block splits into DESCRIPTION and IDENTITY, and only one half is frozen.
    ///
    /// <para>
    /// This test used to assert that changing the registrant's NAME was refused. That refusal was
    /// retired deliberately: the five descriptive fields (name, organization, job title, nationality,
    /// phone) are a snapshot of who filed the request, the edit form has always rendered them as
    /// inputs, and refusing them told a registrant fixing a misspelt name that they were tampering.
    /// The email stays immutable because it IS the identity — it is what the account binding and the
    /// OTP were resolved against, and what every notification is addressed to.
    /// </para>
    /// <para>
    /// So the test now pins BOTH halves of that rule, which is what makes it worth keeping: the
    /// description is accepted and persisted, the identity is still refused with its own error code.
    /// </para>
    /// </summary>
    [Fact]
    public async Task UpdatePending_AcceptsRegistrantDescription_ButStillRefusesTheIdentityEmail()
    {
        var visitId = await SeedVisitRequestAsync(VisitRequestStatuses.PendingApproval, VisitInstanceStatuses.WaitingRequestApproval);
        var client = VisitorClient();

        // ── Description: allowed, and it lands. ──
        var rename = ClonePayload(await CreateValidEditPayloadAsync(visitId));
        rename["registrant"]!["fullName"] = "Integration Registrant (đã sửa)";

        var renameResponse = await client.PutAsJsonAsync($"/api/v2/visit-requests/{visitId}/pending-edit", rename);
        Assert.Equal(System.Net.HttpStatusCode.OK, renameResponse.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var visit = await db.VisitRequests.FirstAsync(v => v.VisitRequestId == visitId);
            Assert.Equal("Integration Registrant (đã sửa)", visit.RegistrantFullName);
            Assert.Equal(VisitRequestStatuses.PendingApproval, visit.Status);
        }

        // ── Identity: refused, and nothing moves. ──
        var hijack = ClonePayload(await CreateValidEditPayloadAsync(visitId));
        hijack["registrant"]!["email"] = "someone.else@example.com";

        var hijackResponse = await client.PutAsJsonAsync($"/api/v2/visit-requests/{visitId}/pending-edit", hijack);
        Assert.Equal(System.Net.HttpStatusCode.UnprocessableEntity, hijackResponse.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var visit = await db.VisitRequests
                .Include(v => v.CampusInstances).ThenInclude(c => c.FormDetail)
                .FirstAsync(v => v.VisitRequestId == visitId);
            // Still exactly what the ACCEPTED edit left behind — the refused one moved nothing on top
            // of it. ("Edited Delegation Name" is what CreateValidEditPayloadAsync writes.)
            Assert.Equal("Integration Registrant (đã sửa)", visit.RegistrantFullName);
            Assert.Equal("Edited Delegation Name", visit.CampusInstances.Single().FormDetail!.DelegationName);
            Assert.Equal(VisitRequestStatuses.PendingApproval, visit.Status);
        }
    }
    [Fact]
    public async Task Resubmit_TamperedContactIdentity_ReturnsUnprocessableEntity()
    {
        var visitId = await SeedVisitRequestAsync(VisitRequestStatuses.Rejected, VisitInstanceStatuses.Rejected);
        var client = VisitorClient();
        
        var command = ClonePayload(await CreateValidEditPayloadAsync(visitId)); command["campusVisits"]![0]!["operationalContact"]!["email"] = "hacked.contact@example.com";

        var response = await client.PostAsJsonAsync($"/api/v2/visit-requests/{visitId}/resubmit", command);

        Assert.Equal(System.Net.HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("IMMUTABLE_CONTACT_IDENTITY", content.GetProperty("errorCode").GetString());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var visit = await db.VisitRequests
            .Include(v => v.CampusInstances).ThenInclude(c => c.FormDetail)
            .FirstAsync(v => v.VisitRequestId == visitId);

        // The refused edit left the campus’s contact address exactly as it was.
        Assert.Equal("contact.integration@example.com",
            visit.CampusInstances.Single().FormDetail!.OperationalContactEmail);
        Assert.Equal(0u, visit.ResubmissionCount);
    }

    [Fact]
    public async Task UpdatePending_TamperedContactIdentity_ReturnsUnprocessableEntity()
    {
        var visitId = await SeedVisitRequestAsync(VisitRequestStatuses.PendingApproval, VisitInstanceStatuses.WaitingRequestApproval);
        var client = VisitorClient();
        
        var command = ClonePayload(await CreateValidEditPayloadAsync(visitId)); command["campusVisits"]![0]!["operationalContact"]!["email"] = _visitorEmail;

        var response = await client.PutAsJsonAsync($"/api/v2/visit-requests/{visitId}/pending-edit", command);

        Assert.Equal(System.Net.HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("IMMUTABLE_CONTACT_IDENTITY", content.GetProperty("errorCode").GetString());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var visit = await db.VisitRequests
            .Include(v => v.CampusInstances).ThenInclude(c => c.FormDetail)
            .FirstAsync(v => v.VisitRequestId == visitId);

        Assert.Equal(VisitRequestStatuses.PendingApproval, visit.Status);
        Assert.Equal("Integration Delegation", visit.CampusInstances.Single().FormDetail!.DelegationName);
    }
}

