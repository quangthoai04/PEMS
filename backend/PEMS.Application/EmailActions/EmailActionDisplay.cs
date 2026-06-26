using System;
using PEMS.Domain.Constants;

namespace PEMS.Application.EmailActions;

/// <summary>Small display helpers shared by the public email-action pages.</summary>
internal static class EmailActionDisplay
{
    public static string RoleLabel(string participantRole) => participantRole switch
    {
        ParticipantRoles.IcHost => "Host chính",
        ParticipantRoles.IcSupport => "Staff hỗ trợ IC",
        ParticipantRoles.DeptSupport => "Trưởng phòng (Phòng ban hỗ trợ)",
        ParticipantRoles.Student => "Sinh viên hỗ trợ",
        _ => "Thành phần tham gia",
    };

    public static string FormatWindow(DateTime start, DateTime end)
        => $"{start:HH:mm dd/MM/yyyy} - {end:HH:mm dd/MM/yyyy}";

    public static string ResponseLabel(string status) => status switch
    {
        ParticipantStatuses.Accepted => "ACCEPTED",
        ParticipantStatuses.Declined => "DECLINED",
        _ => status,
    };
}
