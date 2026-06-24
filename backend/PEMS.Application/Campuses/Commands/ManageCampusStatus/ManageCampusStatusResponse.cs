using System;

namespace PEMS.Application.Campuses.Commands.ManageCampusStatus;

public sealed class ManageCampusStatusResponse
{
    public ulong CampusId { get; init; }
    public string Status { get; init; } = null!;
    public DateTime UpdatedAt { get; init; }
    public ulong? UpdatedBy { get; init; }
    public string Message { get; init; } = "Cập nhật trạng thái campus thành công.";
}
