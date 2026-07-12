using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Partners.Common;
using PEMS.Application.Partners.Contacts.Common;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Partners.Contacts.Commands.UpdatePartnerContact;

public sealed class UpdatePartnerContactCommandHandler
    : IRequestHandler<UpdatePartnerContactCommand, PartnerContactDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;

    public UpdatePartnerContactCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<PartnerContactDto> Handle(UpdatePartnerContactCommand request, CancellationToken cancellationToken)
    {
        var partner = await PartnerContactWriteSupport.LoadPartnerForManageAsync(
            _db, _currentUser, request.PartnerId, cancellationToken);

        var contact = await _db.PartnerContacts
            .FirstOrDefaultAsync(c => c.ContactId == request.ContactId && c.PartnerId == partner.PartnerId, cancellationToken)
            ?? throw new NotFoundException("PartnerContact", request.ContactId);

        await PartnerContactWriteSupport.EnsureEmailUniqueAsync(
            _db, partner.PartnerId, request.Email, contact.ContactId, cancellationToken);

        var now = _clock.VietnamNow;
        contact.FullName = request.FullName.Trim();
        contact.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim().ToLowerInvariant();
        contact.Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
        contact.JobTitle = string.IsNullOrWhiteSpace(request.JobTitle) ? null : request.JobTitle.Trim();
        contact.DepartmentName = string.IsNullOrWhiteSpace(request.DepartmentName) ? null : request.DepartmentName.Trim();
        contact.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        contact.UpdatedAt = now;
        contact.UpdatedBy = _currentUser.UserId;

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = _currentUser.UserId,
            CampusId = partner.OwnerCampusId,
            Action = "UPDATE_PARTNER_CONTACT",
            EntityType = "PartnerContact",
            EntityId = contact.ContactId,
            CreatedAt = now,
        });
        await _db.SaveChangesAsync(cancellationToken);

        return new PartnerContactDto
        {
            ContactId = contact.ContactId,
            PartnerId = contact.PartnerId,
            FullName = contact.FullName,
            Email = contact.Email,
            Phone = contact.Phone,
            JobTitle = contact.JobTitle,
            DepartmentName = contact.DepartmentName,
            Note = contact.Note,
            SourceType = contact.SourceType,
            ScannedCardFileId = contact.ScannedCardFileId,
            OcrConfidence = contact.OcrConfidence,
            IsPrimary = contact.IsPrimary,
            Status = contact.Status,
            CreatedAt = contact.CreatedAt,
        };
    }
}
