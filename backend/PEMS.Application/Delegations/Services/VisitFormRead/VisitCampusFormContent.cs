using System;
using System.Collections.Generic;

namespace PEMS.Application.Delegations.Services.VisitFormRead;

/// <summary>
/// The version-resolved FORM CONTENT of a single visit campus instance — the half of a read model that
/// differs between form_schema_version 1 and 2. A handler owns its own scope / decision / schedule /
/// cancellation metadata (those columns live on visit_requests / visit_request_campuses in BOTH
/// versions) and calls <see cref="IVisitFormReadService.ResolveCampusFormContentAsync"/> for this part,
/// so the dual-read rule is applied in exactly one place:
///   v1 → the global compatibility projection on visit_requests (identical for every instance);
///   v2 → ONLY visit_instance_form_details + visit_instance_guest_members (never the global fields).
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
    public string? MediaConsentNote { get; init; }
    public string? TransportationNote { get; init; }
    public string? NoteToFptu { get; init; }

    /// <summary>v1 → request-level primary contact projection; v2 → the per-campus operational contact.</summary>
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
