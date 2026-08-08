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
    }

    [Theory]
    [InlineData(VisitRequestStatuses.PendingContactConfirmation, "Chờ xác nhận")]
    [InlineData(VisitRequestStatuses.PendingApproval, "Chờ duyệt")]
    [InlineData(VisitRequestStatuses.PartiallyApproved, "Duyệt một phần")]
    [InlineData(VisitRequestStatuses.Approved, "Đã duyệt")]
    [InlineData(VisitRequestStatuses.Rejected, "Từ chối")]
    [InlineData(VisitRequestStatuses.Cancelled, "Đã hủy")]
    public void Request_aggregate_is_used_when_the_row_is_not_one_campus(string requestStatus, string expected)
    {
        Assert.Equal(expected, VisitRowLabels.Status(requestStatus, campusStatus: null));
    }

    [Fact]
    public void An_unknown_status_falls_back_to_the_raw_code_rather_than_an_empty_cell()
    {
        Assert.Equal("SOMETHING_NEW", VisitRowLabels.Status("SOMETHING_NEW", campusStatus: null));
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
    }

    [Theory]
    // HO reads AFTER_VISIT the same as Staff ("Chờ đóng"), but Rejected the same as Visitor.
    [InlineData(VisitInstanceStatus.AfterVisit, "Chờ đóng")]
    [InlineData(VisitInstanceStatus.Rejected, "Đã từ chối")]
    public void Ho_reads_after_visit_like_staff_but_rejected_like_visitor(string campusStatus, string expected)
    {
        Assert.Equal(expected, VisitRowLabels.Status(VisitRequestStatuses.Approved, campusStatus, RoleCodes.Ho));
    }

    [Fact]
    public void Ho_merges_partially_approved_into_the_same_pending_word_as_waiting_request_approval()
    {
        // HO's monitoring view: a multi-campus request with any campus still undecided reads the
        // same "Chờ duyệt" as a single campus that hasn't been touched yet — no separate word.
        Assert.Equal("Chờ duyệt", VisitRowLabels.Status(VisitRequestStatuses.PartiallyApproved, campusStatus: null, RoleCodes.Ho));
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
