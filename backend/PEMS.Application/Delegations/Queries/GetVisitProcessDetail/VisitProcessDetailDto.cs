using PEMS.Application.Delegations.Queries.GetVisitInstanceParticipants;

namespace PEMS.Application.Delegations.Queries.GetVisitProcessDetail;

public sealed class VisitProcessDetailDto
{
    public ulong VisitRequestId { get; set; }
    public ulong VisitInstanceId { get; set; }
    public string DelegationName { get; set; } = default!;
    public string InstanceStatus { get; set; } = default!;
    public DateTime PlannedStartAt { get; set; }
    public DateTime PlannedEndAt { get; set; }
    public string? CampusName { get; set; }
    public ulong? HostUserId { get; set; }
    public string? HostName { get; set; }

    /// <summary>HOST | STAFF_LEADER | HO | VISITOR_OWNER | IC_SUPPORT | DEPT_SUPPORT | STUDENT | NONE.</summary>
    public string Relation { get; set; } = "NONE";

    /// <summary>True only for the official host while the instance is in the editable prep window.</summary>
    public bool CanEditBefore { get; set; }

    /// <summary>Host's internal preparation note (visit_request_campuses.preparation_note). Bound to the
    /// "Ghi chú chung" textarea. Null/empty when not set.</summary>
    public string? PreparationNote { get; set; }

    public List<AgendaItemDto> Agenda { get; set; } = new();

    /// <summary>Read-only snapshot of the guest's original registration (registrant + delegation +
    /// campuses + guest members) so the host sees real submitted data, never hard-coded demo values.
    /// Null only for callers not allowed to see it (and never crashes the FE when absent).</summary>
    public VisitProcessRequestSummaryDto? RequestSummary { get; set; }

    /// <summary>The assigned official host (read-only — never changed from this screen). Null when no
    /// host has been assigned to the instance yet.</summary>
    public VisitProcessHostDto? Host { get; set; }

    /// <summary>Host snapshot + invited supporters of this instance. Empty for the guest/visitor
    /// owner (who must not see the internal process data).</summary>
    public List<VisitParticipantListItemDto> Participants { get; set; } = new();

    /// <summary>Notifications related to this visit instance or its parent request, only loaded for the VISITOR_OWNER.</summary>
    public List<VisitorNotificationDto> Notifications { get; set; } = new();

    /// <summary>Public published news related to this visit instance, only loaded for the VISITOR_OWNER.</summary>
    public List<VisitorPublicNewsListItemDto> PublicNews { get; set; } = new();
}

public sealed class VisitorPublicNewsListItemDto
{
    public ulong NewsId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string? Slug { get; set; }
    public DateTime? PublishedAt { get; set; }
    public string? AuthorName { get; set; }
}

public sealed class VisitorNotificationDto
{
    public ulong NotificationId { get; set; }
    public string Title { get; set; } = default!;
    public string? Message { get; set; }
    public string NotificationType { get; set; } = default!;
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>Mirror of the guest-submitted form, shown read-only on the process screen.</summary>
public sealed class VisitProcessRequestSummaryDto
{
    public string? RegistrantName { get; set; }
    public string? RegistrantEmail { get; set; }
    public string? RegistrantPhone { get; set; }
    public string? RegistrantOrganization { get; set; }
    public string? RegistrantJobTitle { get; set; }
    public string? RegistrantNationality { get; set; }

    public string DelegationName { get; set; } = default!;
    public string VisitScope { get; set; } = default!;       // SINGLE_CAMPUS | MULTI_CAMPUS
    public string? VisitType { get; set; }                   // CAMPUS_TOUR | MEETING | ... | OTHER
    public string? VisitTypeOther { get; set; }
    public string? Purpose { get; set; }
    public string? WorkingContent { get; set; }
    public string? WorkingLanguage { get; set; }
    public string? MediaConsentStatus { get; set; }
    public string? MediaConsentNote { get; set; }
    public string? TransportationType { get; set; }
    public string? TransportationDetail { get; set; }
    public string? NoteToFptu { get; set; }

    public string? ContactPersonFullName { get; set; }
    public string? ContactPersonOrganization { get; set; }
    public string? ContactPersonPhone { get; set; }
    public string? ContactPersonEmail { get; set; }

    public List<VisitProcessCampusDto> Campuses { get; set; } = new();
    public List<VisitProcessGuestMemberDto> GuestMembers { get; set; } = new();
    public List<VisitProcessGuestMemberDto> ExternalSupportMembers { get; set; } = new();
}

public sealed class VisitProcessCampusDto
{
    public ulong VisitInstanceId { get; set; }
    public ulong CampusId { get; set; }
    public string CampusName { get; set; } = default!;
    public DateTime PlannedStartAt { get; set; }
    public DateTime PlannedEndAt { get; set; }
    /// <summary>True for the campus instance currently being processed.</summary>
    public bool IsCurrent { get; set; }
}

public sealed class VisitProcessGuestMemberDto
{
    public ulong GuestMemberId { get; set; }
    public string MemberType { get; set; } = default!;       // GUEST | EXTERNAL_SUPPORT
    public string FullName { get; set; } = default!;
    public string? Organization { get; set; }
    public string? JobTitle { get; set; }
    public string? Nationality { get; set; }
    public int DisplayOrder { get; set; }
}

public sealed class VisitProcessHostDto
{
    public ulong UserId { get; set; }
    public string FullName { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string? Phone { get; set; }
    public string? DepartmentName { get; set; }
    /// <summary>Display-only status label (e.g. "Đã được phân công").</summary>
    public string StatusLabel { get; set; } = "Đã được phân công";
}

public sealed class AgendaItemDto
{
    public ulong AgendaId { get; set; }
    public string Title { get; set; } = default!;
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string? Description { get; set; }
    public string? Location { get; set; }

    /// <summary>Set when this row was generated by applying an agenda template; the source template is
    /// traced via visit_agendas → agenda_template_items → agenda_templates. Null for manual rows.</summary>
    public ulong? SourceTemplateItemId { get; set; }

    /// <summary>The concrete user assigned to run this agenda item (visit_agendas.responsible_user_id).
    /// Null when unassigned. This is a REAL user — distinct from the template's suggested role label.</summary>
    public ulong? ResponsibleUserId { get; set; }
    public string? ResponsibleUserName { get; set; }
    public string? ResponsibleUserEmail { get; set; }

    /// <summary>The suggested role text from the source template item (agenda_template_items.
    /// responsible_role_label), e.g. "IC Host". Display-only hint; NOT a person. Null for manual rows
    /// or when the source template item has no label.</summary>
    public string? TemplateResponsibleRoleLabel { get; set; }
}
