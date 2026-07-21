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
using PEMS.Application.Delegations.Commands.ResubmitRejectedVisitRequest;
using PEMS.Application.Delegations.Commands.UpdatePendingVisitRequest;
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
            FormSchemaVersion = PEMS.Domain.Constants.FormSchemaVersions.PerCampus,
            VisitorUserId = _visitorId,
            RegistrantUserId = _visitorId,
            RegistrantFullName = "Integration Registrant",
            RegistrantNationality = "VN",
            RegistrantOrganization = "FPT",
            RegistrantJobTitle = "Staff",
            RegistrantPhone = "0999999999",
            RegistrantEmail = _visitorEmail,
            DelegationName = "Integration Delegation",
            VisitScope = "SINGLE_CAMPUS",
            VisitType = "CAMPUS_TOUR",
            Purpose = "Test Purpose",
            ContactPersonFullName = "Integration Contact",
            ContactPersonOrganization = "Contact Org",
            ContactPersonPhone = "0888888888",
            ContactPersonEmail = "contact.integration@example.com",
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
                        OperationalContactOrganization = "FPT",
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
        var visit = await db.VisitRequests.FirstAsync(v => v.VisitRequestId == visitId);

        Assert.Equal("Edited Delegation Name", visit.DelegationName);
        Assert.Equal("Integration Contact", visit.ContactPersonFullName);
        Assert.Equal(1u, visit.ResubmissionCount);
        Assert.Equal(VisitRequestStatuses.PendingApproval, visit.Status);
    }

    [Fact]
    public async Task Resubmit_TamperedRegistrant_ReturnsUnprocessableEntity_AndDatabaseUnchanged()
    {
        var visitId = await SeedVisitRequestAsync(VisitRequestStatuses.Rejected, VisitInstanceStatuses.Rejected);
        var client = VisitorClient();
        
        var command = ClonePayload(await CreateValidEditPayloadAsync(visitId)); command["registrant"]!["fullName"] = "Hacked Registrant";

        var response = await client.PostAsJsonAsync($"/api/v2/visit-requests/{visitId}/resubmit", command);

        Assert.Equal(System.Net.HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("IMMUTABLE_REGISTRANT_INFO", content.GetProperty("errorCode").GetString());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var visit = await db.VisitRequests.FirstAsync(v => v.VisitRequestId == visitId);

        // Assert no changes made
        Assert.Equal("Integration Registrant", visit.RegistrantFullName);
        Assert.Equal("Integration Delegation", visit.DelegationName);
        Assert.Equal(0u, visit.ResubmissionCount);
        Assert.Equal(VisitRequestStatuses.Rejected, visit.Status);
    }

    [Fact]
    public async Task Resubmit_RegistrantNotContact_ReturnsOk()
    {
        var visitId = await SeedVisitRequestAsync(VisitRequestStatuses.Rejected, VisitInstanceStatuses.Rejected);
        
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var visit = await db.VisitRequests.FirstAsync(v => v.VisitRequestId == visitId);
        
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
        var visit = await db.VisitRequests.FirstAsync(v => v.VisitRequestId == visitId);

        Assert.Equal("Edited Delegation Name", visit.DelegationName);
        Assert.Equal("Integration Contact", visit.ContactPersonFullName);
        Assert.Equal(VisitRequestStatuses.PendingApproval, visit.Status);
    }

    [Fact]
    public async Task UpdatePending_TamperedRegistrant_ReturnsUnprocessableEntity_AndDbUnchanged()
    {
        var visitId = await SeedVisitRequestAsync(VisitRequestStatuses.PendingApproval, VisitInstanceStatuses.WaitingRequestApproval);
        var client = VisitorClient();
        
        var command = ClonePayload(await CreateValidEditPayloadAsync(visitId)); command["registrant"]!["fullName"] = "Hacked Registrant";

        var response = await client.PutAsJsonAsync($"/api/v2/visit-requests/{visitId}/pending-edit", command);

        Assert.Equal(System.Net.HttpStatusCode.UnprocessableEntity, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var visit = await db.VisitRequests.FirstAsync(v => v.VisitRequestId == visitId);

        // Assert no changes made
        Assert.Equal("Integration Registrant", visit.RegistrantFullName);
        Assert.Equal("Integration Delegation", visit.DelegationName);
        Assert.Equal(VisitRequestStatuses.PendingApproval, visit.Status);
    }
    [Fact]
    public async Task Resubmit_TamperedContactIdentity_ReturnsUnprocessableEntity()
    {
        var visitId = await SeedVisitRequestAsync(VisitRequestStatuses.Rejected, VisitInstanceStatuses.Rejected);
        var client = VisitorClient();
        
        var command = ClonePayload(await CreateValidEditPayloadAsync(visitId)); command["primaryContact"]!["email"] = "hacked.contact@example.com";

        var response = await client.PostAsJsonAsync($"/api/v2/visit-requests/{visitId}/resubmit", command);

        Assert.Equal(System.Net.HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("IMMUTABLE_CONTACT_IDENTITY", content.GetProperty("errorCode").GetString());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var visit = await db.VisitRequests.FirstAsync(v => v.VisitRequestId == visitId);

        Assert.Equal("contact.integration@example.com", visit.ContactPersonEmail);
        Assert.Equal(0u, visit.ResubmissionCount);
    }

    [Fact]
    public async Task UpdatePending_TamperedContactIdentity_ReturnsUnprocessableEntity()
    {
        var visitId = await SeedVisitRequestAsync(VisitRequestStatuses.PendingApproval, VisitInstanceStatuses.WaitingRequestApproval);
        var client = VisitorClient();
        
        var command = ClonePayload(await CreateValidEditPayloadAsync(visitId)); command["primaryContact"]!["email"] = _visitorEmail;

        var response = await client.PutAsJsonAsync($"/api/v2/visit-requests/{visitId}/pending-edit", command);

        Assert.Equal(System.Net.HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("IMMUTABLE_CONTACT_IDENTITY", content.GetProperty("errorCode").GetString());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var visit = await db.VisitRequests.FirstAsync(v => v.VisitRequestId == visitId);

        Assert.Equal(VisitRequestStatuses.PendingApproval, visit.Status);
        Assert.Equal("Integration Delegation", visit.DelegationName);
    }
}

