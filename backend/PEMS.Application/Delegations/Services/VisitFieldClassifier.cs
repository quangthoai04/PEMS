using System;
using System.Collections.Generic;
using PEMS.Domain.Constants;

namespace PEMS.Application.Delegations.Services;

/// <summary>
/// THE single backend source of field classification for GENERIC post-approval editing (plan §16.6). The
/// frontend only shows predictions; every mutation route that answers to this table consults it. Field
/// paths are stable dotted identifiers used in amendment change rows and audit diffs.
///
///   • SAFE                — applies immediately (safe-edit endpoint): registrant name/nationality/
///                           org/partner/job/phone, transportation note, note to FPTU, media-consent
///                           note.
///   • PRIVACY_URGENT      — <c>instance.mediaConsentStatus → DECLINED</c>: applies immediately EVEN
///                           inside the 24h cutoff, with HIGH/URGENT notification.
///   • APPROVAL_SENSITIVE  — per-campus amendment (PENDING_APPROVAL until the campus Staff Leader decides):
///                           delegation name, visit type(/other), purpose, working content, working
///                           language, guest/support member lists. The operational contact's PROFILE and
///                           relation fields are ALSO listed here, but ONLY for backward compatibility
///                           with already-persisted amendments from before plan CanhIter3FixBug — see
///                           the four <c>OperationalContact*</c> constants' own doc comments below.
///   • STRUCTURAL          — add/remove/change campus and schedule changes: routed by lifecycle
///                           (pending-edit / cancel+add / amendment for a decided campus's schedule).
///
/// Unknown paths FAIL CLOSED — they are never silently applied anywhere.
///
/// <para>
/// Same-person operational-contact metadata (FullName/Organization/JobTitle/Phone) and relation
/// (which delegation member the contact IS) are a SEPARATE, dedicated domain mutation, applied directly
/// by <c>VisitSafeEditService</c> — they intentionally never pass through this classifier or its
/// SAFE/PRIVACY_URGENT gate at all (plan CanhIter3FixBug). They stay classified <c>ApprovalSensitive</c>
/// here purely so an amendment proposed before that plan and still <c>PENDING_APPROVAL</c> remains
/// approvable; no NEW amendment proposal writes a change row on any of these four fields any more. A
/// genuine change of WHO the contact is (a different person, not a correction) goes through Replace or
/// Transfer instead, neither of which consults this table at all.
/// </para>
/// </summary>
public static class VisitFieldClassifier
{
    // ── Stable field paths ──
    public const string RegistrantFullName = "request.registrant.fullName";
    public const string RegistrantOrganization = "request.registrant.organization";
    public const string RegistrantNationality = "request.registrant.nationality";
    /// <summary>
    /// The partner profile <see cref="RegistrantOrganization"/> was picked from, or null for free
    /// text. A path OF ITS OWN — rather than folded silently into RegistrantOrganization — so it gets
    /// its own audit-trail row; it is still applied atomically alongside Organization by the service.
    /// </summary>
    public const string RegistrantPartnerId = "request.registrant.partnerId";
    public const string RegistrantJobTitle = "request.registrant.jobTitle";
    public const string RegistrantPhone = "request.registrant.phone";

    public const string TransportationNote = "instance.transportationNote";
    public const string Notes = "instance.notes";
    public const string MediaConsentStatus = "instance.mediaConsentStatus";

    public const string DelegationName = "instance.delegationName";
    public const string VisitType = "instance.visitType";
    public const string VisitTypeOther = "instance.visitTypeOther";
    public const string Purpose = "instance.purpose";
    public const string WorkingContent = "instance.workingContent";
    public const string WorkingLanguage = "instance.workingLanguage";
    /// <summary>
    /// LEGACY paths only (plan CanhIter3FixBug). Kept in the table so a proposal already sitting in
    /// PENDING_APPROVAL from before the contact profile moved into Safe Edit is still approvable — see
    /// the switch in <c>VisitAmendmentService.ApproveAsync</c>. <c>VisitAmendmentService.BuildChangeRows</c>
    /// never writes a NEW row on any of these four paths any more; the contact's name/organisation/job
    /// title/phone are corrected through Sửa nhanh (<c>VisitSafeEditService</c>'s dedicated contact
    /// branch), which applies them directly and never creates an amendment.
    /// </summary>
    public const string OperationalContactFullName = "instance.operationalContact.fullName";
    public const string OperationalContactOrganization = "instance.operationalContact.organization";
    public const string OperationalContactJobTitle = "instance.operationalContact.jobTitle";
    public const string OperationalContactPhone = "instance.operationalContact.phone";
    public const string OperationalContactEmail = "instance.operationalContact.email";
    public const string Visitors = "instance.members.visitors";
    public const string SupportMembers = "instance.members.externalSupport";
    /// <summary>
    /// Durable "who in the delegation the operational contact IS" reference (NP-03), amendment-only.
    /// Carried alongside a member-list change so the backend can re-point the contact link at the
    /// right NEW member row after a replace, instead of falling back to a name/org/title guess.
    ///
    /// <para>
    /// EPHEMERAL-identity path only: the value is a <c>ClientMemberKey</c> minted fresh by the
    /// submitting form and never persisted, so this path only makes sense in the SAME amendment that
    /// also replaces the member list — there is no other way to resolve a fresh key to a row. When the
    /// member list is untouched, use <see cref="OperationalContactGuestMemberId"/> instead, which names
    /// the target by its stable database id.
    /// </para>
    /// </summary>
    public const string OperationalContactMemberKey = "instance.operationalContact.clientMemberKey";

    /// <summary>
    /// PERSISTENT-identity counterpart to <see cref="OperationalContactMemberKey"/> (plan
    /// CanhIter3FixBug "Đầu mối hiện tại có nằm trong danh sách đoàn không?"), amendment-only. Used
    /// exactly when the visitor/support lists are UNCHANGED: every row already has a stable
    /// <c>GuestMemberId</c>, so "which delegation member is the contact" is itself a real business
    /// fact that can change on its own, independent of any member-list edit — recording it against the
    /// ephemeral client-key path would either silently no-op (nothing else changed, so
    /// <c>changes.Count == 0</c>) or force a pointless full member-list replace just to carry a key.
    /// Never written in the SAME amendment as <see cref="Visitors"/>/<see cref="SupportMembers"/> —
    /// see <c>VisitAmendmentService.BuildChangeRows</c>, which picks exactly one of the two paths.
    /// </summary>
    public const string OperationalContactGuestMemberId = "instance.operationalContact.guestMemberId";

    public const string PlannedStartAt = "instance.plannedStartAt";
    public const string PlannedEndAt = "instance.plannedEndAt";

    private static readonly Dictionary<string, string> Table = new(StringComparer.Ordinal)
    {
        [RegistrantFullName] = AmendmentChangeClasses.Safe,
        [RegistrantOrganization] = AmendmentChangeClasses.Safe,
        [RegistrantNationality] = AmendmentChangeClasses.Safe,
        [RegistrantPartnerId] = AmendmentChangeClasses.Safe,
        [RegistrantJobTitle] = AmendmentChangeClasses.Safe,
        [RegistrantPhone] = AmendmentChangeClasses.Safe,

        [TransportationNote] = AmendmentChangeClasses.Safe,
        [Notes] = AmendmentChangeClasses.Safe,
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
        // OperationalContactEmail is deliberately ABSENT — see IsIdentityManaged below.
        [Visitors] = AmendmentChangeClasses.ApprovalSensitive,
        [SupportMembers] = AmendmentChangeClasses.ApprovalSensitive,
        [OperationalContactMemberKey] = AmendmentChangeClasses.ApprovalSensitive,
        [OperationalContactGuestMemberId] = AmendmentChangeClasses.ApprovalSensitive,

        [PlannedStartAt] = AmendmentChangeClasses.Structural,
        [PlannedEndAt] = AmendmentChangeClasses.Structural,
    };

    /// <summary>
    /// Fields that identify WHO the contact is, rather than describing them. They belong to the
    /// contact-management workflow — invite, confirm, transfer, resend, cancel — and to no other path.
    ///
    /// <para>
    /// The address was classified APPROVAL_SENSITIVE, which made it amendable: an amendment could set
    /// <c>operational_contact_email</c> directly on approval, handing a campus to a different person
    /// with no invitation, no acceptance and no identity event. That is a second door onto the same
    /// identity, and the two doors did not agree — one asks the new person to accept, the other does
    /// not ask anybody. Name, organisation, job title and phone remain amendable: correcting how a
    /// person is described does not change which person it is.
    /// </para>
    /// </summary>
    private static readonly HashSet<string> IdentityManaged = new(StringComparer.Ordinal)
    {
        OperationalContactEmail,
    };

    /// <summary>True when the field may only move through the contact-identity workflow.</summary>
    public static bool IsIdentityManaged(string fieldPath) => IdentityManaged.Contains(fieldPath);

    /// <summary>True when the path exists in the classification table at all.</summary>
    public static bool IsKnown(string fieldPath) => Table.ContainsKey(fieldPath) || IdentityManaged.Contains(fieldPath);

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
