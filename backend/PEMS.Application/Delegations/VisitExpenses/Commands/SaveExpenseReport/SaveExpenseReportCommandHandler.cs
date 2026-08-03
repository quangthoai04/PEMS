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
using System.Text.Json;

namespace PEMS.Application.Delegations.VisitExpenses.Commands.SaveExpenseReport;

public class SaveExpenseReportCommandHandler : IRequestHandler<SaveExpenseReportCommand, VisitExpenseReportDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public SaveExpenseReportCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<VisitExpenseReportDto> Handle(SaveExpenseReportCommand request, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId ?? throw new UnauthorizedAccessException();

        var report = await _context.VisitExpenseReports
            .Include(r => r.Items)
            .Include(r => r.VisitInstance)
            .FirstOrDefaultAsync(r => r.ExpenseReportId == request.ExpenseReportId, cancellationToken);

        if (report == null)
            throw new NotFoundException(nameof(VisitExpenseReport), request.ExpenseReportId);

        // Concurrency check
        if (report.RowVersion != request.RowVersion)
            throw new ConflictException("The expense report has been modified by another user. Please refresh and try again.");

        // Status check
        if (report.Status == "FINALIZED" || report.Status == "CANCELLED")
            throw new ForbiddenException("Cannot modify a finalized or cancelled report.");
            
        if (report.VisitInstance.Status == PEMS.Shared.VisitInstanceStatus.Closed || report.VisitInstance.Status == PEMS.Shared.VisitInstanceStatus.Cancelled)
            throw new ForbiddenException("Cannot modify expense report when visit is closed or cancelled.");

        // Security check
        if (report.ReportScope == "GENERAL")
        {
            if (report.VisitInstance.CurrentHostUserId != currentUserId)
                throw new ForbiddenException("Only the Host can modify the general expense report.");
        }
        else if (report.ReportScope == "LOGISTICS")
        {
            var userDepartment = await _context.Users
                .Where(u => u.UserId == currentUserId)
                .Select(u => u.DepartmentId)
                .FirstOrDefaultAsync(cancellationToken);

            if (report.DepartmentId != userDepartment)
                throw new ForbiddenException("You do not have permission to modify this logistics expense report.");
        }

        // Update fields
        report.ReportNote = request.ReportNote;
        report.NoExpense = request.NoExpense;
        report.Status = "SAVED";
        report.SavedAt = DateTime.UtcNow;
        report.SavedBy = currentUserId;
        report.UpdatedAt = DateTime.UtcNow;
        report.UpdatedBy = currentUserId;
        
        // Bump row version
        report.RowVersion++;

        // Update items
        var existingItemIds = request.Items.Where(i => i.ExpenseItemId.HasValue).Select(i => i.ExpenseItemId!.Value).ToList();
        
        // Remove deleted items
        var itemsToRemove = report.Items.Where(i => !existingItemIds.Contains(i.ExpenseItemId)).ToList();
        foreach (var item in itemsToRemove)
        {
            // Do not allow deleting REQUEST_ITEM
            if (item.ItemOrigin == "REQUEST_ITEM")
                throw new ValidationException("Cannot delete the original logistics request item.");
                
            _context.VisitExpenseItems.Remove(item);
        }

        // Update or Add items
        foreach (var reqItem in request.Items)
        {
            if (reqItem.Quantity <= 0) throw new ValidationException("Quantity must be greater than 0.");
            if (reqItem.UnitPrice < 0) throw new ValidationException("Unit price cannot be negative.");
            if (string.IsNullOrWhiteSpace(reqItem.ItemName)) throw new ValidationException("Item name is required.");

            if (reqItem.ExpenseItemId.HasValue)
            {
                var existingItem = report.Items.FirstOrDefault(i => i.ExpenseItemId == reqItem.ExpenseItemId.Value);
                if (existingItem == null)
                    throw new NotFoundException(nameof(VisitExpenseItem), reqItem.ExpenseItemId.Value);

                existingItem.ItemName = reqItem.ItemName;
                existingItem.Description = reqItem.Description;
                existingItem.Quantity = reqItem.Quantity;
                existingItem.UnitName = reqItem.UnitName;
                existingItem.UnitPrice = reqItem.UnitPrice;
                existingItem.ItemNote = reqItem.ItemNote;
                existingItem.DisplayOrder = reqItem.DisplayOrder;
                existingItem.UpdatedAt = DateTime.UtcNow;
                existingItem.UpdatedBy = currentUserId;
                existingItem.RowVersion++;
            }
            else
            {
                // New item
                report.Items.Add(new VisitExpenseItem
                {
                    ItemOrigin = reqItem.ItemOrigin,
                    ItemName = reqItem.ItemName,
                    Description = reqItem.Description,
                    Quantity = reqItem.Quantity,
                    UnitName = reqItem.UnitName,
                    UnitPrice = reqItem.UnitPrice,
                    ItemNote = reqItem.ItemNote,
                    DisplayOrder = reqItem.DisplayOrder,
                    CreatedBy = currentUserId,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        // Event
        _context.VisitExpenseReportEvents.Add(new VisitExpenseReportEvent
        {
            ExpenseReportId = report.ExpenseReportId,
            EventType = "SAVED",
            EventNote = request.NoExpense ? "NO_EXPENSE" : null,
            PerformedBy = currentUserId,
            PerformedAt = DateTime.UtcNow
        });

        using var transaction = await _context.BeginTransactionAsync(cancellationToken);
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new ConflictException("The expense report has been modified by another user. Please refresh and try again.");
        }

        return MapToDto(report);
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
