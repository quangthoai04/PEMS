using MediatR;
using PEMS.Application.Authentication.Models;

namespace PEMS.Application.Authentication.Queries.GetCurrentUserPermissions;

/// <summary>GET /api/auth/permissions — current user's role + permission matrix.</summary>
public sealed class GetCurrentUserPermissionsQuery : IRequest<PermissionsResponse>
{
}
