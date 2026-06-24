using MediatR;
using PEMS.Application.Profiles.Common;

namespace PEMS.Application.Profiles.Queries.ViewProfile;

/// <summary>
/// UC-14 — View my profile. Carries no identifier: the handler resolves the current
/// user from the authenticated token (self-service, never a userId from the client).
/// </summary>
public sealed class ViewProfileQuery : IRequest<ProfileResponse>
{
}
