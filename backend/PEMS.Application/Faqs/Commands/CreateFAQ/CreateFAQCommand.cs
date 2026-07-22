using MediatR;

namespace PEMS.Application.Faqs.Commands.CreateFAQ;

public sealed record CreateFAQCommand(
    string FaqType,
    string Question,
    string Answer,
    string? Status,
    /// <summary>
    /// English question, only set once the admin has opened the "EN" panel (whether the text is
    /// the untouched machine-translation preview or hand-edited afterward). Null/omitted when the
    /// EN panel was never opened — the backend then auto-translates once and stores the result.
    /// </summary>
    string? EnglishQuestion = null,
    string? EnglishAnswer = null
) : IRequest<CreateFAQResponse>;
