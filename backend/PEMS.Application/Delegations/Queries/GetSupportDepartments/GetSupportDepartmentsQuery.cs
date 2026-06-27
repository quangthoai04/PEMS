using System.Collections.Generic;
using MediatR;

namespace PEMS.Application.Delegations.Queries.GetSupportDepartments;

/// <summary>
/// Lists GENERAL departments of the campus instance that the host may invite to support, each with
/// its resolved active Department Leader (the real invitee — the host never invites a department
/// staff directly). Host-only; scope re-validated in the handler.
/// </summary>
public sealed record GetSupportDepartmentsQuery(ulong VisitInstanceId, string? Keyword)
    : IRequest<IReadOnlyList<SupportDepartmentDto>>;
