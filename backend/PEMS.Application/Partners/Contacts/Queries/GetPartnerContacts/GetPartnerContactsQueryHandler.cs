using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Partners.Common;

namespace PEMS.Application.Partners.Contacts.Queries.GetPartnerContacts;

public sealed class GetPartnerContactsQueryHandler
    : IRequestHandler<GetPartnerContactsQuery, List<PartnerContactDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetPartnerContactsQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<List<PartnerContactDto>> Handle(
        GetPartnerContactsQuery request, CancellationToken cancellationToken)
    {
        var partner = await _db.Partners.AsNoTracking()
            .FirstOrDefaultAsync(p => p.PartnerId == request.PartnerId, cancellationToken)
            ?? throw new NotFoundException("Partner", request.PartnerId);

        if (!PartnerAccess.CanViewPartner(_currentUser, partner))
            throw new AuthBusinessException(PartnerErrorCodes.Forbidden,
                "Bạn không có quyền xem người liên hệ của đối tác này.", 403);

        var query = _db.PartnerContacts.AsNoTracking()
            .Where(c => c.PartnerId == request.PartnerId);
        if (!request.IncludeInactive)
            query = query.Where(c => c.Status == "ACTIVE");

        return await query
            .OrderByDescending(c => c.IsPrimary)
            .ThenBy(c => c.FullName)
            .Select(c => new PartnerContactDto
            {
                ContactId = c.ContactId,
                PartnerId = c.PartnerId,
                FullName = c.FullName,
                Email = c.Email,
                Phone = c.Phone,
                JobTitle = c.JobTitle,
                DepartmentName = c.DepartmentName,
                Note = c.Note,
                SourceType = c.SourceType,
                ScannedCardFileId = c.ScannedCardFileId,
                OcrConfidence = c.OcrConfidence,
                IsPrimary = c.IsPrimary,
                Status = c.Status,
                CreatedAt = c.CreatedAt,
            })
            .ToListAsync(cancellationToken);
    }
}
