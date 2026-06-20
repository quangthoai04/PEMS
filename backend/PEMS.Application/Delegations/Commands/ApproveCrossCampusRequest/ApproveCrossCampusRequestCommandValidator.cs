using FluentValidation;

namespace PEMS.Application.Delegations.Commands.ApproveCrossCampusRequest;

public sealed class ApproveCrossCampusRequestCommandValidator : AbstractValidator<ApproveCrossCampusRequestCommand>
{
    public ApproveCrossCampusRequestCommandValidator()
    {
        RuleFor(x => x.VisitRequestId).GreaterThan(0UL);
    }
}
