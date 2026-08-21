using System.IO;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Delegations.Commands.OperationalContact;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;

namespace PEMS.UnitTests.Delegations.OperationalContact;

/// <summary>
/// Who may change a campus's operational contact, and WHEN — the whole matrix, decided by the campus's
/// persisted status and by nothing else.
///
/// <para>
/// The rule these guards now carry is: the contact may be corrected, replaced or handed over only while
/// the campus has not started. From <c>DURING_VISIT</c> onward the identity and the profile are frozen,
/// because from that moment the contact record is what the visit was actually received against rather
/// than a plan still being arranged.
/// </para>
/// <para>
/// The version this replaced answered "may a transfer start" with a CLOCK — a 24-hour lead time before
/// <c>PlannedStartAt</c> — which was wrong in both directions at once. It refused a perfectly ordinary
/// handover the evening before a visit that had not begun, and it allowed one on a campus that had
/// already been moved to DURING_VISIT more than a day early. The two facts it confused are the campus's
/// LIFECYCLE and the invitation's VALIDITY; the second still exists (a link lives 24 hours) and is
/// deliberately unrelated to the first.
/// </para>
/// <para>
/// Pinned without a database on purpose. These are pure decisions over a status string, and the whole
/// nine-status matrix — including the two dead ends a real MySQL row can only reach through several
/// triggers' worth of preconditions — is worth stating in one readable place. The end-to-end races
/// (a transfer accepted after the visit starts, a resend that must not extend one) are integration
/// tests, because those are about ordering and rollback rather than about the verdict.
/// </para>
/// </summary>
public class OperationalContactLifecycleWindowTests
{
    /// <summary>Every campus status, so the matrices below are exhaustive rather than illustrative.</summary>
    public static readonly string[] AllStatuses =
    {
        VisitInstanceStatuses.WaitingContactConfirmation,
        VisitInstanceStatuses.WaitingRequestApproval,
        VisitInstanceStatuses.Assigned,
        VisitInstanceStatuses.BeforeVisit,
        VisitInstanceStatuses.DuringVisit,
        VisitInstanceStatuses.AfterVisit,
        VisitInstanceStatuses.Closed,
        VisitInstanceStatuses.Cancelled,
        VisitInstanceStatuses.Rejected,
    };

    private static readonly DateTime VietnamNow = new(2026, 8, 14, 9, 0, 0);

    private static VisitRequest LiveRequest() => new()
    {
        VisitRequestId = 10,
        Status = VisitRequestStatuses.Approved,
    };

    /// <summary>
    /// A campus in the given status, starting <paramref name="startsInHours"/> from the reference
    /// instant. The start time is a parameter precisely because no verdict below may depend on it.
    /// </summary>
    private static VisitRequestCampus Campus(string status, double startsInHours = 24 * 20) => new()
    {
        VisitInstanceId = 100,
        VisitRequestId = 10,
        CampusId = 1,
        Status = status,
        PlannedStartAt = VietnamNow.AddHours(startsInHours),
        PlannedEndAt = VietnamNow.AddHours(startsInHours + 2),
    };

    // ── Profile update: the four pre-start statuses, and only those ──────────

    [Theory]
    [InlineData(VisitInstanceStatuses.WaitingContactConfirmation)]
    [InlineData(VisitInstanceStatuses.WaitingRequestApproval)]
    [InlineData(VisitInstanceStatuses.Assigned)]
    [InlineData(VisitInstanceStatuses.BeforeVisit)]
    public void A_contact_profile_may_be_corrected_until_the_visit_starts(string status)
        => OperationalContactGuards.EnsureProfileUpdateAllowed(LiveRequest(), Campus(status));

    [Theory]
    [InlineData(VisitInstanceStatuses.DuringVisit)]
    [InlineData(VisitInstanceStatuses.AfterVisit)]
    [InlineData(VisitInstanceStatuses.Closed)]
    [InlineData(VisitInstanceStatuses.Cancelled)]
    [InlineData(VisitInstanceStatuses.Rejected)]
    public void A_contact_profile_is_frozen_from_the_moment_the_visit_starts(string status)
    {
        var ex = Assert.Throws<ConflictException>(
            () => OperationalContactGuards.EnsureProfileUpdateAllowed(LiveRequest(), Campus(status)));

        Assert.Equal(OperationalContactErrorCodes.ChangeConflict, ex.ErrorCode);
    }

    /// <summary>
    /// The case the old implementation got wrong in the other direction: a campus that starts in six
    /// minutes but has NOT started still takes a corrected phone number. That is the moment a wrong
    /// number costs the most, and nothing about authority moves when one is fixed.
    /// </summary>
    [Theory]
    [InlineData(0.1)]
    [InlineData(-3)]   // running late: the clock is past the planned start, the STATUS has not moved
    public void A_profile_correction_is_never_refused_for_being_close_to_the_start(double startsInHours)
        => OperationalContactGuards.EnsureProfileUpdateAllowed(
            LiveRequest(), Campus(VisitInstanceStatuses.BeforeVisit, startsInHours));

    // ── Transfer: a confirmed holder exists, and the campus has not started ──

    /// <summary>
    /// WAITING_REQUEST_APPROVAL belongs here, not with the "nothing to hand over yet" refusals below:
    /// a campus never reaches that status without a confirmed <c>operational_contact_user_id</c> (the
    /// database enforces it), so a real holder is always there to hand something over, whether or not a
    /// Staff Leader has decided the campus yet.
    /// </summary>
    [Theory]
    [InlineData(VisitInstanceStatuses.WaitingRequestApproval)]
    [InlineData(VisitInstanceStatuses.Assigned)]
    [InlineData(VisitInstanceStatuses.BeforeVisit)]
    public void A_handover_may_be_proposed_once_a_confirmed_holder_exists_and_the_campus_has_not_started(string status)
        => OperationalContactGuards.EnsureTransferWindowOpen(LiveRequest(), Campus(status));

    [Theory]
    [InlineData(VisitInstanceStatuses.DuringVisit)]
    [InlineData(VisitInstanceStatuses.AfterVisit)]
    [InlineData(VisitInstanceStatuses.Closed)]
    [InlineData(VisitInstanceStatuses.Cancelled)]
    [InlineData(VisitInstanceStatuses.Rejected)]
    public void A_handover_is_refused_once_the_campus_has_started_or_ended(string status)
    {
        var ex = Assert.Throws<ConflictException>(
            () => OperationalContactGuards.EnsureTransferWindowOpen(LiveRequest(), Campus(status)));

        Assert.Equal(OperationalContactErrorCodes.ChangeConflict, ex.ErrorCode);
    }

    /// <summary>
    /// WAITING_CONTACT_CONFIRMATION is the one status left where a transfer is refused for the opposite
    /// reason from the Theory above — there is nothing to hand over yet, so the message routes the
    /// caller to replacing the contact outright rather than implying the window has closed.
    /// </summary>
    [Fact]
    public void A_campus_with_no_confirmed_holder_is_sent_to_replace_rather_than_to_handover()
    {
        var ex = Assert.Throws<ConflictException>(
            () => OperationalContactGuards.EnsureTransferWindowOpen(
                LiveRequest(), Campus(VisitInstanceStatuses.WaitingContactConfirmation)));

        Assert.Equal(OperationalContactErrorCodes.ChangeConflict, ex.ErrorCode);
        Assert.Contains("chưa có đầu mối vận hành", ex.Message);
    }

    // ── The point of the change: lifecycle, not clock ────────────────────────

    /// <summary>
    /// One minute before the visit begins, with the campus still BEFORE_VISIT: allowed. Under the old
    /// 24-hour lead time this was the flagship refusal — a registrant whose contact fell ill the night
    /// before could not name a successor and was told to phone FPTU.
    /// </summary>
    [Theory]
    [InlineData(1.0 / 60)]
    [InlineData(6)]
    [InlineData(23.5)]
    public void A_handover_a_minute_before_the_start_is_allowed_while_the_campus_has_not_started(
        double startsInHours)
        => OperationalContactGuards.EnsureTransferWindowOpen(
            LiveRequest(), Campus(VisitInstanceStatuses.BeforeVisit, startsInHours));

    /// <summary>
    /// And the mirror image: a campus whose planned start is a fortnight away is STILL refused if the
    /// workflow has moved it to DURING_VISIT. The persisted status is the authority; the schedule is
    /// only what the campus is scheduled to do.
    /// </summary>
    [Theory]
    [InlineData(24 * 14)]
    [InlineData(25)]
    public void A_handover_is_refused_on_a_started_campus_however_far_off_its_planned_start_is(
        double startsInHours)
    {
        var ex = Assert.Throws<ConflictException>(
            () => OperationalContactGuards.EnsureTransferWindowOpen(
                LiveRequest(), Campus(VisitInstanceStatuses.DuringVisit, startsInHours)));

        Assert.Equal(OperationalContactErrorCodes.ChangeConflict, ex.ErrorCode);
    }

    // ── Replace stays pre-decision only ─────────────────────────────────────

    [Theory]
    [InlineData(VisitInstanceStatuses.WaitingContactConfirmation)]
    [InlineData(VisitInstanceStatuses.WaitingRequestApproval)]
    public void A_contact_may_be_replaced_outright_only_before_the_campus_is_decided(string status)
        => OperationalContactGuards.EnsureReplaceWindowOpen(LiveRequest(), Campus(status));

    /// <summary>
    /// A decided campus is transfer territory, and the refusal says so — a Staff Leader has committed
    /// resources against the person named, so the successor has to accept in their own name.
    /// </summary>
    [Theory]
    [InlineData(VisitInstanceStatuses.Assigned)]
    [InlineData(VisitInstanceStatuses.BeforeVisit)]
    public void A_decided_campus_refuses_a_replace_and_names_the_handover(string status)
    {
        var ex = Assert.Throws<ConflictException>(
            () => OperationalContactGuards.EnsureReplaceWindowOpen(LiveRequest(), Campus(status)));

        Assert.Equal(OperationalContactErrorCodes.ChangeConflict, ex.ErrorCode);
        Assert.Contains("chuyển giao", ex.Message);
    }

    [Theory]
    [InlineData(VisitInstanceStatuses.DuringVisit)]
    [InlineData(VisitInstanceStatuses.AfterVisit)]
    [InlineData(VisitInstanceStatuses.Closed)]
    [InlineData(VisitInstanceStatuses.Cancelled)]
    [InlineData(VisitInstanceStatuses.Rejected)]
    public void A_started_or_dead_campus_refuses_a_replace(string status)
    {
        var ex = Assert.Throws<ConflictException>(
            () => OperationalContactGuards.EnsureReplaceWindowOpen(LiveRequest(), Campus(status)));

        Assert.Equal(OperationalContactErrorCodes.ChangeConflict, ex.ErrorCode);
    }

    // ── A cancelled REQUEST closes every door, whatever the campus says ──────

    /// <summary>
    /// Including the four statuses that would otherwise be open: a live campus under a dead request is
    /// still a dead campus, and the refusal names the request rather than the campus so the reader is
    /// not sent looking for a lifecycle problem the campus does not have.
    /// </summary>
    [Theory]
    [MemberData(nameof(EveryStatus))]
    public void A_cancelled_request_refuses_every_contact_change(string status)
    {
        var cancelled = new VisitRequest { VisitRequestId = 10, Status = VisitRequestStatuses.Cancelled };

        foreach (var guard in new Action[]
                 {
                     () => OperationalContactGuards.EnsureProfileUpdateAllowed(cancelled, Campus(status)),
                     () => OperationalContactGuards.EnsureTransferWindowOpen(cancelled, Campus(status)),
                     () => OperationalContactGuards.EnsureReplaceWindowOpen(cancelled, Campus(status)),
                 })
        {
            var ex = Assert.Throws<BusinessRuleException>(guard);
            Assert.Equal(OperationalContactErrorCodes.ChangeConflict, ex.ErrorCode);
            Assert.Contains("Đơn đã bị hủy", ex.Message);
        }
    }

    public static TheoryData<string> EveryStatus()
    {
        var data = new TheoryData<string>();
        foreach (var status in AllStatuses) data.Add(status);
        return data;
    }

    // ── The invitation's 24 hours is a different rule, and it stays ──────────

    /// <summary>
    /// <c>TransferValidityHours</c> says how long an emailed link remains answerable. It is NOT
    /// permission to apply the handover: the guards above are re-run at accept and at resend, so a link
    /// well inside its day is still refused on a campus that has started. Removing the lead time must
    /// not take this with it — a transfer invitation that never expired would be a live handover
    /// sitting in somebody's inbox indefinitely.
    /// </summary>
    [Fact]
    public void The_transfer_invitation_still_expires_after_a_day()
    {
        Assert.Equal(24, OperationalContactGuards.TransferValidityHours);
        Assert.Equal(72, OperationalContactGuards.InitialConfirmationValidityHours);

        Assert.Equal(
            VietnamNow.AddHours(24),
            OperationalContactGuards.ExpiryFor(IdentityChangeKinds.Transfer, VietnamNow));
        Assert.Equal(
            VietnamNow.AddHours(72),
            OperationalContactGuards.ExpiryFor(IdentityChangeKinds.InitialConfirmation, VietnamNow));
    }

    // ── The absence a behavioural test cannot show ───────────────────────────

    /// <summary>
    /// The guards must not read <c>PlannedStartAt</c> at all. This is an ABSENCE: a test that calls them
    /// can only show that today's inputs produce today's verdicts, and re-introducing a cutoff would
    /// pass every matrix above for campuses far from their start. Asserted on the source, the way this
    /// project already pins "no wall clock in the public answer path".
    /// </summary>
    [Fact]
    public void The_contact_guards_decide_from_status_and_never_from_the_schedule()
    {
        var code = CodeOf(ReadSource("OperationalContactGuards.cs"));

        Assert.DoesNotContain("PlannedStartAt", code);
        // The lead-time constant itself is gone; nothing may re-declare it under the old name either.
        Assert.DoesNotContain("TransferLeadHours", code);
    }

    /// <summary>
    /// Cancel and decline SETTLE a pending invitation without moving the contact, so they are cleanup
    /// rather than mutation and must stay reachable after the campus starts. Reusing the transfer guard
    /// in either of them would strand a campus with a stale handover nobody can close.
    /// </summary>
    [Theory]
    [InlineData("ManageOperationalContactHandlers.cs")]              // cancel lives here
    [InlineData("DeclineOperationalContactConfirmationCommandHandler.cs")]
    public void The_cleanup_paths_do_not_borrow_the_transfer_window_guard(string fileName)
        => Assert.DoesNotContain("EnsureTransferWindowOpen", CodeOf(ReadSource(fileName)));

    /// <summary>
    /// And the three paths that DO move a contact all ask. Accept and resend are the easy ones to
    /// forget: both act on an invitation that was legal when it was written, days after it was written.
    /// </summary>
    [Theory]
    [InlineData("InitiateOperationalContactTransferCommandHandler.cs")]
    [InlineData("AcceptOperationalContactConfirmationCommandHandler.cs")]
    [InlineData("ResendOperationalContactConfirmationCommandHandler.cs")]
    public void Every_path_that_moves_a_contact_re_asks_the_lifecycle_question(string fileName)
        => Assert.Contains(
            "OperationalContactGuards.EnsureTransferWindowOpen", CodeOf(ReadSource(fileName)));

    /// <summary>Source minus comments — a comment may legitimately name what the code must not do.</summary>
    private static string CodeOf(string source)
        => string.Join('\n', source
            .Split('\n')
            .Where(l => !l.TrimStart().StartsWith("//")));

    private static string ReadSource(string fileName)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "backend")))
            dir = dir.Parent;

        Assert.NotNull(dir);
        var path = Path.Combine(
            dir!.FullName, "backend", "PEMS.Application", "Delegations", "Commands",
            "OperationalContact", fileName);

        Assert.True(File.Exists(path), $"Expected the contact command at {path}.");
        return File.ReadAllText(path);
    }
}
