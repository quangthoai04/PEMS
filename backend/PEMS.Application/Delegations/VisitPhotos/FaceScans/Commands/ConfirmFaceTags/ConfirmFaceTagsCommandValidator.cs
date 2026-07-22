using FluentValidation;

namespace PEMS.Application.Delegations.VisitPhotos.FaceScans.Commands.ConfirmFaceTags;

public sealed class ConfirmFaceTagsCommandValidator : AbstractValidator<ConfirmFaceTagsCommand>
{
    public ConfirmFaceTagsCommandValidator()
    {
        RuleFor(x => x.FaceScanId).GreaterThan(0UL);
        RuleFor(x => x.Faces).NotEmpty().WithMessage("Danh sách khuôn mặt không được rỗng.");
        RuleForEach(x => x.Faces).ChildRules(face =>
        {
            face.RuleFor(f => f.FaceDetectionId).GreaterThan(0UL);
            face.RuleFor(f => f.GuestMemberId).GreaterThan(0UL)
                .When(f => f.GuestMemberId.HasValue)
                .WithMessage("Mã khách không hợp lệ.");
            face.RuleFor(f => f)
                .Must(f => f.Ignored ? f.GuestMemberId is null : f.GuestMemberId is not null)
                .WithMessage("Mỗi khuôn mặt phải được gán một khách hoặc đánh dấu bỏ qua (không cả hai, không để trống).");
        });
    }
}
