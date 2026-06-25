using MediatR;

namespace PEMS.Application.Delegations.Queries.GetVisitProcessPermissions;

/// <summary>
/// Returns the permission flags the signed-in user has on a campus instance's process page
/// (Trước/Trong/Sau tiếp khách, biên bản, tin tức, đóng đoàn). The backend is the single source
/// of truth — the frontend renders/edits tabs purely from these booleans and never re-derives
/// permission from status text. Every mutating command still re-validates server-side.
/// </summary>
public sealed record GetVisitProcessPermissionsQuery(ulong VisitInstanceId)
    : IRequest<VisitProcessPermissionDto>;
