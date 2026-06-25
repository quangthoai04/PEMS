using MediatR;

namespace PEMS.Application.Faqs.Queries.ViewFAQDetail;

public sealed record ViewFAQDetailQuery(ulong FaqId) : IRequest<ViewFAQDetailDto>;
