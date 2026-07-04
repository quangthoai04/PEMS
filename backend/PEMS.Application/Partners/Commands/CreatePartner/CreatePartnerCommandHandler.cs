using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
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

    public CreatePartnerCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IDateTimeService clock,
        INotificationService notifications)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _notifications = notifications;
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

        var now = _clock.UtcNow;
        var actorId = _currentUser.UserId;

        var partner = new Partner
        {
            OwnerCampusId = ownerCampusId,
            PartnerCode = code!,
            Name = name,
            ShortName = string.IsNullOrWhiteSpace(request.ShortName) ? null : request.ShortName.Trim(),
            Country = string.IsNullOrWhiteSpace(request.Country) ? null : request.Country.Trim(),
            City = string.IsNullOrWhiteSpace(request.City) ? null : request.City.Trim(),
            WebsiteUrl = string.IsNullOrWhiteSpace(request.WebsiteUrl) ? null : request.WebsiteUrl.Trim(),
            Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            PartnerType = string.IsNullOrWhiteSpace(request.PartnerType) ? "UNIVERSITY" : request.PartnerType!,
            CooperationStatus = "POTENTIAL",
            ProfileStatus = PartnerProfileStatuses.PendingApproval,
            Visibility = visibility,
            LogoFileId = request.LogoFileId,
            CoverFileId = request.CoverFileId,
            CreatedAt = now,
            CreatedBy = actorId,
        };

        PartnerContact? contact = null;

        await using var transaction = await _db.BeginTransactionAsync(cancellationToken);
        try
        {
            _db.Partners.Add(partner);
            await _db.SaveChangesAsync(cancellationToken); // populates PartnerId

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
                leaderId,
                "Đối tác mới chờ duyệt",
                $"Đối tác \"{partner.Name}\" vừa được tạo và đang chờ duyệt.",
                NotificationTypes.PartnerPendingApproval,
                "Partner",
                partner.PartnerId,
                cancellationToken);
        }

        return new CreatePartnerResponse
        {
            PartnerId = partner.PartnerId,
            ProfileStatus = partner.ProfileStatus,
            OwnerCampusId = partner.OwnerCampusId,
            InitialContactId = contact?.ContactId,
        };
    }
}
