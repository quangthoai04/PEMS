using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Application.Common.Security;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Users;
using PEMS.Infrastructure.Persistence;
using PEMS.IntegrationTests.TestInfrastructure;
using Xunit;

namespace PEMS.IntegrationTests.DepartmentLeaderPersonnel;

/// <summary>
/// HTTP-level enforcement of the login-email whitelist for Department Leader personnel
/// (spec §11, §12, §19).
///
/// The frontend validator is a courtesy; this is the boundary. These tests bypass the modal
/// entirely and speak to the API the way a stale client, a curl command or a tampered request
/// would — the refusal has to come from the server, and it has to come BEFORE anything is written.
///
/// Scope note: only the REFUSAL paths are exercised here. The accepted-address paths provision a
/// real account and dispatch real confirmation mail, so they are covered by
/// PEMS.UnitTests/DepartmentLeaderPersonnel (which asserts the same handler against an in-memory
/// store, including normalization) rather than left behind in a shared test database.
/// </summary>
public sealed class DepartmentPersonnelEmailDomainApiTests
    : IClassFixture<PemsWebApplicationFactory>, IAsyncLifetime
{
    private const string CreateUrl = "/api/department-leader/personnel";
    private const string DomainMessage = "Chỉ chấp nhận @gmail.com và @fpt.edu.vn.";
    private const string TargetFullNamePrefix = "[IT-DL-EMAIL-DOMAIN] ";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Addresses the server must refuse: removed domain, outsiders, subdomains, look-alikes.</summary>
    public static TheoryData<string> DisallowedAddresses => new()
    {
        "it-dl-domain@fe.edu.vn",
        "it-dl-domain@yahoo.com",
        "it-dl-domain@student.fpt.edu.vn",
        "it-dl-domain@mail.gmail.com",
        "it-dl-domain@gmail.com.vn",
        "it-dl-domain@fpt.edu.vn.evil.com",
        "it-dl-domain@fake-fpt.edu.vn",
        "it-dl-domain+tag@gmail.com",
    };

    private readonly PemsWebApplicationFactory _factory;

    /// <summary>The seed department whose head we borrow, so DisposeAsync can hand it back.</summary>
    private ulong _departmentId;
    private ulong? _originalHeadUserId;
    private ulong _targetUserId;

    public DepartmentPersonnelEmailDomainApiTests(PemsWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    /// <summary>
    /// Removes the personnel row this class created and restores the borrowed department head.
    /// Nothing this class touched may outlive it — the department is seed data other classes read.
    /// </summary>
    public async Task DisposeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var created = await db.Users.Where(u => u.FullName.StartsWith(TargetFullNamePrefix)).ToListAsync();
        if (created.Count > 0)
        {
            db.Users.RemoveRange(created);
            await db.SaveChangesAsync();
        }

        if (_departmentId != 0)
        {
            var department = await db.Departments.FirstOrDefaultAsync(d => d.DepartmentId == _departmentId);
            if (department is not null && department.HeadUserId != _originalHeadUserId)
            {
                department.HeadUserId = _originalHeadUserId;
                await db.SaveChangesAsync();
            }
        }
    }

    /// <summary>
    /// A client authenticated as the ACTUAL head of a GENERAL department — the scope service checks
    /// the seat, not just the role, so the test user has to be seated before any request is sent.
    /// </summary>
    private async Task<HttpClient> CreateSeatedLeaderClientAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var userId = await DatabaseResetHelper.EnsureTestUserAsync(db, EffectiveRole.DepartmentLead);
        var sessionId = await DatabaseResetHelper.CreateActiveSessionAsync(db, userId, EffectiveRole.DepartmentLead);
        var leader = await db.Users.AsNoTracking().FirstAsync(u => u.UserId == userId);

        var department = await db.Departments.FirstAsync(d => d.DepartmentId == leader.DepartmentId!.Value);
        _departmentId = department.DepartmentId;
        _originalHeadUserId ??= department.HeadUserId;
        if (department.HeadUserId != userId)
        {
            department.HeadUserId = userId;
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.RoleCodeHeader, RoleCode.Department);
        client.DefaultRequestHeaders.Add(TestAuthHandler.SubRoleHeader, SubRole.Leader);
        client.DefaultRequestHeaders.Add(TestAuthHandler.SessionIdHeader, sessionId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.PrimaryCampusIdHeader, leader.PrimaryCampusId!.Value.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.DepartmentIdHeader, department.DepartmentId.ToString());

        return client;
    }

    /// <summary>An editable DEPARTMENT/STAFF row in the leader's department, in the given status.</summary>
    private async Task<(ulong UserId, string Email)> EnsureTargetAsync(string status)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var department = await db.Departments.AsNoTracking().FirstAsync(d => d.DepartmentId == _departmentId);
        var roleId = await db.Roles.AsNoTracking()
            .Where(r => r.RoleCode == RoleCode.Department)
            .Select(r => r.RoleId)
            .FirstAsync();

        if (_targetUserId != 0)
        {
            var existing = await db.Users.FirstAsync(u => u.UserId == _targetUserId);
            existing.Status = status;
            await db.SaveChangesAsync();
            return (existing.UserId, existing.Email);
        }

        var email = $"it-dl-target-{Guid.NewGuid():N}@fpt.edu.vn";
        var target = new User
        {
            FullName = $"{TargetFullNamePrefix}Nhan Vien",
            Email = email,
            Phone = "0912345678",
            RoleId = roleId,
            SubRole = SubRole.Staff,
            PrimaryCampusId = department.CampusId,
            DepartmentId = department.DepartmentId,
            Status = status,
            CreatedVia = "MANUAL_CREATED",
            CreatedAt = DateTime.UtcNow,
        };

        db.Users.Add(target);
        await db.SaveChangesAsync();
        _targetUserId = target.UserId;
        return (target.UserId, email);
    }

    private static async Task<string?> ReadMessageAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        return body.TryGetProperty("message", out var message) ? message.GetString() : null;
    }

    // ── POST /api/department-leader/personnel (spec §11.3, §19) ─────────────

    [Theory]
    [MemberData(nameof(DisallowedAddresses))]
    public async Task Create_refuses_a_disallowed_address_and_creates_nothing(string email)
    {
        var client = await CreateSeatedLeaderClientAsync();

        var response = await client.PostAsJsonAsync(CreateUrl, new
        {
            fullName = "Nguyen Van A",
            email,
            phone = "0912345678",
            gender = "MALE",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var normalized = email.Trim().ToLowerInvariant();
        Assert.False(await db.Users.AsNoTracking().AnyAsync(u => u.Email == normalized));
    }

    [Fact]
    public async Task Create_reports_the_domain_rule_verbatim()
    {
        var client = await CreateSeatedLeaderClientAsync();

        var response = await client.PostAsJsonAsync(CreateUrl, new
        {
            fullName = "Nguyen Van A",
            email = "it-dl-domain@fe.edu.vn",
            phone = "0912345678",
            gender = "MALE",
        });

        // The wording the modal shows and the wording the server sends must be the same sentence.
        Assert.Equal(DomainMessage, await ReadMessageAsync(response));
    }

    /// <summary>
    /// A refused address must leave no confirmation token and no <c>sent_emails</c> row behind: the
    /// account was never created, so there is nothing to confirm and nobody to notify.
    /// </summary>
    [Fact]
    public async Task Create_with_a_refused_domain_issues_no_confirmation_and_sends_no_mail()
    {
        var client = await CreateSeatedLeaderClientAsync();
        const string email = "it-dl-domain@fe.edu.vn";

        using var before = _factory.Services.CreateScope();
        var beforeDb = before.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var confirmationsBefore = await beforeDb.AccountEmailConfirmations.CountAsync();
        var mailsBefore = await beforeDb.SentEmails.CountAsync();

        var response = await client.PostAsJsonAsync(CreateUrl, new
        {
            fullName = "Nguyen Van A",
            email,
            phone = "0912345678",
            gender = "MALE",
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var after = _factory.Services.CreateScope();
        var afterDb = after.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(confirmationsBefore, await afterDb.AccountEmailConfirmations.CountAsync());
        Assert.Equal(mailsBefore, await afterDb.SentEmails.CountAsync());
    }

    // ── PUT /api/department-leader/personnel/{userId} (spec §12.3, §19) ─────

    /// <summary>
    /// The rule does not bend for any status. ACTIVE, INACTIVE, PENDING_EMAIL_CONFIRMATION and
    /// LOCKED all refuse the same address, and none of them loses its current identity or status
    /// in the attempt.
    /// </summary>
    [Theory]
    [InlineData(UserStatuses.Active)]
    [InlineData(UserStatuses.Inactive)]
    [InlineData(UserStatuses.PendingEmailConfirmation)]
    [InlineData(UserStatuses.Locked)]
    public async Task Update_refuses_a_disallowed_domain_in_every_status(string status)
    {
        var client = await CreateSeatedLeaderClientAsync();
        var (userId, originalEmail) = await EnsureTargetAsync(status);

        var response = await client.PutAsJsonAsync($"{CreateUrl}/{userId}", new
        {
            fullName = "Nguyen Van B",
            email = "it-dl-domain@fe.edu.vn",
            phone = "0987654321",
            gender = "FEMALE",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(DomainMessage, await ReadMessageAsync(response));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var target = await db.Users.AsNoTracking().FirstAsync(u => u.UserId == userId);

        // Identity, status and the profile fields submitted alongside it are all untouched.
        Assert.Equal(originalEmail, target.Email);
        Assert.Equal(status, target.Status);
        Assert.StartsWith(TargetFullNamePrefix, target.FullName);
    }

    [Fact]
    public async Task Update_with_a_refused_domain_revokes_no_session_and_sends_no_mail()
    {
        var client = await CreateSeatedLeaderClientAsync();
        var (userId, _) = await EnsureTargetAsync(UserStatuses.Active);

        using var before = _factory.Services.CreateScope();
        var beforeDb = before.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var mailsBefore = await beforeDb.SentEmails.CountAsync();

        var response = await client.PutAsJsonAsync($"{CreateUrl}/{userId}", new
        {
            fullName = $"{TargetFullNamePrefix}Nhan Vien",
            email = "it-dl-domain@fe.edu.vn",
            phone = "0912345678",
            gender = "MALE",
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var after = _factory.Services.CreateScope();
        var afterDb = after.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(mailsBefore, await afterDb.SentEmails.CountAsync());
        Assert.False(await afterDb.UserSessions.AsNoTracking()
            .AnyAsync(s => s.UserId == userId && s.RevokedReason == SessionRevokeReasons.AccountEmailChanged));
    }
}
