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

namespace PEMS.Application.Delegations.VisitExpenses.Commands.GetOrCreateLogisticsExpenseReport;

public class GetOrCreateLogisticsExpenseReportCommandHandler : IRequestHandler<GetOrCreateLogisticsExpenseReportCommand, VisitExpenseReportDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetOrCreateLogisticsExpenseReportCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<VisitExpenseReportDto> Handle(GetOrCreateLogisticsExpenseReportCommand request, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId ?? throw new UnauthorizedAccessException();

        // 1. Validate logistics item
        var logisticsItem = await _context.VisitLogisticsItems
            .Include(li => li.VisitInstance)
            .Include(li => li.Handovers)
            .FirstOrDefaultAsync(li => li.LogisticsItemId == request.LogisticsItemId, cancellationToken);

        if (logisticsItem == null)
            throw new NotFoundException(nameof(VisitLogisticsItem), request.LogisticsItemId);

        // 2. Security: department members (own the item) can view/edit; the campus instance's current
        // Host can VIEW a report the department already saved — read-only, never triggers creation
        // (see the existing-report short-circuit below).
        var userDepartment = await _context.Users
            .Where(u => u.UserId == currentUserId)
            .Select(u => u.DepartmentId)
            .FirstOrDefaultAsync(cancellationToken);

        var isDepartmentMember = logisticsItem.RequestedToDepartmentId.HasValue && logisticsItem.RequestedToDepartmentId == userDepartment;
        var isInstanceHost = logisticsItem.VisitInstance.CurrentHostUserId == currentUserId;

        if (!isDepartmentMember && !isInstanceHost)
            throw new ForbiddenException("You do not have permission to view or edit this logistics expense report.");

        // 3. Status checks
        // Needs to be DONE. If it requires return (borrowed equipment), we must check handover.
        // Prompt says: "Nếu loại tài sản cần trả, chỉ mở nhập chính thức sau khi ký trả hợp lệ. Với vật tư tiêu hao/dịch vụ không có bước trả, dùng trạng thái DONE làm gate."
        bool isDone = logisticsItem.Status == "DONE";
        bool isClosed = logisticsItem.VisitInstance.Status == PEMS.Shared.VisitInstanceStatus.Closed;
        bool isAfterVisit = logisticsItem.VisitInstance.Status == PEMS.Shared.VisitInstanceStatus.AfterVisit;

        if (!isDone && logisticsItem.Status != "CANCELLED") 
        {
            // Usually, only DONE items get expenses. But if it's already there, we might just view it.
        }

        // 4. Existing report?
        var existingReport = await _context.VisitExpenseReports
            .Include(r => r.Items)
            .FirstOrDefaultAsync(r => r.LogisticsItemId == request.LogisticsItemId && r.ReportScope == "LOGISTICS", cancellationToken);

        if (existingReport != null)
        {
            return MapToDto(existingReport);
        }

        // Host chỉ được XEM báo cáo đã có — khởi tạo báo cáo mới là việc riêng của phòng ban.
        if (!isDepartmentMember)
            throw new NotFoundException(nameof(VisitExpenseReport), request.LogisticsItemId);

        // Must be AFTER_VISIT to create
        if (!isAfterVisit && !isClosed)
            throw new ForbiddenException("Can only initialize expense report in AFTER_VISIT state.");

        // Check return handover if needed (simplified check based on Handovers)
        bool needsReturn = logisticsItem.Handovers.Any(h => h.HandoverType == "BORROW");
        if (needsReturn)
        {
            bool hasReturned = logisticsItem.Handovers.Any(h => h.HandoverType == "RETURN" && h.ProviderSignedBy != null);
            if (!hasReturned)
                throw new ValidationException("Must complete RETURN handover before initializing expense report for borrowed items.");
        }
        else if (!isDone)
        {
            throw new ValidationException("Logistics item must be DONE before initializing expense report.");
        }

        // 5. Idempotent creation
        using var transaction = await _context.BeginTransactionAsync(cancellationToken);
        try
        {
            existingReport = await _context.VisitExpenseReports
                .Include(r => r.Items)
                .FirstOrDefaultAsync(r => r.LogisticsItemId == request.LogisticsItemId && r.ReportScope == "LOGISTICS", cancellationToken);

            if (existingReport == null)
            {
                existingReport = new VisitExpenseReport
                {
                    VisitInstanceId = logisticsItem.VisitInstanceId,
                    ReportScope = "LOGISTICS",
                    LogisticsItemId = request.LogisticsItemId,
                    DepartmentId = logisticsItem.RequestedToDepartmentId,
                    Status = "DRAFT",
                    CurrencyCode = "VND",
                    CreatedBy = currentUserId,
                    CreatedAt = DateTime.UtcNow
                };

                // Add initial request item
                // "nếu có proposed_quantity đã được ACCEPTED thì dùng số lượng đó, nếu không dùng quantity."
                int finalQuantity = logisticsItem.Quantity ?? 1;
                if (logisticsItem.ProposalResponse == "ACCEPTED" && logisticsItem.ProposedQuantity.HasValue)
                {
                    finalQuantity = logisticsItem.ProposedQuantity.Value;
                }

                existingReport.Items.Add(new VisitExpenseItem
                {
                    ItemOrigin = "REQUEST_ITEM",
                    ItemName = logisticsItem.Title,
                    Description = logisticsItem.Description,
                    Quantity = finalQuantity,
                    UnitPrice = 0, // User will set this later
                    DisplayOrder = 1,
                    CreatedBy = currentUserId,
                    CreatedAt = DateTime.UtcNow
                });

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
            existingReport = await _context.VisitExpenseReports
                .Include(r => r.Items)
                .FirstOrDefaultAsync(r => r.LogisticsItemId == request.LogisticsItemId && r.ReportScope == "LOGISTICS", cancellationToken);
                
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
            NoExpense = entity.NoExpense,
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
