using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.BusinessCardOcr.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Partners.Common;
using PEMS.Domain.Entities.ApiIntegrations;

namespace PEMS.Application.BusinessCardOcr.Commands.DiscardBusinessCardOcrJob;

public sealed class DiscardBusinessCardOcrJobCommandHandler
    : IRequestHandler<DiscardBusinessCardOcrJobCommand, DiscardBusinessCardOcrJobResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;

    public DiscardBusinessCardOcrJobCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<DiscardBusinessCardOcrJobResponse> Handle(
        DiscardBusinessCardOcrJobCommand request, CancellationToken cancellationToken)
    {
        var job = await _db.BusinessCardOcrJobs
            .FirstOrDefaultAsync(j => j.OcrJobId == request.OcrJobId && j.DeletedAt == null, cancellationToken)
            ?? throw new NotFoundException("BusinessCardOcrJob", request.OcrJobId);

        // Owner or scoped roles only.
        if (job.CreatedBy != _currentUser.UserId
            && !PartnerAccess.IsStaffLeader(_currentUser)
            && !PartnerAccess.IsAdmin(_currentUser))
            throw new AuthBusinessException(BusinessCardOcrErrorCodes.Forbidden,
                "Bạn không có quyền huỷ OCR job này.", 403);

        if (job.Status == BusinessCardOcrJob.StatusConfirmed)
            throw new ConflictException("OCR job đã được xác nhận, không thể huỷ.",
                BusinessCardOcrErrorCodes.JobAlreadyConfirmed);

        job.Status = BusinessCardOcrJob.StatusDiscarded;
        // Drop the draft payload immediately — a discarded card leaves no readable OCR text behind.
        job.RawTextEncrypted = null;
        job.ParsedJsonEncrypted = null;
        job.DeletedAt = null;
        job.ProcessedAt ??= _clock.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        return new DiscardBusinessCardOcrJobResponse { OcrJobId = job.OcrJobId };
    }
}
