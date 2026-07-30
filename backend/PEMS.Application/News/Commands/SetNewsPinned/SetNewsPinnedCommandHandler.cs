using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;
using PEMS.Domain.Constants;

namespace PEMS.Application.News.Commands.SetNewsPinned;

public sealed class SetNewsPinnedCommandHandler
    : IRequestHandler<SetNewsPinnedCommand, SetNewsPinnedResponse>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;

    public SetNewsPinnedCommandHandler(IApplicationDbContext dbContext, ICurrentUserService currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<SetNewsPinnedResponse> Handle(
        SetNewsPinnedCommand request, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUser.UserId
            ?? throw new ForbiddenException("Bạn chưa đăng nhập.");

        var roleCode = _currentUser.RoleCode ?? string.Empty;
        var subRole  = _currentUser.SubRole  ?? string.Empty;
        var campusId = _currentUser.PrimaryCampusId;

        // Only Staff Leader
        if (roleCode != RoleCodes.Staff || subRole != UserSubRoles.Leader)
            throw new ForbiddenException("Chỉ Staff Leader mới có thể ghim tin tức.");

        // Load news (tracked for update)
        var news = await _dbContext.News
            .Where(n => n.NewsId == request.NewsId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Tin tức", request.NewsId);

        // Campus scope
        if (news.CampusId != campusId)
            throw new ForbiddenException("Bạn không có quyền thay đổi bài viết này.");

        // RowVersion optimistic concurrency
        if (news.RowVersion != request.RowVersion)
            throw new ConflictException("Bài viết đã được cập nhật bởi người khác. Vui lòng tải lại trang.");

        news.IsPinned  = request.IsPinned;
        news.UpdatedAt = VietnamTime.Now();
        news.UpdatedBy = currentUserId;
        news.RowVersion++;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new SetNewsPinnedResponse
        {
            Success    = true,
            Message    = request.IsPinned
                ? "Đã ghim bài viết ở Dấu ấn các chuyến thăm."
                : "Đã bỏ ghim bài viết.",
            IsPinned   = news.IsPinned,
            RowVersion = news.RowVersion
        };
    }
}
