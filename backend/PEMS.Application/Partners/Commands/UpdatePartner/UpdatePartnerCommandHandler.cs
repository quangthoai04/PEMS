using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;
using PEMS.Application.News.Services;
using PEMS.Application.Partners.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Partners;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Partners.Commands.UpdatePartner;

public sealed class UpdatePartnerCommandHandler : IRequestHandler<UpdatePartnerCommand, UpdatePartnerResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;
    private readonly INewsTranslationService _translator;
    private readonly IHtmlSanitizerService _sanitizer;

    public UpdatePartnerCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IDateTimeService clock,
        INewsTranslationService translator,
        IHtmlSanitizerService sanitizer)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _translator = translator;
        _sanitizer = sanitizer;
    }

    public async Task<UpdatePartnerResponse> Handle(UpdatePartnerCommand request, CancellationToken cancellationToken)
    {
        var partner = await _db.Partners
            .FirstOrDefaultAsync(p => p.PartnerId == request.PartnerId, cancellationToken)
            ?? throw new NotFoundException("Partner", request.PartnerId);

        if (!PartnerAccess.CanEditPartner(_currentUser, partner))
            throw new AuthBusinessException(PartnerErrorCodes.Forbidden,
                "Bạn không có quyền chỉnh sửa đối tác này.", 403);

        var name = request.Name.Trim();
        var code = string.IsNullOrWhiteSpace(request.PartnerCode) ? null : request.PartnerCode.Trim().ToUpperInvariant();

        if (code is not null &&
            await _db.Partners.AnyAsync(p => p.PartnerCode == code && p.PartnerId != partner.PartnerId, cancellationToken))
            throw new ConflictException("Mã đối tác đã tồn tại.", PartnerErrorCodes.CodeDuplicated);

        var nameKey = PartnerNormalization.NormalizeKey(name);
        var otherNames = await _db.Partners
            .Where(p => p.PartnerId != partner.PartnerId)
            .Select(p => new { p.PartnerId, p.Name })
            .ToListAsync(cancellationToken);
        if (otherNames.Any(p => PartnerNormalization.NormalizeKey(p.Name) == nameKey))
            throw new ConflictException("Tên đối tác đã tồn tại.", PartnerErrorCodes.NameDuplicated);

        // PUBLIC visibility is reserved for APPROVED profiles. Updating a REJECTED profile
        // resubmits it, so PUBLIC is also blocked on that path.
        var wasRejected = partner.ProfileStatus == PartnerProfileStatuses.Rejected;
        var targetStatus = wasRejected ? PartnerProfileStatuses.PendingApproval : partner.ProfileStatus;
        var visibility = string.IsNullOrWhiteSpace(request.Visibility) ? partner.Visibility : request.Visibility!;
        if (visibility == PartnerVisibilities.Public && targetStatus != PartnerProfileStatuses.Approved)
            throw new BusinessRuleException(
                "Không thể đặt hiển thị PUBLIC khi hồ sơ đối tác chưa được duyệt.",
                PartnerErrorCodes.PublicRequiresApproved);

        var now = _clock.VietnamNow;
        var before = JsonSerializer.Serialize(new
        {
            partner.PartnerCode, partner.Name, partner.ShortName, partner.Country, partner.City,
            partner.WebsiteUrl, partner.Address, partner.PartnerType, partner.CooperationStatus,
            partner.Visibility, partner.ProfileStatus,
        });

        var shortName = string.IsNullOrWhiteSpace(request.ShortName) ? null : request.ShortName.Trim();
        var countryVal = string.IsNullOrWhiteSpace(request.Country) ? null : request.Country.Trim();
        var cityVal = string.IsNullOrWhiteSpace(request.City) ? null : request.City.Trim();
        var addressVal = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim();
        var descriptionVal = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();

        partner.PartnerCode = code!;
        partner.Name = name;
        partner.ShortName = shortName;
        partner.Country = countryVal;
        partner.City = cityVal;
        partner.WebsiteUrl = string.IsNullOrWhiteSpace(request.WebsiteUrl) ? null : request.WebsiteUrl.Trim();
        partner.Address = addressVal;
        partner.Description = descriptionVal;
        if (!string.IsNullOrWhiteSpace(request.PartnerType)) partner.PartnerType = request.PartnerType!;
        if (!string.IsNullOrWhiteSpace(request.CooperationStatus)) partner.CooperationStatus = request.CooperationStatus!;
        partner.Visibility = visibility;
        partner.LogoFileId = request.LogoFileId ?? partner.LogoFileId;
        partner.CoverFileId = request.CoverFileId ?? partner.CoverFileId;
        partner.UpdatedAt = now;
        partner.UpdatedBy = _currentUser.UserId;

        if (wasRejected)
        {
            partner.ProfileStatus = PartnerProfileStatuses.PendingApproval;
            partner.ReviewNote = null;
            partner.ReviewedBy = null;
            partner.ReviewedAt = null;
        }

        // Keep the primary alias in sync with the (possibly renamed) official name.
        var aliasExists = await _db.PartnerAliases.AnyAsync(
            a => a.PartnerId == partner.PartnerId && a.AliasNameKey == nameKey, cancellationToken);
        if (!aliasExists)
        {
            _db.PartnerAliases.Add(new Domain.Entities.Partners.PartnerAlias
            {
                PartnerId = partner.PartnerId,
                AliasName = name,
                AliasNameKey = nameKey,
                Source = "MANUAL",
                Status = "ACTIVE",
                CreatedAt = now,
                CreatedBy = _currentUser.UserId,
            });
        }

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = _currentUser.UserId,
            CampusId = partner.OwnerCampusId,
            Action = "UPDATE_PARTNER",
            EntityType = "Partner",
            EntityId = partner.PartnerId,
            Changes = new List<AuditLogChange>
            {
                new()
                {
                    FieldName = "Partner",
                    OldValueText = before,
                    NewValueText = JsonSerializer.Serialize(new
                    {
                        partner.PartnerCode, partner.Name, partner.ShortName, partner.Country, partner.City,
                        partner.WebsiteUrl, partner.Address, partner.PartnerType, partner.CooperationStatus,
                        partner.Visibility, partner.ProfileStatus,
                    }),
                },
            },
            CreatedAt = now,
        });

        var sourceHash = ComputeHash(name, shortName, countryVal, cityVal, descriptionVal, addressVal);

        var translations = await _db.PartnerTranslations
            .Where(t => t.PartnerId == partner.PartnerId)
            .ToListAsync(cancellationToken);

        var viTranslation = translations.FirstOrDefault(t => t.LanguageCode == "vi");
        if (viTranslation is null)
        {
            _db.PartnerTranslations.Add(new PartnerTranslation
            {
                PartnerId = partner.PartnerId,
                LanguageCode = "vi",
                Name = name,
                ShortName = shortName,
                Country = countryVal,
                City = cityVal,
                Description = descriptionVal,
                Address = addressVal,
                TranslationSource = "LEGACY",
                TranslationStatus = "READY",
                SourceHash = sourceHash,
                TranslatedAt = now,
                CreatedAt = now,
                CreatedBy = _currentUser.UserId,
            });
        }
        else
        {
            viTranslation.Name = name;
            viTranslation.ShortName = shortName;
            viTranslation.Country = countryVal;
            viTranslation.City = cityVal;
            viTranslation.Description = descriptionVal;
            viTranslation.Address = addressVal;
            viTranslation.SourceHash = sourceHash;
            viTranslation.TranslatedAt = now;
            viTranslation.UpdatedAt = now;
            viTranslation.UpdatedBy = _currentUser.UserId;
        }

        var providedEnglishName = _sanitizer.Sanitize(request.EnglishName ?? string.Empty).Trim();
        var providedEnglishShortName = _sanitizer.Sanitize(request.EnglishShortName ?? string.Empty).Trim();
        var providedEnglishDescription = _sanitizer.Sanitize(request.EnglishDescription ?? string.Empty).Trim();
        var providedEnglishAddress = _sanitizer.Sanitize(request.EnglishAddress ?? string.Empty).Trim();
        var englishProvided = !string.IsNullOrWhiteSpace(providedEnglishName);

        var enTranslation = translations.FirstOrDefault(t => t.LanguageCode == "en");

        string englishNameOut;
        string? englishShortNameOut;
        string? englishDescriptionOut;
        string? englishAddressOut;

        if (englishProvided)
        {
            // EN panel was open this save — trust whatever is shown there (machine-translated
            // preview left as-is, or hand-edited). Never re-call the translator here.
            englishNameOut = providedEnglishName;
            englishShortNameOut = string.IsNullOrWhiteSpace(providedEnglishShortName) ? null : providedEnglishShortName;
            englishDescriptionOut = string.IsNullOrWhiteSpace(providedEnglishDescription) ? null : providedEnglishDescription;
            englishAddressOut = string.IsNullOrWhiteSpace(providedEnglishAddress) ? null : providedEnglishAddress;

            if (enTranslation is null)
            {
                _db.PartnerTranslations.Add(new PartnerTranslation
                {
                    PartnerId = partner.PartnerId,
                    LanguageCode = "en",
                    Name = englishNameOut,
                    ShortName = englishShortNameOut,
                    Country = countryVal,
                    City = cityVal,
                    Description = englishDescriptionOut,
                    Address = englishAddressOut,
                    TranslationSource = "MANUAL",
                    TranslationStatus = "READY",
                    SourceHash = sourceHash,
                    TranslatedAt = now,
                    CreatedAt = now,
                    CreatedBy = _currentUser.UserId,
                });
            }
            else
            {
                enTranslation.Name = englishNameOut;
                enTranslation.ShortName = englishShortNameOut;
                enTranslation.Country = countryVal;
                enTranslation.City = cityVal;
                enTranslation.Description = englishDescriptionOut;
                enTranslation.Address = englishAddressOut;
                enTranslation.TranslationSource = "MANUAL";
                enTranslation.TranslationStatus = "READY";
                enTranslation.SourceHash = sourceHash;
                enTranslation.TranslatedAt = now;
                enTranslation.UpdatedAt = now;
                enTranslation.UpdatedBy = _currentUser.UserId;
            }
        }
        else if (enTranslation is null)
        {
            // EN panel was never opened for this partner — auto-translate once now so the profile
            // is never left English-less, same "translate exactly once at save time" rule as create.
            var translated = await _translator.TranslateTextAsync(
                new List<string> { name, shortName ?? string.Empty, descriptionVal ?? string.Empty, addressVal ?? string.Empty },
                NewsConstants.Languages.Default, "en", cancellationToken);
            englishNameOut = _sanitizer.Sanitize(translated[0]).Trim();
            if (string.IsNullOrWhiteSpace(englishNameOut)) englishNameOut = name;
            var t1 = _sanitizer.Sanitize(translated[1]).Trim();
            var t2 = _sanitizer.Sanitize(translated[2]).Trim();
            var t3 = _sanitizer.Sanitize(translated[3]).Trim();
            englishShortNameOut = string.IsNullOrWhiteSpace(t1) ? null : t1;
            englishDescriptionOut = string.IsNullOrWhiteSpace(t2) ? null : t2;
            englishAddressOut = string.IsNullOrWhiteSpace(t3) ? null : t3;

            _db.PartnerTranslations.Add(new PartnerTranslation
            {
                PartnerId = partner.PartnerId,
                LanguageCode = "en",
                Name = englishNameOut,
                ShortName = englishShortNameOut,
                Country = countryVal,
                City = cityVal,
                Description = englishDescriptionOut,
                Address = englishAddressOut,
                TranslationSource = "AUTO",
                TranslationStatus = "READY",
                SourceHash = sourceHash,
                TranslatedAt = now,
                CreatedAt = now,
                CreatedBy = _currentUser.UserId,
            });
        }
        else
        {
            // EN already exists and the panel wasn't opened this time — leave it untouched.
            englishNameOut = enTranslation.Name;
            englishShortNameOut = enTranslation.ShortName;
            englishDescriptionOut = enTranslation.Description;
            englishAddressOut = enTranslation.Address;
        }

        await _db.SaveChangesAsync(cancellationToken);

        return new UpdatePartnerResponse
        {
            PartnerId = partner.PartnerId,
            ProfileStatus = partner.ProfileStatus,
            EnglishName = englishNameOut,
            EnglishShortName = englishShortNameOut,
            EnglishDescription = englishDescriptionOut,
            EnglishAddress = englishAddressOut,
        };
    }

    /// <summary>SHA-256 of the Vietnamese source content a translation was derived from.</summary>
    private static string ComputeHash(params string?[] parts)
    {
        var joined = string.Join('\x1f', parts.Select(p => p ?? string.Empty));
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(joined));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
