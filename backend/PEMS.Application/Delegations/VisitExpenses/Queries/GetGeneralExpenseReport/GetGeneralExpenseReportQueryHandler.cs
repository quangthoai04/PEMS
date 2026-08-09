using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.VisitExpenses.Models;
using PEMS.Domain.Entities.Delegations;
using System.Threading;
using System.Threading.Tasks;

namespace PEMS.Application.Delegations.VisitExpenses.Queries.GetGeneralExpenseReport;

/// <summary>
/// The read half of what used to be one get-or-create command.
///
/// GET is a read. The previous endpoint tried to CREATE the report on every fetch, so merely opening the
/// tab on a campus that had not finished — or on a closed one that never filed costs — produced a 403 and
/// a red toast, for a screen the user had opened to look at. Creation now lives in its own POST, and this
/// query answers with the report or with nothing at all.
/// </summary>
public sealed class GetGeneralExpenseReportQueryHandler
    : IRequestHandler<GetGeneralExpenseReportQuery, VisitExpenseReportDto?>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetGeneralExpenseReportQueryHandler(
        IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<VisitExpenseReportDto?> Handle(
        GetGeneralExpenseReportQuery request, CancellationToken cancellationToken)
    {
        var exists = await _context.VisitRequestCampuses.AsNoTracking()
            .AnyAsync(v => v.VisitInstanceId == request.VisitInstanceId, cancellationToken);
        if (!exists)
            throw new NotFoundException(nameof(VisitRequestCampus), request.VisitInstanceId);

        // Same read scope as every other expense read: Host, own-campus Staff Leader, HO/Admin, or a
        // department holding a logistics report here.
        await VisitExpenseAccessScope.EnsureCanViewAsync(
            _context, _currentUserService, request.VisitInstanceId, cancellationToken);

        var report = await _context.VisitExpenseReports.AsNoTracking()
            .Include(r => r.Items)
            .FirstOrDefaultAsync(
                r => r.VisitInstanceId == request.VisitInstanceId && r.ReportScope == "GENERAL",
                cancellationToken);

        return report is null ? null : VisitExpenseReportMapper.ToDto(report);
    }
}
