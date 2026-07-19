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
using PEMS.Application.Delegations.Queries.GetVisitInvitationDetail;
using PEMS.Application.Delegations.Services.VisitFormRead;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Infrastructure.Persistence;
using Xunit;

namespace PEMS.IntegrationTests.VisitRequests;

/// <summary>
/// Dual-read migration tests for GetVisitInvitationDetailQueryHandler — INSTANCE-LEVEL (key ParticipantId;
/// the participant is bound to ONE campus instance; auth = p.UserId == current user, no token). A MIXED v2
/// request returns 200 with the INVITED instance's data; a participant on campus A never reads campus B.
/// Disposable <c>pems_pr3_test</c>, per-test rolled-back transaction.
/// </summary>
public sealed class VisitInvitationDetailV2Tests
{
    private const string ConnString =
        "server=localhost;port=3306;database=pems_pr3_test;user=root;password=123456;AllowUserVariables=True;GuidFormat=None";

    private const ulong VisitorOwner = 8, InvitedUserId = 4, OtherUserId = 9;
    private const ulong Campus1 = 1, Campus2 = 2, Campus3 = 3;

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
        public string? RoleCode { get; init; }
        public string? SubRole { get; init; }
        public ulong? PrimaryCampusId { get; init; }
        public ulong? DepartmentId => null;
        public ulong? SessionId => null;
        public string? LoginPortal => null;
    }

    private static FakeUser AsUser(ulong userId) => new() { UserId = userId, RoleCode = RoleCodes.Staff, SubRole = UserSubRoles.Staff };

    private static GetVisitInvitationDetailQueryHandler Handler(ApplicationDbContext db, ICurrentUserService user)
        => new(db, user, new VisitFormReadService(db, user, NullLogger<VisitFormReadService>.Instance));

    private static Task<VisitInvitationDetailDto> Run(ApplicationDbContext db, ICurrentUserService user, ulong participantId)
        => Handler(db, user).Handle(new GetVisitInvitationDetailQuery(participantId), CancellationToken.None);

    // ── Tests ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task V1_returns_global_delegation_name()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (_, _, parts) = await Seed(db, FormSchemaVersions.Legacy, new[] { Campus1 }, mixed: false, InvitedUserId);
        var dto = await Run(db, AsUser(InvitedUserId), parts[0]);

        Assert.Equal("GLOBAL-DELEG", dto.DelegationName);
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task V2_nonmixed_reads_invited_instance_detail()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (_, _, parts) = await Seed(db, FormSchemaVersions.PerCampus, new[] { Campus1 }, mixed: false, InvitedUserId);
        var dto = await Run(db, AsUser(InvitedUserId), parts[0]);

        Assert.Equal("V2-DELEG", dto.DelegationName);
        Assert.NotEqual("GLOBAL-DELEG", dto.DelegationName);
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task V2_mixed_participant_A_and_B_read_their_own_campus()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        // Same MIXED request, same user invited to BOTH campuses via two participant rows.
        var (_, _, parts) = await Seed(db, FormSchemaVersions.PerCampus, new[] { Campus1, Campus2 }, mixed: true, InvitedUserId);

        var dtoA = await Run(db, AsUser(InvitedUserId), parts[0]); // participant on campus A
        var dtoB = await Run(db, AsUser(InvitedUserId), parts[1]); // participant on campus B

        Assert.Equal("DELEG-A", dtoA.DelegationName); // 200 with campus-A data
        Assert.Equal("DELEG-B", dtoB.DelegationName); // 200 with campus-B data (no cross-leak)
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task V2_missing_detail_throws_no_fallback()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (_, inst, parts) = await Seed(db, FormSchemaVersions.PerCampus, new[] { Campus1 }, mixed: false, InvitedUserId);
        var detail = await db.VisitInstanceFormDetails.FirstAsync(d => d.VisitInstanceId == inst[0].VisitInstanceId);
        db.VisitInstanceFormDetails.Remove(detail);
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ConflictException>(() => Run(db, AsUser(InvitedUserId), parts[0]));
        Assert.Equal(VisitFormV2ErrorCodes.VisitFormDetailMissing, ex.ErrorCode);
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task Wrong_recipient_and_removed_invitation_are_not_found()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (_, _, parts) = await Seed(db, FormSchemaVersions.PerCampus, new[] { Campus1 }, mixed: false, InvitedUserId);
        // Wrong recipient: a different user cannot read this participant's invitation.
        await Assert.ThrowsAsync<NotFoundException>(() => Run(db, AsUser(OtherUserId), parts[0]));

        // Removed invitation is invisible even to its own recipient.
        var p = await db.VisitParticipants.FirstAsync(x => x.ParticipantId == parts[0]);
        p.Status = ParticipantStatuses.Removed;
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<NotFoundException>(() => Run(db, AsUser(InvitedUserId), parts[0]));
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
            var (_, _, parts) = await Seed(db, FormSchemaVersions.PerCampus, new[] { Campus1 }, mixed: false, InvitedUserId);
            c1.Count = 0;
            await Run(db, AsUser(InvitedUserId), parts[0]);
            small = c1.Count;
            await tx.RollbackAsync();
        }
        var c3 = new CommandCounter();
        using (var db = NewContext(c3))
        using (var tx = await db.Database.BeginTransactionAsync())
        {
            var (_, _, parts) = await Seed(db, FormSchemaVersions.PerCampus, new[] { Campus1, Campus2, Campus3 }, mixed: true, InvitedUserId);
            c3.Count = 0;
            await Run(db, AsUser(InvitedUserId), parts[0]);
            large = c3.Count;
            await tx.RollbackAsync();
        }
        Assert.Equal(small, large);
    }

    // ── Seed helpers ──────────────────────────────────────────────────────────

    private static VisitRequest NewRequest(byte schemaVersion, string scope, bool mixed) => new()
    {
        RequestCode = "INV-" + Guid.NewGuid().ToString("N")[..12],
        VisitorUserId = VisitorOwner,
        RegistrantUserId = VisitorOwner,
        CreatedSource = "VISITOR_SUBMITTED",
        FormSchemaVersion = schemaVersion,
        HasMixedCampusDetails = mixed,
        RegistrantFullName = "Reg", RegistrantOrganization = "Org", RegistrantJobTitle = "Job",
        RegistrantPhone = "+8490", RegistrantEmail = "reg@example.com", RegistrantNationality = "VN",
        DelegationName = "GLOBAL-DELEG", VisitScope = scope, VisitType = "MEETING",
        Purpose = "GLOBAL-PURPOSE", WorkingContent = "GLOBAL-CONTENT",
        ContactPersonFullName = "Primary Contact", ContactPersonOrganization = "COrg",
        ContactPersonPhone = "+8491", ContactPersonEmail = "contact@example.com",
        WorkingLanguage = "EN", MediaConsentStatus = "DECLINED",
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
        OperationalContactPhone = "+8410", OperationalContactEmail = $"op-{tag}@example.com",
        WorkingLanguage = "EN", MediaConsentStatus = "AGREED",
        FormRevision = 1, ApprovalRevision = 1, CreatedAt = DateTime.Now,
    };

    private static VisitGuestMember NewMember(ulong requestId, string name) => new()
    {
        VisitRequestId = requestId, MemberType = "GUEST", FullName = name,
        Organization = "GOrg", JobTitle = "GJob", Nationality = "VN", CreatedAt = DateTime.Now,
    };

    private static async Task<(VisitRequest req, List<VisitRequestCampus> instances, List<ulong> participantIds)> Seed(
        ApplicationDbContext db, byte schemaVersion, ulong[] campusIds, bool mixed, ulong invitedUserId)
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
        if (isV2)
        {
            for (var i = 0; i < ordered.Count; i++)
            {
                var member = NewMember(req.VisitRequestId, $"{tags[i]}-guest");
                db.VisitGuestMembers.Add(member);
                await db.SaveChangesAsync();
                db.VisitInstanceGuestMembers.Add(new VisitInstanceGuestMember
                {
                    VisitRequestId = req.VisitRequestId,
                    VisitInstanceId = ordered[i].VisitInstanceId,
                    GuestMemberId = member.GuestMemberId,
                    DisplayOrder = 0, CreatedAt = DateTime.Now,
                });
            }
            await db.SaveChangesAsync();
        }

        // One invitation (participant) for the invited user on each campus instance.
        var participantIds = new List<ulong>();
        foreach (var inst in ordered)
        {
            var p = new VisitParticipant
            {
                VisitInstanceId = inst.VisitInstanceId,
                UserId = invitedUserId,
                ParticipantRole = ParticipantRoles.IcSupport,
                IsHost = false,
                Status = ParticipantStatuses.Invited,
                InvitedAt = DateTime.Now,
                CreatedAt = DateTime.Now,
            };
            db.VisitParticipants.Add(p);
            await db.SaveChangesAsync();
            participantIds.Add(p.ParticipantId);
        }
        return (req, ordered, participantIds);
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
