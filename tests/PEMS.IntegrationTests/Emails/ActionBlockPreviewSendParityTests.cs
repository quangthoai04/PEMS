using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;
using Xunit;

namespace PEMS.IntegrationTests.Emails;

/// <summary>
/// Phase C of the email fidelity plan: preview and runtime must be visually and textually identical for
/// every registered action template, in both languages, except for the runtime secret itself.
///
/// <para>
/// A theory over the CATALOG (every template code <see cref="EmailActionTemplates.For"/> recognises),
/// not a hand-picked list — a template registered later is covered the day it is added. Which real
/// builder to call is looked up by <see cref="ActionPresentationKind"/>, the SAME value production code
/// (<see cref="EmailActionTemplates.DisabledBlockFor"/>) uses to pick the disabled stand-in — this test
/// does not maintain an independent second classification of which buttons a template shows.
/// </para>
/// <para>
/// Normalisation removes ONLY what a real send is allowed to differ on: the concrete href/token value,
/// and the clickable <c>&lt;a&gt;</c> vs inert <c>&lt;span&gt;</c> tag. Colours, padding, border-radius,
/// font-weight, spacing and every word of visible text are compared byte-for-byte — a passing test here
/// is what proves an operator's preview shows exactly what the recipient receives.
/// </para>
/// </summary>
public sealed class ActionBlockPreviewSendParityTests
{
    private const string DummyUrl1 = "https://pems.test/action/one";
    private const string DummyUrl2 = "https://pems.test/action/two";
    private const string DummyUrl3 = "https://pems.test/action/three";

    public static IEnumerable<object[]> ActionTemplateCodes() =>
        SystemEmailTemplates.All
            .Select(t => t.TemplateCode)
            .Where(code => EmailActionTemplates.For(code) is not null)
            .OrderBy(code => code, StringComparer.Ordinal)
            .Select(code => new object[] { code });

    /// <summary>
    /// Builds the REAL block for one template code, calling the exact production method that template's
    /// own send path calls (confirmed against each command handler) — not a re-derived generic per kind,
    /// since a couple of templates sharing a kind still call genuinely different production methods
    /// (VisitReminderHost/Participants call <see cref="EmailComposition.VisitDetailBlock"/>, the other
    /// DetailOnly templates call <see cref="EmailComposition.DetailLinkBlock"/> with their own label).
    /// </summary>
    private static string BuildRealBlock(string templateCode, ActionPresentationKind kind, string language)
    {
        return kind switch
        {
            ActionPresentationKind.Confirm =>
                EmailComposition.ConfirmEmailBlock(DummyUrl1, EmailActionTemplates.ConfirmEmailLabel(language)),

            ActionPresentationKind.AcceptDecline =>
                EmailComposition.AcceptDeclineBlock(DummyUrl1, DummyUrl2, assignUrl: null, language: language),

            ActionPresentationKind.AcceptDeclineAssign =>
                EmailComposition.AcceptDeclineBlock(DummyUrl1, DummyUrl2, DummyUrl3, language),

            ActionPresentationKind.ContactRoleInvitation =>
                EmailComposition.ContactRoleInvitationBlock(DummyUrl1, DummyUrl2, language),

            ActionPresentationKind.LogisticsAction =>
                EmailComposition.LogisticsActionBlock(DummyUrl1, DummyUrl2, DummyUrl3, language: language),

            ActionPresentationKind.LogisticsAssignee =>
                EmailComposition.LogisticsAssigneeActionBlock(
                    DummyUrl1, DummyUrl2, DummyUrl3,
                    EmailActionTemplates.DetailLinkLabelFor(templateCode, language), language),

            ActionPresentationKind.DetailOnly => templateCode is EmailActionTemplates.VisitReminderHost
                    or EmailActionTemplates.VisitReminderParticipants
                ? EmailComposition.VisitDetailBlock(DummyUrl1, language)
                : EmailComposition.DetailLinkBlock(
                    DummyUrl1, EmailActionTemplates.DetailLinkLabelFor(templateCode, language) ?? "Mở yêu cầu để xử lý"),

            _ => throw new InvalidOperationException($"Unhandled presentation kind: {kind}"),
        };
    }

    /// <summary>Strips the START/END action-block markers, which carry no visible content.</summary>
    private static string StripMarkers(string html) => html
        .Replace(EmailComposition.ActionBlockStart, string.Empty)
        .Replace(EmailComposition.ActionBlockEnd, string.Empty);

    /// <summary>
    /// Normalises away ONLY the allowed differences: an <c>&lt;a href="...">text&lt;/a></c> becomes
    /// <c>&lt;span>text&lt;/span></c> — same attributes otherwise, same inner text, same position.
    ///
    /// <c>text-decoration:none</c> is stripped along with the tag swap: an anchor needs it to suppress
    /// the browser's default underline, and a <c>&lt;span></c> never had one to suppress — so the two
    /// render identically with or without the declaration once the tag itself has changed. Removing it
    /// here is part of normalising the clickability difference, not a second, separate style difference.
    /// </summary>
    private static string NormalizeRuntimeSecrets(string html)
    {
        var normalized = Regex.Replace(
            html,
            @"<a\s+href=""[^""]*""([^>]*)>",
            m => $"<span{m.Groups[1].Value}>",
            RegexOptions.IgnoreCase);
        normalized = normalized.Replace("</a>", "</span>", StringComparison.OrdinalIgnoreCase);
        normalized = normalized.Replace("text-decoration:none;", "", StringComparison.OrdinalIgnoreCase);
        return normalized;
    }

    [Theory]
    [MemberData(nameof(ActionTemplateCodes))]
    public void Preview_and_runtime_match_exactly_after_normalizing_only_the_runtime_secret_vi(string templateCode)
        => AssertParity(templateCode, EmailLanguages.Vi);

    [Theory]
    [MemberData(nameof(ActionTemplateCodes))]
    public void Preview_and_runtime_match_exactly_after_normalizing_only_the_runtime_secret_en(string templateCode)
        => AssertParity(templateCode, EmailLanguages.En);

    private static void AssertParity(string templateCode, string language)
    {
        var spec = EmailActionTemplates.For(templateCode)!;

        var real = StripMarkers(BuildRealBlock(templateCode, spec.PresentationKind, language));
        var disabled = EmailActionTemplates.DisabledBlockFor(templateCode, language);

        var normalizedReal = NormalizeRuntimeSecrets(real);

        Assert.Equal(disabled.Trim(), normalizedReal.Trim());

        // The real block must actually have carried a live href/token for at least one control —
        // otherwise this test would trivially pass by comparing two already-identical stand-ins.
        Assert.Contains("href=", real, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("href=", disabled, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Language actually changes the words, not just an internal flag nobody reads — so this cannot pass
    /// by having both sides silently stay Vietnamese regardless of what was asked for.
    /// </summary>
    [Theory]
    [MemberData(nameof(ActionTemplateCodes))]
    public void English_preview_and_runtime_do_not_carry_leftover_vietnamese_labels(string templateCode)
    {
        var spec = EmailActionTemplates.For(templateCode)!;
        var real = StripMarkers(BuildRealBlock(templateCode, spec.PresentationKind, EmailLanguages.En));
        var disabled = EmailActionTemplates.DisabledBlockFor(templateCode, EmailLanguages.En);

        foreach (var staleVietnameseLabel in new[]
                 {
                     "Chấp nhận", "Từ chối", "Xác nhận", "Đồng ý", "Gán nhân sự",
                     "Xem chi tiết", "Mở yêu cầu", "Mở biên bản", "Đăng nhập", "nhiệm vụ",
                 })
        {
            Assert.DoesNotContain(staleVietnameseLabel, real, StringComparison.Ordinal);
            Assert.DoesNotContain(staleVietnameseLabel, disabled, StringComparison.Ordinal);
        }
    }

    [Theory]
    [MemberData(nameof(ActionTemplateCodes))]
    public void Vietnamese_preview_and_runtime_carry_vietnamese_labels(string templateCode)
    {
        var spec = EmailActionTemplates.For(templateCode)!;
        var real = StripMarkers(BuildRealBlock(templateCode, spec.PresentationKind, EmailLanguages.Vi));
        var disabled = EmailActionTemplates.DisabledBlockFor(templateCode, EmailLanguages.Vi);

        // At least one accented Vietnamese character somewhere in the visible text of both sides — a
        // coarse but effective guard against a label silently regressing to English by default.
        bool HasVietnameseDiacritic(string s) => s.Any(c => c > 127);
        Assert.True(HasVietnameseDiacritic(real), $"{templateCode}: real block has no Vietnamese text.");
        Assert.True(HasVietnameseDiacritic(disabled), $"{templateCode}: disabled block has no Vietnamese text.");
    }
}
