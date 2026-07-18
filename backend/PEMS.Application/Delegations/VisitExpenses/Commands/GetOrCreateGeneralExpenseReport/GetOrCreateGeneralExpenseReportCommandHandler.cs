using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;
using PEMS.Domain.Entities.Delegations;
using PEMS.Application.Delegations.VisitExpenses.Models;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PEMS.Application.Delegations.VisitExpenses.Commands.GetOrCreateGeneralExpenseReport;

public class GetOrCreateGeneralExpenseReportCommandHandler : IRequestHandler<GetOrCreateGeneralExpenseReportCommand, VisitExpenseReportDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetOrCreateGeneralExpenseReportCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<VisitExpenseReportDto> Handle(GetOrCreateGeneralExpenseReportCommand request, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId ?? throw new UnauthorizedAccessException();

        // Validate instance
        var instance = await _context.VisitRequestCampuses
            .Include(v => v.VisitRequest)
            .FirstOrDefaultAsync(v => v.VisitInstanceId == request.VisitInstanceId, cancellationToken);

        if (instance == null)
            throw new NotFoundException(nameof(VisitRequestCampus), request.VisitInstanceId);

        // Security: Host ownership
        bool isHost = instance.CurrentHostUserId == currentUserId;
        bool isReadOnly = instance.Status == PEMS.Shared.VisitInstanceStatus.Closed;

        // Existing report?
        var existingReport = await _context.VisitExpenseReports
            .Include(r => r.Items)
            .FirstOrDefaultAsync(r => r.VisitInstanceId == request.VisitInstanceId && r.ReportScope == "GENERAL", cancellationToken);

        if (existingReport != null)
        {
            return MapToDto(existingReport);
        }

        // Must be host to create and must be in AFTER_VISIT (or IN_PROGRESS if allowed, but prompt says AFTER_VISIT)
        // Actually, if it's not created yet, and they just want to view it, maybe they can't.
        // But Host needs to create it.
        if (!isHost)
            throw new ForbiddenException("Only the Host can initialize the general expense report.");

        if (instance.Status != PEMS.Shared.VisitInstanceStatus.AfterVisit)
            throw new ForbiddenException("Can only initialize expense report in AFTER_VISIT state.");

        // Idempotent creation
        using var transaction = await _context.BeginTransactionAsync(cancellationToken);
        try
        {
            // Re-check after lock/transaction
            existingReport = await _context.VisitExpenseReports
                .Include(r => r.Items)
                .FirstOrDefaultAsync(r => r.VisitInstanceId == request.VisitInstanceId && r.ReportScope == "GENERAL", cancellationToken);

            if (existingReport == null)
            {
                existingReport = new VisitExpenseReport
                {
                    VisitInstanceId = request.VisitInstanceId,
                    ReportScope = "GENERAL",
                    Status = "DRAFT",
                    CurrencyCode = "VND",
                    CreatedBy = currentUserId,
                    CreatedAt = DateTime.UtcNow
                };

                _context.VisitExpenseReports.Add(existingReport);
                await _context.SaveChangesAsync(cancellationToken);

                // Add creation event
                _context.VisitExpenseReportEvents.Add(new VisitExpenseReportEvent
                {
                    ExpenseReportId = existingReport.ExpenseReportId,
                    EventType = "CREATED",
                    PerformedBy = currentUserId,
                    PerformedAt = DateTime.UtcNow
                });
                await _context.SaveChangesAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);
            }
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            // In case of unique constraint violation, read it again
            existingReport = await _context.VisitExpenseReports
                .Include(r => r.Items)
                .FirstOrDefaultAsync(r => r.VisitInstanceId == request.VisitInstanceId && r.ReportScope == "GENERAL", cancellationToken);
                
            if (existingReport == null)
                throw;
        }

        return MapToDto(existingReport);
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
            TotalAmount = entity.Items.Where(i => i.ItemOrigin != "CANCELLED").Sum(i => i.Quantity * i.UnitPrice), // Wait, total amount is unit * quantity
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
