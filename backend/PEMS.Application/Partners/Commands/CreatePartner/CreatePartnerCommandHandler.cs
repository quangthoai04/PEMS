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
using Microsoft.Extensions.Logging;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;
using PEMS.Application.News.Services;
using PEMS.Application.Partners.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Partners;
using PEMS.Domain.Entities.Users;
using INotificationService = PEMS.Application.Notifications.Common.INotificationService;

namespace PEMS.Application.Partners.Commands.CreatePartner;

public sealed class CreatePartnerCommandHandler : IRequestHandler<CreatePartnerCommand, CreatePartnerResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;
    private readonly INotificationService _notifications;
    private readonly INewsTranslationService _translator;
    private readonly IHtmlSanitizerService _sanitizer;
    private readonly ILogger<CreatePartnerCommandHandler> _logger;

    public CreatePartnerCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IDateTimeService clock,
        INotificationService notifications,
        INewsTranslationService translator,
        IHtmlSanitizerService sanitizer,
        ILogger<CreatePartnerCommandHandler> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _notifications = notifications;
        _translator = translator;
        _sanitizer = sanitizer;
        _logger = logger;
    }

    public async Task<CreatePartnerResponse> Handle(CreatePartnerCommand request, CancellationToken cancellationToken)
    {
        if (!PartnerAccess.CanCreatePartner(_currentUser))
            throw new AuthBusinessException(PartnerErrorCodes.Forbidden,
                "Chỉ IC Staff / Trưởng phòng IC mới được tạo đối tác.", 403);

        if (_currentUser.PrimaryCampusId is not { } ownerCampusId || ownerCampusId == 0)
            throw new BusinessRuleException("Tài khoản của bạn chưa gắn campus nên không thể tạo đối tác.",
                PartnerErrorCodes.WrongCampus);

        var name = request.Name.Trim();
        var code = string.IsNullOrWhiteSpace(request.PartnerCode) ? null : request.PartnerCode.Trim().ToUpperInvariant();

        // PUBLIC while not yet APPROVED is blocked — new partners start PENDING_APPROVAL.
        var visibility = string.IsNullOrWhiteSpace(request.Visibility)
            ? PartnerVisibilities.Internal
            : request.Visibility!;
        if (visibility == PartnerVisibilities.Public)
            throw new BusinessRuleException(
                "Không thể đặt hiển thị PUBLIC khi hồ sơ đối tác chưa được duyệt.",
                PartnerErrorCodes.PublicRequiresApproved);

        if (code is not null &&
            await _db.Partners.AnyAsync(p => p.PartnerCode == code, cancellationToken))
            throw new ConflictException("Mã đối tác đã tồn tại.", PartnerErrorCodes.CodeDuplicated);

        // Duplicate normalized-name guard (block — one org, one profile).
        var nameKey = PartnerNormalization.NormalizeKey(name);
        var sameNames = await _db.Partners
            .Select(p => new { p.PartnerId, p.Name })
            .ToListAsync(cancellationToken);
        if (sameNames.Any(p => PartnerNormalization.NormalizeKey(p.Name) == nameKey))
            throw new ConflictException("Tên đối tác đã tồn tại.", PartnerErrorCodes.NameDuplicated);

        var now = _clock.VietnamNow;
        var actorId = _currentUser.UserId;

        var shortName = string.IsNullOrWhiteSpace(request.ShortName) ? null : request.ShortName.Trim();
        var countryVal = string.IsNullOrWhiteSpace(request.Country) ? null : request.Country.Trim();
        var cityVal = string.IsNullOrWhiteSpace(request.City) ? null : request.City.Trim();
        var addressVal = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim();
        var descriptionVal = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();

        var partner = new Partner
        {
            OwnerCampusId = ownerCampusId,
            PartnerCode = code!,
            Name = name,
            ShortName = shortName,
            Country = countryVal,
            City = cityVal,
            WebsiteUrl = string.IsNullOrWhiteSpace(request.WebsiteUrl) ? null : request.WebsiteUrl.Trim(),
            Address = addressVal,
            Description = descriptionVal,
            PartnerType = string.IsNullOrWhiteSpace(request.PartnerType) ? "UNIVERSITY" : request.PartnerType!,
            CooperationStatus = "POTENTIAL",
            ProfileStatus = PartnerProfileStatuses.PendingApproval,
            Visibility = visibility,
            LogoFileId = request.LogoFileId,
            CoverFileId = request.CoverFileId,
            CreatedAt = now,
            CreatedBy = actorId,
        };

        // English: content provided by the admin (EN panel was opened — machine-translated preview
        // or hand-edited) wins as-is (MANUAL). Otherwise the backend translates once, right now, so
        // a partner profile is never created English-less. country/city are proper nouns, copied
        // through unchanged rather than machine translated.
        var providedEnglishName = _sanitizer.Sanitize(request.EnglishName ?? string.Empty).Trim();
        var providedEnglishShortName = _sanitizer.Sanitize(request.EnglishShortName ?? string.Empty).Trim();
        var providedEnglishDescription = _sanitizer.Sanitize(request.EnglishDescription ?? string.Empty).Trim();
        var providedEnglishAddress = _sanitizer.Sanitize(request.EnglishAddress ?? string.Empty).Trim();
        var englishProvided = !string.IsNullOrWhiteSpace(providedEnglishName);

        // Null englishName means "translation unavailable" (never requested, or the auto-translate
        // attempt below failed) — the EN row is then simply not created; public reads already fall
        // back requested language → vi, and the admin can translate later via the EN panel.
        string? englishName;
        string? englishShortName = null;
        string? englishDescription = null;
        string? englishAddress = null;
        string englishSource = "AUTO";
        if (englishProvided)
        {
            englishName = providedEnglishName;
            englishShortName = string.IsNullOrWhiteSpace(providedEnglishShortName) ? null : providedEnglishShortName;
            englishDescription = string.IsNullOrWhiteSpace(providedEnglishDescription) ? null : providedEnglishDescription;
            englishAddress = string.IsNullOrWhiteSpace(providedEnglishAddress) ? null : providedEnglishAddress;
            englishSource = "MANUAL";
        }
        else
        {
            // Best-effort only: a translation-provider hiccup (quota, HTTP 400, config) must never
            // block creating the partner itself — many callers (e.g. the OCR/guest quick-create
            // flow) only ever supply a name and never intended to wait on a translation at all.
            try
            {
                var translated = await _translator.TranslateTextAsync(
                    new List<string> { name, shortName ?? string.Empty, descriptionVal ?? string.Empty, addressVal ?? string.Empty },
                    NewsConstants.Languages.Default, "en", cancellationToken);
                var t0 = _sanitizer.Sanitize(translated[0]).Trim();
                var t1 = _sanitizer.Sanitize(translated[1]).Trim();
                var t2 = _sanitizer.Sanitize(translated[2]).Trim();
                var t3 = _sanitizer.Sanitize(translated[3]).Trim();
                englishName = string.IsNullOrWhiteSpace(t0) ? name : t0;
                englishShortName = string.IsNullOrWhiteSpace(t1) ? null : t1;
                englishDescription = string.IsNullOrWhiteSpace(t2) ? null : t2;
                englishAddress = string.IsNullOrWhiteSpace(t3) ? null : t3;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Auto-translate to English failed for partner \"{Name}\"; saving Vietnamese-only for now.",
                    name);
                englishName = null;
            }
        }

        var sourceHash = ComputePartnerHash(name, shortName, countryVal, cityVal, descriptionVal, addressVal);

        PartnerContact? contact = null;

        await using var transaction = await _db.BeginTransactionAsync(cancellationToken);
        try
        {
            _db.Partners.Add(partner);
            await _db.SaveChangesAsync(cancellationToken); // populates PartnerId

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
                CreatedBy = actorId,
            });
            if (englishName is not null)
            {
                _db.PartnerTranslations.Add(new PartnerTranslation
                {
                    PartnerId = partner.PartnerId,
                    LanguageCode = "en",
                    Name = englishName,
                    ShortName = englishShortName,
                    Country = countryVal,
                    City = cityVal,
                    Description = englishDescription,
                    Address = englishAddress,
                    TranslationSource = englishSource,
                    TranslationStatus = "READY",
                    SourceHash = sourceHash,
                    TranslatedAt = now,
                    CreatedAt = now,
                    CreatedBy = actorId,
                });
            }

            // The official name doubles as the first ACTIVE alias so future matching hits it.
            _db.PartnerAliases.Add(new PartnerAlias
            {
                PartnerId = partner.PartnerId,
                AliasName = name,
                AliasNameKey = nameKey,
                Source = request.Source == "BUSINESS_CARD_OCR" ? "OCR" : "MANUAL",
                Status = "ACTIVE",
                CreatedAt = now,
                CreatedBy = actorId,
            });

            if (request.InitialContact is { } ic)
            {
                contact = new PartnerContact
                {
                    PartnerId = partner.PartnerId,
                    FullName = ic.FullName.Trim(),
                    Email = string.IsNullOrWhiteSpace(ic.Email) ? null : ic.Email.Trim().ToLowerInvariant(),
                    Phone = string.IsNullOrWhiteSpace(ic.Phone) ? null : ic.Phone.Trim(),
                    JobTitle = string.IsNullOrWhiteSpace(ic.JobTitle) ? null : ic.JobTitle.Trim(),
                    DepartmentName = string.IsNullOrWhiteSpace(ic.DepartmentName) ? null : ic.DepartmentName.Trim(),
                    Note = string.IsNullOrWhiteSpace(ic.Note) ? null : ic.Note.Trim(),
                    SourceType = request.Source == "BUSINESS_CARD_OCR" ? "BUSINESS_CARD_OCR" : "MANUAL",
                    IsPrimary = true,
                    Status = "ACTIVE",
                    CreatedAt = now,
                    CreatedBy = actorId,
                };
                _db.PartnerContacts.Add(contact);
            }

            _db.AuditLogs.Add(new AuditLog
            {
                ActorUserId = actorId,
                CampusId = ownerCampusId,
                Action = "CREATE_PARTNER",
                EntityType = "Partner",
                EntityId = partner.PartnerId,
                Changes = new List<AuditLogChange>
                {
                    new()
                    {
                        FieldName = "Partner",
                        NewValueText = JsonSerializer.Serialize(new
                        {
                            partner.PartnerCode, partner.Name, partner.Country,
                            partner.ProfileStatus, partner.Visibility, partner.OwnerCampusId,
                        }),
                    },
                },
                CreatedAt = now,
            });

            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        // Notify Staff Leaders of the owner campus (outside the transaction — best effort).
        var leaderIds = await _db.Users
            .Where(u => u.Status == "ACTIVE"
                        && u.PrimaryCampusId == ownerCampusId
                        && u.Role.RoleCode == RoleCodes.Staff
                        && u.SubRole == UserSubRoles.Leader)
            .Select(u => u.UserId)
            .ToListAsync(cancellationToken);
        foreach (var leaderId in leaderIds.Where(id => id != actorId))
        {
            await _notifications.CreateAsync(
                new PEMS.Application.Notifications.Common.CreateNotificationRequest(
                    RecipientUserId: leaderId,
                    Title: "Đối tác mới chờ duyệt",
                    Message: $"Đối tác \"{partner.Name}\" vừa được tạo và đang chờ duyệt.",
                    NotificationType: NotificationTypes.PartnerPendingApproval,
                    RelatedType: "Partner",
                    RelatedId: partner.PartnerId,
                    ActorUserId: actorId,
                    Category: PEMS.Application.Notifications.Common.NotificationCategories.Partner,
                    IsActionRequired: true,
                    CampusId: ownerCampusId,
                    ActionType: PEMS.Application.Notifications.Common.NotificationActionTypes.OpenPartnerDetail,
                    // Trang quản lý đối tác lọc đúng 1 hồ sơ (có nút "Xem tất cả"), không vào thẳng chi tiết.
                    ActionUrl: $"/dashboard/partners?partnerId={partner.PartnerId}"),
                cancellationToken);
        }

        return new CreatePartnerResponse
        {
            PartnerId = partner.PartnerId,
            ProfileStatus = partner.ProfileStatus,
            OwnerCampusId = partner.OwnerCampusId,
            InitialContactId = contact?.ContactId,
            EnglishName = englishName,
            EnglishShortName = englishShortName,
            EnglishDescription = englishDescription,
            EnglishAddress = englishAddress,
        };
    }

    /// <summary>SHA-256 of the Vietnamese source content a translation was derived from.</summary>
    private static string ComputePartnerHash(params string?[] parts)
    {
        var joined = string.Join('\x1f', parts.Select(p => p ?? string.Empty));
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(joined));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
