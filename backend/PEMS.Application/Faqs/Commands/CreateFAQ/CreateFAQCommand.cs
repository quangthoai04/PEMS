using MediatR;

namespace PEMS.Application.Faqs.Commands.CreateFAQ;

public sealed record CreateFAQCommand(
    string FaqType,
    string Question,
    string Answer,
    string? Status
) : IRequest<CreateFAQResponse>;
