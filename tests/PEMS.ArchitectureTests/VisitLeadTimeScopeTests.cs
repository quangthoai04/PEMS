using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace PEMS.ArchitectureTests;

/// <summary>
/// The 72-hour rule belongs to REGISTRATION, and to nothing else.
///
/// <para>
/// PEMS carries two lead times that are easy to mistake for one, and mixing them has already caused a
/// defect once:
/// </para>
/// <list type="bullet">
/// <item><description><c>VisitMutationPolicy.MinScheduleLeadHours</c> (72h) — the floor on a schedule
/// being FILED FOR APPROVAL. It exists so a Staff Leader always has three days' notice on a request
/// they are being asked to decide, and it belongs to exactly three write paths: create, pre-approval
/// edit, and resubmit-after-rejection.</description></item>
/// <item><description><c>VisitMutationPolicy.RequiredLeadHours</c> (6h) — how late a self-service
/// action stays open on a request that is ALREADY DECIDED. This is what an amendment, a safe edit and
/// a host handover answer to.</description></item>
/// </list>
///
/// <para>
/// The consequence of confusing them is concrete and user-visible: a visit approved for tomorrow could
/// no longer be amended at all, because a rule about how far ahead one must BOOK would have been asked
/// about a booking that already exists. An amendment two days before a visit is normal business.
/// </para>
///
/// <para>
/// This test is a SOURCE-TEXT guard, unusual for this project and deliberate: the rule it protects is
/// "this constant is not referenced from over there", which no type-level assertion can express — a
/// handler that read the wrong constant would have identical dependencies to one that read the right
/// one. It fails the moment somebody reaches for the registration floor from an amendment, a safe
/// edit, a host transfer or the operational-contact workflow, which is precisely when a reviewer needs
/// to be stopped and asked whether they meant the six-hour cutoff instead.
/// </para>
/// </summary>
public class VisitLeadTimeScopeTests
{
    private const string RegistrationFloor = "MinScheduleLeadHours";

    /// <summary>The only three write paths that file a schedule for approval.</summary>
    private static readonly string[] AllowedFiles =
    {
        "VisitMutationPolicy.cs",              // where it is declared
        "VisitRequestV2CreateService.cs",      // create
        "VisitRequestV2EditService.cs",        // pre-approval edit + resubmit
    };

    /// <summary>
    /// Post-approval and identity flows, named one by one rather than caught by a pattern: the point is
    /// to state which files this rule must never appear in, so a new one is a deliberate addition here
    /// rather than something a wildcard quietly covered.
    /// </summary>
    private static readonly string[] MustNotReference =
    {
        "VisitAmendmentService.cs",
        "VisitSafeEditService.cs",
        "OperationalContactInvitationService.cs",
        "OperationalContactMaintenanceService.cs",
        // The contact-management workflow, every branch of it (repair v3 §12). Correcting a phone
        // number on a campus that starts tomorrow is the case this protects: the registration floor is
        // about when a visit may be SCHEDULED, and asking it here would refuse the correction at
        // exactly the moment it matters most.
        "SaveOperationalContactCommandHandler.cs",
        "UpdateOperationalContactProfileCommandHandler.cs",
        "ReplaceOperationalContactCommandHandler.cs",
        "InitiateOperationalContactTransferCommandHandler.cs",
        "AcceptOperationalContactConfirmationCommandHandler.cs",
        "DeclineOperationalContactConfirmationCommandHandler.cs",
        "ResendOperationalContactConfirmationCommandHandler.cs",
        "ManageOperationalContactHandlers.cs",
        "OperationalContactGuards.cs",
    };

    private static string BackendRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "backend")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "backend");
    }

    private static IEnumerable<string> BackendSources() =>
        Directory.EnumerateFiles(BackendRoot(), "*.cs", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));

    [Fact]
    public void The_registration_lead_time_is_referenced_only_by_the_three_submission_paths()
    {
        var strays = BackendSources()
            .Where(p => File.ReadAllText(p).Contains(RegistrationFloor, StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .Where(name => !AllowedFiles.Contains(name, StringComparer.Ordinal))
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        Assert.True(strays.Count == 0,
            $"{RegistrationFloor} is the 72h floor on a schedule being FILED FOR APPROVAL — create, "
            + "pre-approval edit and resubmit only. A post-approval action (amendment, safe edit, host "
            + "handover) answers to VisitMutationPolicy.RequiredLeadHours instead; refusing one for "
            + "'not 72 hours away' would block a change to a visit that is already happening. "
            + "Unexpected references in: " + string.Join(", ", strays));
    }

    [Fact]
    public void Post_approval_and_contact_flows_do_not_read_the_registration_lead_time()
    {
        var offenders = BackendSources()
            .Where(p => MustNotReference.Contains(Path.GetFileName(p), StringComparer.Ordinal))
            .Where(p => File.ReadAllText(p).Contains(RegistrationFloor, StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        Assert.True(offenders.Count == 0,
            "An amendment, a safe edit or a contact invitation must never be refused because the visit "
            + "is less than 72 hours away — each has its own policy. Offenders: "
            + string.Join(", ", offenders));
    }
}
