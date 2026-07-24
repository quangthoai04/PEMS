using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Queries.GetEditableVisitRequestDetail;
using PEMS.Application.Delegations.Services.VisitFormRead;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Infrastructure.Persistence;
using Xunit;

namespace PEMS.IntegrationTests.VisitRequests;

/// <summary>
/// Dual-read migration tests for GetEditableVisitRequestDetailQueryHandler (the Visitor-owner-only
/// edit/resubmit prefill form). Runs against the disposable MySQL <c>pems_pr3_test</c> (PR-2 master),
/// each test inside a rolled-back transaction. Contract:
///  • v1 (form_schema_version=1)  → global projection, unchanged.
///  • v2 non-mixed                → form content DERIVED from visit_instance_form_details +
///                                  visit_instance_guest_members (never the global fields).
///  • v2 mixed                    → 409 FORM_VERSION_UPGRADE_REQUIRED (flat single-form editor).
///  • v2 visible w/o detail       → 409 VISIT_FORM_DETAIL_MISSING (no fallback).
///  • owner-only                  → non-owner / non-visitor → 403.
/// </summary>
public sealed class EditableVisitRequestDetailV2Tests
{
    private static string ConnString => PEMS.IntegrationTests.TestInfrastructure.DisposableDatabaseManager.GetDisposableConnectionString("server=localhost;port=3306;database=pems_pr3_test;user=root;password=123456;AllowUserVariables=True;GuidFormat=None");

    private const ulong VisitorOwner = 8, VisitorOther = 22, HoUser = 2;
    private const ulong Campus1 = 1, Campus2 = 2;

    private static bool? _dbUp;

    private static ApplicationDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseMySql(ConnString, ServerVersion.AutoDetect(ConnString)).Options;
        return new ApplicationDbContext(options);
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

    private sealed class FakeClock : IDateTimeService
    {
        public DateTime UtcNow => DateTime.UtcNow;
        public DateTime VietnamNow => DateTime.Now;
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

    private static FakeUser Owner() => new() { UserId = VisitorOwner, RoleCode = RoleCodes.Visitor };
    private static FakeUser Unrelated() => new() { UserId = VisitorOther, RoleCode = RoleCodes.Visitor };
    private static FakeUser Ho() => new() { UserId = HoUser, RoleCode = RoleCodes.Ho };

    private static GetEditableVisitRequestDetailQueryHandler Handler(ApplicationDbContext db, ICurrentUserService user)
        => new(db, user, new FakeClock(),
            new VisitFormReadService(db, user, NullLogger<VisitFormReadService>.Instance));

    private static Task<EditableVisitRequestDetailDto> Run(ApplicationDbContext db, ICurrentUserService user, ulong reqId)
        => Handler(db, user).Handle(new GetEditableVisitRequestDetailQuery(reqId), CancellationToken.None);

    // ── Tests ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Replaces the former V1 test, which was the only one asserting the edit MODE and the guest list on a
    /// single-campus request. Under Pure V2 the guest list is reached through
    /// <c>visit_instance_guest_members</c> for that one campus — there is no request-level roster to fall
    /// back to, so an editable payload that still produced one would be reading something that must not exist.
    /// </summary>
    [Fact]
    public async Task Single_campus_editable_payload_is_edit_mode_with_the_campus_guest_list()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, _) = await SeedEditable(db, FormSchemaVersions.PerCampus, new[] { Campus1 }, mixed: false);
        var dto = await Run(db, Owner(), req.VisitRequestId);

        Assert.Equal("EDIT", dto.Mode);
        Assert.Equal("V2-DELEG", dto.DelegationName);
        Assert.Equal("V2-PURPOSE", dto.Purpose);
        Assert.Contains(dto.Visitors, m => m.FullName == "A-guest");
        Assert.Single(dto.CampusVisits);
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task V2_nonmixed_editable_derives_form_from_detail_not_global()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, _) = await SeedEditable(db, FormSchemaVersions.PerCampus, new[] { Campus1, Campus2 }, mixed: false);
        var dto = await Run(db, Owner(), req.VisitRequestId);

        Assert.Equal("V2-DELEG", dto.DelegationName);
        Assert.NotEqual("GLOBAL-DELEG", dto.DelegationName);
        Assert.Equal("V2-PURPOSE", dto.Purpose);
        Assert.Contains(dto.Visitors, m => m.FullName == "A-guest");
        Assert.Equal(2, dto.CampusVisits.Count);
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task V2_mixed_editable_throws_upgrade_required()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, _) = await SeedEditable(db, FormSchemaVersions.PerCampus, new[] { Campus1, Campus2 }, mixed: true);

        var ex = await Assert.ThrowsAsync<ConflictException>(() => Run(db, Owner(), req.VisitRequestId));
        Assert.Equal(VisitFormV2ErrorCodes.FormVersionUpgradeRequired, ex.ErrorCode);
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task V2_missing_detail_throws_no_fallback()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, instances) = await SeedEditable(db, FormSchemaVersions.PerCampus, new[] { Campus1 }, mixed: false);
        var detail = await db.VisitInstanceFormDetails.FirstAsync(d => d.VisitInstanceId == instances[0].VisitInstanceId);
        db.VisitInstanceFormDetails.Remove(detail);
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ConflictException>(() => Run(db, Owner(), req.VisitRequestId));
        Assert.Equal(VisitFormV2ErrorCodes.VisitFormDetailMissing, ex.ErrorCode);
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task Non_owner_and_non_visitor_are_forbidden()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, _) = await SeedEditable(db, FormSchemaVersions.PerCampus, new[] { Campus1 }, mixed: false);

        await Assert.ThrowsAsync<ForbiddenException>(() => Run(db, Unrelated(), req.VisitRequestId)); // not the owner
        await Assert.ThrowsAsync<ForbiddenException>(() => Run(db, Ho(), req.VisitRequestId));        // not a Visitor
        await tx.RollbackAsync();
    }

    // ── Seed helpers ──────────────────────────────────────────────────────────

    private static VisitRequest NewRequest(byte schemaVersion, string scope, bool mixed) => new()
    {
        RequestCode = "EDV-" + Guid.NewGuid().ToString("N")[..12],
        VisitorUserId = VisitorOwner,
        RegistrantUserId = VisitorOwner,
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
        OperationalContactPhone = "+8410", OperationalContactEmail = $"op-{tag}@example.com",
        WorkingLanguage = "EN", MediaConsentStatus = "AGREED",
        FormRevision = 1, ApprovalRevision = 1, CreatedAt = DateTime.Now,
    };

    private static VisitGuestMember NewMember(ulong requestId, string name, string type = "GUEST") => new()
    {
        VisitRequestId = requestId, MemberType = type, FullName = name,
        Organization = "GOrg", JobTitle = "GJob", Nationality = "VN", CreatedAt = DateTime.Now,
    };

    private static async Task<(VisitRequest req, List<VisitRequestCampus> instances)> SeedEditable(
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
        if (!isV2)
        {
            req.GuestMembers.Add(NewMember(0, "G1"));
            req.GuestMembers.Add(NewMember(0, "S1", "EXTERNAL_SUPPORT"));
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
        return (req, ordered);
    }
}
