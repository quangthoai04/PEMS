using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Notifications;

using PEMS.Application.Common;
namespace PEMS.Application.News.Commands.ApproveNews;

public sealed class ApproveNewsCommandHandler
    : IRequestHandler<ApproveNewsCommand, ApproveNewsResponse>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly PEMS.Application.Notifications.Common.INotificationService _notificationService;

    public ApproveNewsCommandHandler(IApplicationDbContext dbContext, ICurrentUserService currentUser, PEMS.Application.Notifications.Common.INotificationService notificationService)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _notificationService = notificationService;
    }

    public async Task<ApproveNewsResponse> Handle(ApproveNewsCommand request, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUser.UserId
            ?? throw new ForbiddenException("Bạn chưa đăng nhập.");

        var roleCode = _currentUser.RoleCode ?? string.Empty;
        var subRole  = _currentUser.SubRole  ?? string.Empty;
        var campusId = _currentUser.PrimaryCampusId;

        // Only Staff Leader
        if (roleCode != RoleCodes.Staff || subRole != UserSubRoles.Leader)
            throw new ForbiddenException("Chỉ Staff Leader mới có thể duyệt hoặc từ chối tin tức.");

        // Load news (tracked for update)
        var news = await _dbContext.News
            .Where(n => n.NewsId == request.NewsId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Tin tức", request.NewsId);

        // Campus scope
        if (news.CampusId != campusId)
            throw new ForbiddenException("Bạn không có quyền xử lý bài viết này.");

        // Must be PENDING_REVIEW
        if (news.Status != NewsConstants.Status.PendingReview)
            throw new ConflictException("Chỉ có thể duyệt/từ chối bài viết đang ở trạng thái Chờ Duyệt.");

        // RowVersion optimistic concurrency
        if (news.RowVersion != request.RowVersion)
            throw new ConflictException("Bài viết đã được cập nhật bởi người khác. Vui lòng tải lại trang.");

        var now = VietnamTime.Now();
        string newStatus;
        string notificationTitle;
        string notificationMessage;
        string notificationEventKey;
        object notificationEventParams;

        if (request.Action == "APPROVE")
        {
            news.Status     = NewsConstants.Status.Published;
            news.PublishedAt = now;
            news.ReviewNote = null;
            newStatus = NewsConstants.Status.Published;
            notificationTitle   = "Tin tức của bạn đã được duyệt";
            notificationMessage = $"Bài viết \"{await GetNewsTitleAsync(request.NewsId, cancellationToken)}\" đã được Staff Leader duyệt và xuất bản.";
            notificationEventKey = PEMS.Application.Notifications.Common.NotificationEventKeys.NewsApproved;
            notificationEventParams = new { newsTitle = await GetNewsTitleAsync(request.NewsId, cancellationToken) };
        }
        else // REJECT
        {
            news.Status     = NewsConstants.Status.Rejected;
            news.PublishedAt = null;
            news.ReviewNote = request.Reason!.Trim();
            newStatus = NewsConstants.Status.Rejected;
            notificationTitle   = "Tin tức của bạn bị từ chối";
            notificationMessage = $"Bài viết \"{await GetNewsTitleAsync(request.NewsId, cancellationToken)}\" bị từ chối. Lý do: {request.Reason!.Trim()}";
            notificationEventKey = PEMS.Application.Notifications.Common.NotificationEventKeys.NewsRejected;
            notificationEventParams = new { newsTitle = await GetNewsTitleAsync(request.NewsId, cancellationToken), reason = request.Reason!.Trim() };
        }

        if (request.IsFeatured.HasValue)
            news.IsFeatured = request.IsFeatured.Value;

        if (request.IsPinned.HasValue)
            news.IsPinned = request.IsPinned.Value;

        news.ReviewedBy = currentUserId;
        news.ReviewedAt = now;
        news.RowVersion++;

        // Notify author
        await _notificationService.CreateAsync(
            new PEMS.Application.Notifications.Common.CreateNotificationRequest(
                RecipientUserId: news.AuthorUserId,
                Title: notificationTitle,
                Message: notificationMessage,
                NotificationType: PEMS.Application.Notifications.Common.NotificationTypes.NewsReviewed,
                RelatedType: PEMS.Application.Notifications.Common.NotificationRelatedTypes.News,
                RelatedId: news.NewsId,
                ActorUserId: currentUserId,
                Category: PEMS.Application.Notifications.Common.NotificationCategories.News,
                ActionType: PEMS.Application.Notifications.Common.NotificationActionTypes.OpenNewsDetail,
                // Trang quản lý tin tức lọc đúng 1 bài (có nút "Xem tất cả"), không vào thẳng chi tiết.
                ActionUrl: $"/dashboard/news?newsId={news.NewsId}",
                MetadataJson: PEMS.Application.Notifications.Common.NotificationEventKeys.BuildMetadata(
                    notificationEventKey, notificationEventParams)),
            cancellationToken
        );

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new ApproveNewsResponse
        {
            Success        = true,
            Message        = request.Action == "APPROVE"
                ? "Bài viết đã được duyệt và xuất bản thành công."
                : "Bài viết đã bị từ chối.",
            NewStatus      = newStatus,
            NewStatusLabel = NewsConstants.ToVietnameseStatusLabel(newStatus)
        };
    }

    private async Task<string> GetNewsTitleAsync(ulong newsId, CancellationToken cancellationToken)
    {
        return await _dbContext.NewsTranslations
            .AsNoTracking()
            .Where(t => t.NewsId == newsId && t.LanguageCode == "vi")
            .Select(t => t.Title)
            .FirstOrDefaultAsync(cancellationToken) ?? "Không có tiêu đề";
    }
}
