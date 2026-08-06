using System;
using System.Collections.Generic;
using PEMS.Domain.Constants;

namespace PEMS.Application.Delegations.Services;

/// <summary>
/// THE single backend source of field classification for post-approval editing (plan §16.6). The frontend
/// only shows predictions; every mutation route consults this table. Field paths are stable dotted
/// identifiers used in amendment change rows and audit diffs.
///
///   • SAFE                — applies immediately (safe-edit endpoint): registrant name/org/job/phone,
///                           primary-contact name/org/phone (NEVER the email — identity workflow),
///                           transportation note, note to FPTU, media-consent note.
///   • PRIVACY_URGENT      — <c>instance.mediaConsentStatus → DECLINED</c>: applies immediately EVEN
///                           inside the 24h cutoff, with HIGH/URGENT notification.
///   • APPROVAL_SENSITIVE  — per-campus amendment (PENDING_APPROVAL until the campus Staff Leader decides):
///                           delegation name, visit type(/other), purpose, working content, working
///                           language, operational contact, guest/support member lists.
///   • STRUCTURAL          — add/remove/change campus and schedule changes: routed by lifecycle
///                           (pending-edit / cancel+add / amendment for a decided campus's schedule).
///
/// Unknown paths FAIL CLOSED — they are never silently applied anywhere.
/// </summary>
public static class VisitFieldClassifier
{
    // ── Stable field paths ──
    public const string RegistrantFullName = "request.registrant.fullName";
    public const string RegistrantOrganization = "request.registrant.organization";
    public const string RegistrantJobTitle = "request.registrant.jobTitle";
    public const string RegistrantPhone = "request.registrant.phone";
    public const string ContactFullName = "request.contact.fullName";
    public const string ContactOrganization = "request.contact.organization";
    public const string ContactPhone = "request.contact.phone";

    public const string TransportationNote = "instance.transportationNote";
    public const string MediaConsentNote = "instance.mediaConsentNote";
    public const string MediaConsentStatus = "instance.mediaConsentStatus";

    public const string DelegationName = "instance.delegationName";
    public const string VisitType = "instance.visitType";
    public const string VisitTypeOther = "instance.visitTypeOther";
    public const string Purpose = "instance.purpose";
    public const string WorkingContent = "instance.workingContent";
    public const string WorkingLanguage = "instance.workingLanguage";
    public const string OperationalContactFullName = "instance.operationalContact.fullName";
    public const string OperationalContactOrganization = "instance.operationalContact.organization";
    public const string OperationalContactJobTitle = "instance.operationalContact.jobTitle";
    public const string OperationalContactPhone = "instance.operationalContact.phone";
    public const string OperationalContactEmail = "instance.operationalContact.email";
    public const string Visitors = "instance.members.visitors";
    public const string SupportMembers = "instance.members.externalSupport";

    public const string PlannedStartAt = "instance.plannedStartAt";
    public const string PlannedEndAt = "instance.plannedEndAt";

    private static readonly Dictionary<string, string> Table = new(StringComparer.Ordinal)
    {
        [RegistrantFullName] = AmendmentChangeClasses.Safe,
        [RegistrantOrganization] = AmendmentChangeClasses.Safe,
        [RegistrantJobTitle] = AmendmentChangeClasses.Safe,
        [RegistrantPhone] = AmendmentChangeClasses.Safe,
        [ContactFullName] = AmendmentChangeClasses.Safe,
        [ContactOrganization] = AmendmentChangeClasses.Safe,
        [ContactPhone] = AmendmentChangeClasses.Safe,

        [TransportationNote] = AmendmentChangeClasses.Safe,
        [MediaConsentNote] = AmendmentChangeClasses.Safe,
        // mediaConsentStatus is special-cased in ClassifyChange (→ DECLINED = PRIVACY_URGENT).
        [MediaConsentStatus] = AmendmentChangeClasses.Safe,

        [DelegationName] = AmendmentChangeClasses.ApprovalSensitive,
        [VisitType] = AmendmentChangeClasses.ApprovalSensitive,
        [VisitTypeOther] = AmendmentChangeClasses.ApprovalSensitive,
        [Purpose] = AmendmentChangeClasses.ApprovalSensitive,
        [WorkingContent] = AmendmentChangeClasses.ApprovalSensitive,
        [WorkingLanguage] = AmendmentChangeClasses.ApprovalSensitive,
        [OperationalContactFullName] = AmendmentChangeClasses.ApprovalSensitive,
        [OperationalContactOrganization] = AmendmentChangeClasses.ApprovalSensitive,
        [OperationalContactJobTitle] = AmendmentChangeClasses.ApprovalSensitive,
        [OperationalContactPhone] = AmendmentChangeClasses.ApprovalSensitive,
        [OperationalContactEmail] = AmendmentChangeClasses.ApprovalSensitive,
        [Visitors] = AmendmentChangeClasses.ApprovalSensitive,
        [SupportMembers] = AmendmentChangeClasses.ApprovalSensitive,

        [PlannedStartAt] = AmendmentChangeClasses.Structural,
        [PlannedEndAt] = AmendmentChangeClasses.Structural,
    };

    /// <summary>True when the path exists in the classification table at all.</summary>
    public static bool IsKnown(string fieldPath) => Table.ContainsKey(fieldPath);

    /// <summary>
    /// Classifies a concrete change. Unknown paths return null — callers MUST fail closed on null.
    /// <c>mediaConsentStatus → DECLINED</c> upgrades to PRIVACY_URGENT (withdrawal applies even &lt;24h);
    /// every other transition of that field is a normal SAFE edit.
    /// </summary>
    public static string? ClassifyChange(string fieldPath, string? oldValue, string? newValue)
    {
        if (!Table.TryGetValue(fieldPath, out var cls))
            return null;
        if (fieldPath == MediaConsentStatus
            && string.Equals(newValue, "DECLINED", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(oldValue, "DECLINED", StringComparison.OrdinalIgnoreCase))
            return AmendmentChangeClasses.PrivacyUrgent;
        return cls;
    }

    /// <summary>Paths a per-campus AMENDMENT may propose (approval-sensitive + decided-campus schedule).</summary>
    public static bool IsAmendable(string fieldPath)
        => Table.TryGetValue(fieldPath, out var cls)
           && (cls == AmendmentChangeClasses.ApprovalSensitive || cls == AmendmentChangeClasses.Structural);

    /// <summary>Paths the SAFE-EDIT endpoint may apply (safe + the privacy-urgent media withdrawal).</summary>
    public static bool IsSafeEditable(string fieldPath)
        => Table.TryGetValue(fieldPath, out var cls) && cls == AmendmentChangeClasses.Safe;
}
