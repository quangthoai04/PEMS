using System;
using System.Collections.Generic;

namespace PEMS.Application.Delegations.Services.VisitFormRead;

/// <summary>
/// The FORM CONTENT of a single visit campus instance — the half of a read model that belongs to the
/// campus rather than to the request. A handler owns its own scope / decision / schedule / cancellation
/// metadata (those columns live on visit_requests / visit_request_campuses) and calls
/// <see cref="IVisitFormReadService.ResolveCampusFormContentAsync"/> for this part, so the per-campus
/// rule is applied in exactly one place: content comes from that instance's own
/// visit_instance_form_details row and visit_instance_guest_members links, and from nowhere else.
/// </summary>
public sealed class VisitCampusFormContent
{
    public string DelegationName { get; init; } = "";
    public string? VisitType { get; init; }
    public string? VisitTypeOther { get; init; }
    public string? Purpose { get; init; }
    public string? WorkingContent { get; init; }
    public string? WorkingLanguage { get; init; }
    public string? MediaConsentStatus { get; init; }
    public string? TransportationNote { get; init; }

    /// <summary>"Ghi chú gửi FPTU" — this campus's one general remark, independent of media consent.</summary>
    public string? Notes { get; init; }

    /// <summary>
    /// This campus's OPERATIONAL contact — the only contact there is. It grants nothing on a sibling
    /// campus, and it must never be filled from one.
    /// </summary>
    public VisitFormOperationalContact OperationalContact { get; init; } = new();

    public IReadOnlyList<VisitFormMemberRow> Visitors { get; init; } = Array.Empty<VisitFormMemberRow>();
    public IReadOnlyList<VisitFormMemberRow> SupportMembers { get; init; } = Array.Empty<VisitFormMemberRow>();

    public uint FormRevision { get; init; }
    public uint ApprovalRevision { get; init; }
    public int RowVersion { get; init; }
}

public sealed class VisitFormOperationalContact
{
    public string? FullName { get; init; }
    public string? Organization { get; init; }
    public string? JobTitle { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
    /// <summary>
    /// True only when this contact IS a delegation member (NP-03 — see
    /// <c>VisitInstanceFormDetail.OperationalContactGuestMemberId</c>) AND that member's own
    /// <c>OrganizationPartnerId</c> is set. Never derived from the <see cref="Organization"/> text —
    /// a name coincidentally matching a Partner proves nothing about this contact.
    /// </summary>
    public bool IsOrganizationInSystem { get; init; }
}

/// <summary>A guest / external-support member row, already resolved to the campus that should show it.</summary>
/// <param name="OrganizationPartnerId">
/// The partner profile this member was actually picked from, or null for free text. Carried through
/// the read path so an edit round-trip cannot silently drop the identity the registrant chose
/// (PART-01). Defaulted so existing positional constructions stay valid.
/// </param>
public sealed record VisitFormMemberRow(
    long GuestMemberId,
    string MemberType,
    string FullName,
    string? Organization,
    string? JobTitle,
    string? Nationality,
    int DisplayOrder,
    ulong? OrganizationPartnerId = null);
