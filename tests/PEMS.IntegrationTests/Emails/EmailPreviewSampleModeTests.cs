using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;
using PEMS.Application.Emails.Common;
using PEMS.Application.Emails.Queries.PreviewEmailTemplate;
using PEMS.Domain.Constants;
using PEMS.Infrastructure.Email;
using PEMS.Infrastructure.Persistence;
using PEMS.IntegrationTests.TestInfrastructure;
using Xunit;

namespace PEMS.IntegrationTests.Emails;

/// <summary>
/// Preview sample data comes from the backend contract, and only when the caller asks for it (G11-J).
///
/// <para>
/// Both halves matter and they pull against each other. The template-management screen has no real
/// message in hand, so without samples a canonical template previews as an error — which is what an
/// operator was seeing. The compose modal IS previewing a real message; filling a caller's gap with
/// "Nguyễn Văn An" there would show a host something the recipient will never receive, and they would
/// approve it. So the samples are opt-in, and the strict behaviour stays the default.
/// </para>
/// </summary>
public sealed class EmailPreviewSampleModeTests : IDisposable
{
    private readonly EmailEvidenceHarness _h = new("g11j-sample@partner.example.com");

    public void Dispose() => _h.Dispose();

    private sealed class FakeCurrentUser : ICurrentUserService
    {
        public bool IsAuthenticated => true;
        public ulong? UserId => 1;
        public string? Email => "preview-sample@fpt.edu.vn";
        public ulong? RoleId => null;
        public string? RoleCode => RoleCodes.Ho;
        public string? SubRole => null;
        public ulong? PrimaryCampusId => null;
        public ulong? DepartmentId => null;
        public ulong? SessionId => null;
        public string? LoginPortal => null;
    }

    private static PreviewEmailTemplateQueryHandler Preview(ApplicationDbContext db)
        => new(db, new FakeCurrentUser(), new EmailTemplateRenderer(db),
               EmailEvidenceHarness.Senders(db),
               EmailEvidenceHarness.PreviewTokens());

    private static readonly Regex Placeholder = new(@"\{\{\s*[A-Za-z_][A-Za-z0-9_]*\s*\}\}", RegexOptions.Compiled);

    // ── Sample mode: the whole catalog previews ──────────────────────────────

    /// <summary>
    /// Every active template, in both languages, with no caller context at all — which is exactly the
    /// situation the template editor is in. Nothing may be left unresolved.
    /// </summary>
    [Theory]
    [InlineData("VI")]
    [InlineData("EN")]
    public async Task Every_template_previews_from_samples_alone(string language)
    {
        EmailEvidenceHarness.RequireDb();
        await using var db = EmailEvidenceHarness.NewContext();

        var failures = new List<string>();

        foreach (var code in SystemEmailTemplates.AllCodes.OrderBy(c => c, StringComparer.Ordinal))
        {
            try
            {
                var response = await Preview(db).Handle(
                    new PreviewEmailTemplateQuery(code, null, language, UseSampleData: true),
                    CancellationToken.None);

                if (Placeholder.IsMatch(response.Subject))
                    failures.Add($"{code}: unresolved placeholder in subject");

                if (Placeholder.IsMatch(response.BodyHtml))
                    failures.Add($"{code}: unresolved placeholder in body");
            }
            catch (Exception ex)
            {
                failures.Add($"{code}: {ex.GetType().Name} {ex.Message}");
            }
        }

        Assert.True(failures.Count == 0,
            $"{language}: " + string.Join(Environment.NewLine, failures));
    }

    /// <summary>
    /// A preview must not mint anything real. The action block is inert by construction (R-106); this
    /// asserts the SAMPLE side of the same promise — no live URL, no plausible code, no script.
    /// </summary>
    [Theory]
    [InlineData("VI")]
    [InlineData("EN")]
    public async Task No_preview_contains_a_real_token_or_a_clickable_link(string language)
    {
        EmailEvidenceHarness.RequireDb();
        await using var db = EmailEvidenceHarness.NewContext();

        var failures = new List<string>();

        foreach (var code in SystemEmailTemplates.AllCodes.OrderBy(c => c, StringComparer.Ordinal))
        {
            var response = await Preview(db).Handle(
                new PreviewEmailTemplateQuery(code, null, language, UseSampleData: true),
                CancellationToken.None);

            var whole = response.Subject + "\n" + response.BodyHtml + "\n" + (response.LockedActionBlockHtml ?? "");

            if (whole.Contains("javascript:", StringComparison.OrdinalIgnoreCase))
                failures.Add($"{code}: javascript: URL");

            if (whole.Contains("<script", StringComparison.OrdinalIgnoreCase))
                failures.Add($"{code}: script tag");

            if (Regex.IsMatch(whole, @"\bonerror\s*=", RegexOptions.IgnoreCase))
                failures.Add($"{code}: inline event handler");

            // An OTP-carrying template must show the fixed fake, never a generated one.
            if (EmailTemplateContracts.For(code)!.SensitiveVariables.Contains("otpCode")
                && Regex.IsMatch(response.BodyHtml, @"\b(?!000000)\d{6}\b"))
            {
                failures.Add($"{code}: a six-digit value that is not the fixed sample");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    // ── Strict mode is still strict ──────────────────────────────────────────

    /// <summary>
    /// The default. A caller previewing a real message and forgetting a variable must be told, not
    /// handed a sample — otherwise the host approves a preview the recipient will never receive.
    /// </summary>
    [Fact]
    public async Task Without_sample_mode_a_missing_caller_variable_still_fails()
    {
        EmailEvidenceHarness.RequireDb();
        await using var db = EmailEvidenceHarness.NewContext();

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Preview(db).Handle(
                new PreviewEmailTemplateQuery(
                    SystemEmailTemplates.AccountEmailConfirmation,
                    new Dictionary<string, string> { ["fullName"] = "Người dùng" },
                    EmailLanguages.Vi),
                CancellationToken.None));

        Assert.Equal(EmailErrorCodes.TemplateVariableMissing, ex.ErrorCode);
    }

    [Fact]
    public async Task Sample_mode_does_not_excuse_a_variable_the_template_never_declared()
    {
        EmailEvidenceHarness.RequireDb();
        await using var db = EmailEvidenceHarness.NewContext();

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Preview(db).Handle(
                new PreviewEmailTemplateQuery(
                    SystemEmailTemplates.AccountEmailConfirmation,
                    new Dictionary<string, string> { ["ghostVariable"] = "x" },
                    EmailLanguages.Vi,
                    UseSampleData: true),
                CancellationToken.None));

        Assert.Equal(EmailErrorCodes.TemplateVariableUnknown, ex.ErrorCode);
    }

    // ── The caller wins ──────────────────────────────────────────────────────

    [Fact]
    public async Task A_caller_supplied_value_is_used_in_place_of_the_sample()
    {
        EmailEvidenceHarness.RequireDb();
        await using var db = EmailEvidenceHarness.NewContext();

        const string realName = "Trần Quốc Toản";

        var response = await Preview(db).Handle(
            new PreviewEmailTemplateQuery(
                SystemEmailTemplates.AccountEmailConfirmation,
                new Dictionary<string, string> { ["fullName"] = realName },
                EmailLanguages.Vi,
                UseSampleData: true),
            CancellationToken.None);

        Assert.Contains(realName, response.BodyHtml);
        Assert.DoesNotContain(
            EmailVariableCatalog.Sample("fullName", EmailLanguages.Vi), response.BodyHtml);
    }

    /// <summary>
    /// A caller must not be able to pass the action block: trusted blocks are the only route by which
    /// markup, and therefore a live action URL, enters a rendered message.
    /// </summary>
    [Fact]
    public async Task A_caller_cannot_inject_the_action_block_through_the_context()
    {
        EmailEvidenceHarness.RequireDb();
        await using var db = EmailEvidenceHarness.NewContext();

        var response = await Preview(db).Handle(
            new PreviewEmailTemplateQuery(
                SystemEmailTemplates.VisitParticipantInvitation,
                new Dictionary<string, string>
                {
                    [EmailTrustedBlocks.ActionBlock] = "<a href=\"https://evil.example/accept\">Chấp nhận</a>",
                },
                EmailLanguages.Vi,
                UseSampleData: true),
            CancellationToken.None);

        var whole = response.Subject + response.BodyHtml + (response.LockedActionBlockHtml ?? "");
        Assert.DoesNotContain("evil.example", whole);
    }
}
