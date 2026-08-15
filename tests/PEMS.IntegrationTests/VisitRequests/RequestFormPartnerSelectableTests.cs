using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Partners.Common;
using PEMS.Infrastructure.Persistence;
using Xunit;

namespace PEMS.IntegrationTests.VisitRequests;

/// <summary>
/// PART-09, against the real schema and the real seeded partners: which profiles a registration form
/// may cite as a delegation member's employer.
///
/// <para>
/// The unit tests pin the predicate; this pins what it MEANS on the data the system actually holds —
/// including that it translates to SQL at all (Pomelo), and that the seeded pending / internal /
/// rejected / inactive profiles really are refused rather than merely looking as if they would be.
/// </para>
///
/// <para>
/// The rule stopped depending on who is asking. It used to: an authenticated Staff Leader was
/// validated against the wider internal set, which includes their own campus's profiles awaiting a
/// decision — so a pending profile offered by a dropdown that had widened for the same reason sailed
/// through to <c>visit_guest_members</c>. Partner 120 below is exactly that fixture: PENDING_APPROVAL,
/// owned by campus HN, INTERNAL.
/// </para>
/// </summary>
public sealed class RequestFormPartnerSelectableTests
{
    private static string ConnString => TestInfrastructure.DisposableDatabaseManager.GetDisposableConnectionString(
        "server=localhost;port=3306;database=pems_pr3_test;user=root;password=123456;AllowUserVariables=True;GuidFormat=None");

    /// <summary>APPROVED + PUBLIC + ACTIVE — the only shape a registration form may cite.</summary>
    private const ulong ApprovedPublic = 103;
    /// <summary>PENDING_APPROVAL, campus HN's own, INTERNAL — the profile that used to slip through.</summary>
    private const ulong PendingOwnCampus = 120;
    /// <summary>APPROVED + ACTIVE but INTERNAL visibility.</summary>
    private const ulong ApprovedInternal = 6;
    /// <summary>APPROVED + INTERNAL but the cooperation itself is INACTIVE.</summary>
    private const ulong ApprovedInactive = 105;
    /// <summary>REJECTED.</summary>
    private const ulong Rejected = 104;
    /// <summary>DRAFT.</summary>
    private const ulong Draft = 101;

    private static bool? _dbUp;

    private static ApplicationDbContext NewContext()
        => new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseMySql(ConnString, ServerVersion.AutoDetect(ConnString)).Options);

    private static void RequireDb()
    {
        if (_dbUp is null)
        {
            try { using var db = NewContext(); _dbUp = db.Database.CanConnect(); }
            catch { _dbUp = false; }
        }
        Assert.True(_dbUp!.Value, "pems_pr3_test is not reachable.");
    }

    private static Task EnsureAsync(ApplicationDbContext db, params ulong[] ids) =>
        GuestOrganizationPartnerPolicy.EnsureRequestFormSelectableAsync(db, ids, CancellationToken.None);

    [Fact]
    public async Task An_approved_public_active_profile_is_accepted()
    {
        RequireDb();
        using var db = NewContext();
        await EnsureAsync(db, ApprovedPublic);   // does not throw
    }

    [Fact]
    public async Task Nothing_to_check_is_not_an_error()
    {
        RequireDb();
        using var db = NewContext();
        // Members with a free-text organisation carry no id at all, which is the ordinary case.
        await EnsureAsync(db);
    }

    [Theory]
    [InlineData(PendingOwnCampus)]
    [InlineData(ApprovedInternal)]
    [InlineData(ApprovedInactive)]
    [InlineData(Rejected)]
    [InlineData(Draft)]
    public async Task Everything_else_is_refused_with_the_same_code(ulong partnerId)
    {
        RequireDb();
        using var db = NewContext();

        var refusal = await Assert.ThrowsAsync<BusinessRuleException>(() => EnsureAsync(db, partnerId));

        Assert.Equal(GuestOrganizationPartnerPolicy.NotSelectableCode, refusal.ErrorCode);
        // One message for every reason: telling "does not exist" apart from "not approved" would turn
        // the form into a probe for which partner ids exist and what state they are in.
        Assert.Equal(GuestOrganizationPartnerPolicy.NotSelectableMessage, refusal.Message);
    }

    [Fact]
    public async Task A_partner_id_that_names_nothing_is_refused_the_same_way()
    {
        RequireDb();
        using var db = NewContext();

        var refusal = await Assert.ThrowsAsync<BusinessRuleException>(() => EnsureAsync(db, 999_999_999));
        Assert.Equal(GuestOrganizationPartnerPolicy.NotSelectableCode, refusal.ErrorCode);
    }

    [Fact]
    public async Task One_bad_id_in_a_set_refuses_the_whole_set()
    {
        RequireDb();
        using var db = NewContext();

        // A delegation of thirty people from three organisations is three ids, checked as a SET in one
        // round trip — and a single unusable one has to stop the submit, not be quietly dropped from it.
        await Assert.ThrowsAsync<BusinessRuleException>(
            () => EnsureAsync(db, ApprovedPublic, PendingOwnCampus));
    }

    [Fact]
    public async Task The_partner_module_still_sees_what_the_form_may_not_cite()
    {
        RequireDb();
        using var db = NewContext();

        // The module's own scope is deliberately untouched: seeing a pending profile is its job, and
        // narrowing it would have broken approving one. Only the FORM's rule changed.
        var moduleSees = await GuestOrganizationPartnerPolicy
            .InternalSelectable(db.Partners.AsNoTracking(), actorCampusId: 1)
            .Select(p => p.PartnerId)
            .ToListAsync();

        Assert.Contains(PendingOwnCampus, moduleSees);
        Assert.Contains(ApprovedInternal, moduleSees);
        Assert.DoesNotContain(Rejected, moduleSees);
    }
}
