using MediatR;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;
using PEMS.Application.News.Services;
using PEMS.Domain.Constants;

namespace PEMS.Application.Faqs.Commands.TranslateFaqDraft;

public sealed class TranslateFaqDraftCommandHandler
    : IRequestHandler<TranslateFaqDraftCommand, TranslateFaqDraftResponse>
{
    private readonly INewsTranslationService _translator;
    private readonly IHtmlSanitizerService _sanitizer;

    public TranslateFaqDraftCommandHandler(INewsTranslationService translator, IHtmlSanitizerService sanitizer)
    {
        _translator = translator;
        _sanitizer = sanitizer;
    }

    public async Task<TranslateFaqDraftResponse> Handle(
        TranslateFaqDraftCommand request, CancellationToken cancellationToken)
    {
        var sourceLang = string.IsNullOrWhiteSpace(request.SourceLanguage) ? "vi" : request.SourceLanguage.Trim();
        var targetLang = string.IsNullOrWhiteSpace(request.TargetLanguage) ? "en" : request.TargetLanguage.Trim();

        var question = (request.Question ?? string.Empty).Trim();
        var answer = (request.Answer ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(question) && string.IsNullOrWhiteSpace(answer))
            throw new ValidationException("Vui lòng nhập câu hỏi và câu trả lời trước khi dịch.");

        var batch = new List<string> { question, answer };
        var translated = await _translator.TranslateTextAsync(batch, sourceLang, targetLang, cancellationToken);

        var translatedQuestion = _sanitizer.Sanitize(translated[0]).Trim();
        var translatedAnswer = _sanitizer.Sanitize(translated[1]).Trim();

        if (string.IsNullOrWhiteSpace(translatedQuestion))
            throw new BusinessRuleException(
                "Kết quả dịch câu hỏi rỗng — vui lòng thử lại.", "TRANSLATION_EMPTY_RESULT");

        return new TranslateFaqDraftResponse
        {
            Success = true,
            Message = $"Đã dịch sang '{targetLang}'. Xem lại nội dung trước khi lưu.",
            SourceLanguage = sourceLang,
            TargetLanguage = targetLang,
            Question = translatedQuestion,
            Answer = translatedAnswer,
        };
    }
}
