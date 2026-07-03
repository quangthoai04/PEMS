using FluentValidation;
using PEMS.Application.Feedbacks.Common;

namespace PEMS.Application.Feedbacks.Commands.SubmitVisitFeedback;

public sealed class SubmitVisitFeedbackCommandValidator : AbstractValidator<SubmitVisitFeedbackCommand>
{
    public SubmitVisitFeedbackCommandValidator()
    {
        RuleFor(x => x.VisitInstanceId).GreaterThan(0UL);
        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("Chưa có mục đánh giá nào để gửi.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.Rating)
                .InclusiveBetween((byte)1, (byte)5)
                .WithMessage("Số sao đánh giá bắt buộc từ 1 đến 5.");
            item.RuleFor(i => i.FeedbackType)
                .Must(t => FeedbackTypes.All.Contains(t))
                .WithMessage("Loại feedback không hợp lệ.");
            item.RuleFor(i => i.TargetType)
                .Must(t => FeedbackTargetTypes.All.Contains(t))
                .WithMessage("Loại đối tượng đánh giá không hợp lệ.");
            item.RuleFor(i => i)
                .Must(i => FeedbackRules.IsTargetTypeAllowed(i.FeedbackType, i.TargetType))
                .WithMessage("Đối tượng đánh giá không khớp loại feedback.")
                .Must(i => FeedbackRules.ValidateTargetColumns(
                    i.TargetType, i.TargetUserId, i.TargetParticipantId, i.TargetGuestMemberId,
                    i.TargetLogisticsItemId, i.TargetHandoverId, i.TargetDepartmentId) is null)
                .WithMessage(i => FeedbackRules.ValidateTargetColumns(
                    i.TargetType, i.TargetUserId, i.TargetParticipantId, i.TargetGuestMemberId,
                    i.TargetLogisticsItemId, i.TargetHandoverId, i.TargetDepartmentId) ?? "Target không hợp lệ.");
            item.RuleFor(i => i.Comment)
                .MaximumLength(4000).WithMessage("Nhận xét tối đa 4000 ký tự.");
        });
    }
}
