using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;
using PEMS.Domain.Constants;

using PEMS.Application.Common;
namespace PEMS.Application.News.Commands.SetNewsFeatured;

public sealed class SetNewsFeaturedCommandHandler
    : IRequestHandler<SetNewsFeaturedCommand, SetNewsFeaturedResponse>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;

    public SetNewsFeaturedCommandHandler(IApplicationDbContext dbContext, ICurrentUserService currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<SetNewsFeaturedResponse> Handle(
        SetNewsFeaturedCommand request, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUser.UserId
            ?? throw new ForbiddenException("Bạn chưa đăng nhập.");

        var roleCode = _currentUser.RoleCode ?? string.Empty;
        var subRole  = _currentUser.SubRole  ?? string.Empty;
        var campusId = _currentUser.PrimaryCampusId;

        // Only Staff Leader
        if (roleCode != RoleCodes.Staff || subRole != UserSubRoles.Leader)
            throw new ForbiddenException("Chỉ Staff Leader mới có thể đánh dấu tin tức nổi bật.");

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

        news.IsFeatured = request.IsFeatured;
        news.UpdatedAt  = VietnamTime.Now();
        news.UpdatedBy  = currentUserId;
        news.RowVersion++;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new SetNewsFeaturedResponse
        {
            Success    = true,
            Message    = request.IsFeatured
                ? "Đã đánh dấu bài viết là nổi bật."
                : "Đã bỏ đánh dấu nổi bật.",
            IsFeatured = news.IsFeatured,
            RowVersion = news.RowVersion
        };
    }
}
