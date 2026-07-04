using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Partners.Contacts.Common;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Partners.Contacts.Commands.DeactivatePartnerContact;

public sealed class DeactivatePartnerContactCommandHandler
    : IRequestHandler<DeactivatePartnerContactCommand, DeactivatePartnerContactResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;

    public DeactivatePartnerContactCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<DeactivatePartnerContactResponse> Handle(
        DeactivatePartnerContactCommand request, CancellationToken cancellationToken)
    {
        var partner = await PartnerContactWriteSupport.LoadPartnerForManageAsync(
            _db, _currentUser, request.PartnerId, cancellationToken);

        var contact = await _db.PartnerContacts
            .FirstOrDefaultAsync(c => c.ContactId == request.ContactId && c.PartnerId == partner.PartnerId, cancellationToken)
            ?? throw new NotFoundException("PartnerContact", request.ContactId);

        var now = _clock.UtcNow;
        contact.Status = "INACTIVE";
        contact.IsPrimary = false; // an inactive contact cannot stay the primary
        contact.UpdatedAt = now;
        contact.UpdatedBy = _currentUser.UserId;

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = _currentUser.UserId,
            CampusId = partner.OwnerCampusId,
            Action = "DEACTIVATE_PARTNER_CONTACT",
            EntityType = "PartnerContact",
            EntityId = contact.ContactId,
            CreatedAt = now,
        });
        await _db.SaveChangesAsync(cancellationToken);

        return new DeactivatePartnerContactResponse { ContactId = contact.ContactId };
    }
}
