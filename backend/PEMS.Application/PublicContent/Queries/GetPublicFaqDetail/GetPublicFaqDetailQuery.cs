using MediatR;
using PEMS.Application.PublicContent.Queries.ViewFAQ;

namespace PEMS.Application.PublicContent.Queries.GetPublicFaqDetail;

/// <summary>
/// GET /api/public/faqs/{faqId} — one PUBLISHED FAQ in one language, for the
/// <c>/faq?faqId=</c> deep link (a search hit must open its own accordion even when the FAQ sits on
/// another pagination page). Anonymous. Returns the same <see cref="ViewFaqDto"/> shape as the list
/// endpoint so the page can drop it straight into its existing rendering.
/// </summary>
public sealed record GetPublicFaqDetailQuery(ulong FaqId, string? LanguageCode) : IRequest<ViewFaqDto>;
