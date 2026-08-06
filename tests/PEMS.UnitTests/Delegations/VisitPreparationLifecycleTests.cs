using System.Linq;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Delegations.Common;
using PEMS.Application.Delegations.Services;
using PEMS.Domain.Constants;
using PEMS.Shared;
using Xunit;

namespace PEMS.UnitTests.Delegations;

/// <summary>
/// The boundary between ASSIGNED and BEFORE_VISIT.
///
/// <para>
/// These two used to be interchangeable, and the whole point of the current model is that they are
/// not: approving a campus lands it on ASSIGNED with a Host and a decision and NOTHING set up, and
/// only the Host's explicit "Bắt đầu chuẩn bị" opens the setup window. Every rule below is one half
/// of that split, so a future edit that quietly makes one status accept the other fails here first.
/// </para>
/// </summary>
public class VisitPreparationLifecycleTests
{
    // ── The preparation gate: what a setup mutation is allowed to do ────────────────────────────

    [Fact]
    public void PreparationGate_AllowsSetup_OnlyOnceTheHostStarted()
        => VisitPreparationGate.EnsurePreparationOpen(
            VisitInstanceStatuses.BeforeVisit, "chỉnh sửa lịch trình"); // must not throw

    /// <summary>
    /// The refusal a Host sees on their own freshly approved campus. It carries its own code because
    /// it is the recoverable one: one click fixes it, and the UI is expected to offer that click.
    /// </summary>
    [Fact]
    public void PreparationGate_RefusesSetupWhileAssigned_WithItsOwnErrorCode()
    {
        var ex = Assert.Throws<ConflictException>(() =>
            VisitPreparationGate.EnsurePreparationOpen(
                VisitInstanceStatuses.Assigned, "chỉnh sửa lịch trình"));

        Assert.Equal(VisitRequestErrorCodes.VisitPreparationNotStarted, ex.ErrorCode);
    }

    /// <summary>
    /// Every other refusal is a plain conflict. Sharing the recoverable code here would tell the UI
    /// to offer "start preparation" on a campus that is closed, cancelled or already receiving guests.
    /// </summary>
    [Theory]
    [InlineData(VisitInstanceStatuses.WaitingContactConfirmation)]
    [InlineData(VisitInstanceStatuses.WaitingRequestApproval)]
    [InlineData(VisitInstanceStatuses.DuringVisit)]
    [InlineData(VisitInstanceStatuses.AfterVisit)]
    [InlineData(VisitInstanceStatuses.Closed)]
    [InlineData(VisitInstanceStatuses.Cancelled)]
    [InlineData(VisitInstanceStatuses.Rejected)]
    public void PreparationGate_RefusesSetupElsewhere_WithoutTheRecoverableCode(string status)
    {
        var ex = Assert.Throws<ConflictException>(() =>
            VisitPreparationGate.EnsurePreparationOpen(status, "chỉnh sửa lịch trình"));

        Assert.NotEqual(VisitRequestErrorCodes.VisitPreparationNotStarted, ex.ErrorCode);
    }

    // ── The two statuses are genuinely distinct values ──────────────────────────────────────────

    [Fact]
    public void AssignedAndBeforeVisit_AreDistinctStates()
    {
        Assert.NotEqual(VisitInstanceStatuses.Assigned, VisitInstanceStatuses.BeforeVisit);
        Assert.Equal("ASSIGNED", VisitInstanceStatuses.Assigned);
        Assert.Equal("BEFORE_VISIT", VisitInstanceStatuses.BeforeVisit);
        // The two constant classes are hand-synced; drift between them would split the codebase in
        // half without a single compile error.
        Assert.Equal(VisitInstanceStatus.Assigned, VisitInstanceStatuses.Assigned);
        Assert.Equal(VisitInstanceStatus.BeforeVisit, VisitInstanceStatuses.BeforeVisit);
    }

    // ── Decided-campus rules: ASSIGNED counts, because the campus already has an owner ───────────

    [Fact]
    public void ApprovedOrBeyond_CountsAssigned()
    {
        Assert.Contains(VisitInstanceStatuses.Assigned, VisitInstanceStatuses.ApprovedOrBeyond);
        Assert.Contains(VisitInstanceStatuses.BeforeVisit, VisitInstanceStatuses.ApprovedOrBeyond);
        Assert.DoesNotContain(VisitInstanceStatuses.WaitingRequestApproval, VisitInstanceStatuses.ApprovedOrBeyond);
    }

    [Fact]
    public void DecidedNotStarted_IsExactlyAssignedAndBeforeVisit()
        => Assert.Equal(
            new[] { VisitInstanceStatuses.Assigned, VisitInstanceStatuses.BeforeVisit }.OrderBy(x => x),
            VisitInstanceStatuses.DecidedNotStarted.OrderBy(x => x));

    /// <summary>
    /// An ASSIGNED campus is an approved campus to the request aggregate. Leaving it out would make a
    /// request read as still-pending the moment its last campus was approved.
    /// </summary>
    [Theory]
    [InlineData(VisitInstanceStatus.Assigned)]
    [InlineData(VisitInstanceStatus.BeforeVisit)]
    public void Aggregate_TreatsAssignedAsApproved(string campusStatus)
    {
        var service = new VisitRequestAggregateStatusService(db: null!);

        var result = service.Compute(
            VisitRequestStatuses.PendingApproval,
            new[] { new CampusAggregateInput(campusStatus, HasOperationalContact: true) });

        Assert.Equal(VisitRequestStatuses.Approved, result);
    }

    [Fact]
    public void Aggregate_AssignedPlusPending_IsPartiallyApproved()
    {
        var service = new VisitRequestAggregateStatusService(db: null!);

        var result = service.Compute(
            VisitRequestStatuses.PendingApproval,
            new[]
            {
                new CampusAggregateInput(VisitInstanceStatus.Assigned, HasOperationalContact: true),
                new CampusAggregateInput(VisitInstanceStatus.WaitingRequestApproval, HasOperationalContact: true),
            });

        Assert.Equal(VisitRequestStatuses.PartiallyApproved, result);
    }
}
