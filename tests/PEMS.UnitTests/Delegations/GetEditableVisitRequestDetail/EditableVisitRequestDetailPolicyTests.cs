using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Delegations.Queries.GetEditableVisitRequestDetail;
using PEMS.Application.Delegations.Services;
using PEMS.Application.Delegations.Services.VisitFormRead;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Policies;
using PEMS.UnitTests.TestInfrastructure;
using Xunit;

namespace PEMS.UnitTests.Delegations.GetEditableVisitRequestDetail;

/// <summary>
/// The editor screen's own answer to "may I still edit this", asserted against the canonical policy.
///
/// <para>
/// This handler used to keep a private copy of the rule — request PENDING_APPROVAL, every campus
/// WAITING_REQUEST_APPROVAL, and a bare <c>AddHours(24)</c> — while the list that offers "Sửa đơn" and
/// the command that accepts the payload had both moved on to <see cref="VisitMutationPolicy"/>. The
/// visible symptom was a registrant clicking a button the list had just shown them and being told the
/// request was no longer editable, on a request nobody had decided anything about: it was still waiting
/// for its operational contacts to confirm.
/// </para>
/// <para>
/// So the interesting assertions here are not "editable ⇒ true" in isolation; they are the ones that
/// pin the editor to the SAME verdict the other two layers reach for the same state and the same clock.
/// </para>
/// </summary>
public class EditableVisitRequestDetailPolicyTests
{
    private const ulong RegistrantId = 4242;
    private const ulong RequestId = 77;

    // Wall clock throughout (planned_start_at is a local DATETIME) — VietnamNow is what the handler reads.
    private static readonly DateTime Now = new(2026, 8, 10, 9, 0, 0);

    // ── Fixture ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Content resolution is not what these tests are about, and reaching it at all proves the gate
    /// let the caller through. It answers for whatever instances it is handed, so a test can assert
    /// "the payload was produced" without seeding form details.
    /// </summary>
    private sealed class StubFormReadService : IVisitFormReadService
    {
        public Task<ResolvedVisitFormDto> ResolveAsync(ulong visitRequestId, CancellationToken cancellationToken)
            => throw new NotSupportedException("The editable-detail handler never calls this overload.");

        public Task<IReadOnlyDictionary<ulong, VisitCampusFormContent>> ResolveCampusFormContentAsync(
            VisitRequest request, IReadOnlyList<ulong> visibleInstanceIds, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyDictionary<ulong, VisitCampusFormContent>>(
                visibleInstanceIds.ToDictionary(id => id, _ => new VisitCampusFormContent
                {
                    DelegationName = "Đoàn khách kiểm thử",
                    Purpose = "Tham quan",
                    OperationalContact = new VisitFormOperationalContact { Email = "op@test.local" },
                }));
    }

    private static VisitRequest SeedRequest(
        DelegationsTestDbContext db, string requestStatus, params (string Status, DateTime Start)[] campuses)
    {
        db.Campuses.Add(DelegationsTestData.CreateCampus(1));
        db.Campuses.Add(DelegationsTestData.CreateCampus(2));

        var visit = new VisitRequest
        {
            VisitRequestId = RequestId,
            RequestCode = "VR-77",
            RegistrantUserId = RegistrantId,
            RegistrantFullName = "Nguyễn Văn Khách",
            RegistrantNationality = "VN",
            RegistrantOrganization = "Đối tác",
            RegistrantJobTitle = "Trưởng đoàn",
            RegistrantPhone = "0900000000",
            RegistrantEmail = "guest@test.local",
            VisitScope = campuses.Length > 1 ? VisitScopes.MultiCampus : VisitScopes.SingleCampus,
            HasMixedCampusDetails = false,
            Status = requestStatus,
            SubmittedAt = Now.AddDays(-3),
            CreatedAt = Now.AddDays(-3),
        };
        db.VisitRequests.Add(visit);

        ulong instanceId = 100;
        for (var i = 0; i < campuses.Length; i++)
        {
            db.VisitRequestCampuses.Add(new VisitRequestCampus
            {
                VisitInstanceId = instanceId++,
                VisitRequestId = RequestId,
                CampusId = (ulong)(i + 1),
                PlannedStartAt = campuses[i].Start,
                PlannedEndAt = campuses[i].Start.AddHours(2),
                Status = campuses[i].Status,
                CreatedAt = Now.AddDays(-3),
            });
        }

        db.SaveChanges();
        return visit;
    }

    private static Task<EditableVisitRequestDetailDto> Run(DelegationsTestDbContext db)
    {
        var handler = new GetEditableVisitRequestDetailQueryHandler(
            db,
            new FakeDelegationsCurrentUser { UserId = RegistrantId, RoleCode = RoleCodes.Visitor, SubRole = null },
            new FakeDateTimeService { UtcNow = Now.AddHours(-7) },   // VietnamNow == Now
            new StubFormReadService());
        return handler.Handle(new GetEditableVisitRequestDetailQuery(RequestId), CancellationToken.None);
    }

    // ── §9.1 The reported bug ────────────────────────────────────────────────

    /// <summary>
    /// The exact state from the report: nothing decided, one campus still waiting on its contact, the
    /// other already waiting on approval. The old rule refused it twice over — on the request status
    /// and on the campus status — while the list happily offered EDIT_PENDING_REQUEST.
    /// </summary>
    [Fact]
    public async Task A_request_still_waiting_on_its_contacts_is_editable()
    {
        using var db = DelegationsTestDbContext.Create();
        SeedRequest(db, VisitRequestStatuses.PendingContactConfirmation,
            (VisitInstanceStatuses.WaitingContactConfirmation, Now.AddDays(10)),
            (VisitInstanceStatuses.WaitingRequestApproval, Now.AddDays(12)));

        var dto = await Run(db);

        Assert.Equal("EDIT", dto.Mode);
        Assert.True(dto.IsEditablePending);
        Assert.False(dto.IsResubmittable);
        Assert.Equal(2, dto.CampusVisits.Count);
    }

    // ── §9.2 The state that already worked, kept working ─────────────────────

    [Fact]
    public async Task A_request_waiting_only_on_approval_is_still_editable()
    {
        using var db = DelegationsTestDbContext.Create();
        SeedRequest(db, VisitRequestStatuses.PendingApproval,
            (VisitInstanceStatuses.WaitingRequestApproval, Now.AddDays(10)),
            (VisitInstanceStatuses.WaitingRequestApproval, Now.AddDays(11)));

        var dto = await Run(db);

        Assert.Equal("EDIT", dto.Mode);
        Assert.True(dto.IsEditablePending);
    }

    // ── §9.3 / §9.4 The cutoff, at its edges ─────────────────────────────────

    /// <summary>
    /// "At least six hours before" includes the six-hour mark, and the deadline is taken from the
    /// EARLIEST campus — the sibling twenty days out cannot buy the imminent one more time.
    /// </summary>
    [Fact]
    public async Task Exactly_at_the_six_hour_mark_the_editor_still_opens()
    {
        using var db = DelegationsTestDbContext.Create();
        SeedRequest(db, VisitRequestStatuses.PendingApproval,
            (VisitInstanceStatuses.WaitingRequestApproval, Now.AddHours(VisitMutationPolicy.RequiredLeadHours)),
            (VisitInstanceStatuses.WaitingContactConfirmation, Now.AddDays(20)));

        var dto = await Run(db);

        Assert.Equal("EDIT", dto.Mode);
    }

    [Fact]
    public async Task One_second_inside_the_cutoff_is_refused_with_the_cutoff_code()
    {
        using var db = DelegationsTestDbContext.Create();
        SeedRequest(db, VisitRequestStatuses.PendingApproval,
            (VisitInstanceStatuses.WaitingRequestApproval,
                Now.AddHours(VisitMutationPolicy.RequiredLeadHours).AddSeconds(-1)));

        var ex = await Assert.ThrowsAsync<VisitMutationRefusedException>(() => Run(db));

        // The structured refusal, not a flat "không còn ở trạng thái có thể sửa": the client needs the
        // deadline and the start time to explain WHY, and it must never parse a sentence to find them.
        Assert.Equal(VisitMutationErrorCodes.CutoffReached, ex.ErrorCode);
        Assert.Equal(VisitMutationPolicy.RequiredLeadHours, ex.RequiredLeadHours);
        Assert.NotNull(ex.CutoffAt);
        Assert.NotNull(ex.PlannedStartAt);
    }

    /// <summary>
    /// The 24-hour rule this handler used to carry is gone, not merely renumbered: a request whose
    /// earliest campus is 10 hours out sits between the old cutoff and the new one, and the old code
    /// refused it.
    /// </summary>
    [Fact]
    public async Task Ten_hours_out_is_editable_which_the_old_twenty_four_hour_rule_refused()
    {
        using var db = DelegationsTestDbContext.Create();
        SeedRequest(db, VisitRequestStatuses.PendingApproval,
            (VisitInstanceStatuses.WaitingRequestApproval, Now.AddHours(10)));

        var dto = await Run(db);

        Assert.Equal("EDIT", dto.Mode);
    }

    // ── §9.5 Topology: a decided campus closes the whole-request door ────────

    [Fact]
    public async Task A_request_with_one_campus_already_decided_cannot_be_edited_as_a_whole()
    {
        using var db = DelegationsTestDbContext.Create();
        SeedRequest(db, VisitRequestStatuses.PartiallyApproved,
            (VisitInstanceStatuses.Assigned, Now.AddDays(10)),
            (VisitInstanceStatuses.WaitingRequestApproval, Now.AddDays(12)));

        var ex = await Assert.ThrowsAsync<VisitMutationRefusedException>(() => Run(db));

        Assert.Equal(VisitRequestErrorCodes.VisitRequestNotEditable, ex.ErrorCode);
        // And it points at the door that IS open, rather than only closing this one.
        Assert.Contains("từng cơ sở", ex.Message);
    }

    [Fact]
    public async Task A_cancelled_request_is_refused()
    {
        using var db = DelegationsTestDbContext.Create();
        SeedRequest(db, VisitRequestStatuses.Cancelled,
            (VisitInstanceStatuses.Cancelled, Now.AddDays(10)));

        // A lifecycle refusal reuses the caller's domain code, so clients already matching on
        // VISIT_REQUEST_NOT_EDITABLE keep working; only a CUTOFF refusal switches to its own code.
        var ex = await Assert.ThrowsAsync<VisitMutationRefusedException>(() => Run(db));
        Assert.Equal(VisitRequestErrorCodes.VisitRequestNotEditable, ex.ErrorCode);
        Assert.Contains("đã bị hủy", ex.Message);
    }

    // ── §9.6 Resubmit, unchanged ─────────────────────────────────────────────

    [Fact]
    public async Task A_fully_rejected_request_is_resubmittable()
    {
        using var db = DelegationsTestDbContext.Create();
        SeedRequest(db, VisitRequestStatuses.Rejected,
            (VisitInstanceStatuses.Rejected, Now.AddDays(10)),
            (VisitInstanceStatuses.Rejected, Now.AddDays(11)));

        var dto = await Run(db);

        Assert.Equal("RESUBMIT", dto.Mode);
        Assert.True(dto.IsResubmittable);
        Assert.False(dto.IsEditablePending);
    }

    /// <summary>
    /// Resubmit is the one action NOT measured against the existing schedule — a rejected request
    /// normally sits until long after its original date, and testing that date would refuse every
    /// resubmit that matters. The new date answers to the registration floor instead, in the edit
    /// service that receives it.
    /// </summary>
    [Fact]
    public async Task A_rejected_request_whose_original_date_has_passed_is_still_resubmittable()
    {
        using var db = DelegationsTestDbContext.Create();
        SeedRequest(db, VisitRequestStatuses.Rejected,
            (VisitInstanceStatuses.Rejected, Now.AddDays(-30)));

        var dto = await Run(db);

        Assert.Equal("RESUBMIT", dto.Mode);
    }

    [Fact]
    public async Task A_partially_rejected_request_is_neither_editable_nor_resubmittable()
    {
        using var db = DelegationsTestDbContext.Create();
        SeedRequest(db, VisitRequestStatuses.Rejected,
            (VisitInstanceStatuses.Rejected, Now.AddDays(10)),
            (VisitInstanceStatuses.Assigned, Now.AddDays(12)));

        var ex = await Assert.ThrowsAsync<VisitMutationRefusedException>(() => Run(db));
        Assert.Equal(VisitRequestErrorCodes.VisitRequestNotEditable, ex.ErrorCode);
    }

    // ── Relation is still the editor's first gate ────────────────────────────

    [Fact]
    public async Task Somebody_who_is_not_the_registrant_is_forbidden()
    {
        using var db = DelegationsTestDbContext.Create();
        SeedRequest(db, VisitRequestStatuses.PendingApproval,
            (VisitInstanceStatuses.WaitingRequestApproval, Now.AddDays(10)));

        var handler = new GetEditableVisitRequestDetailQueryHandler(
            db,
            new FakeDelegationsCurrentUser { UserId = RegistrantId + 1, RoleCode = RoleCodes.Visitor },
            new FakeDateTimeService { UtcNow = Now.AddHours(-7) },
            new StubFormReadService());

        await Assert.ThrowsAsync<ForbiddenException>(
            () => handler.Handle(new GetEditableVisitRequestDetailQuery(RequestId), CancellationToken.None));
    }

    // ── §10 Parity: list, editor and command answer the same question ────────

    /// <summary>
    /// The state from the report, asked three ways: the way the LIST decides whether to offer the
    /// button (<see cref="VisitMutationPolicy.Evaluate"/> on the governing campus), the way the COMMAND
    /// decides whether to accept the payload (<see cref="VisitMutationGuard.EnsureRequestLevelAllowed"/>,
    /// the identical call <c>UpdatePendingVisitRequestV2CommandHandler</c> makes), and the way the
    /// EDITOR decides whether to render. Three yeses, or the bug is back in a different layer.
    /// </summary>
    [Fact]
    public async Task List_editor_and_command_reach_the_same_verdict_for_the_same_state()
    {
        using var db = DelegationsTestDbContext.Create();
        SeedRequest(db, VisitRequestStatuses.PendingContactConfirmation,
            (VisitInstanceStatuses.WaitingContactConfirmation, Now.AddDays(10)),
            (VisitInstanceStatuses.WaitingRequestApproval, Now.AddDays(12)));

        var visit = await db.VisitRequests
            .Include(v => v.CampusInstances)
            .FirstAsync(v => v.VisitRequestId == RequestId);

        // 1. What the list asks, for every campus it would take the deadline from.
        foreach (var instance in visit.CampusInstances)
        {
            var decision = VisitMutationPolicy.Evaluate(new VisitMutationContext(
                VisitMutationAction.EditPendingRequest, visit.Status, instance.Status,
                instance.PlannedStartAt, Now, VisitViewerRelations.Requester));
            Assert.True(decision.Allowed, $"list would hide the action on {instance.Status}");
        }

        // 2. What the command asks — verbatim.
        VisitMutationGuard.EnsureRequestLevelAllowed(
            VisitMutationAction.EditPendingRequest, visit, Now,
            c => c.Status is VisitInstanceStatuses.WaitingContactConfirmation
                          or VisitInstanceStatuses.WaitingRequestApproval,
            VisitRequestErrorCodes.VisitRequestNotEditable);

        // 3. What the editor answers.
        var dto = await Run(db);
        Assert.True(dto.IsEditablePending);
    }

    /// <summary>
    /// Six hours and seventy-two hours answer different questions, and this is the case that proves the
    /// editor is not quietly using the second one: a campus 10 hours away is well inside the
    /// registration floor, yet the editor opens — while the schedule the user might type INTO it is
    /// still measured against that floor, and a start 20 hours out is refused there.
    /// </summary>
    [Fact]
    public async Task The_editor_opens_on_the_six_hour_cutoff_while_a_new_schedule_answers_to_the_registration_floor()
    {
        using var db = DelegationsTestDbContext.Create();
        SeedRequest(db, VisitRequestStatuses.PendingApproval,
            (VisitInstanceStatuses.WaitingRequestApproval, Now.AddHours(10)));

        var dto = await Run(db);
        Assert.Equal("EDIT", dto.Mode);

        // The SAME clock, asked about a proposed start rather than the existing one.
        var tooSoon = VisitMutationPolicy.EvaluateScheduleLeadTime(
            Now.AddHours(20), Now, actorMayOverride: false, overrideConfirmed: false);
        Assert.False(tooSoon.Allowed);

        var farEnough = VisitMutationPolicy.EvaluateScheduleLeadTime(
            Now.AddHours(80), Now, actorMayOverride: false, overrideConfirmed: false);
        Assert.True(farEnough.Allowed);
    }
}
