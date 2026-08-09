using Microsoft.EntityFrameworkCore;
using Moq;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Options;
using PEMS.Application.Delegations.Commands.OperationalContact;
using PEMS.Application.Delegations.Services;
using PEMS.Domain.Constants;
using PEMS.UnitTests.TestInfrastructure;

namespace PEMS.UnitTests.Delegations.OperationalContact;

/// <summary>
/// Re-inviting closes the contact gate, so the REQUEST's status has to be re-derived in the same
/// transaction — by the canonical aggregate service, not by hand and not later.
///
/// <para>
/// The command puts one campus back to <c>WAITING_CONTACT_CONFIRMATION</c>, and
/// <c>visit_requests.status</c> is a function of its campuses: a campus with no confirmed contact
/// means the whole request belongs behind the gate at <c>PENDING_CONTACT_CONFIRMATION</c>. Without the
/// recompute the tracked request kept whatever it said before — typically
/// <c>PENDING_APPROVAL</c> — and three things followed from that one omission. The response handed the
/// registrant a status that was already untrue. The database's own aggregate trigger would write the
/// correct value underneath, so the application and the row disagreed until something reloaded. And
/// <c>ContactGateRevision</c> never moved, which is the part that outlives the request: the revision
/// is the dedupe key for the approval-ready mail, so the NEXT time this request opened its gate the
/// key would look already-used and every Staff Leader on it would simply never be told.
/// </para>
/// </summary>
public class ReinviteRecomputesRequestAggregateTests
{
    private static readonly DateTime Now = new(2026, 8, 9, 10, 0, 0);

    private const ulong RegistrantId = 500;
    private const ulong RequestId = DelegationsTestData.VisitRequestId;
    private const ulong InstanceId = DelegationsTestData.VisitInstanceId;

    /// <summary>
    /// The state a re-invite is FOR, and the one that exposes the bug: a cancelled invitation left this
    /// campus at WAITING_REQUEST_APPROVAL with nobody holding it, so the request had already been
    /// aggregated forward to PENDING_APPROVAL while the campus has no operational contact at all.
    /// </summary>
    private static async Task<(OperationalContactTestDbContext Db, RecordingOperationalContactInvitationService Invitations)>
        ArrangeCampusPastTheGateWithNoContactAsync()
    {
        var db = OperationalContactTestDbContext.Create();

        db.Campuses.Add(DelegationsTestData.CreateCampus());
        var visit = DelegationsTestData.CreateVisitRequest();
        visit.Status = VisitRequestStatuses.PendingApproval;
        visit.RegistrantUserId = RegistrantId;
        visit.EmailVerifiedAt = Now.AddDays(-1);
        visit.ContactGateRevision = 3;

        var instance = DelegationsTestData.CreateVisitInstance(
            status: VisitInstanceStatuses.WaitingRequestApproval, currentHostUserId: null);
        instance.OperationalContactUserId = null;
        visit.CampusInstances.Add(instance);
        db.VisitRequests.Add(visit);

        await db.SaveChangesAsync(CancellationToken.None);
        db.Journal.Clear();
        db.ChangeTracker.Clear();

        return (db, new RecordingOperationalContactInvitationService(db, Now));
    }

    [Fact]
    public async Task Reinvite_puts_the_campus_and_the_request_back_behind_the_contact_gate()
    {
        var (db, invitations) = await ArrangeCampusPastTheGateWithNoContactAsync();

        var response = await Handler(db, invitations).Handle(
            new ReinviteOperationalContactConfirmationCommand(RequestId, InstanceId), CancellationToken.None);

        var visit = await db.VisitRequests.AsNoTracking()
            .Include(v => v.CampusInstances)
            .SingleAsync(v => v.VisitRequestId == RequestId);

        Assert.Equal(
            VisitInstanceStatuses.WaitingContactConfirmation,
            visit.CampusInstances.Single().Status);
        // The whole point: the request is re-derived from the campus rather than left where it was.
        Assert.Equal(VisitRequestStatuses.PendingContactConfirmation, visit.Status);

        // …and the caller is told the status that is now true, not the one the entity was tracking
        // when the command started.
        Assert.Equal(VisitRequestStatuses.PendingContactConfirmation, response.RequestStatus);
        Assert.Equal(VisitInstanceStatuses.WaitingContactConfirmation, response.CampusStatus);
    }

    /// <summary>
    /// The gate CLOSED here, so its revision must move — once. The revision is the dedupe key for the
    /// approval-ready notification, and a close that does not bump it lets the next opening reuse a key
    /// that has already been mailed against.
    /// </summary>
    [Fact]
    public async Task Reinvite_bumps_the_contact_gate_revision_exactly_once()
    {
        var (db, invitations) = await ArrangeCampusPastTheGateWithNoContactAsync();

        await Handler(db, invitations).Handle(
            new ReinviteOperationalContactConfirmationCommand(RequestId, InstanceId), CancellationToken.None);

        var visit = await db.VisitRequests.AsNoTracking().SingleAsync(v => v.VisitRequestId == RequestId);
        Assert.Equal(4u, visit.ContactGateRevision);
    }

    /// <summary>
    /// A campus that was ALREADY behind the gate is not a gate transition, so the revision must stay
    /// put. Bumping on every re-invite would burn dedupe keys for an event that never happened.
    /// </summary>
    [Fact]
    public async Task Reinviting_a_campus_already_behind_the_gate_does_not_move_the_revision()
    {
        var db = OperationalContactTestDbContext.Create();
        db.Campuses.Add(DelegationsTestData.CreateCampus());
        var visit = DelegationsTestData.CreateVisitRequest();
        visit.Status = VisitRequestStatuses.PendingContactConfirmation;
        visit.RegistrantUserId = RegistrantId;
        visit.EmailVerifiedAt = Now.AddDays(-1);
        visit.ContactGateRevision = 3;
        var instance = DelegationsTestData.CreateVisitInstance(
            status: VisitInstanceStatuses.WaitingContactConfirmation, currentHostUserId: null);
        instance.OperationalContactUserId = null;
        visit.CampusInstances.Add(instance);
        db.VisitRequests.Add(visit);
        await db.SaveChangesAsync(CancellationToken.None);
        db.Journal.Clear();
        db.ChangeTracker.Clear();

        var invitations = new RecordingOperationalContactInvitationService(db, Now);
        var response = await Handler(db, invitations).Handle(
            new ReinviteOperationalContactConfirmationCommand(RequestId, InstanceId), CancellationToken.None);

        var after = await db.VisitRequests.AsNoTracking().SingleAsync(v => v.VisitRequestId == RequestId);
        Assert.Equal(3u, after.ContactGateRevision);
        Assert.Equal(VisitRequestStatuses.PendingContactConfirmation, after.Status);
        Assert.Equal(VisitRequestStatuses.PendingContactConfirmation, response.RequestStatus);
    }

    /// <summary>
    /// The recompute goes through the canonical service and nothing else: the handler must not carry a
    /// second copy of the aggregate rule, and must not touch the gate revision by hand. Asserted by
    /// comparing against what the service itself computes for the resulting campus state.
    /// </summary>
    [Fact]
    public async Task The_recomputed_status_is_the_canonical_services_own_answer()
    {
        var (db, invitations) = await ArrangeCampusPastTheGateWithNoContactAsync();

        await Handler(db, invitations).Handle(
            new ReinviteOperationalContactConfirmationCommand(RequestId, InstanceId), CancellationToken.None);

        var visit = await db.VisitRequests.AsNoTracking()
            .Include(v => v.CampusInstances)
            .SingleAsync(v => v.VisitRequestId == RequestId);

        var canonical = new VisitRequestAggregateStatusService(db).Compute(
            VisitRequestStatuses.PendingApproval,
            visit.CampusInstances
                .Select(c => new CampusAggregateInput(c.Status, c.OperationalContactUserId is not null))
                .ToList());

        Assert.Equal(canonical, visit.Status);
    }

    private static ReinviteOperationalContactConfirmationCommandHandler Handler(
        OperationalContactTestDbContext db, IOperationalContactInvitationService invitations)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(c => c.IsAuthenticated).Returns(true);
        currentUser.SetupGet(c => c.UserId).Returns(RegistrantId);

        var clock = new Mock<IDateTimeService>();
        clock.SetupGet(c => c.VietnamNow).Returns(Now);
        clock.SetupGet(c => c.UtcNow).Returns(Now.AddHours(-7));

        return new ReinviteOperationalContactConfirmationCommandHandler(
            db, currentUser.Object, clock.Object, invitations,
            new VisitRequestAggregateStatusService(db),
            new PerCampusFormV2WriteOptions());
    }
}
