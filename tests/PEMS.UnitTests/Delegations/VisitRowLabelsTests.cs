using PEMS.Application.Delegations.Services;
using PEMS.Domain.Constants;
using PEMS.Shared;
using Xunit;

namespace PEMS.UnitTests.Delegations;

/// <summary>
/// The row's first two layers — where the request IS, and what the reader is TO it.
///
/// They are asserted here rather than trusted because they are the labels a Staff Leader sorts a
/// morning's work by, and because the previous single badge conflated them: "Chờ xử lý tại cơ sở"
/// was simultaneously a status, an instruction to one person and noise to everyone else.
/// </summary>
public class VisitRowLabelsTests
{
    [Theory]
    [InlineData(VisitInstanceStatus.WaitingContactConfirmation, "Chờ xác nhận")]
    [InlineData(VisitInstanceStatus.WaitingRequestApproval, "Chờ duyệt")]
    // ASSIGNED and BEFORE_VISIT are different steps and must read differently: the first says a
    // person has the campus, the second says they have started on it.
    [InlineData(VisitInstanceStatus.Assigned, "Đã duyệt")]
    [InlineData(VisitInstanceStatus.BeforeVisit, "Đang chuẩn bị")]
    [InlineData(VisitInstanceStatus.DuringVisit, "Đang diễn ra")]
    [InlineData(VisitInstanceStatus.AfterVisit, "Chờ đóng")]
    [InlineData(VisitInstanceStatus.Closed, "Đã hoàn tất")]
    [InlineData(VisitInstanceStatus.Cancelled, "Đã hủy")]
    [InlineData(VisitInstanceStatus.Rejected, "Từ chối")]
    public void Campus_status_wins_over_the_request_aggregate(string campusStatus, string expected)
    {
        // The aggregate deliberately disagrees: a PARTIALLY_APPROVED request says nothing useful about
        // the campus the reader is actually looking at, so the instance has to win.
        // No roleCode → the internal (Staff/Staff Leader) vocabulary, the shared default.
        Assert.Equal(expected, VisitRowLabels.Status(VisitRequestStatuses.PartiallyApproved, campusStatus));
        // The canonical CODE is the campus status itself, always — a filter on EffectiveCode must
        // therefore be a straight passthrough for every campus-driven row (P0-01's fix hinges on this).
        Assert.Equal(campusStatus, VisitRowLabels.EffectiveCode(VisitRequestStatuses.PartiallyApproved, campusStatus));
    }

    [Theory]
    [InlineData(VisitInstanceStatus.AfterVisit)]
    [InlineData(VisitInstanceStatus.Rejected)]
    [InlineData(VisitInstanceStatus.WaitingRequestApproval)]
    [InlineData(VisitInstanceStatus.Assigned)]
    [InlineData(VisitInstanceStatus.Closed)]
    public void EffectiveCode_never_changes_with_role_when_a_campus_status_is_present(string campusStatus)
    {
        // Role changes WORDING for AFTER_VISIT/REJECTED (see Visitor_reads_after_visit_and_rejected_differently
        // below) but never the underlying CODE — a filter for a given campus stage must catch the row
        // for every role that can see it, regardless of how that role's badge happens to read.
        var expected = VisitRowLabels.EffectiveCode(VisitRequestStatuses.Approved, campusStatus);
        Assert.Equal(expected, VisitRowLabels.EffectiveCode(VisitRequestStatuses.Approved, campusStatus, RoleCodes.Visitor));
        Assert.Equal(expected, VisitRowLabels.EffectiveCode(VisitRequestStatuses.Approved, campusStatus, RoleCodes.Ho));
        Assert.Equal(campusStatus, expected);
    }

    [Theory]
    [InlineData(VisitRequestStatuses.PendingContactConfirmation, "Chờ xác nhận", VisitInstanceStatus.WaitingContactConfirmation)]
    [InlineData(VisitRequestStatuses.PendingApproval, "Chờ duyệt", VisitInstanceStatus.WaitingRequestApproval)]
    [InlineData(VisitRequestStatuses.PartiallyApproved, "Duyệt một phần", EffectiveStatusCodes.PartiallyApproved)]
    [InlineData(VisitRequestStatuses.Approved, "Đã duyệt", EffectiveStatusCodes.ApprovedNoLiveCampus)]
    [InlineData(VisitRequestStatuses.Rejected, "Từ chối", VisitInstanceStatus.Rejected)]
    [InlineData(VisitRequestStatuses.Cancelled, "Đã hủy", VisitInstanceStatus.Cancelled)]
    public void Request_aggregate_is_used_when_the_row_is_not_one_campus(string requestStatus, string expectedLabel, string expectedCode)
    {
        Assert.Equal(expectedLabel, VisitRowLabels.Status(requestStatus, campusStatus: null));
        // The code half must agree with the label half for the SAME call — this is the P0-01 contract:
        // a filter on EffectiveCode and the badge from Status can never point at different rows.
        Assert.Equal(expectedCode, VisitRowLabels.EffectiveCode(requestStatus, campusStatus: null));
    }

    [Fact]
    public void An_unknown_status_falls_back_to_the_raw_code_rather_than_an_empty_cell()
    {
        Assert.Equal("SOMETHING_NEW", VisitRowLabels.Status("SOMETHING_NEW", campusStatus: null));
        Assert.Equal("SOMETHING_NEW", VisitRowLabels.EffectiveCode("SOMETHING_NEW", campusStatus: null));
    }

    [Theory]
    // Visitor reads AFTER_VISIT as "waiting on my own feedback", not "waiting on staff paperwork" —
    // the one campus-status word that reads differently for them.
    [InlineData(VisitInstanceStatus.AfterVisit, "Chờ đánh giá")]
    [InlineData(VisitInstanceStatus.Rejected, "Đã từ chối")]
    [InlineData(VisitInstanceStatus.WaitingRequestApproval, "Chờ duyệt")]
    [InlineData(VisitInstanceStatus.Assigned, "Đã duyệt")]
    public void Visitor_reads_after_visit_and_rejected_differently(string campusStatus, string expected)
    {
        Assert.Equal(expected, VisitRowLabels.Status(VisitRequestStatuses.Approved, campusStatus, RoleCodes.Visitor));
        // Wording differs, code does not — see EffectiveCode_never_changes_with_role_when_a_campus_status_is_present.
        Assert.Equal(campusStatus, VisitRowLabels.EffectiveCode(VisitRequestStatuses.Approved, campusStatus, RoleCodes.Visitor));
    }

    [Theory]
    // HO reads AFTER_VISIT the same as Staff ("Chờ đóng"), but Rejected the same as Visitor.
    [InlineData(VisitInstanceStatus.AfterVisit, "Chờ đóng")]
    [InlineData(VisitInstanceStatus.Rejected, "Đã từ chối")]
    public void Ho_reads_after_visit_like_staff_but_rejected_like_visitor(string campusStatus, string expected)
    {
        Assert.Equal(expected, VisitRowLabels.Status(VisitRequestStatuses.Approved, campusStatus, RoleCodes.Ho));
        Assert.Equal(campusStatus, VisitRowLabels.EffectiveCode(VisitRequestStatuses.Approved, campusStatus, RoleCodes.Ho));
    }

    [Fact]
    public void Ho_merges_partially_approved_into_the_same_pending_word_as_waiting_request_approval()
    {
        // HO's monitoring view: a multi-campus request with any campus still undecided reads the
        // same "Chờ duyệt" as a single campus that hasn't been touched yet — no separate word.
        Assert.Equal("Chờ duyệt", VisitRowLabels.Status(VisitRequestStatuses.PartiallyApproved, campusStatus: null, RoleCodes.Ho));
        // This is the exact P0-01 regression: HO's "Đã duyệt" filter must NOT match this row (it isn't
        // ASSIGNED/APPROVED), and HO's "Chờ duyệt" filter MUST — the code has to merge exactly like the label.
        Assert.Equal(VisitInstanceStatus.WaitingRequestApproval,
            VisitRowLabels.EffectiveCode(VisitRequestStatuses.PartiallyApproved, campusStatus: null, RoleCodes.Ho));
    }

    [Fact]
    public void Non_ho_reads_partially_approved_as_its_own_distinct_code_and_label()
    {
        // Every role EXCEPT HO keeps PARTIALLY_APPROVED as its own bucket ("Duyệt một phần") — must not
        // collide with WAITING_REQUEST_APPROVAL ("Chờ duyệt"), or a Staff Leader's "Chờ duyệt" filter
        // would wrongly start returning partially-approved multi-campus requests.
        Assert.Equal("Duyệt một phần", VisitRowLabels.Status(VisitRequestStatuses.PartiallyApproved, campusStatus: null));
        Assert.Equal(EffectiveStatusCodes.PartiallyApproved,
            VisitRowLabels.EffectiveCode(VisitRequestStatuses.PartiallyApproved, campusStatus: null));
        Assert.NotEqual(VisitInstanceStatus.WaitingRequestApproval, EffectiveStatusCodes.PartiallyApproved);
    }

    [Fact]
    public void Approved_aggregate_with_no_live_campus_gets_its_own_code_distinct_from_assigned()
    {
        // A multi-campus request stuck at aggregate APPROVED after every one of its campuses was later
        // individually cancelled/rejected (MultiCampusProgress finds nothing to rank) must NOT share
        // ASSIGNED's code — a filter meaning "campus has a live host, hasn't started prep" must not
        // silently pick up this zombie row just because both happen to render "Đã duyệt".
        Assert.Equal(EffectiveStatusCodes.ApprovedNoLiveCampus,
            VisitRowLabels.EffectiveCode(VisitRequestStatuses.Approved, campusStatus: null));
        Assert.NotEqual(VisitInstanceStatus.Assigned, EffectiveStatusCodes.ApprovedNoLiveCampus);

        // And MultiCampusProgress correctly returns null for this shape (every campus terminal), so the
        // handler's override never fires and the plain aggregate resolution above is what actually ships.
        Assert.Null(VisitRowLabels.MultiCampusProgress(new[] { VisitInstanceStatus.Cancelled, VisitInstanceStatus.Rejected }));
        Assert.Null(VisitRowLabels.MultiCampusProgressCode(new[] { VisitInstanceStatus.Cancelled, VisitInstanceStatus.Rejected }));
    }

    [Theory]
    [InlineData(new[] { VisitInstanceStatus.Assigned, VisitInstanceStatus.BeforeVisit }, VisitInstanceStatus.Assigned, "Đã duyệt")]
    [InlineData(new[] { VisitInstanceStatus.Cancelled, VisitInstanceStatus.BeforeVisit }, VisitInstanceStatus.BeforeVisit, "Đang chuẩn bị")]
    [InlineData(new[] { VisitInstanceStatus.Closed, VisitInstanceStatus.DuringVisit }, VisitInstanceStatus.DuringVisit, "Đang diễn ra")]
    public void MultiCampusProgressCode_matches_MultiCampusProgress_for_the_same_least_progressed_campus(
        string[] campusStatuses, string expectedCode, string expectedLabel)
    {
        Assert.Equal(expectedLabel, VisitRowLabels.MultiCampusProgress(campusStatuses));
        Assert.Equal(expectedCode, VisitRowLabels.MultiCampusProgressCode(campusStatuses));
    }

    [Theory]
    [InlineData("HOST", "Bạn phụ trách tiếp đón")]
    [InlineData("VISITOR_OWNER", "Bạn là đầu mối đoàn khách")]
    [InlineData("REGISTRANT_VIEWER", "Bạn là người đăng ký")]
    [InlineData("CAMPUS_APPROVER", "Bạn có quyền duyệt tại cơ sở")]
    [InlineData("IC_SUPPORT", "Bạn được mời tham dự")]
    [InlineData("DEPT_SUPPORT", "Bạn được mời tham dự")]
    [InlineData("STUDENT_SUPPORT", "Bạn được mời tham dự")]
    [InlineData("DEPARTMENT_TASK_OWNER", "Bạn được giao nhiệm vụ")]
    public void Relation_says_what_the_reader_is_to_the_row(string relation, string expected)
    {
        Assert.Equal(expected, VisitRowLabels.Relation(relation));
    }

    [Theory]
    [InlineData("HO_MONITOR")]
    [InlineData("NONE")]
    [InlineData("")]
    public void A_reader_with_no_stake_is_told_they_are_only_watching(string relation)
    {
        // Never the word "Host", and never blank: "Chỉ theo dõi" is the answer, not the absence of one.
        Assert.Equal("Chỉ theo dõi", VisitRowLabels.Relation(relation));
    }

    [Fact]
    public void No_label_uses_the_bare_technical_word_Host()
    {
        string[] relations =
        {
            "HOST", "TEMP_HOST", "VISITOR_OWNER", "REGISTRANT_VIEWER", "CAMPUS_APPROVER",
            "IC_SUPPORT", "DEPT_SUPPORT", "STUDENT_SUPPORT", "DEPARTMENT_TASK_OWNER", "NONE",
        };
        foreach (var relation in relations)
            Assert.DoesNotContain("Host", VisitRowLabels.Relation(relation));

        string[] campusStatuses =
        {
            VisitInstanceStatus.WaitingContactConfirmation, VisitInstanceStatus.WaitingRequestApproval,
            VisitInstanceStatus.Assigned,
            VisitInstanceStatus.BeforeVisit, VisitInstanceStatus.DuringVisit,
            VisitInstanceStatus.AfterVisit, VisitInstanceStatus.Closed,
            VisitInstanceStatus.Cancelled, VisitInstanceStatus.Rejected,
        };
        foreach (var status in campusStatuses)
            Assert.DoesNotContain("Host", VisitRowLabels.Status(VisitRequestStatuses.Approved, status));
    }
}
