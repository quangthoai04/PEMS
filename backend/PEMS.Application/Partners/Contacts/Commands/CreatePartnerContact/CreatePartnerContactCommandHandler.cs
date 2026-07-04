using System.Threading;
using System.Threading.Tasks;
using MediatR;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Partners.Common;
using PEMS.Application.Partners.Contacts.Common;
using PEMS.Domain.Entities.Partners;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Partners.Contacts.Commands.CreatePartnerContact;

public sealed class CreatePartnerContactCommandHandler
    : IRequestHandler<CreatePartnerContactCommand, PartnerContactDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;

    public CreatePartnerContactCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<PartnerContactDto> Handle(CreatePartnerContactCommand request, CancellationToken cancellationToken)
    {
        var partner = await PartnerContactWriteSupport.LoadPartnerForManageAsync(
            _db, _currentUser, request.PartnerId, cancellationToken);

        await PartnerContactWriteSupport.EnsureEmailUniqueAsync(
            _db, partner.PartnerId, request.Email, null, cancellationToken);

        var now = _clock.UtcNow;
        var contact = new PartnerContact
        {
            PartnerId = partner.PartnerId,
            FullName = request.FullName.Trim(),
            Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim().ToLowerInvariant(),
            Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
            JobTitle = string.IsNullOrWhiteSpace(request.JobTitle) ? null : request.JobTitle.Trim(),
            DepartmentName = string.IsNullOrWhiteSpace(request.DepartmentName) ? null : request.DepartmentName.Trim(),
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            SourceType = "MANUAL",
            IsPrimary = request.IsPrimary,
            Status = "ACTIVE",
            CreatedAt = now,
            CreatedBy = _currentUser.UserId,
        };

        if (request.IsPrimary)
            await PartnerContactWriteSupport.UnsetOtherPrimariesAsync(_db, partner.PartnerId, null, cancellationToken);

        _db.PartnerContacts.Add(contact);
        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = _currentUser.UserId,
            CampusId = partner.OwnerCampusId,
            Action = "CREATE_PARTNER_CONTACT",
            EntityType = "PartnerContact",
            EntityId = null,
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
            IsPrimary = contact.IsPrimary,
            Status = contact.Status,
            CreatedAt = contact.CreatedAt,
        };
    }
}
