using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;
using PEMS.Application.Emails.Sender;
using PEMS.Domain.Entities.Emails;
using PEMS.Infrastructure.Email;
using PEMS.Infrastructure.Persistence;
using Xunit;

namespace PEMS.IntegrationTests.Emails;

/// <summary>
/// The join between a template's CONTRACT and the code that actually sends it (V4 §23, §24, §32).
///
/// <para>
/// <b>What was already proved, and what was not.</b> The seed matches the registry, the registry matches
/// the contract the editor is offered, and the renderer refuses a send whose supplied values do not match
/// the declared set exactly. All of that is about declarations. None of it says the twenty-odd handlers
/// that call <c>SendAsync</c> actually BUILD those values — so adding a variable to a template and
/// forgetting one of its callers produced a template that saved cleanly, previewed correctly, and then
/// failed at send time with "thiếu giá trị cho biến", on a flow nobody runs in a test.
/// </para>
/// <para>
/// <b>How this closes it.</b> Every registered template is mapped to the source file(s) that construct
/// its <c>SystemEmailRequest</c>, and the variable names those files write are read out of the source
/// itself. A template whose caller does not name every declared variable fails here — at build time,
/// in a test that names the handler — rather than in production. A NEW template with no entry in the map
/// fails too, which is what keeps the map honest.
/// </para>
/// <para>
/// <b>Why source text rather than reflection.</b> The values are built inside a handler's method body,
/// from entities loaded a moment earlier; there is no seam to call and nothing to reflect over. Reading
/// the dictionary keys the file writes is the only mechanical evidence available short of running every
/// business flow, and it fails in the direction that matters: a caller that stops mentioning a variable
/// cannot pass.
/// </para>
/// </summary>
public sealed class SendPointVariableCoverageTests
{
    // ── Where the sends live ─────────────────────────────────────────────────

    /// <summary>
    /// One send point: the file that builds the request, plus any shared builder it delegates to.
    ///
    /// <para>
    /// Several handlers do delegate — <c>AccountEmailVariables</c>, <c>OtpEmailVariables</c>,
    /// <c>VisitSetupProgressEmailGuard</c> — and that is the recommended shape rather than a smell, so
    /// the builder is listed beside the caller and its keys count as supplied by that send point.
    /// </para>
    /// </summary>
    private sealed record SendPoint(string TemplateCode, string Name, params string[] Files);

    private const string App = "backend/PEMS.Application/";
    private const string Infra = "backend/PEMS.Infrastructure/";

    private const string AccountVars = App + "Accounts/Common/AccountEmailVariables.cs";
    private const string OtpVars = App + "Emails/Common/OtpEmailVariables.cs";

    /// <summary>
    /// Every place this application sends a system email, found by searching for the template constants
    /// and for `new SystemEmailRequest(`. Thirty-one entries for thirty templates: several templates are
    /// sent from more than one flow, and each of those flows builds its own values.
    /// </summary>
    private static readonly SendPoint[] SendPoints =
    {
        // ── ACCOUNT ──────────────────────────────────────────────────────────
        new(SystemEmailTemplates.AccountEmailConfirmation, "CreateAccount",
            App + "Accounts/Commands/CreateAccount/CreateAccountCommandHandler.cs", AccountVars),
        new(SystemEmailTemplates.AccountEmailConfirmation, "ReplaceStaffLeader (successor)",
            App + "Accounts/Commands/ReplaceStaffLeader/ReplaceStaffLeaderCommandHandler.cs", AccountVars),
        new(SystemEmailTemplates.AccountEmailConfirmation, "PendingEmailChange (new address)",
            App + "Accounts/Common/PendingAccountEmailChangeMails.cs", AccountVars),
        new(SystemEmailTemplates.AccountEmailConfirmation, "CreateDepartmentPersonnel",
            App + "DepartmentLeaderPersonnel/Commands/CreateDepartmentPersonnel/CreateDepartmentPersonnelCommandHandler.cs", AccountVars),
        new(SystemEmailTemplates.AccountEmailConfirmation, "ResendPersonnelEmailConfirmation",
            App + "DepartmentLeaderPersonnel/Commands/ResendPersonnelEmailConfirmation/ResendPersonnelEmailConfirmationCommandHandler.cs", AccountVars),
        new(SystemEmailTemplates.AccountEmailConfirmation, "UpdateDepartmentPersonnel (pending address)",
            App + "DepartmentLeaderPersonnel/Commands/UpdateDepartmentPersonnel/UpdateDepartmentPersonnelCommandHandler.cs", AccountVars),
        new(SystemEmailTemplates.AccountEmailConfirmation, "AddDepartmentPersonnel",
            App + "Departments/Commands/AddDepartmentPersonnel/AddDepartmentPersonnelCommandHandler.cs", AccountVars),

        new(SystemEmailTemplates.AccountActivated, "ConfirmAccountEmail",
            App + "Accounts/Commands/ConfirmAccountEmail/ConfirmAccountEmailCommandHandler.cs", AccountVars),

        // The two "address unlinked" notices declare NO variables by design — see the registry.
        new(SystemEmailTemplates.AccountPendingEmailChangedOldNotice, "PendingEmailChange (old address)",
            App + "Accounts/Common/PendingAccountEmailChangeMails.cs"),
        new(SystemEmailTemplates.AccountPendingEmailChangedOldNotice, "UpdateDepartmentPersonnel (old address)",
            App + "DepartmentLeaderPersonnel/Commands/UpdateDepartmentPersonnel/UpdateDepartmentPersonnelCommandHandler.cs"),
        new(SystemEmailTemplates.AccountEmailChangedOldNotice, "UpdateAccountRole (old address)",
            App + "Accounts/Commands/UpdateAccountRole/UpdateAccountRoleCommandHandler.cs"),
        new(SystemEmailTemplates.AccountEmailChangedOldNotice, "UpdateBasicAccountInfo (old address)",
            App + "Accounts/Commands/UpdateBasicAccountInfo/UpdateBasicAccountInfoCommandHandler.cs"),
        new(SystemEmailTemplates.AccountEmailChangedOldNotice, "UpdateDepartmentPersonnel (old address)",
            App + "DepartmentLeaderPersonnel/Commands/UpdateDepartmentPersonnel/UpdateDepartmentPersonnelCommandHandler.cs"),

        new(SystemEmailTemplates.AccountEmailChangedNewNotice, "UpdateAccountRole (new address)",
            App + "Accounts/Commands/UpdateAccountRole/UpdateAccountRoleCommandHandler.cs"),
        new(SystemEmailTemplates.AccountEmailChangedNewNotice, "UpdateBasicAccountInfo (new address)",
            App + "Accounts/Commands/UpdateBasicAccountInfo/UpdateBasicAccountInfoCommandHandler.cs"),
        new(SystemEmailTemplates.AccountEmailChangedNewNotice, "UpdateDepartmentPersonnel (new address)",
            App + "DepartmentLeaderPersonnel/Commands/UpdateDepartmentPersonnel/UpdateDepartmentPersonnelCommandHandler.cs"),

        new(SystemEmailTemplates.AccountRoleChanged, "UpdateAccountRole",
            App + "Accounts/Commands/UpdateAccountRole/UpdateAccountRoleCommandHandler.cs"),
        new(SystemEmailTemplates.AccountStaffLeaderAssigned, "ReplaceStaffLeader (new leader)",
            App + "Accounts/Commands/ReplaceStaffLeader/ReplaceStaffLeaderCommandHandler.cs", AccountVars),
        new(SystemEmailTemplates.AccountStaffLeaderReplaced, "ReplaceStaffLeader (outgoing leader)",
            App + "Accounts/Commands/ReplaceStaffLeader/ReplaceStaffLeaderCommandHandler.cs", AccountVars),

        // ── DEPARTMENT PERSONNEL ─────────────────────────────────────────────
        new(SystemEmailTemplates.DeptPersonnelAccountDisabled, "ChangePersonnelStatus (disable)",
            App + "DepartmentLeaderPersonnel/Commands/ChangePersonnelStatus/ChangePersonnelStatusCommandHandler.cs"),
        new(SystemEmailTemplates.DeptPersonnelAccountEnabled, "ChangePersonnelStatus (enable)",
            App + "DepartmentLeaderPersonnel/Commands/ChangePersonnelStatus/ChangePersonnelStatusCommandHandler.cs"),
        // SEC-09: the variable-building code (SendTransferMailsAsync) moved out of the handler and
        // into the shared IDepartmentLeadershipTransferService — now used by both this canonical
        // self-service flow AND the legacy third-party ReassignDepartmentLeadCommandHandler, so both
        // send points are backed by the exact same implementation.
        new(SystemEmailTemplates.DeptLeadershipGranted, "TransferDepartmentLeadership (successor)",
            App + "DepartmentLeaderPersonnel/Common/DepartmentLeadershipTransferService.cs"),
        new(SystemEmailTemplates.DeptLeadershipHandedOver, "TransferDepartmentLeadership (predecessor)",
            App + "DepartmentLeaderPersonnel/Common/DepartmentLeadershipTransferService.cs"),

        // ── AUTH / OTP ───────────────────────────────────────────────────────
        new(SystemEmailTemplates.AuthPasswordResetOtp, "ForgotPassword",
            App + "Authentication/Commands/ForgotPassword/ForgotPasswordCommandHandler.cs", OtpVars),
        new(SystemEmailTemplates.VisitRequestOtp, "VisitRequestOtpMail",
            App + "Emails/Common/VisitRequestOtpMail.cs", OtpVars),

        // ── VISIT REQUEST — operational contact ──────────────────────────────
        new(SystemEmailTemplates.VisitContactClaim, "OperationalContactInvitation (claim)",
            Infra + "Services/OperationalContactInvitationService.cs"),
        new(SystemEmailTemplates.VisitContactTransfer, "OperationalContactInvitation (transfer)",
            Infra + "Services/OperationalContactInvitationService.cs"),
        // The campus decision the guest side needs to hear about, and the lapsed invitation the
        // registrant is the only person who can act on.
        //
        // The variables for both live in a BUILDER rather than at the send point, because each has two
        // callers: the transition that caused it, sending immediately, and the recovery sweep, sending
        // later because the first attempt failed. Writing the message twice is how a retry ends up
        // saying something slightly different from the original — so the file named here is the builder,
        // which is the one place either message is composed.
        new(SystemEmailTemplates.VisitCampusRejected, "CampusRejectionEmail",
            App + "Delegations/VisitNotifications/CampusRejectionEmail.cs"),
        new(SystemEmailTemplates.VisitContactInvitationExpired, "ContactInvitationExpiryEmail",
            App + "Delegations/VisitNotifications/ContactInvitationExpiryEmail.cs"),

        // ── VISIT PARTICIPANT ────────────────────────────────────────────────
        new(SystemEmailTemplates.VisitParticipantInvitation, "InviteVisitParticipant (IC/staff)",
            App + "Delegations/Commands/InviteVisitParticipant/InviteVisitParticipantCommandHandler.cs"),
        new(SystemEmailTemplates.VisitStudentInvitation, "InviteVisitParticipant (student)",
            App + "Delegations/Commands/InviteVisitParticipant/InviteVisitParticipantCommandHandler.cs"),
        new(SystemEmailTemplates.VisitDepartmentLeaderInvitation, "InviteVisitParticipant (department leader)",
            App + "Delegations/Commands/InviteVisitParticipant/InviteVisitParticipantCommandHandler.cs"),
        new(SystemEmailTemplates.VisitDepartmentStaffAssignment, "AssignDepartmentStaff",
            App + "Delegations/Commands/AssignDepartmentStaff/AssignDepartmentStaffCommandHandler.cs"),

        // ── REMINDERS ────────────────────────────────────────────────────────
        new(SystemEmailTemplates.VisitReminderHost, "VisitReminderDispatchService (host)",
            App + "Delegations/Reminders/VisitReminderDispatchService.cs"),
        new(SystemEmailTemplates.VisitReminderParticipants, "VisitReminderDispatchService (participants)",
            App + "Delegations/Reminders/VisitReminderDispatchService.cs"),

        // ── LOGISTICS ────────────────────────────────────────────────────────
        new(SystemEmailTemplates.LogisticsRequestToDepartment, "PrepareVisitLogistics",
            App + "Delegations/Commands/PrepareVisitLogistics/PrepareVisitLogisticsCommandHandler.cs"),
        new(SystemEmailTemplates.LogisticsAssigneeAssignment, "AssignRequestAssignee",
            App + "DepartmentReceptionTasks/Commands/AssignRequestAssignee/AssignRequestAssigneeCommand.cs"),
        new(SystemEmailTemplates.LogisticsChangeProposalToHost, "ProposeRequestChange",
            App + "DepartmentReceptionTasks/Commands/ProposeRequestChange/ProposeRequestChangeCommand.cs"),
        new(SystemEmailTemplates.LogisticsExpenseReportReminder, "RemindExpenseReports",
            App + "Delegations/VisitExpenses/Commands/RemindExpenseReports/RemindExpenseReportsCommandHandler.cs"),

        // ── REPORTS + SETUP PROGRESS ─────────────────────────────────────────
        new(SystemEmailTemplates.VisitSetupProgressUpdate, "VisitSetupProgressComposer",
            App + "Delegations/SetupProgressEmail/VisitSetupProgressComposer.cs",
            App + "Delegations/SetupProgressEmail/VisitSetupProgressEmailGuard.cs"),
        new(SystemEmailTemplates.ReportCampusOperation, "SendHoCampusReport",
            App + "Reports/Commands/SendHoCampusReport/SendHoCampusReportCommand.cs"),
        new(SystemEmailTemplates.ReportDepartmentCollaboration, "SendStaffLeaderDepartmentReport",
            App + "Reports/Commands/SendStaffLeaderDepartmentReport/SendStaffLeaderDepartmentReportCommand.cs"),
        new(SystemEmailTemplates.ReportDepartmentInvoice, "SendDeptLeaderInvoiceToStaffLeader",
            App + "Reports/Commands/SendDeptLeaderInvoiceToStaffLeader/SendDeptLeaderInvoiceToStaffLeaderCommand.cs"),
        new(SystemEmailTemplates.ReportDepartmentInvoice, "SendStaffLeaderDeptInvoice",
            App + "Reports/Commands/SendStaffLeaderDeptInvoice/SendStaffLeaderDeptInvoiceCommand.cs"),
        new(SystemEmailTemplates.ReportPersonnelPerformance, "SendDeptLeaderPersonnelReport",
            App + "Reports/Commands/SendDeptLeaderPersonnelReport/SendDeptLeaderPersonnelReportCommand.cs"),
        new(SystemEmailTemplates.ReportPersonnelPerformance, "SendStaffLeaderPersonnelReport",
            App + "Reports/Commands/SendStaffLeaderPersonnelReport/SendStaffLeaderPersonnelReportCommand.cs"),
    };

    public static TheoryData<string, string> AllSendPoints()
    {
        var data = new TheoryData<string, string>();
        foreach (var point in SendPoints) data.Add(point.TemplateCode, point.Name);
        return data;
    }

    // ── Source access ────────────────────────────────────────────────────────

    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PEMS.slnx"))) dir = dir.Parent;

        Assert.True(dir is not null, "Could not locate the repository root from " + AppContext.BaseDirectory);
        return dir!.FullName;
    }

    /// <summary>Every `["name"]` key the file writes — the shape every send point uses for its values.</summary>
    private static readonly Regex DictionaryKey = new(@"\[""([A-Za-z_][A-Za-z0-9_]*)""\]", RegexOptions.Compiled);

    private static HashSet<string> KeysWrittenBy(SendPoint point)
    {
        var root = RepositoryRoot();
        var keys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var relative in point.Files)
        {
            var path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(path), $"{point.TemplateCode} / {point.Name}: send point file not found — {relative}");

            foreach (Match match in DictionaryKey.Matches(File.ReadAllText(path)))
                keys.Add(match.Groups[1].Value);
        }

        return keys;
    }

    // ── The audit ────────────────────────────────────────────────────────────

    /// <summary>
    /// A registered template with no audited send point is either dead or unaudited, and both are
    /// findings. This is what stops the map above from silently falling behind the registry.
    /// </summary>
    [Fact]
    public void Every_registered_template_has_at_least_one_audited_send_point()
    {
        var audited = SendPoints.Select(p => p.TemplateCode).ToHashSet(StringComparer.Ordinal);

        var unaudited = SystemEmailTemplates.All
            .Select(t => t.TemplateCode)
            .Where(code => !audited.Contains(code))
            .OrderBy(c => c, StringComparer.Ordinal)
            .ToList();

        Assert.True(unaudited.Count == 0,
            "Registered templates with no send point in this audit: " + string.Join(", ", unaudited));
    }

    /// <summary>Every audited send point names a template that still exists.</summary>
    [Fact]
    public void Every_audited_send_point_names_a_registered_template()
    {
        foreach (var point in SendPoints)
            Assert.True(SystemEmailTemplates.Find(point.TemplateCode) is not null,
                $"{point.Name} sends '{point.TemplateCode}', which is not in the registry.");
    }

    /// <summary>
    /// The audit itself: every variable the template DECLARES is a variable this send point BUILDS.
    ///
    /// <para>
    /// The six sender variables are excluded because no caller supplies them — <c>SystemEmailDispatcher</c>
    /// merges them from <see cref="IEmailSenderVariableResolver"/> for every template that declares them,
    /// which is asserted separately below. Everything else is the caller's, and a declared name the caller
    /// never writes is a send that fails at run time with "thiếu giá trị cho biến".
    /// </para>
    /// </summary>
    [Theory]
    [MemberData(nameof(AllSendPoints))]
    public void A_send_point_builds_every_variable_its_template_declares(string templateCode, string name)
    {
        var point = SendPoints.Single(p => p.TemplateCode == templateCode && p.Name == name);
        var template = SystemEmailTemplates.Find(templateCode)!;

        var mustSupply = template.DeclaredVariables
            .Where(v => !EmailSenderVariableNames.IsSenderVariable(v))
            .Where(v => !EmailTrustedBlocks.All.Contains(v, StringComparer.Ordinal))
            .ToList();

        var supplied = KeysWrittenBy(point);

        var missing = mustSupply.Where(v => !supplied.Contains(v)).OrderBy(v => v, StringComparer.Ordinal).ToList();

        Assert.True(missing.Count == 0,
            $"{templateCode} / {name}: the send point does not build {string.Join(", ", missing.Select(m => "{{" + m + "}}"))}"
            + $" (files: {string.Join(", ", point.Files)})");
    }

    /// <summary>
    /// A template whose contract carries an action area has a send point that BUILDS one.
    ///
    /// <para>
    /// The renderer substitutes `{{actionBlock}}` from the trusted blocks the caller passes, and a body
    /// that keeps the placeholder because nobody passed one does not go out at all — it fails the
    /// unresolved-placeholder guard. So "the contract says this template has buttons" and "some flow
    /// actually mints them" have to be the same set, and this is where they are compared. The block is
    /// built by a helper on <c>EmailComposition</c> in every flow, which is what the search looks for.
    /// </para>
    /// </summary>
    [Fact]
    public void Every_template_with_an_action_area_has_a_send_point_that_builds_one()
    {
        var problems = new List<string>();

        foreach (var template in SystemEmailTemplates.All)
        {
            var contract = EmailTemplateContracts.For(template.TemplateCode);
            if (contract is null || !contract.ActionSupported) continue;

            var points = SendPoints.Where(p => p.TemplateCode == template.TemplateCode).ToList();
            if (points.Count == 0) continue;      // reported by the coverage test above

            foreach (var point in points)
            {
                var source = string.Concat(point.Files.Select(f =>
                    File.ReadAllText(Path.Combine(RepositoryRoot(), f.Replace('/', Path.DirectorySeparatorChar)))));

                var buildsOne = source.Contains("EmailTrustedBlocks.ActionBlock", StringComparison.Ordinal)
                    || source.Contains("ConfirmationBlocks", StringComparison.Ordinal)
                    || source.Contains("LoginBlocks", StringComparison.Ordinal)
                    || source.Contains("TrustedBlocks:", StringComparison.Ordinal);

                if (!buildsOne)
                    problems.Add($"{template.TemplateCode} / {point.Name}: the contract declares an action area, "
                                 + "but this send point passes no trusted block to build it");
            }
        }

        Assert.True(problems.Count == 0, string.Join(" | ", problems));
    }

    /// <summary>
    /// The sender half, in one place: a template that declares the sender variables has them merged by
    /// the dispatcher, so no caller has to — and a template that does not declare them is not given them.
    /// </summary>
    [Fact]
    public void Sender_variables_are_declared_exactly_where_the_capability_allows_them()
    {
        var problems = new List<string>();

        foreach (var template in SystemEmailTemplates.All)
        {
            var allowed = EmailSenderVariableCapabilities.AllowsVariables(template.TemplateCode);
            var declared = template.DeclaredVariables.Where(EmailSenderVariableNames.IsSenderVariable).ToList();

            if (allowed && declared.Count != EmailSenderVariableNames.All.Count)
                problems.Add($"{template.TemplateCode}: capability allows sender variables but declares {declared.Count}/6");

            if (!allowed && declared.Count != 0)
                problems.Add($"{template.TemplateCode}: capability forbids sender variables but declares {string.Join(",", declared)}");
        }

        Assert.True(problems.Count == 0, string.Join(" | ", problems));
    }

    // ── Happy path, against the real seed ────────────────────────────────────

    private static string ConnString =>
        PEMS.IntegrationTests.TestInfrastructure.DisposableDatabaseManager.GetDisposableConnectionString(
            "server=localhost;port=3306;database=pems_pr3_test;user=root;password=123456;AllowUserVariables=True;GuidFormat=None");

    private static ApplicationDbContext NewContext()
        => new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseMySql(ConnString, ServerVersion.AutoDetect(ConnString)).Options);

    private static bool? _dbUp;
    private static string? _dbFailure;

    private static void RequireDb()
    {
        if (_dbUp is null)
        {
            try { using var db = NewContext(); _dbUp = db.Database.CanConnect(); }
            catch (Exception ex) { _dbUp = false; _dbFailure = ex.ToString(); }
        }

        Assert.True(_dbUp, "Disposable MySQL database is not reachable. " + _dbFailure);
    }

    /// <summary>
    /// Every seeded template, rendered with a distinct value per variable — and every one of those values
    /// has to APPEAR in the message.
    ///
    /// <para>
    /// The catalog already has a test that nothing is left wearing braces. That is the fail-closed half,
    /// and it passes just as happily on a body that prints none of its variables at all. This is the other
    /// half: the value a caller supplies for <c>{{recipientName}}</c> is the text a person reads. A
    /// template whose body stopped mentioning a declared variable — an editor deleting a chip, a sync
    /// writing an older body — fails here, with the name of the variable that went missing.
    /// </para>
    /// <para>
    /// Sender variables are exempt, and ONLY they: the registry declares all six on every capable template
    /// so an administrator can add one to a body at any time, so "declared and not printed" is the
    /// intended state for them (see the registry's own note).
    /// </para>
    /// </summary>
    [Theory]
    [InlineData(EmailLanguages.Vi)]
    [InlineData(EmailLanguages.En)]
    public async Task Every_declared_variable_reaches_the_rendered_message(string language)
    {
        RequireDb();
        using var db = NewContext();
        var renderer = new EmailTemplateRenderer(db);

        var rows = await db.EmailTemplates.AsNoTracking().OrderBy(t => t.TemplateCode).ToListAsync();
        var failures = new List<string>();

        foreach (var row in rows)
        {
            var registered = SystemEmailTemplates.Find(row.TemplateCode);
            if (registered is null) continue;

            // A recognisable value per variable, so a missing one can be named rather than merely counted.
            var values = registered.DeclaredVariables.ToDictionary(
                v => v, v => $"VALUE-OF-{v}", StringComparer.Ordinal);

            var trusted = EmailTrustedBlocks.All.ToDictionary(
                b => b, b => $"<div>BLOCK-{b}</div>", StringComparer.Ordinal);

            EmailRenderResult result;
            try
            {
                result = await renderer.RenderAsync(
                    new EmailRenderRequest(row.TemplateCode, language, values, trusted));
            }
            catch (Exception ex)
            {
                failures.Add($"{row.TemplateCode}: {ex.GetType().Name} {ex.Message}");
                continue;
            }

            var rendered = System.Net.WebUtility.HtmlDecode(result.Subject + " " + result.Body);

            foreach (var name in registered.DeclaredVariables)
            {
                if (EmailSenderVariableNames.IsSenderVariable(name)) continue;
                if (EmailTrustedBlocks.All.Contains(name, StringComparer.Ordinal)) continue;

                if (!rendered.Contains($"VALUE-OF-{name}", StringComparison.Ordinal))
                    failures.Add($"{row.TemplateCode} ({language}): {{{{{name}}}}} is declared but its value never appears");
            }

            // …and a required block's markup is where the body said it goes.
            foreach (var (block, _) in EmailTemplateContracts.RequiredBlocksFor(row.TemplateCode))
            {
                if (!result.Body.Contains($"BLOCK-{block}", StringComparison.Ordinal))
                    failures.Add($"{row.TemplateCode} ({language}): required block {{{{{block}}}}} did not render");
            }
        }

        Assert.True(failures.Count == 0, string.Join(" | ", failures));
    }
}
