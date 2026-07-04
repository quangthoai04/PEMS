using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Partners.Contacts.Common;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Partners.Contacts.Commands.SetPrimaryPartnerContact;

public sealed class SetPrimaryPartnerContactCommandHandler
    : IRequestHandler<SetPrimaryPartnerContactCommand, SetPrimaryPartnerContactResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;

    public SetPrimaryPartnerContactCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<SetPrimaryPartnerContactResponse> Handle(
        SetPrimaryPartnerContactCommand request, CancellationToken cancellationToken)
    {
        var partner = await PartnerContactWriteSupport.LoadPartnerForManageAsync(
            _db, _currentUser, request.PartnerId, cancellationToken);

        var contact = await _db.PartnerContacts
            .FirstOrDefaultAsync(c => c.ContactId == request.ContactId && c.PartnerId == partner.PartnerId, cancellationToken)
            ?? throw new NotFoundException("PartnerContact", request.ContactId);

        if (contact.Status != "ACTIVE")
            throw new BusinessRuleException("Không thể đặt người liên hệ INACTIVE làm liên hệ chính.",
                "PARTNER_CONTACT_INACTIVE");

        await PartnerContactWriteSupport.UnsetOtherPrimariesAsync(
            _db, partner.PartnerId, contact.ContactId, cancellationToken);

        var now = _clock.UtcNow;
        contact.IsPrimary = true;
        contact.UpdatedAt = now;
        contact.UpdatedBy = _currentUser.UserId;

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = _currentUser.UserId,
            CampusId = partner.OwnerCampusId,
            Action = "SET_PRIMARY_PARTNER_CONTACT",
            EntityType = "PartnerContact",
            EntityId = contact.ContactId,
            CreatedAt = now,
        });
        await _db.SaveChangesAsync(cancellationToken);

        return new SetPrimaryPartnerContactResponse { ContactId = contact.ContactId, IsPrimary = true };
    }
}
