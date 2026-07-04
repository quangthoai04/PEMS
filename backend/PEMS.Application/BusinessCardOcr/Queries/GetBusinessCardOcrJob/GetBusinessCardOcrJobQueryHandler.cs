using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.BusinessCardOcr.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Partners.Common;

namespace PEMS.Application.BusinessCardOcr.Queries.GetBusinessCardOcrJob;

public sealed class GetBusinessCardOcrJobQueryHandler
    : IRequestHandler<GetBusinessCardOcrJobQuery, BusinessCardOcrJobDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetBusinessCardOcrJobQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<BusinessCardOcrJobDto> Handle(
        GetBusinessCardOcrJobQuery request, CancellationToken cancellationToken)
    {
        var job = await _db.BusinessCardOcrJobs.AsNoTracking()
            .FirstOrDefaultAsync(j => j.OcrJobId == request.OcrJobId && j.DeletedAt == null, cancellationToken)
            ?? throw new NotFoundException("BusinessCardOcrJob", request.OcrJobId);

        if (job.CreatedBy != _currentUser.UserId
            && !PartnerAccess.IsStaffLeader(_currentUser)
            && !PartnerAccess.IsAdmin(_currentUser))
            throw new AuthBusinessException(BusinessCardOcrErrorCodes.Forbidden,
                "Bạn không có quyền xem OCR job này.", 403);

        var dto = new BusinessCardOcrJobDto
        {
            OcrJobId = job.OcrJobId,
            Status = job.Status,
            ProviderName = job.ProviderName,
            ConfidenceScore = job.ConfidenceScore,
            ScannedCardFileId = job.ScannedCardFileId,
            ConfirmedPartnerId = job.ConfirmedPartnerId,
            ConfirmedContactId = job.ConfirmedContactId,
            SourceVisitInstanceId = job.SourceVisitInstanceId,
            SourceGuestMemberId = job.SourceGuestMemberId,
            SourceMinuteParticipantId = job.SourceMinuteParticipantId,
            ErrorMessage = job.ErrorMessage,
            CreatedAt = job.CreatedAt,
            ProcessedAt = job.ProcessedAt,
            Parsed = new BusinessCardOcrParsedDto
            {
                FullName = job.ParsedFullName,
                Email = job.ParsedEmail,
                Phone = job.ParsedPhone,
                JobTitle = job.ParsedJobTitle,
                DepartmentName = job.ParsedDepartmentName,
                Organization = job.ParsedOrganization,
                WebsiteUrl = job.ParsedWebsiteUrl,
                Address = job.ParsedAddress,
            },
        };

        if (job.MatchedPartnerId is { } matchedId)
        {
            var partner = await _db.Partners.AsNoTracking()
                .Where(p => p.PartnerId == matchedId)
                .Select(p => new { p.Name, p.ProfileStatus })
                .FirstOrDefaultAsync(cancellationToken);
            if (partner is not null)
            {
                dto.MatchedPartner = new BusinessCardOcrMatchedPartnerDto
                {
                    PartnerId = matchedId,
                    PartnerName = partner.Name,
                    ProfileStatus = partner.ProfileStatus,
                    Confidence = job.ConfidenceScore ?? 0,
                    Reason = "Matched by organization/email",
                };
            }
        }

        return dto;
    }
}
