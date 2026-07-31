using MediatR;

namespace PEMS.Application.Faqs.Commands.UpdateFAQ;

public sealed record UpdateFAQCommand(
    ulong FaqId,
    string Question,
    string Answer,
    /// <summary>
    /// English question/answer as currently shown in the EN panel. Null/omitted when the EN panel
    /// was never opened during this edit — existing EN content (if any) is then left untouched;
    /// if no EN translation exists yet either, the backend auto-translates once and stores it.
    /// </summary>
    string? EnglishQuestion = null,
    string? EnglishAnswer = null
) : IRequest<UpdateFAQResponse>;

public sealed record UpdateFAQBody(
    string Question,
    string Answer,
    string? EnglishQuestion = null,
    string? EnglishAnswer = null
);
