using MediatR;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Security;
using PEMS.Application.News.Services;

namespace PEMS.Application.Partners.Commands.TranslatePartnerDraft;

public sealed class TranslatePartnerDraftCommandHandler
    : IRequestHandler<TranslatePartnerDraftCommand, TranslatePartnerDraftResponse>
{
    private readonly INewsTranslationService _translator;
    private readonly IHtmlSanitizerService _sanitizer;

    public TranslatePartnerDraftCommandHandler(INewsTranslationService translator, IHtmlSanitizerService sanitizer)
    {
        _translator = translator;
        _sanitizer = sanitizer;
    }

    public async Task<TranslatePartnerDraftResponse> Handle(
        TranslatePartnerDraftCommand request, CancellationToken cancellationToken)
    {
        var sourceLang = string.IsNullOrWhiteSpace(request.SourceLanguage) ? "vi" : request.SourceLanguage.Trim();
        var targetLang = string.IsNullOrWhiteSpace(request.TargetLanguage) ? "en" : request.TargetLanguage.Trim();

        var name = (request.Name ?? string.Empty).Trim();
        var shortName = (request.ShortName ?? string.Empty).Trim();
        var description = (request.Description ?? string.Empty).Trim();
        var address = (request.Address ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(name))
            throw new ValidationException("Vui lòng nhập tên đối tác trước khi dịch.");

        // country/city are proper nouns — copied through unchanged, never sent to the translator.
        var batch = new List<string> { name, shortName, description, address };
        var translated = await _translator.TranslateTextAsync(batch, sourceLang, targetLang, cancellationToken);

        var translatedName = _sanitizer.Sanitize(translated[0]).Trim();
        var translatedShortName = _sanitizer.Sanitize(translated[1]).Trim();
        var translatedDescription = _sanitizer.Sanitize(translated[2]).Trim();
        var translatedAddress = _sanitizer.Sanitize(translated[3]).Trim();

        if (string.IsNullOrWhiteSpace(translatedName))
            throw new BusinessRuleException(
                "Kết quả dịch tên đối tác rỗng — vui lòng thử lại.", "TRANSLATION_EMPTY_RESULT");

        return new TranslatePartnerDraftResponse
        {
            Success = true,
            Message = $"Đã dịch sang '{targetLang}'. Xem lại nội dung trước khi lưu.",
            SourceLanguage = sourceLang,
            TargetLanguage = targetLang,
            Name = translatedName,
            ShortName = string.IsNullOrWhiteSpace(translatedShortName) ? null : translatedShortName,
            Country = request.Country,
            City = request.City,
            Description = string.IsNullOrWhiteSpace(translatedDescription) ? null : translatedDescription,
            Address = string.IsNullOrWhiteSpace(translatedAddress) ? null : translatedAddress,
        };
    }
}
