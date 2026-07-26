using FluentValidation;
using MediatR;

namespace PEMS.Application.Delegations.Commands.TransferVisitHost;

/// <summary>
/// Hand one campus's Host role to a different eligible user, AFTER the campus was approved.
///
/// This is not the approve-and-assign path with the guard relaxed. That path gives a campus its FIRST
/// Host as one half of the approval decision, and refuses to run a second time on purpose — it has no
/// way to express "the Host changed", so it writes no before/after, notifies nobody about a handover,
/// and would have to re-open an approval to run at all. Reaching for it here is what produced requests
/// whose Host silently differed from the one the visitor had been told about.
/// </summary>
public sealed record TransferVisitHostCommand(
    ulong VisitInstanceId,
    ulong NewHostUserId,
    string? Reason,
    int ExpectedRowVersion) : IRequest<TransferVisitHostResponse>;

/// <summary>Request body for POST /api/v2/visit-instances/{id}/host-transfer.</summary>
public sealed record TransferVisitHostBody(
    ulong NewHostUserId,
    string? Reason,
    int ExpectedRowVersion);

public sealed record TransferVisitHostResponse(
    ulong VisitInstanceId,
    ulong PreviousHostUserId,
    string PreviousHostName,
    ulong NewHostUserId,
    string NewHostName,
    int RowVersion,
    string Message);

public sealed class TransferVisitHostCommandValidator : AbstractValidator<TransferVisitHostCommand>
{
    public TransferVisitHostCommandValidator()
    {
        RuleFor(x => x.NewHostUserId).GreaterThan(0ul)
            .WithMessage("Vui lòng chọn Host mới.");
        // A handover is a decision other people have to live with — the Host who loses the assignment,
        // the one who gains it, and the visitor who was told a name. Requiring a reason keeps the
        // notification and the history entry able to say why.
        RuleFor(x => x.Reason).NotEmpty()
            .WithMessage("Vui lòng nhập lý do chuyển Host.")
            .MaximumLength(500);
    }
}

public static class VisitHostTransferErrorCodes
{
    /// <summary>The campus has no Host yet — approval assigns the first one.</summary>
    public const string NoCurrentHost = "HOST_TRANSFER_NO_CURRENT_HOST";
    /// <summary>The proposed Host already holds the role.</summary>
    public const string SameHost = "HOST_TRANSFER_SAME_HOST";
    /// <summary>The proposed Host is not eligible for this campus.</summary>
    public const string HostNotEligible = "HOST_TRANSFER_HOST_NOT_ELIGIBLE";
}
