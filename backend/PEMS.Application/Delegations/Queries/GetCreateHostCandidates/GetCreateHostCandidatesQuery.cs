using System;
using System.Collections.Generic;
using MediatR;
using PEMS.Application.Delegations.Queries.GetHostCandidates;

namespace PEMS.Application.Delegations.Queries.GetCreateHostCandidates;

/// <summary>
/// Host candidates for the AUTHENTICATED create form's ASSIGN_HOST mode — before any
/// campus instance exists. Staff Leader only; the campus is ALWAYS the caller's own
/// primary campus (never a parameter, so other campuses can't be probed). The planned
/// window is used for the same non-blocking schedule-conflict warnings as the
/// per-instance host-candidates API.
/// </summary>
public sealed record GetCreateHostCandidatesQuery(
    DateTime? WindowStartAt,
    DateTime? WindowEndAt) : IRequest<IReadOnlyList<HostCandidateDto>>;
