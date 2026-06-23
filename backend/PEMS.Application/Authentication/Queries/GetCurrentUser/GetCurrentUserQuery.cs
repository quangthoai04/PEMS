using MediatR;
using PEMS.Application.Authentication.Models;

namespace PEMS.Application.Authentication.Queries.GetCurrentUser;

/// <summary>GET /api/auth/me — current user profile only.</summary>
public sealed class GetCurrentUserQuery : IRequest<UserProfileResponse>
{
}
