using FluentValidation;

namespace PEMS.Application.Delegations.Commands.SendVisitAgendaEmail;

public sealed class SendVisitAgendaEmailCommandValidator
    : AbstractValidator<SendVisitAgendaEmailCommand>
{
    public SendVisitAgendaEmailCommandValidator()
    {
        RuleFor(x => x.VisitRequestId).GreaterThan(0ul).WithMessage("VisitRequestId không hợp lệ.");
        RuleFor(x => x.VisitInstanceId).GreaterThan(0ul).WithMessage("VisitInstanceId không hợp lệ.");
    }
}
