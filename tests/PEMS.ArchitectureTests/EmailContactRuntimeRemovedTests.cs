using System.Reflection;
using Xunit;

namespace PEMS.ArchitectureTests;

/// <summary>
/// The email CONTACT architecture stays gone (V4 §2, acceptance 1–4).
///
/// <para>
/// It was removed because it could not be made correct. <c>{{contactInformationBlock}}</c> was markup
/// the backend appended, describing a configured THIRD PARTY rather than the person sending the message,
/// so an operational preview had to return the block separately and ask the client not to merge it — and
/// a host who edited a body containing the stand-in card sent the stand-in back as authored content
/// while the dispatcher appended the real card underneath. Sender VARIABLES replaced it: they are
/// values, so there is one body, it already reads correctly, and editing it cannot duplicate anything.
/// </para>
/// <para>
/// A grep proves nothing durable — the names come back one handler at a time, and the first one looks
/// harmless. This asserts against the compiled assemblies, so a reintroduced service, query or DTO fails
/// the build regardless of what it is called in a comment.
/// </para>
/// </summary>
public class EmailContactRuntimeRemovedTests
{
    private static readonly Assembly[] ProductionAssemblies =
    {
        Assembly.Load("PEMS.Application"),
        Assembly.Load("PEMS.Api"),
        Assembly.Load("PEMS.Infrastructure"),
    };

    /// <summary>
    /// Type names that belonged to the email contact architecture. Matched as substrings, because the
    /// shape that comes back is rarely spelled the same way twice.
    /// </summary>
    private static readonly string[] Forbidden =
    {
        "EmailContactPolicy",
        "EmailContactOverride",
        "EmailContactCandidate",
        "EmailContactBlock",
        "EmailContactEnum",
        "ResolveEmailContactPreview",
        "IEmailContactCandidateService",
        "ContactInformationBlock",
    };

    /// <summary>
    /// The visit-side contact features, which are a DIFFERENT thing and must not be caught by this net.
    ///
    /// <para>
    /// A visit has a guest Primary Contact — the person at the visiting organisation PEMS talks to — and
    /// two flows for claiming and transferring that role. None of it has anything to do with who appears
    /// at the bottom of an email; deleting it in the name of this cleanup would remove real business
    /// behaviour. They are listed by exact name rather than by namespace so the exemption cannot quietly
    /// widen to cover a reintroduced email-contact type sitting next to them.
    /// </para>
    /// </summary>
    private static readonly string[] Allowed =
    {
        "VisitContactClaim",
        "VisitContactTransfer",
        "PrimaryContact",
        "VisitRequestContact",
        "CampusContact",
    };

    private static bool IsAllowed(string typeName) =>
        Allowed.Any(a => typeName.Contains(a, StringComparison.Ordinal));

    [Fact]
    public void No_email_contact_type_exists_in_production_code()
    {
        var offenders = new List<string>();

        foreach (var assembly in ProductionAssemblies)
        {
            foreach (var type in assembly.GetTypes())
            {
                var name = type.FullName ?? type.Name;
                if (IsAllowed(name)) continue;

                foreach (var forbidden in Forbidden)
                {
                    if (name.Contains(forbidden, StringComparison.OrdinalIgnoreCase))
                    {
                        offenders.Add($"{assembly.GetName().Name}: {name} (matched {forbidden})");
                    }
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "The email contact architecture is back. Email identifies its SENDER through sender "
            + "variables — {{senderName}}, {{senderRole}}, {{senderEmail}}, {{senderPhone}}, "
            + "{{senderDepartment}}, {{senderCampus}} — resolved from the signed-in account. "
            + "Offending types: " + string.Join(", ", offenders.OrderBy(o => o)));
    }

    /// <summary>
    /// No route serves the removed contact preview/candidate endpoints.
    ///
    /// <para>
    /// Checked separately from the type scan because a route can be reintroduced on an existing
    /// controller without any new type at all — which is exactly how a deleted endpoint tends to come
    /// back.
    /// </para>
    /// </summary>
    [Fact]
    public void No_controller_serves_a_contact_preview_or_candidate_route()
    {
        var offenders = Assembly.Load("PEMS.Api").GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.Name.EndsWith("Controller", StringComparison.Ordinal))
            .SelectMany(controller => controller
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .SelectMany(m => m.GetCustomAttributes()
                    .OfType<Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute>()
                    .Select(a => (Owner: $"{controller.Name}.{m.Name}", Template: a.Template ?? string.Empty))))
            .Where(r =>
                r.Template.Contains("contact-preview", StringComparison.OrdinalIgnoreCase)
                || r.Template.Contains("contact-candidates", StringComparison.OrdinalIgnoreCase)
                || r.Template.Contains("email-contact", StringComparison.OrdinalIgnoreCase))
            .Select(r => $"{r.Owner} -> {r.Template}")
            .OrderBy(x => x)
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "These routes belong to the removed email contact architecture: " + string.Join(", ", offenders));
    }

    /// <summary>
    /// …and the replacement really is present, so the two tests above cannot both pass on an email
    /// system that resolves nobody at all.
    /// </summary>
    [Fact]
    public void The_sender_variable_resolver_is_what_replaced_it()
    {
        var application = Assembly.Load("PEMS.Application");

        var resolver = application.GetTypes()
            .SingleOrDefault(t => t.Name == "IEmailSenderVariableResolver");

        Assert.True(resolver is not null,
            "IEmailSenderVariableResolver is missing — email has no way to say who sent it.");

        var variables = application.GetTypes().SingleOrDefault(t => t.Name == "EmailSenderVariableNames");
        Assert.True(variables is not null, "EmailSenderVariableNames is missing.");
    }
}
