using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.VisitExpenses.Models;
using PEMS.Domain.Entities.Delegations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PEMS.Application.Delegations.VisitExpenses.Queries.GetVisitInstanceExpenseSummary;

public class GetVisitInstanceExpenseSummaryCommandHandler : IRequestHandler<GetVisitInstanceExpenseSummaryQuery, VisitInstanceExpenseSummaryDto>
{
    private readonly IApplicationDbContext _context;

    public GetVisitInstanceExpenseSummaryCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<VisitInstanceExpenseSummaryDto> Handle(GetVisitInstanceExpenseSummaryQuery request, CancellationToken cancellationToken)
    {
        var instance = await _context.VisitRequestCampuses
            .FirstOrDefaultAsync(v => v.VisitInstanceId == request.VisitInstanceId, cancellationToken);

        if (instance == null)
            throw new NotFoundException(nameof(VisitRequestCampus), request.VisitInstanceId);

        var reports = await _context.VisitExpenseReports
            .Include(r => r.Items)
            .Where(r => r.VisitInstanceId == request.VisitInstanceId && r.Status != "CANCELLED")
            .ToListAsync(cancellationToken);

        var dto = new VisitInstanceExpenseSummaryDto
        {
            VisitInstanceId = request.VisitInstanceId,
            TotalAmount = 0
        };

        var generalReport = reports.FirstOrDefault(r => r.ReportScope == "GENERAL");
        if (generalReport != null)
        {
            dto.GeneralReport = MapToDto(generalReport);
            dto.TotalAmount += dto.GeneralReport.TotalAmount;
        }

        var logisticsReports = reports.Where(r => r.ReportScope == "LOGISTICS").ToList();
        foreach (var lr in logisticsReports)
        {
            var lrDto = MapToDto(lr);
            dto.LogisticsReports.Add(lrDto);
            dto.TotalAmount += lrDto.TotalAmount;
        }

        return dto;
    }

    private VisitExpenseReportDto MapToDto(VisitExpenseReport entity)
    {
        return new VisitExpenseReportDto
        {
            ExpenseReportId = entity.ExpenseReportId,
            VisitInstanceId = entity.VisitInstanceId,
            ReportScope = entity.ReportScope,
            LogisticsItemId = entity.LogisticsItemId,
            DepartmentId = entity.DepartmentId,
            Status = entity.Status,
            ReportNote = entity.ReportNote,
            CurrencyCode = entity.CurrencyCode,
            RowVersion = entity.RowVersion,
            CreatedAt = entity.CreatedAt,
            TotalAmount = entity.Items.Sum(i => i.Quantity * i.UnitPrice),
            Items = entity.Items.OrderBy(i => i.DisplayOrder).Select(i => new VisitExpenseItemDto
            {
                ExpenseItemId = i.ExpenseItemId,
                ItemOrigin = i.ItemOrigin,
                ItemName = i.ItemName,
                Description = i.Description,
                Quantity = i.Quantity,
                UnitName = i.UnitName,
                UnitPrice = i.UnitPrice,
                TotalAmount = i.Quantity * i.UnitPrice,
                ItemNote = i.ItemNote,
                DisplayOrder = i.DisplayOrder,
                RowVersion = i.RowVersion
            }).ToList()
        };
    }
}
