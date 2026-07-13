using System.Collections.Generic;
using MediatR;

namespace PEMS.Application.Delegations.Queries.GetSupportDepartments;

/// <summary>
/// Lists GENERAL departments of the campus instance for the host's prep screen, each with its
/// resolved active Department Leader. The list serves TWO independent host actions — inviting the
/// leader as a DEPT_SUPPORT participant AND picking the department for a system logistics request —
/// each gated by its own capability flag on the DTO. Host-only; scope re-validated in the handler.
/// </summary>
public sealed record GetSupportDepartmentsQuery(ulong VisitInstanceId, string? Keyword)
    : IRequest<IReadOnlyList<SupportDepartmentDto>>;
