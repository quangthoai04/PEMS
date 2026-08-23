using MediatR;
using PEMS.Application.Common.Models;
using System.Collections.Generic;

namespace PEMS.Application.Delegations.Queries.ViewGuestDelegationList;

public class ViewGuestDelegationListQuery : IRequest<PaginatedResult<VisitRequestManagementItemDto>>
{
    /// <summary>
    /// "responsible" (default) = Tab "Đơn phụ trách" — requests the user is responsible for
    /// (created, approves, hosts, or is assigned a task on), scoped by role.
    /// "attending" = Tab "Đơn mời tham dự" — requests the user has ACCEPTED a participation
    /// invitation for. Not available to Visitor/Admin.
    /// </summary>
    public string? Tab { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 10;
    public string? Keyword { get; init; }
    /// <summary>
    /// Notification deep-link target resolution: return only this request, ignoring status/date/
    /// keyword filters (but NOT tab or authorization scoping — a caller with no relation to it still
    /// gets an empty result, same as any other row they cannot see). Population/authorization are
    /// unaffected; this only narrows WHICH request within them.
    /// </summary>
    public ulong? VisitRequestId { get; init; }
    public string? RequestStatus { get; init; }
    public string? CampusStatus { get; init; }
    /// <summary>
    /// Canonical status filter (see <c>VisitRowLabels.EffectiveCode</c>/<c>EffectiveStatusCodes</c>) —
    /// matches a row on the SAME code its badge (<c>StatusLabel</c>/<c>EffectiveStatusCode</c>) was
    /// resolved from, so a row can never pass this filter while showing a different status (P0-01).
    /// Purely additive: <see cref="RequestStatus"/>/<see cref="CampusStatus"/>/<see cref="ApprovedAny"/>/
    /// <see cref="PendingApprovalAny"/> keep working unchanged for any caller that still sends them;
    /// this is evaluated first when present (see the handler's filter branch ordering).
    /// </summary>
    public string? EffectiveStatus { get; init; }
    /// <summary>
    /// Comma-separated union of <see cref="EffectiveStatus"/> codes — needed because at least one
    /// existing UI grouping (Visitor's "Đã duyệt", which its own description already says covers
    /// "đang chuẩn bị, đang diễn ra, chờ đánh giá hoặc đã hoàn tất") is a deliberate union of several
    /// canonical codes, not one. Mirrors the existing <see cref="VisitScopes"/> comma-list pattern.
    /// Ignored when <see cref="EffectiveStatus"/> is also set (single code wins).
    /// </summary>
    public string? EffectiveStatuses { get; init; }
    public ulong? CampusId { get; init; }
    public string? VisitScope { get; init; }
    public string? VisitScopes { get; init; }
    public DateTime? FromDate { get; init; }
    public DateTime? ToDate { get; init; }
    public bool CancelledOnly { get; init; }
    /// <summary>
    /// HO's "Chờ duyệt" filter row merges what used to be two separate dropdown options —
    /// a campus still WAITING_REQUEST_APPROVAL, or a multi-campus request whose aggregate is
    /// PARTIALLY_APPROVED — into the union of both, same set of rows either option used to return.
    /// </summary>
    public bool PendingApprovalAny { get; init; }
    /// <summary>
    /// HO's "Đã duyệt" filter row merges the other two former options — a campus ASSIGNED a host,
    /// or a request whose aggregate is fully APPROVED — into the union of both.
    /// </summary>
    public bool ApprovedAny { get; init; }
    public string? Relation { get; init; }
    public bool? ReadOnlyOnly { get; init; }
    public bool? ActionableOnly { get; init; }
    public string? Timing { get; init; }
    public string? SortBy { get; init; }
    public string? SortOrder { get; init; }
}