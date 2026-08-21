using System.Linq;
using PEMS.Api.Email;
using PEMS.Application.EmailActions;
using PEMS.Domain.Constants;
using Xunit;

namespace PEMS.UnitTests.EmailActions;

/// <summary>
/// Semantic coverage for the 4 live public email-action contexts (spec §10.3): direct participant
/// invitation, Department-Staff delegated assignment, logistics request response, and logistics assignee
/// response each render distinct, context-appropriate wording — not the collapsed
/// <c>isLogisticsRequest</c> boolean that used to fold assignment/assignee into invitation copy.
/// </summary>
public sealed class EmailActionHtmlPagesTests
{
    private static EmailActionInfoResult ValidInfo(string context, string action = "ACCEPT") => new()
    {
        Status = EmailActionViewStatuses.Valid,
        Action = action,
        Context = context,
        RecipientName = "Nguyễn Văn A",
        DelegationName = "Đoàn kiểm thử",
        CampusName = "FPT Hà Nội",
        PlannedTimeText = "09:00 01/09/2026",
        ParticipantRoleLabel = "Hỗ trợ IC",
    };

    [Theory]
    [InlineData(EmailActionContexts.ParticipationResponse, "chấp nhận", "lời mời tham gia")]
    [InlineData(EmailActionContexts.ParticipationAssignmentResponse, "xác nhận nhận", "nhiệm vụ tham gia")]
    [InlineData(EmailActionContexts.LogisticsRequestResponse, "tiếp nhận", "yêu cầu hậu cần")]
    [InlineData(EmailActionContexts.LogisticsAssigneeResponse, "xác nhận nhận", "nhiệm vụ hậu cần")]
    public void Accept_landing_names_the_right_action_and_business_object_per_context(
        string context, string expectedVerb, string expectedObject)
    {
        var html = EmailActionHtmlPages.RenderLanding(ValidInfo(context, "ACCEPT"));

        Assert.Contains(expectedVerb, html);
        Assert.Contains(expectedObject, html);
        Assert.Contains("<form method=\"post\"", html);
        Assert.DoesNotContain("declineReason", html); // ACCEPT is one-click, no reason field
    }

    [Theory]
    [InlineData(EmailActionContexts.ParticipationResponse)]
    [InlineData(EmailActionContexts.ParticipationAssignmentResponse)]
    [InlineData(EmailActionContexts.LogisticsRequestResponse)]
    [InlineData(EmailActionContexts.LogisticsAssigneeResponse)]
    public void Decline_landing_always_renders_the_mandatory_reason_form(string context)
    {
        var html = EmailActionHtmlPages.RenderLanding(ValidInfo(context, "DECLINE"));

        Assert.Contains("name=\"declineReason\"", html);
        Assert.Contains("required", html);
        Assert.Contains("minlength=\"5\"", html);
        Assert.Contains("maxlength=\"1000\"", html);
    }

    [Fact]
    public void The_4_contexts_render_4_distinct_accept_titles_not_one_collapsed_boolean()
    {
        var titles = new[]
        {
            EmailActionContexts.ParticipationResponse,
            EmailActionContexts.ParticipationAssignmentResponse,
            EmailActionContexts.LogisticsRequestResponse,
            EmailActionContexts.LogisticsAssigneeResponse,
        }.Select(c => ExtractH2(EmailActionHtmlPages.RenderLanding(ValidInfo(c, "ACCEPT")))).ToArray();

        Assert.Equal(titles.Length, titles.Distinct().Count());
    }

    [Fact]
    public void Participation_assignment_response_is_not_rendered_as_a_direct_invitation()
    {
        // Regression for the pre-fix behavior: everything except LogisticsRequestResponse fell back to
        // direct-invitation copy ("lời mời tham gia"), which is wrong for a delegated Staff assignment
        // (there was never an "invitation" — it's an assignment the Staff must accept/decline).
        var html = EmailActionHtmlPages.RenderLanding(
            ValidInfo(EmailActionContexts.ParticipationAssignmentResponse, "ACCEPT"));

        Assert.DoesNotContain("lời mời tham gia", html);
        Assert.Contains("nhiệm vụ", html);
    }

    [Theory]
    [InlineData(EmailActionViewStatuses.AlreadyResponded)]
    [InlineData(EmailActionViewStatuses.Expired)]
    [InlineData(EmailActionViewStatuses.Invalid)]
    public void A_terminal_landing_status_never_shows_the_response_form(string terminalStatus)
    {
        var info = ValidInfo(EmailActionContexts.LogisticsAssigneeResponse, "ACCEPT");
        info.Status = terminalStatus;

        var html = EmailActionHtmlPages.RenderLanding(info);

        Assert.DoesNotContain("<form method=\"post\"", html);
    }

    [Theory]
    [InlineData(EmailActionContexts.ParticipationAssignmentResponse, "nhiệm vụ được phân công này")]
    [InlineData(EmailActionContexts.LogisticsRequestResponse, "yêu cầu logistics này")]
    [InlineData(EmailActionContexts.LogisticsAssigneeResponse, "nhiệm vụ hậu cần này")]
    public void AlreadyResponded_landing_names_the_right_object_per_context(string context, string expectedFragment)
    {
        var info = ValidInfo(context);
        info.Status = EmailActionViewStatuses.AlreadyResponded;
        info.CurrentResponse = "ACCEPTED";

        // AlreadyRespondedBody is HTML-encoded by TerminalBody (unlike AcceptIntro/DeclineIntro, which
        // embed a deliberate <strong> tag and so are interpolated raw) — decode before comparing.
        var html = System.Net.WebUtility.HtmlDecode(EmailActionHtmlPages.RenderLanding(info));

        Assert.Contains(expectedFragment, html);
        Assert.Contains("(Chấp nhận)", html);
    }

    [Fact]
    public void Result_page_after_a_successful_decline_uses_the_reject_accent_and_names_the_delegation()
    {
        var result = new EmailActionExecuteResult
        {
            Status = EmailActionViewStatuses.Success,
            Action = "DECLINE",
            Context = EmailActionContexts.LogisticsAssigneeResponse,
            DelegationName = "Đoàn kiểm thử",
            Message = "Bạn đã từ chối yêu cầu hậu cần.",
        };

        var rawHtml = EmailActionHtmlPages.RenderResult(result);
        var html = System.Net.WebUtility.HtmlDecode(rawHtml);

        Assert.Contains("#ef4444", rawHtml); // reject accent, not the accept green
        Assert.Contains("Đoàn kiểm thử", html);
        Assert.Contains("Đã ghi nhận từ chối", html);
    }

    [Fact]
    public void Result_page_on_reason_required_re_renders_the_form_with_the_submitted_text_and_error()
    {
        var result = new EmailActionExecuteResult
        {
            Status = EmailActionViewStatuses.ReasonRequired,
            Action = "DECLINE",
            Context = EmailActionContexts.LogisticsRequestResponse,
            Message = "Lý do từ chối phải có ít nhất 5 ký tự.",
            SubmittedReason = "ok",
        };

        var rawHtml = EmailActionHtmlPages.RenderResult(result);
        var html = System.Net.WebUtility.HtmlDecode(rawHtml);

        Assert.Contains("name=\"declineReason\"", rawHtml);
        Assert.Contains(">ok<", rawHtml); // the too-short submission is echoed back, not lost
        Assert.Contains("Lý do từ chối phải có ít nhất 5 ký tự.", html);
    }

    [Fact]
    public void Html_encodes_a_hostile_delegation_name_instead_of_injecting_it_raw()
    {
        var info = ValidInfo(EmailActionContexts.LogisticsAssigneeResponse, "ACCEPT");
        info.DelegationName = "<script>alert(1)</script>";

        var html = EmailActionHtmlPages.RenderLanding(info);

        Assert.DoesNotContain("<script>alert(1)</script>", html);
        Assert.Contains("&lt;script&gt;", html);
    }

    private static string ExtractH2(string html)
    {
        var start = html.IndexOf("<h2", System.StringComparison.Ordinal);
        var innerStart = html.IndexOf('>', start) + 1;
        var end = html.IndexOf("</h2>", innerStart, System.StringComparison.Ordinal);
        return html[innerStart..end];
    }
}
