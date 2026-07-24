using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Queries.ViewMyVisitInvitations;
using PEMS.Application.Delegations.Services.VisitFormRead;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Infrastructure.Persistence;
using Xunit;

namespace PEMS.IntegrationTests.VisitRequests;

/// <summary>
/// Dual-read migration tests for <c>GetVisitInvitationByIdQueryHandler</c> (ViewMyVisitInvitations) — the
/// invited user's own invitation-detail screen (key = participant_id, ownership-scoped by
/// <c>p.UserId == currentUser</c>). An invitation is bound to exactly ONE campus instance, so this is
/// INSTANCE-LEVEL: a MIXED v2 request returns 200 with the TARGET instance's delegation / purpose /
/// working-content, never a sibling campus and never FORM_VERSION_UPGRADE_REQUIRED. OrganizationName is the
/// registrant organisation (identity) and stays. Runs against disposable <c>pems_pr3_test</c>, each test in a
/// rolled-back transaction.
/// </summary>
public sealed class MyVisitInvitationByIdV2Tests
{
    private static string ConnString => PEMS.IntegrationTests.TestInfrastructure.DisposableDatabaseManager.GetDisposableConnectionString("server=localhost;port=3306;database=pems_pr3_test;user=root;password=123456;AllowUserVariables=True;GuidFormat=None");

    private const ulong Campus1 = 1, Campus2 = 2, Campus3 = 3;
    private const ulong Owner = 8;

    private static bool? _dbUp;

    private static ApplicationDbContext NewContext(CommandCounter? counter = null)
    {
        var b = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseMySql(ConnString, ServerVersion.AutoDetect(ConnString));
        if (counter is not null) b.AddInterceptors(counter);
        return new ApplicationDbContext(b.Options);
    }

    private static void RequireDb()
    {
        if (_dbUp is null)
        {
            try { using var db = NewContext(); _dbUp = db.Database.CanConnect(); }
            catch { _dbUp = false; }
        }
        Assert.True(_dbUp!.Value, "pems_pr3_test is not reachable — import the PR-2 master into it to run these tests.");
    }

    private sealed class FakeUser : ICurrentUserService
    {
        public bool IsAuthenticated => UserId is not null;
        public ulong? UserId { get; init; }
        public string? Email => null;
        public ulong? RoleId => null;
        public string? RoleCode => RoleCodes.Visitor;
        public string? SubRole => null;
        public ulong? PrimaryCampusId => null;
        public ulong? DepartmentId => null;
        public ulong? SessionId => null;
        public string? LoginPortal => null;
    }

    private static GetVisitInvitationByIdQueryHandler Handler(ApplicationDbContext db, ulong asUser)
    {
        var user = new FakeUser { UserId = asUser };
        return new(db, user, new VisitFormReadService(db, user, NullLogger<VisitFormReadService>.Instance));
    }

    private static Task<VisitInvitationDto> Run(ApplicationDbContext db, ulong participantId, ulong asUser = Owner)
        => Handler(db, asUser).Handle(new GetVisitInvitationByIdQuery(participantId), CancellationToken.None);

    // ── Tests ────────────────────────────────────────────────────────────────

    /// <summary>
    /// The request/campus split, asserted on a MIXED request so the two sources cannot coincide.
    ///
    /// Replaces the former V1 test, which was the only one here asserting the request-level side at all:
    /// every form field must come from the invited instance's own detail, while the registrant's
    /// organisation stays request-level. "No fallback" must not degrade into "ignore the request row".
    /// </summary>
    [Fact]
    public async Task Form_fields_come_from_the_instance_detail_while_registrant_stays_request_level()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (_, parts) = await Seed(db, FormSchemaVersions.PerCampus, new[] { Campus1, Campus2 }, mixed: true);
        var dto = await Run(db, parts[0]);

        Assert.Equal("DELEG-A", dto.DelegationName);
        Assert.Equal("PURPOSE-A", dto.Purpose);
        Assert.Equal("CONTENT-A", dto.WorkingContent);
        Assert.Equal("Org", dto.OrganizationName); // registrant organisation, request-level identity
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task V2_single_reads_target_detail()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (_, parts) = await Seed(db, FormSchemaVersions.PerCampus, new[] { Campus1 }, mixed: false);
        var dto = await Run(db, parts[0]);

        Assert.Equal("V2-DELEG", dto.DelegationName);
        Assert.NotEqual("GLOBAL-DELEG", dto.DelegationName);
        Assert.Equal("Org", dto.OrganizationName); // unchanged
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task V2_multi_nonmixed_returns_200()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (_, parts) = await Seed(db, FormSchemaVersions.PerCampus, new[] { Campus1, Campus2 }, mixed: false);
        var dto = await Run(db, parts[0]);

        Assert.Equal("V2-DELEG", dto.DelegationName);
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task V2_multi_mixed_target_A_returns_200_with_A_only()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (_, parts) = await Seed(db, FormSchemaVersions.PerCampus, new[] { Campus1, Campus2 }, mixed: true);
        var dto = await Run(db, parts[0]); // campus A

        Assert.Equal("DELEG-A", dto.DelegationName);
        Assert.Equal("PURPOSE-A", dto.Purpose);
        Assert.Equal("CONTENT-A", dto.WorkingContent);
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task V2_multi_mixed_target_B_returns_200_with_B_only()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (_, parts) = await Seed(db, FormSchemaVersions.PerCampus, new[] { Campus1, Campus2 }, mixed: true);
        var dto = await Run(db, parts[1]); // campus B, SAME request

        Assert.Equal("DELEG-B", dto.DelegationName);
        Assert.Equal("PURPOSE-B", dto.Purpose);
        Assert.NotEqual("DELEG-A", dto.DelegationName); // no sibling A leak
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task V2_missing_detail_throws_no_fallback()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (_, parts) = await Seed(db, FormSchemaVersions.PerCampus, new[] { Campus1 }, mixed: false);
        var instanceId = await db.VisitParticipants.Where(p => p.ParticipantId == parts[0])
            .Select(p => p.VisitInstanceId).FirstAsync();
        var detail = await db.VisitInstanceFormDetails.FirstAsync(d => d.VisitInstanceId == instanceId);
        db.VisitInstanceFormDetails.Remove(detail);
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ConflictException>(() => Run(db, parts[0]));
        Assert.Equal(VisitFormV2ErrorCodes.VisitFormDetailMissing, ex.ErrorCode);
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task Non_owner_gets_not_found()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (_, parts) = await Seed(db, FormSchemaVersions.PerCampus, new[] { Campus1 }, mixed: false);
        // A different user must not read (nor even learn the existence of) this invitation.
        await Assert.ThrowsAsync<NotFoundException>(() => Run(db, parts[0], asUser: 999));
        // …the owner can.
        var ok = await Run(db, parts[0], asUser: Owner);
        Assert.Equal("V2-DELEG", ok.DelegationName);
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task Query_count_constant_regardless_of_campus_count()
    {
        RequireDb();

        int small, large;
        var c1 = new CommandCounter();
        using (var db = NewContext(c1))
        using (var tx = await db.Database.BeginTransactionAsync())
        {
            var (_, parts) = await Seed(db, FormSchemaVersions.PerCampus, new[] { Campus1 }, mixed: false);
            c1.Count = 0;
            await Run(db, parts[0]);
            small = c1.Count;
            await tx.RollbackAsync();
        }
        var c3 = new CommandCounter();
        using (var db = NewContext(c3))
        using (var tx = await db.Database.BeginTransactionAsync())
        {
            var (_, parts) = await Seed(db, FormSchemaVersions.PerCampus, new[] { Campus1, Campus2, Campus3 }, mixed: true);
            c3.Count = 0;
            await Run(db, parts[0]);
            large = c3.Count;
            await tx.RollbackAsync();
        }
        Assert.Equal(small, large);
    }

    // ── Seed helpers ──────────────────────────────────────────────────────────

    private static VisitRequest NewRequest(byte schemaVersion, string scope, bool mixed) => new()
    {
        RequestCode = "MYI-" + Guid.NewGuid().ToString("N")[..12],
        VisitorUserId = Owner,
        RegistrantUserId = Owner,
        CreatedSource = "VISITOR_SUBMITTED",
        HasMixedCampusDetails = mixed,
        RegistrantFullName = "Reg", RegistrantOrganization = "Org", RegistrantJobTitle = "Job",
        RegistrantPhone = "+8490", RegistrantEmail = "reg@example.com", RegistrantNationality = "VN",
        VisitScope = scope,
        // Pure V2: form content is per campus (see the detail builder). The request row keeps only the
        // PRIMARY contact — a request-level relation, distinct from each campus's operational contact.
        ContactPersonFullName = "Primary Contact", ContactPersonOrganization = "COrg",
        ContactPersonPhone = "+8491", ContactPersonEmail = "contact@example.com",
        PrimaryContactAccessStatus = "ACTIVE", PrimaryContactVerifiedAt = DateTime.Now,
        Status = "PENDING_APPROVAL", SubmittedAt = DateTime.Now, CreatedAt = DateTime.Now,
    };

    private static VisitRequestCampus NewInstance(ulong campusId) => new()
    {
        CampusId = campusId,
        PlannedStartAt = DateTime.Now.AddDays(20),
        PlannedEndAt = DateTime.Now.AddDays(20).AddHours(2),
        Status = "WAITING_REQUEST_APPROVAL",
        CreatedAt = DateTime.Now,
    };

    private static VisitInstanceFormDetail NewDetail(string tag, bool perCampus) => new()
    {
        DelegationName = perCampus ? $"DELEG-{tag}" : "V2-DELEG",
        VisitType = "MEETING",
        Purpose = perCampus ? $"PURPOSE-{tag}" : "V2-PURPOSE",
        WorkingContent = perCampus ? $"CONTENT-{tag}" : "V2-CONTENT",
        OperationalContactFullName = $"Op-{tag}", OperationalContactOrganization = $"OpOrg-{tag}",
        OperationalContactPhone = "+8410", OperationalContactEmail = $"op-{tag.ToLowerInvariant()}@example.com",
        WorkingLanguage = "EN", MediaConsentStatus = "AGREED",
        FormRevision = 1, ApprovalRevision = 1, CreatedAt = DateTime.Now,
    };

    private static VisitParticipant NewParticipant(ulong instanceId) => new()
    {
        VisitInstanceId = instanceId,
        UserId = Owner,
        ParticipantRole = ParticipantRoles.IcSupport,
        IsHost = false,
        Status = "INVITED",
        InvitedAt = DateTime.Now,
        CreatedAt = DateTime.Now,
    };

    /// <summary>Seeds one request with N campus instances (v2 → a per-campus detail each), plus one invitation
    /// (participant) per instance owned by <see cref="Owner"/>. Returns participant ids ordered by campus.</summary>
    private static async Task<(VisitRequest req, List<ulong> participantIds)> Seed(
        ApplicationDbContext db, byte schemaVersion, ulong[] campusIds, bool mixed)
    {
        var req = NewRequest(schemaVersion, campusIds.Length > 1 ? "MULTI_CAMPUS" : "SINGLE_CAMPUS", mixed);
        var isV2 = schemaVersion >= FormSchemaVersions.PerCampus;
        var tags = new[] { "A", "B", "C", "D", "E" };
        for (var i = 0; i < campusIds.Length; i++)
        {
            var inst = NewInstance(campusIds[i]);
            if (isV2) inst.FormDetail = NewDetail(tags[i], perCampus: mixed);
            req.CampusInstances.Add(inst);
        }
        db.VisitRequests.Add(req);
        await db.SaveChangesAsync();

        var ordered = req.CampusInstances.OrderBy(c => c.CampusId).ToList();
        var partIds = new List<ulong>();
        foreach (var inst in ordered)
        {
            var part = NewParticipant(inst.VisitInstanceId);
            db.VisitParticipants.Add(part);
            await db.SaveChangesAsync();
            partIds.Add(part.ParticipantId);
        }
        return (req, partIds);
    }

    private sealed class CommandCounter : DbCommandInterceptor
    {
        public int Count;
        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
        { Count++; return base.ReaderExecuting(command, eventData, result); }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        { Count++; return base.ReaderExecutingAsync(command, eventData, result, cancellationToken); }
    }
}
