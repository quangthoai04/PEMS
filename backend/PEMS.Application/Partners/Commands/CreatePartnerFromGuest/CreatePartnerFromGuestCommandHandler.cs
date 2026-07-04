using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Partners.Commands.CreatePartner;
using PEMS.Application.Partners.Common;
using PEMS.Application.Partners.VisitLinks.Commands.CreateOrUpdateVisitGuestPartnerLink;
using PEMS.Application.Partners.VisitLinks.Common;

namespace PEMS.Application.Partners.Commands.CreatePartnerFromGuest;

public sealed class CreatePartnerFromGuestCommandHandler
    : IRequestHandler<CreatePartnerFromGuestCommand, CreatePartnerFromGuestResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ISender _sender;

    public CreatePartnerFromGuestCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser, ISender sender)
    {
        _db = db;
        _currentUser = currentUser;
        _sender = sender;
    }

    public async Task<CreatePartnerFromGuestResponse> Handle(
        CreatePartnerFromGuestCommand request, CancellationToken cancellationToken)
    {
        if (request.GuestMemberId is null && request.MinuteParticipantId is null)
            throw new BusinessRuleException(
                "Phải chỉ định khách trong đoàn hoặc người tham gia biên bản.",
                PartnerErrorCodes.LinkTargetRequired);

        var instance = await VisitLinkSupport.LoadInstanceWithAccessAsync(
            _db, _currentUser, request.VisitInstanceId, cancellationToken);

        // Prefill từ snapshot khách/biên bản.
        string? organization = null, contactName = null, jobTitle = null, contactEmail = null;
        if (request.GuestMemberId is { } gid)
        {
            var guest = await _db.VisitGuestMembers.AsNoTracking()
                .FirstOrDefaultAsync(g => g.GuestMemberId == gid
                                          && g.VisitRequestId == instance.VisitRequestId, cancellationToken)
                ?? throw new NotFoundException("VisitGuestMember", gid);
            organization = guest.Organization;
            contactName = guest.FullName;
            jobTitle = guest.JobTitle;
        }
        else if (request.MinuteParticipantId is { } mid)
        {
            var participant = await (
                    from mp in _db.MinuteParticipants
                    join m in _db.Minutes on mp.MinutesId equals m.MinutesId
                    where mp.MinuteParticipantId == mid && m.VisitInstanceId == instance.VisitInstanceId
                    select mp)
                .AsNoTracking()
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new NotFoundException("MinuteParticipant", mid);
            organization = participant.OrganizationSnapshot;
            contactName = participant.FullNameSnapshot;
            jobTitle = participant.RoleSnapshot;
            contactEmail = participant.EmailSnapshot;
        }

        var partnerName = string.IsNullOrWhiteSpace(request.PartnerName) ? organization : request.PartnerName;
        if (string.IsNullOrWhiteSpace(partnerName))
            throw new BusinessRuleException("Không xác định được tên tổ chức để tạo đối tác.",
                PartnerErrorCodes.NameDuplicated);

        var created = await _sender.Send(new CreatePartnerCommand
        {
            PartnerCode = request.PartnerCode,
            Name = partnerName!,
            Country = request.Country,
            WebsiteUrl = request.WebsiteUrl,
            Description = request.Description,
            PartnerType = request.PartnerType,
            Source = "FROM_GUEST",
            InitialContact = string.IsNullOrWhiteSpace(contactName)
                ? null
                : new CreatePartnerCommand.InitialContactPayload
                {
                    FullName = contactName!,
                    Email = string.IsNullOrWhiteSpace(request.ContactEmail) ? contactEmail : request.ContactEmail,
                    Phone = request.ContactPhone,
                    JobTitle = jobTitle,
                },
        }, cancellationToken);

        var link = await _sender.Send(new CreateOrUpdateVisitGuestPartnerLinkCommand
        {
            VisitInstanceId = request.VisitInstanceId,
            GuestMemberId = request.GuestMemberId,
            MinuteParticipantId = request.MinuteParticipantId,
            PartnerId = created.PartnerId,
            PartnerContactId = created.InitialContactId,
            MatchSource = PartnerLinkMatchSources.CreatedFromGuest,
            MatchStatus = PartnerLinkMatchStatuses.Confirmed,
        }, cancellationToken);

        return new CreatePartnerFromGuestResponse
        {
            PartnerId = created.PartnerId,
            ProfileStatus = created.ProfileStatus,
            LinkId = link.LinkId,
            InitialContactId = created.InitialContactId,
        };
    }
}
