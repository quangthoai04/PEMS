using System;
using System.Collections.Generic;
using System.Linq;
using PEMS.Application.Common.Interfaces;

namespace PEMS.Application.Emails.Common;

/// <summary>
/// Which template placeholders carry a secret, stated once for the whole system.
///
/// <para>
/// A "secret" here means something that grants access on its own if a third party reads it: a one-time
/// code, a raw token, or a URL with a token in it. It is not the same thing as "private": a person's
/// name or a delegation title may well be confidential, but reading it does not let anybody reset a
/// password, confirm somebody else's account or accept an invitation in their name.
/// </para>
/// <para>
/// This classification decides two things. The dispatcher does not store the body of a template that
/// carries one (<see cref="SensitiveEmailHistory"/>), and the renderer refuses a SUBJECT that would
/// interpolate one — a subject IS stored, so a hot-edit that moved <c>{{otpCode}}</c> into it would put
/// the code straight back into <c>sent_emails</c>, the history screen, and every backup.
/// </para>
/// </summary>
public static class SensitiveEmailVariables
{
    /// <summary>
    /// Declared variables whose VALUE is a credential. Kept explicit rather than pattern-matched on the
    /// name: <c>requestCode</c> is a reference people quote over the phone, <c>otpCode</c> is a secret,
    /// and no naming rule tells those two apart.
    /// </summary>
    public static readonly IReadOnlySet<string> Names =
        new HashSet<string>(StringComparer.Ordinal) { "otpCode" };

    /// <summary>
    /// Every declared variable in the registry that is NOT a credential. The union of this set and
    /// <see cref="Names"/> must cover the whole registry, which is what forces a new variable to be
    /// classified deliberately instead of defaulting to "safe" (see the contract test).
    /// </summary>
    public static readonly IReadOnlySet<string> KnownNonSensitive =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "assigneeName", "campusName", "contactFullName", "coordinationNote", "currentContactName",
            "delegationName", "departmentLeaderName", "departmentName", "dueAt", "effectiveDate",
            // hostEmail is gone: the Host's address is no longer a template variable at all. It now
            // arrives inside {{contactInformationBlock}}, resolved from the visit instance. The
            // classification it used to carry still holds for the block's contents — a work address the
            // guest is being told to write to authorises nothing, so it may stay in the stored body and
            // in the email history — but there is no variable left here to classify.
            "expireMinutes", "expiresInHours", "fullName", "hostMessage", "hostName", "itemTitle",
            "logisticsItemType", "logisticsTitle", "newRoleName", "oldEmailMasked", "oldRoleName",
            "originalQuantity", "periodFrom", "periodTo", "personName", "plannedEnd", "plannedStart",
            "plannedTime",
            // The change-proposal counter-offer. These are business figures the Host is being asked to
            // approve — the same kind of data as `quantity` and `usageStartAt` above, which they sit
            // beside in the message. Reading one grants nothing: it authorises no action and unlocks no
            // account, so the proposal email keeps a stored body and remains readable in the history.
            "proposalNote", "proposedDescription", "proposedQuantity", "proposedUsageEndAt",
            "proposedUsageStartAt",
            "quantity", "reason", "recipientName", "requestCode", "requesterName",
            "roleLabel", "roleName", "scopeLabel", "successorName", "usageEndAt", "usageStartAt",
        };

    /// <summary>
    /// True when a placeholder must never appear in a subject: a credential variable, or ANY trusted
    /// block. Trusted blocks are the only route by which markup — and therefore a one-time URL — enters
    /// a rendered message, so a subject that interpolates one is storing a live link by construction.
    /// </summary>
    public static bool ForbiddenInSubject(string placeholderName)
        => Names.Contains(placeholderName) || EmailTrustedBlocks.All.Contains(placeholderName);

    /// <summary>The credential variables a given template declares (empty for most templates).</summary>
    public static IReadOnlyList<string> DeclaredBy(SystemEmailTemplate template)
        => template.DeclaredVariables.Where(Names.Contains).ToList();

    /// <summary>
    /// True when the template carries a secret at all — either through a credential variable or through
    /// a trusted block holding a one-time link. This is what <c>HasSensitiveAction</c> has to agree with.
    /// </summary>
    public static bool CarriesSecret(SystemEmailTemplate template)
        => DeclaredBy(template).Count > 0 || template.HasSensitiveAction;
}
