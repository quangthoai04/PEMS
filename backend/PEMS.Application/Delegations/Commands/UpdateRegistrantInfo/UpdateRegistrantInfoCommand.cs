using MediatR;

namespace PEMS.Application.Delegations.Commands.UpdateRegistrantInfo;

/// <summary>
/// Update the registrant ("người đăng ký / người tạo form") block of a visit request.
/// Allowed ONLY for an internally created request (created_source = STAFF_CREATED) and ONLY by the
/// user who created it, while the request is still live (not CANCELLED/REJECTED). A Visitor-submitted
/// request's registrant is the guest and is never editable through this command.
/// </summary>
public sealed record UpdateRegistrantInfoCommand(
    ulong VisitRequestId,
    string FullName,
    string Organization,
    string? JobTitle,
    string Phone,
    string Email) : IRequest<UpdateRegistrantInfoResponse>;
