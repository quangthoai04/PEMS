using System;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Security;
using PEMS.Application.Emails.Common;
using PEMS.Domain.Enums;

namespace PEMS.UnitTests.Emails;

/// <summary>
/// What a person is allowed to put in an email they wrote themselves.
///
/// <para>
/// Manual mail is the one family that legitimately does not come from <c>email_templates</c>, so these
/// rules are all that stand between a compose box and a message header. The two prohibitions worth
/// naming: the sender may not forge the action-block markers (they are the boundary the history strip
/// removes one-time links on), and may not write a newline into a subject (which would open a second
/// header).
/// </para>
/// <para>
/// The sanitiser here is a stand-in — the real one is exercised against genuine markup in the integration
/// suite. Nothing below depends on how it cleans HTML, only on the rules applied around it.
/// </para>
/// </summary>
public class ManualEmailContentTests
{
    /// <summary>Passes content through unchanged, so a test failure can only be about the rules.</summary>
    private sealed class PassThroughSanitizer : IHtmlSanitizerService
    {
        public string Sanitize(string? html) => html ?? string.Empty;
        public string SanitizeEmailHtml(string? html) => html ?? string.Empty;
    }

    /// <summary>Models the real sanitiser's habit of reducing markup-only input to nothing.</summary>
    private sealed class StripEverythingSanitizer : IHtmlSanitizerService
    {
        public string Sanitize(string? html) => string.Empty;
        public string SanitizeEmailHtml(string? html) => string.Empty;
    }

    private static readonly PassThroughSanitizer Sanitizer = new();

    private static ManualEmailContent.Result Validate(
        string? subject, string? body, EmailBodyFormat format = EmailBodyFormat.HTML)
        => ManualEmailContent.Validate(subject, body, format, Sanitizer);

    private static string CodeOf(Action act)
        => Assert.Throws<ValidationException>(act).ErrorCode ?? string.Empty;

    // ── Happy path ───────────────────────────────────────────────────────────

    [Fact]
    public void Accepts_html_content_and_trims_the_subject()
    {
        var result = Validate("  Mời họp chuẩn bị đoàn  ", "<p>Kính gửi anh Bình,</p>");

        Assert.Equal("Mời họp chuẩn bị đoàn", result.Subject);
        Assert.Equal("<p>Kính gửi anh Bình,</p>", result.Body);
        Assert.True(result.IsHtml);
    }

    [Fact]
    public void Keeps_plain_text_verbatim_instead_of_running_it_through_an_html_sanitiser()
    {
        // "<" is ordinary punctuation in plain text. Sanitising it away would silently rewrite what the
        // sender typed — the message would arrive saying something they did not write.
        var result = Validate("Ngưỡng chi phí", "Chi phí < 5.000.000 đ", EmailBodyFormat.PLAIN_TEXT);

        Assert.Equal("Chi phí < 5.000.000 đ", result.Body);
        Assert.False(result.IsHtml);
    }

    // ── Subject ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_a_missing_subject(string? subject)
        => Assert.Equal(
            EmailErrorCodes.AuthoredSubjectRequired,
            CodeOf(() => Validate(subject, "<p>nội dung</p>")));

    [Fact]
    public void Rejects_a_subject_over_the_column_limit()
        => Assert.Equal(
            EmailErrorCodes.AuthoredSubjectTooLong,
            CodeOf(() => Validate(new string('x', EmailOverrideLimits.SubjectMax + 1), "<p>nội dung</p>")));

    [Theory]
    [InlineData("Họp\r\nBcc: attacker@evil.com")]
    [InlineData("Họp\nX-Injected: 1")]
    public void Rejects_a_subject_carrying_a_header_break(string subject)
        => Assert.Equal(
            EmailErrorCodes.HeaderInvalid, CodeOf(() => Validate(subject, "<p>nội dung</p>")));

    [Theory]
    [InlineData("Họp\r\nBcc: attacker@evil.com")]
    [InlineData("Họp\nX-Injected: 1")]
    public void Rejects_a_header_break_in_a_plain_text_subject_too(string subject)
        => Assert.Equal(
            EmailErrorCodes.HeaderInvalid,
            CodeOf(() => Validate(subject, "nội dung", EmailBodyFormat.PLAIN_TEXT)));

    // ── Body ─────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_an_empty_body(string? body)
        => Assert.Equal(
            EmailErrorCodes.AuthoredBodyRequired, CodeOf(() => Validate("Tiêu đề", body)));

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void Rejects_an_empty_plain_text_body(string? body)
        => Assert.Equal(
            EmailErrorCodes.AuthoredBodyRequired,
            CodeOf(() => Validate("Tiêu đề", body, EmailBodyFormat.PLAIN_TEXT)));

    [Fact]
    public void Rejects_a_body_over_the_stored_limit()
        => Assert.Equal(
            EmailErrorCodes.AuthoredBodyTooLong,
            CodeOf(() => Validate("Tiêu đề", new string('x', EmailOverrideLimits.BodyMax + 1))));

    [Fact]
    public void Rejects_a_body_that_sanitises_down_to_nothing()
        => Assert.Equal(
            EmailErrorCodes.AuthoredBodyRequired,
            Assert.Throws<ValidationException>(() => ManualEmailContent.Validate(
                "Tiêu đề", "<script>alert(1)</script>", EmailBodyFormat.HTML,
                new StripEverythingSanitizer())).ErrorCode);

    // ── The two things a sender may never supply ─────────────────────────────

    [Fact]
    public void Rejects_a_forged_action_block_marker_in_html()
        => Assert.Equal(
            EmailErrorCodes.AuthoredActionBlockForbidden,
            CodeOf(() => Validate(
                "Tiêu đề",
                EmailComposition.ActionBlockStart + "<a href='#'>Đồng ý</a>" + EmailComposition.ActionBlockEnd)));

    [Fact]
    public void Rejects_a_forged_action_block_marker_in_plain_text()
        => Assert.Equal(
            EmailErrorCodes.AuthoredActionBlockForbidden,
            CodeOf(() => Validate(
                "Tiêu đề",
                "Xin xác nhận " + EmailComposition.ActionBlockStart, EmailBodyFormat.PLAIN_TEXT)));

    [Fact]
    public void Rejects_a_hand_written_trusted_block_placeholder()
        => Assert.Equal(
            EmailErrorCodes.AuthoredActionBlockForbidden,
            CodeOf(() => Validate("Tiêu đề", "Bấm vào đây: {{actionBlock}}", EmailBodyFormat.PLAIN_TEXT)));

    [Fact]
    public void An_ordinary_placeholder_is_not_a_trusted_block_and_is_left_alone()
    {
        // Manual mail is not rendered from a template, so braces in it are just characters the sender
        // typed. Only the trusted-block names are refused.
        var result = Validate("Tiêu đề", "Cú pháp mẫu: {{tenNguoiNhan}}", EmailBodyFormat.PLAIN_TEXT);

        Assert.Equal("Cú pháp mẫu: {{tenNguoiNhan}}", result.Body);
    }
}
