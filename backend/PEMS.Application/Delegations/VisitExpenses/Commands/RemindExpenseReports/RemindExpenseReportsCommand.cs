using MediatR;
using System.Collections.Generic;

namespace PEMS.Application.Delegations.VisitExpenses.Commands.RemindExpenseReports;

/// <summary>
/// Host bấm "Nhắc nhở" trên bảng chi phí tiến trình đoàn: gửi thông báo hệ thống + email
/// đến người phụ trách từng đơn yêu cầu chưa kê khai chi phí (chưa lưu bảng kê và chưa
/// xác nhận "Không có chi phí"). Thông báo trỏ thẳng tới biên bản có phần ghi chú chi phí.
/// </summary>
public class RemindExpenseReportsCommand : IRequest<RemindExpenseReportsResultDto>
{
    public ulong VisitInstanceId { get; set; }
}

public class RemindExpenseReportsResultDto
{
    /// <summary>Số đơn yêu cầu đã được gửi nhắc nhở.</summary>
    public int RemindedCount { get; set; }

    /// <summary>Tên người nhận nhắc nhở (để toast phía frontend).</summary>
    public List<string> Recipients { get; set; } = new();
}
