using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.VisitExpenses.Models;
using PEMS.Domain.Entities.Delegations;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PEMS.Application.Delegations.VisitExpenses.Commands.InitializeGeneralExpenseReport;

/// <summary>
/// The write half of what used to be one get-or-create command: creating the general expense report is
/// a deliberate act by the Host, at the one point in the lifecycle where it means something.
///
/// Only the instance's current Host, and only at AFTER_VISIT. A CLOSED campus is settled — reopening it
/// to file costs after the fact is exactly what closing was for. Every refusal carries a stable code so
/// the client can distinguish "not yet" from "not you".
/// </summary>
public sealed class InitializeGeneralExpenseReportCommandHandler
    : IRequestHandler<InitializeGeneralExpenseReportCommand, VisitExpenseReportDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public InitializeGeneralExpenseReportCommandHandler(
        IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<VisitExpenseReportDto> Handle(
        InitializeGeneralExpenseReportCommand request, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId ?? throw new UnauthorizedAccessException();

        var instance = await _context.VisitRequestCampuses
            .FirstOrDefaultAsync(v => v.VisitInstanceId == request.VisitInstanceId, cancellationToken)
            ?? throw new NotFoundException(nameof(VisitRequestCampus), request.VisitInstanceId);

        await VisitExpenseAccessScope.EnsureCanViewAsync(
            _context, _currentUserService, request.VisitInstanceId, cancellationToken);

        // Already there → hand it back. An initialize that races itself, or a user who double-clicks,
        // must not end with two reports on one campus.
        var existingReport = await _context.VisitExpenseReports
            .Include(r => r.Items)
            .FirstOrDefaultAsync(
                r => r.VisitInstanceId == request.VisitInstanceId && r.ReportScope == "GENERAL",
                cancellationToken);
        if (existingReport is not null)
            return VisitExpenseReportMapper.ToDto(existingReport);

        if (instance.CurrentHostUserId != currentUserId)
            throw new ForbiddenException(
                "Chỉ người phụ trách tiếp đón mới khởi tạo được bảng chi phí chung.",
                VisitExpenseErrorCodes.ReportHostRequired);

        if (instance.Status != PEMS.Shared.VisitInstanceStatus.AfterVisit)
            throw new ForbiddenException(
                "Chỉ khởi tạo được bảng chi phí khi chuyến thăm đã ở giai đoạn Sau tiếp khách.",
                VisitExpenseErrorCodes.ReportNotInitializable);

        using var transaction = await _context.BeginTransactionAsync(cancellationToken);
        try
        {
            existingReport = new VisitExpenseReport
            {
                VisitInstanceId = request.VisitInstanceId,
                ReportScope = "GENERAL",
                Status = "DRAFT",
                CurrencyCode = "VND",
                CreatedBy = currentUserId,
                CreatedAt = DateTime.UtcNow,
            };
            _context.VisitExpenseReports.Add(existingReport);
            await _context.SaveChangesAsync(cancellationToken);

            _context.VisitExpenseReportEvents.Add(new VisitExpenseReportEvent
            {
                ExpenseReportId = existingReport.ExpenseReportId,
                EventType = "CREATED",
                PerformedBy = currentUserId,
                PerformedAt = DateTime.UtcNow,
            });
            await _context.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            // A unique-constraint violation means somebody else won the race — read theirs.
            existingReport = await _context.VisitExpenseReports.AsNoTracking()
                .Include(r => r.Items)
                .FirstOrDefaultAsync(
                    r => r.VisitInstanceId == request.VisitInstanceId && r.ReportScope == "GENERAL",
                    cancellationToken);
            if (existingReport is null) throw;
        }

        return VisitExpenseReportMapper.ToDto(existingReport);
    }
}
