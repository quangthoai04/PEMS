using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.BusinessCardOcr.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Partners.Common;
using PEMS.Application.Partners.Contacts.Common;
using PEMS.Application.Partners.VisitLinks.Commands.CreateOrUpdateVisitGuestPartnerLink;
using PEMS.Domain.Entities.ApiIntegrations;
using PEMS.Domain.Entities.Partners;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.BusinessCardOcr.Commands.ConfirmBusinessCardContact;

public sealed class ConfirmBusinessCardContactCommandHandler
    : IRequestHandler<ConfirmBusinessCardContactCommand, ConfirmBusinessCardContactResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;
    private readonly ISender _sender;

    public ConfirmBusinessCardContactCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock, ISender sender)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _sender = sender;
    }

    public async Task<ConfirmBusinessCardContactResponse> Handle(
        ConfirmBusinessCardContactCommand request, CancellationToken cancellationToken)
    {
        // 1–2) Job must exist and be a reviewable SUCCEEDED draft.
        var job = await _db.BusinessCardOcrJobs
            .FirstOrDefaultAsync(j => j.OcrJobId == request.OcrJobId && j.DeletedAt == null, cancellationToken)
            ?? throw new NotFoundException("BusinessCardOcrJob", request.OcrJobId);

        if (job.Status == BusinessCardOcrJob.StatusConfirmed)
            throw new ConflictException("OCR job này đã được xác nhận trước đó.",
                BusinessCardOcrErrorCodes.JobAlreadyConfirmed);
        if (job.Status != BusinessCardOcrJob.StatusSucceeded)
            throw new BusinessRuleException(
                "Chỉ xác nhận được OCR job ở trạng thái SUCCEEDED.",
                BusinessCardOcrErrorCodes.JobNotConfirmable);

        // Only the scanner (owner) or a same-campus manager confirms the draft.
        if (job.CreatedBy != _currentUser.UserId && !PartnerAccess.IsStaffLeader(_currentUser)
            && !PartnerAccess.IsAdmin(_currentUser))
            throw new AuthBusinessException(BusinessCardOcrErrorCodes.Forbidden,
                "Bạn không có quyền xác nhận OCR job này.", 403);

        // 3) Partner scope.
        var partner = await PartnerContactWriteSupport.LoadPartnerForManageAsync(
            _db, _currentUser, request.PartnerId, cancellationToken);

        // 4–6) Field validation (validator) + duplicate email within the partner.
        await PartnerContactWriteSupport.EnsureEmailUniqueAsync(
            _db, partner.PartnerId, request.Email, null, cancellationToken);

        var now = _clock.VietnamNow;

        // 7) Create the contact from the REVIEWED values (not blindly from OCR). Trim-only storage —
        // symmetric with Create/UpdatePartnerContactCommandHandler: the reviewer confirmed this exact
        // text off the card, so it is what gets persisted, not a reformatted/lowercased version of it.
        var contact = new PartnerContact
        {
            PartnerId = partner.PartnerId,
            FullName = request.FullName.Trim(),
            Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
            Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
            JobTitle = string.IsNullOrWhiteSpace(request.JobTitle) ? null : request.JobTitle.Trim(),
            DepartmentName = string.IsNullOrWhiteSpace(request.DepartmentName) ? null : request.DepartmentName.Trim(),
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            SourceType = "BUSINESS_CARD_OCR",
            ScannedCardFileId = job.ScannedCardFileId,
            OcrConfidence = job.ConfidenceScore,
            IsPrimary = request.IsPrimary,
            Status = "ACTIVE",
            CreatedAt = now,
            CreatedBy = _currentUser.UserId,
        };

        // 8) One ACTIVE primary per partner.
        if (request.IsPrimary)
            await PartnerContactWriteSupport.UnsetOtherPrimariesAsync(_db, partner.PartnerId, null, cancellationToken);

        await using var transaction = await _db.BeginTransactionAsync(cancellationToken);
        try
        {
            _db.PartnerContacts.Add(contact);
            await _db.SaveChangesAsync(cancellationToken); // gives ContactId

            // 9) Flip the job to CONFIRMED exactly once.
            job.Status = BusinessCardOcrJob.StatusConfirmed;
            job.ConfirmedPartnerId = partner.PartnerId;
            job.ConfirmedContactId = contact.ContactId;
            job.ConfirmedBy = _currentUser.UserId;
            job.ConfirmedAt = now;

            // 11) Audit.
            _db.AuditLogs.Add(new AuditLog
            {
                ActorUserId = _currentUser.UserId,
                CampusId = partner.OwnerCampusId,
                Action = "CREATE_PARTNER_CONTACT",
                EntityType = "PartnerContact",
                EntityId = contact.ContactId,
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

        // 10) Visit context → visit_guest_partner_links (after the confirm committed).
        ulong? linkId = null;
        var visitInstanceId = request.VisitInstanceId ?? job.SourceVisitInstanceId;
        var guestMemberId = request.GuestMemberId ?? job.SourceGuestMemberId;
        var minuteParticipantId = request.MinuteParticipantId ?? job.SourceMinuteParticipantId;
        if (visitInstanceId is { } vid && (guestMemberId is not null || minuteParticipantId is not null))
        {
            var link = await _sender.Send(new CreateOrUpdateVisitGuestPartnerLinkCommand
            {
                VisitInstanceId = vid,
                GuestMemberId = guestMemberId,
                MinuteParticipantId = minuteParticipantId,
                PartnerId = partner.PartnerId,
                PartnerContactId = contact.ContactId,
                MatchSource = PartnerLinkMatchSources.BusinessCardOcr,
                MatchStatus = PartnerLinkMatchStatuses.Confirmed,
                ConfidenceScore = job.ConfidenceScore,
            }, cancellationToken);
            linkId = link.LinkId;
        }

        return new ConfirmBusinessCardContactResponse
        {
            OcrJobId = job.OcrJobId,
            Status = job.Status,
            PartnerId = partner.PartnerId,
            ContactId = contact.ContactId,
            VisitGuestPartnerLinkId = linkId,
        };
    }
}
