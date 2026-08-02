using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;
using PEMS.Domain.Enums;

namespace PEMS.Infrastructure.Email;

/// <summary>
/// The one renderer for every system email — preview and real send alike.
///
/// <para>
/// It reads <c>email_templates</c> on every call and deliberately does NOT cache. An operator who edits a
/// template must see the change on the next preview and the next send, without a deploy or a restart; a
/// cache without a trustworthy invalidation path would quietly break that promise, and template content
/// is a tiny read next to the SMTP round-trip it precedes.
/// </para>
/// <para>
/// It never logs the subject, the body or a variable value: those carry OTP codes, one-time links and
/// personal data.
/// </para>
/// </summary>
public sealed class EmailTemplateRenderer : IEmailTemplateRenderer
{
    /// <summary>
    /// The placeholder shape is owned by <see cref="EmailTemplateVariables"/> so the renderer and the
    /// seed-contract test can never disagree about what counts as a placeholder.
    /// </summary>
    private static readonly Regex PlaceholderPattern = EmailTemplateVariables.PlaceholderPattern;

    private static readonly Regex HtmlTagPattern = new("<[^>]+>", RegexOptions.Compiled);

    private readonly IApplicationDbContext _db;

    public EmailTemplateRenderer(IApplicationDbContext db) => _db = db;

    public async Task<EmailRenderResult> RenderAsync(
        EmailRenderRequest request, CancellationToken cancellationToken = default)
    {
        var code = request.TemplateCode?.Trim() ?? string.Empty;

        // 1) The code must be a template this application actually sends. Checking the registry before
        //    the database means an unregistered code fails the same way whether or not somebody has
        //    hand-inserted a row for it.
        _ = SystemEmailTemplates.Find(code)
            ?? throw new NotFoundException(
                $"Mã template email '{code}' không nằm trong danh mục hệ thống.",
                EmailErrorCodes.TemplateNotFound);

        // 2) Read the content fresh (see the no-cache note above).
        var template = await _db.EmailTemplates
            .AsNoTracking()
            .Where(t => t.TemplateCode == code)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(
                $"Không tìm thấy template email '{code}' trong cơ sở dữ liệu.",
                EmailErrorCodes.TemplateNotFound);

        // 3) Inactive is a deliberate operator action, not a reason to improvise content.
        if (!string.Equals(template.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
            throw new ConflictException(
                $"Template email '{code}' đang ở trạng thái không hoạt động.", EmailErrorCodes.TemplateInactive);

        // 4) Language content — strict. Falling back to the other language would ship Vietnamese to a
        //    recipient who asked for English and hide the fact that a translation is missing.
        var language = EmailLanguages.Normalize(request.Language);
        var (rawSubject, rawBody) = language == EmailLanguages.En
            ? (template.SubjectEn, template.BodyEn)
            : (template.SubjectVi, template.BodyVi);

        if (string.IsNullOrWhiteSpace(rawSubject) || string.IsNullOrWhiteSpace(rawBody))
            throw new BusinessRuleException(
                $"Template email '{code}' thiếu tiêu đề hoặc nội dung cho ngôn ngữ {language}.",
                EmailErrorCodes.TemplateLanguageContentMissing);

        var subjectTemplate = EmailTemplateVariables.NormalizeEncodedBraces(rawSubject!);
        var bodyTemplate = EmailTemplateVariables.NormalizeEncodedBraces(rawBody!);

        // 4b) A subject that interpolates a credential is refused, in BOTH languages, before a single
        //     value is substituted. The body of a sensitive template is deliberately not stored; the
        //     subject is, so moving {{otpCode}} or an action block into it would put the secret straight
        //     back into sent_emails and the history screen. Checking here — not in the seed — is what
        //     makes it hold for a template edited live in the database or through the template screen.
        //     Both languages are checked on every render so poisoning the one you are not sending still
        //     surfaces as a configuration fault instead of waiting for a recipient to trip over it.
        AssertNoSecretInSubject(code, template.SubjectVi, EmailLanguages.Vi);
        AssertNoSecretInSubject(code, template.SubjectEn, EmailLanguages.En);

        // 5) + 6) The declared set and the supplied set must match exactly, in both directions. This holds
        //    in authored mode too: the caller must still be able to render the template it named, so
        //    "the Host edited the words" never becomes a way to skip the contract.
        var declared = EmailTemplateVariables.ParseDeclared(template.VariablesText);
        var supplied = request.Variables ?? new Dictionary<string, string>();
        var trusted = request.TrustedHtmlBlocks ?? new Dictionary<string, string>();

        AssertVariableSetsMatch(code, declared, supplied);

        // 6b) Content mode. The template decides identity, format, language availability and policy in
        //     BOTH modes; only the sentences differ. Authored content arrives already validated and
        //     sanitised — SystemEmailContent.AuthoredByUser cannot be constructed otherwise.
        var isHtmlBody = template.BodyFormat == EmailBodyFormat.HTML;
        string subjectSource, bodySource;

        if (request.Content is SystemEmailContent.AuthoredByUser authored)
        {
            AssertNoSecretInSubject(code, authored.Subject, "người dùng soạn");

            // The author writes the message; the backend owns the buttons. Any action anchor or action
            // placeholder the author pasted is removed, then the real block is appended — which is why
            // the author is not allowed to place {{actionBlock}} and decide where it goes.
            var content = EmailComposition.StripActionArtifacts(
                EmailTemplateVariables.NormalizeEncodedBraces(authored.BodyHtml));

            subjectSource = EmailTemplateVariables.NormalizeEncodedBraces(authored.Subject);
            bodySource = content + TrustedBlockSuffix(trusted);
        }
        else
        {
            subjectSource = subjectTemplate;
            bodySource = bodyTemplate;

            // 6c) A template whose CONTENT is a trusted block must still be asking for it.
            //
            // Checked here, on the stored body before substitution, because this is the one failure the
            // guards below cannot see: a body that lost {{setupSummaryBlock}} leaves nothing to
            // substitute, so there are no leftover braces for the unresolved-placeholder check and no
            // missing variable for the contract check. The caller built the tables, the row had nowhere
            // to put them, and the mail went out saying "here is the update" with no update in it.
            //
            // Authored mode is exempt by construction: there the block is APPENDED to the author's text
            // rather than substituted into it, so it cannot be dropped by what the row happens to say.
            AssertRequiredTrustedBlockIsInBody(code, bodySource);
        }

        // 7) + 8) Substitute. Values are untrusted text everywhere; only TrustedHtmlBlocks may be markup.
        //    The subject is a header, never HTML — encoding there would show a recipient "&amp;".
        var subject = Substitute(subjectSource, supplied, trusted, encodeValues: false);
        var body = Substitute(bodySource, supplied, trusted, encodeValues: isHtmlBody);

        // 9) A subject is plain text on one line. Strip any markup that slipped into the template and
        //    refuse control characters outright — a newline here injects a header.
        subject = HtmlTagPattern.Replace(subject, string.Empty);
        subject = WebUtility.HtmlDecode(subject).Trim();
        EmailRecipientValidator.AssertNoHeaderBreak(subject, $"tiêu đề template '{code}'");

        // 9b) The placeholder guard above catches {{otpCode}}; this catches the value itself, typed in as
        //     text by somebody who could edit the subject. Both are the same leak — a subject is stored.
        AssertRenderedSubjectCarriesNoSecret(code, subject, supplied, trusted);

        // 10) Nothing may reach a recipient still wearing braces.
        AssertNoUnresolvedPlaceholder(code, subject, "tiêu đề");
        AssertNoUnresolvedPlaceholder(code, body, "nội dung");

        // 10b) Exactly one well-formed action block, or none. Checked on the FINAL body, so it also covers
        //      a template that carries a hand-written block of its own next to the injected one.
        EmailComposition.AssertActionBlockStructure(body);

        return new EmailRenderResult(
            template.EmailTemplateId,
            template.TemplateCode,
            subject,
            body,
            template.BodyFormat,
            language);
    }

    /// <summary>
    /// Compares declared against supplied with case-sensitive names. Case matters because the contract is
    /// lower camelCase: accepting "FullName" for "fullName" would let the old PascalCase spellings live on
    /// indefinitely, which is exactly the drift this work exists to end.
    /// </summary>
    private static void AssertVariableSetsMatch(
        string code, IReadOnlySet<string> declared, IReadOnlyDictionary<string, string> supplied)
    {
        var missing = declared.Where(d => !supplied.ContainsKey(d)).OrderBy(d => d, StringComparer.Ordinal).ToList();
        if (missing.Count > 0)
            throw new BusinessRuleException(
                $"Template email '{code}' thiếu giá trị cho biến: {string.Join(", ", missing)}.",
                EmailErrorCodes.TemplateVariableMissing);

        var unknown = supplied.Keys.Where(k => !declared.Contains(k)).OrderBy(k => k, StringComparer.Ordinal).ToList();
        if (unknown.Count > 0)
            throw new BusinessRuleException(
                $"Template email '{code}' không khai báo các biến được truyền vào: {string.Join(", ", unknown)}.",
                EmailErrorCodes.TemplateVariableUnknown);
    }

    /// <summary>
    /// Replaces every <c>{{name}}</c>. A name found in <paramref name="trusted"/> is injected verbatim;
    /// anything else is a variable value and is encoded when the target is HTML. A name in neither map is
    /// left alone so the unresolved-placeholder check reports it.
    /// </summary>
    private static string Substitute(
        string template,
        IReadOnlyDictionary<string, string> values,
        IReadOnlyDictionary<string, string> trusted,
        bool encodeValues)
        => PlaceholderPattern.Replace(template, match =>
        {
            var name = match.Groups[1].Value;

            if (trusted.TryGetValue(name, out var trustedHtml))
                return trustedHtml ?? string.Empty;

            if (values.TryGetValue(name, out var value))
            {
                value ??= string.Empty;
                return encodeValues ? WebUtility.HtmlEncode(value) : value;
            }

            return match.Value;
        });

    /// <summary>
    /// Refuses a subject that would interpolate a credential. Runs on the RAW subject, so the rejection
    /// happens before any value is substituted — the exception names the offending placeholder and never
    /// carries its value, and nothing is sent or recorded.
    /// </summary>
    private static void AssertNoSecretInSubject(string code, string? rawSubject, string source)
    {
        if (string.IsNullOrWhiteSpace(rawSubject)) return;   // absence is handled by the language check

        var normalized = EmailTemplateVariables.NormalizeEncodedBraces(rawSubject);
        var offenders = EmailTemplateVariables.ExtractPlaceholders(normalized)
            .Where(SensitiveEmailVariables.ForbiddenInSubject)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        if (offenders.Count == 0) return;

        throw new BusinessRuleException(
            $"Tiêu đề email '{code}' ({source}) không được chứa dữ liệu bí mật: " +
            $"{string.Join(", ", offenders.Select(o => "{{" + o + "}}"))}.",
            EmailErrorCodes.TemplateSensitiveInSubject);
    }

    /// <summary>
    /// Appends the backend's trusted blocks to authored content, in registry order. Authored bodies carry
    /// no <c>{{actionBlock}}</c> of their own (that is refused when the content is created), so appending
    /// is the only way the block gets in — and its position is the system's decision, not the author's.
    /// </summary>
    private static string TrustedBlockSuffix(IReadOnlyDictionary<string, string> trusted)
    {
        if (trusted.Count == 0) return string.Empty;

        var parts = EmailTrustedBlocks.All
            .Where(trusted.ContainsKey)
            .Select(name => trusted[name])
            .Where(html => !string.IsNullOrEmpty(html));

        return string.Concat(parts);
    }

    /// <summary>
    /// Refuses a subject that literally contains a secret: the value of a credential variable, an action
    /// URL from a trusted block, or an action-block marker. Runs on the FINAL subject, after substitution,
    /// which is the only point at which a pasted value can be recognised.
    ///
    /// <para>
    /// The exception deliberately names what kind of thing was found and nothing else — repeating the
    /// offending value would put it into the error payload, the log and the audit trail, which is the
    /// exact place this check exists to keep it out of.
    /// </para>
    /// </summary>
    private static void AssertRenderedSubjectCarriesNoSecret(
        string code,
        string subject,
        IReadOnlyDictionary<string, string> values,
        IReadOnlyDictionary<string, string> trusted)
    {
        if (string.IsNullOrEmpty(subject)) return;

        if (EmailComposition.ContainsActionBlockMarker(subject))
            throw new BusinessRuleException(
                $"Tiêu đề email '{code}' chứa dấu mốc khối hành động của hệ thống.",
                EmailErrorCodes.SubjectSecretLeak);

        foreach (var name in SensitiveEmailVariables.Names)
        {
            if (!values.TryGetValue(name, out var value)) continue;
            // Short values are skipped: a 1-2 character "code" is not a credential anybody could use, and
            // matching on one would reject ordinary subjects by coincidence.
            if (string.IsNullOrWhiteSpace(value) || value.Trim().Length < 4) continue;

            if (subject.Contains(value.Trim(), StringComparison.Ordinal))
                throw new BusinessRuleException(
                    $"Tiêu đề email '{code}' chứa giá trị bí mật của biến {{{{{name}}}}}.",
                    EmailErrorCodes.SubjectSecretLeak);
        }

        foreach (var url in EmailComposition.ExtractActionUrls(trusted))
        {
            if (url.Length < 8) continue;

            if (subject.Contains(url, StringComparison.Ordinal))
                throw new BusinessRuleException(
                    $"Tiêu đề email '{code}' chứa liên kết dùng một lần.",
                    EmailErrorCodes.SubjectSecretLeak);

            // …and the token on its own, without the address around it.
            var lastSegment = url.Split('/', '=', '?').LastOrDefault();
            if (!string.IsNullOrEmpty(lastSegment) && lastSegment.Length >= 12
                && subject.Contains(lastSegment, StringComparison.Ordinal))
                throw new BusinessRuleException(
                    $"Tiêu đề email '{code}' chứa mã dùng một lần.",
                    EmailErrorCodes.SubjectSecretLeak);
        }
    }

    /// <summary>
    /// Refuses to render a template whose stored body has lost the trusted block that IS its content.
    ///
    /// <para>
    /// Only fires when the caller actually supplied the block: a template rendered without it (the
    /// generic preview hands every template a setup-summary block it may not use) is not the situation
    /// this is about. The pairing that matters is "the backend built this content AND the row will not
    /// take it" — which means the row is behind the canonical template and needs re-syncing, not that
    /// the send is wrong.
    /// </para>
    /// <para>
    /// The message names the repair rather than the symptom, because the person who reads it is an
    /// operator looking at a template screen, not the host who pressed send.
    /// </para>
    /// </summary>
    private static void AssertRequiredTrustedBlockIsInBody(string code, string bodyTemplate)
    {
        // Deliberately NOT conditional on the caller having supplied the block. The contract says these
        // blocks ARE this template's content, so a stored body without one is unsendable no matter who is
        // asking — and the caller that forgot to build it is the one case where making the check
        // caller-dependent would let both halves of the same fault cancel out into a silent success:
        // nothing to substitute, nothing left unresolved, a mail that reads "here is the update" and
        // shows none. The two faults are reported separately (this one names the row to repair; a body
        // that still has the placeholder falls through to the unresolved-placeholder guard, which names
        // the missing block), because they have different repairs.
        foreach (var (block, errorCode) in EmailTemplateContracts.RequiredBlocksFor(code))
        {
            if (bodyTemplate.Contains("{{" + block + "}}", StringComparison.Ordinal)) continue;

            var repair = errorCode == EmailErrorCodes.TemplateRequiredContactBlockNotInBody
                ? "Hãy thêm lại khối này vào nội dung, hoặc đổi mức bắt buộc trong cấu hình thông tin liên hệ."
                : "Hãy đồng bộ lại template này từ bản chuẩn.";

            throw new BusinessRuleException(
                $"Nội dung template email '{code}' trong cơ sở dữ liệu không còn chỗ đặt khối "
                + $"{{{{{block}}}}}, nên phần nội dung do hệ thống dựng sẽ bị mất. {repair}",
                errorCode);
        }
    }

    private static void AssertNoUnresolvedPlaceholder(string code, string rendered, string part)
    {
        var leftover = PlaceholderPattern.Match(rendered);
        if (leftover.Success)
            throw new BusinessRuleException(
                $"Template email '{code}' còn placeholder chưa thay thế trong {part}: {{{{{leftover.Groups[1].Value}}}}}.",
                EmailErrorCodes.TemplateUnresolvedPlaceholder);
    }
}
