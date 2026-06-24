using MediatR;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Profiles.Common;

namespace PEMS.Application.Profiles.Queries.ViewProfile;

/// <summary>
/// UC-14 handler. Returns the current user's own profile, built role-aware by
/// <see cref="ProfileResponseBuilder"/>. The user id always comes from the JWT
/// (<see cref="ICurrentUserService"/>), never from the request.
/// </summary>
public sealed class ViewProfileQueryHandler : IRequestHandler<ViewProfileQuery, ProfileResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public ViewProfileQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ProfileResponse> Handle(ViewProfileQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new AuthenticationFailedException("Phiên đăng nhập không hợp lệ.");

        return await ProfileResponseBuilder.BuildAsync(_db, userId, cancellationToken);
    }
}
