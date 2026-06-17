using MediatR;
using PEMS.Application.Authentication.Models;

namespace PEMS.Application.Authentication.Queries.GetCurrentUser;

/// <summary>GET /api/auth/me — current user profile + permissions.</summary>
public sealed class GetCurrentUserQuery : IRequest<UserProfileResponse>
{
}
