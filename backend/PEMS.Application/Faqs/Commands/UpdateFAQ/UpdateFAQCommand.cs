using MediatR;

namespace PEMS.Application.Faqs.Commands.UpdateFAQ;

public sealed record UpdateFAQCommand(
    ulong FaqId,
    string FaqType,
    string Question,
    string Answer
) : IRequest<UpdateFAQResponse>;

public sealed record UpdateFAQBody(
    string FaqType,
    string Question,
    string Answer
);
