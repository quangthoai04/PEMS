using Microsoft.EntityFrameworkCore;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Campuses;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Entities.Departments;
using PEMS.Domain.Entities.Users;
using PEMS.Infrastructure.Persistence;
using PEMS.Shared;

namespace PEMS.IntegrationTests.Reports;

/// <summary>
/// Minimal, explicit fixture data for the canonical v2 reader regressions.
///
/// Everything a test asserts on is written here in full — no hidden seed file, no reliance on
/// whatever rows a shared database happens to contain. Each test scenario gets its own campus,
/// department and request ids so the assertions stay independent even though the schema is shared.
///
/// The literals used for the two competing values are deliberately unmistakable:
/// <c>STALE_GLOBAL_*</c> only ever exists on <c>visit_requests</c> (the v2 compatibility
/// projection), and <c>CANONICAL_*</c> only ever exists on <c>visit_instance_form_details</c>.
/// A v2 read that returns a STALE value has therefore provably read the projection.
/// </summary>
internal static class CanonicalV2Seed
{
    public const string StaleGlobalName = "STALE_GLOBAL_DELEGATION";
    public const string CanonicalNameA = "CANONICAL_CAMPUS_A";
    public const string CanonicalNameB = "CANONICAL_CAMPUS_B";
    public const string V1GlobalName = "V1_GLOBAL_DELEGATION";

    public const string StaleGlobalVisitType = "CAMPUS_TOUR";
    public const string CanonicalVisitType = "MOU_SIGNING";

    /// <summary>Fixed so period filters are deterministic regardless of when the suite runs.</summary>
    public static readonly DateTime PeriodStart = new(2026, 3, 1, 0, 0, 0);
    public static readonly DateTime PlannedStart = new(2026, 3, 10, 9, 0, 0);
    public static readonly DateTime PlannedEnd = new(2026, 3, 10, 17, 0, 0);

    /// <summary>
    /// Truncates the tables these tests touch.
    ///
    /// Deliberately raw DELETEs rather than <c>RemoveRange</c>: loading the rows first makes EF
    /// cascade-sever tracked relationships and throw before it ever reaches the database. The
    /// change tracker is cleared afterwards so a following seed cannot collide with stale
    /// identities left over from the previous test.
    /// </summary>
    public static void Reset(ApplicationDbContext db)
    {
        db.Database.ExecuteSqlRaw("SET FOREIGN_KEY_CHECKS = 0");
        foreach (var table in new[]
                 {
                     "visit_logistics_items", "visit_instance_form_details", "visit_request_campuses",
                     "visit_requests", "departments", "users", "campuses", "roles",
                 })
        {
            db.Database.ExecuteSqlRaw($"DELETE FROM `{table}`");
        }

        db.Database.ExecuteSqlRaw("SET FOREIGN_KEY_CHECKS = 1");
        db.ChangeTracker.Clear();
    }

    public static void SeedOrganisation(ApplicationDbContext db)
    {
        db.Roles.Add(new Role { RoleId = 1, RoleCode = "STAFF", Name = "Staff", Status = "ACTIVE", CreatedAt = PeriodStart });

        db.Campuses.Add(NewCampus(1, "HN", "Hoa Lac"));
        db.Campuses.Add(NewCampus(2, "HCM", "Ho Chi Minh"));

        db.Users.Add(new User
        {
            UserId = 1,
            FullName = "Staff Leader HN",
            Email = "leader.hn@example.test",
            RoleId = 1,
            SubRole = "LEADER",
            PrimaryCampusId = 1,
            Status = "ACTIVE",
            CreatedAt = PeriodStart,
        });

        // One department per campus. The invoice query scopes by department AND campus, so the
        // HCM department exists purely to prove the cross-campus rejection path is real.
        db.Departments.Add(NewDepartment(10, campusId: 1));
        db.Departments.Add(NewDepartment(20, campusId: 2));

        db.SaveChanges();
    }

    private static Campus NewCampus(ulong id, string code, string name) => new()
    {
        CampusId = id,
        CampusCode = code,
        Name = name,
        City = name,
        Address = $"{name} address",
        Status = "ACTIVE",
        CreatedAt = PeriodStart,
    };

    private static Department NewDepartment(ulong id, ulong campusId) => new()
    {
        DepartmentId = id,
        CampusId = campusId,
        Name = $"Facilities {campusId}",
        DepartmentType = "GENERAL",
        Status = "ACTIVE",
        CreatedAt = PeriodStart,
    };

    /// <summary>
    /// Creates one request with one campus instance per entry in <paramref name="campusDetails"/>.
    /// A null canonical name means "this v2 instance has NO per-campus detail row" — the missing
    /// detail case, which must never silently fall back to the projection.
    /// </summary>
    public static void SeedRequest(
        ApplicationDbContext db,
        ulong requestId,
        byte formSchemaVersion,
        bool hasMixedCampusDetails,
        string globalDelegationName,
        string globalVisitType,
        IReadOnlyList<(ulong InstanceId, ulong CampusId, ulong DepartmentId, string? CanonicalName, string? CanonicalVisitType)> campusDetails)
    {
        db.VisitRequests.Add(new VisitRequest
        {
            VisitRequestId = requestId,
            RequestCode = $"REQ-{requestId:D4}",
            FormSchemaVersion = formSchemaVersion,
            HasMixedCampusDetails = hasMixedCampusDetails,
            RegistrantFullName = "Registrant",
            RegistrantNationality = "VN",
            RegistrantOrganization = "Org",
            RegistrantJobTitle = "Manager",
            RegistrantPhone = "0900000000",
            RegistrantEmail = $"registrant{requestId}@example.test",
            DelegationName = globalDelegationName,
            VisitScope = campusDetails.Count > 1 ? "MULTI_CAMPUS" : "SINGLE_CAMPUS",
            VisitType = globalVisitType,
            Purpose = "Purpose",
            ContactPersonFullName = "Contact",
            ContactPersonOrganization = "Contact Org",
            ContactPersonPhone = "0900000001",
            ContactPersonEmail = $"contact{requestId}@example.test",
            WorkingLanguage = "EN",
            MediaConsentStatus = "DECLINED",
            Status = VisitRequestStatus.Approved,
            SubmittedAt = PeriodStart.AddDays(1),
            CreatedAt = PeriodStart,
        });

        foreach (var (instanceId, campusId, departmentId, canonicalName, canonicalVisitType) in campusDetails)
        {
            db.VisitRequestCampuses.Add(new VisitRequestCampus
            {
                VisitInstanceId = instanceId,
                VisitRequestId = requestId,
                CampusId = campusId,
                PlannedStartAt = PlannedStart,
                PlannedEndAt = PlannedEnd,
                Status = VisitInstanceStatus.Closed,
                CreatedAt = PeriodStart,
            });

            if (canonicalName != null)
            {
                db.VisitInstanceFormDetails.Add(new VisitInstanceFormDetail
                {
                    VisitInstanceId = instanceId,
                    DelegationName = canonicalName,
                    VisitType = canonicalVisitType ?? CanonicalVisitType,
                    Purpose = "Canonical purpose",
                    OperationalContactFullName = "Operational contact",
                    OperationalContactPhone = "0900000002",
                    WorkingLanguage = "EN",
                    MediaConsentStatus = "DECLINED",
                    CreatedAt = PeriodStart,
                });
            }

            // A DONE logistics item is what makes the instance visible to the invoice query.
            db.VisitLogisticsItems.Add(new VisitLogisticsItem
            {
                LogisticsItemId = instanceId,
                VisitInstanceId = instanceId,
                ItemType = "OTHER",
                Title = $"Item {instanceId}",
                Quantity = 1,
                Status = LogisticsItemStatus.Done,
                RequestedToDepartmentId = departmentId,
                UsageStartAt = PlannedStart,
                UsageEndAt = PlannedEnd,
                RequestedAt = PeriodStart,
                CreatedAt = PeriodStart,
            });
        }

        db.SaveChanges();
    }
}
