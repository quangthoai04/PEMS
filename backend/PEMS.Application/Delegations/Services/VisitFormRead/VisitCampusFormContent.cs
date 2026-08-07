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
}

/// <summary>A guest / external-support member row, already resolved to the campus that should show it.</summary>
public sealed record VisitFormMemberRow(
    long GuestMemberId,
    string MemberType,
    string FullName,
    string? Organization,
    string? JobTitle,
    string? Nationality,
    int DisplayOrder);
