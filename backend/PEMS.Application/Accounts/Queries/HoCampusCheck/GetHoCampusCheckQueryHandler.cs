using MediatR;
using PEMS.Application.Accounts.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;

namespace PEMS.Application.Accounts.Queries.HoCampusCheck;

/// <summary>
/// UC-96 — HO campus pre-check. HO-only; campus is whatever HO picked in the modal. Delegates the
/// case-matrix evaluation to <see cref="HoCampusAvailability"/> so the hint can never disagree
/// with the write-side check in CreateAccount.
/// </summary>
public sealed class GetHoCampusCheckQueryHandler
    : IRequestHandler<GetHoCampusCheckQuery, HoCampusCheckDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetHoCampusCheckQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<HoCampusCheckDto> Handle(GetHoCampusCheckQuery request, CancellationToken cancellationToken)
    {
        // Only HO creates HO accounts, so only HO needs this pre-check (spec §11.1).
        if (_currentUser.RoleCode != RoleCodes.Ho)
            throw new ForbiddenException("Chỉ Head Office mới được kiểm tra khả năng tạo tài khoản HO.");

        if (request.CampusId == 0)
            throw new ValidationException("Vui lòng chọn cơ sở.");

        var result = await HoCampusAvailability.ResolveAsync(_db, request.CampusId, cancellationToken);

        return new HoCampusCheckDto
        {
            CampusId = result.CampusId,
            CampusName = result.CampusName,
            CanCreateHo = result.CanCreate,
            ExistingHo = result.Ho is null ? null : new ExistingHoDto
            {
                UserId = result.Ho.UserId,
                FullName = result.Ho.FullName,
                Email = result.Ho.Email,
                Status = result.Ho.Status,
            },
            ReasonCode = result.ReasonCode,
            Message = result.Message,
        };
    }
}
