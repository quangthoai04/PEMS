using MediatR;

namespace PEMS.Application.Accounts.Commands.UpdateBasicAccountInfo;

/// <summary>
/// HO basic-info edit. An HO updates ONLY the full name and login email of another HO or a
/// Staff Leader (Trưởng phòng IC). Role, sub-role, campus, department, MSSV and status are never
/// accepted here so an HO can never widen a target's privileges through this endpoint. The handler
/// re-derives the target's role/campus from the database and is the final authorization gate.
/// </summary>
public sealed class UpdateBasicAccountInfoCommand : IRequest<UpdateBasicAccountInfoResponse>
{
    /// <summary>Target account whose full name / email is being edited.</summary>
    public ulong UserId { get; set; }

    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}
