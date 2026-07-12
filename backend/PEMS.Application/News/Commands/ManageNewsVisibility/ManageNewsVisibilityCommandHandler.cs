using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;
using PEMS.Domain.Constants;

using PEMS.Application.Common;
namespace PEMS.Application.News.Commands.ManageNewsVisibility;

public sealed class ManageNewsVisibilityCommandHandler
    : IRequestHandler<ManageNewsVisibilityCommand, ManageNewsVisibilityResponse>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;

    public ManageNewsVisibilityCommandHandler(IApplicationDbContext dbContext, ICurrentUserService currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<ManageNewsVisibilityResponse> Handle(
        ManageNewsVisibilityCommand request,
        CancellationToken cancellationToken)
    {
        var currentUserId = _currentUser.UserId
            ?? throw new ForbiddenException("Bạn chưa đăng nhập.");

        var roleCode = _currentUser.RoleCode ?? string.Empty;
        var subRole  = _currentUser.SubRole  ?? string.Empty;
        var campusId = _currentUser.PrimaryCampusId;

        // Only Staff Leader
        if (roleCode != RoleCodes.Staff || subRole != UserSubRoles.Leader)
            throw new ForbiddenException("Chỉ Staff Leader mới có thể ẩn/hiện tin tức.");

        // Load news (tracked for update)
        var news = await _dbContext.News
            .Where(n => n.NewsId == request.NewsId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Tin tức", request.NewsId);

        // Campus scope
        if (news.CampusId != campusId)
            throw new ForbiddenException("Bạn không có quyền thay đổi hiển thị bài viết này.");

        // Validate allowed transitions: PUBLISHED → HIDDEN or HIDDEN → PUBLISHED
        var allowedTransitions = new Dictionary<string, string>
        {
            [NewsConstants.Status.Published] = NewsConstants.Status.Hidden,
            [NewsConstants.Status.Hidden]    = NewsConstants.Status.Published
        };

        if (!allowedTransitions.TryGetValue(news.Status, out var expectedTarget)
            || expectedTarget != request.TargetStatus)
        {
            throw new ConflictException(
                "Không thể thay đổi hiển thị với trạng thái hiện tại của bài viết.");
        }

        // RowVersion optimistic concurrency
        if (news.RowVersion != request.RowVersion)
            throw new ConflictException("Bài viết đã được cập nhật bởi người khác. Vui lòng tải lại trang.");

        var now = VietnamTime.Now();

        // Only update status + audit fields (NOT reviewed_by/reviewed_at/published_at)
        news.Status    = request.TargetStatus;
        news.UpdatedAt = now;
        news.UpdatedBy = currentUserId;
        news.RowVersion++;

        await _dbContext.SaveChangesAsync(cancellationToken);

        var message = request.TargetStatus == NewsConstants.Status.Hidden
            ? "Bài viết đã được ẩn thành công."
            : "Bài viết đã được hiển thị lại thành công.";

        return new ManageNewsVisibilityResponse
        {
            Success        = true,
            Message        = message,
            NewStatus      = news.Status,
            NewStatusLabel = NewsConstants.ToVietnameseStatusLabel(news.Status)
        };
    }
}
