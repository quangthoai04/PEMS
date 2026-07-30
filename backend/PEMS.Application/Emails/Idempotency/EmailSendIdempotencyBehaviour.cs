using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;

namespace PEMS.Application.Emails.Idempotency;

/// <summary>
/// Makes one user action produce at most one outbound message, for the six report/invoice sends
/// (G11 / R-103).
///
/// <para>
/// It sits in the MediatR pipeline rather than inside the six handlers for two reasons. The reservation
/// has to be taken BEFORE the handler runs — otherwise a replay would regenerate the PDF it is supposed
/// to avoid regenerating — and putting it in six places would mean six chances to get the transitions
/// subtly different, which is precisely the class of bug this closes.
/// </para>
/// <para>
/// It is not authorization and does not stand in for any. <c>[RoleAuthorize]</c>, the in-handler scope
/// guards and the backend's own recipient resolution all still run, on the first request and on every
/// replay. The actor comes from the validated JWT and is part of the reservation's unique key, so one
/// user's key can never reach another user's record.
/// </para>
/// <para>
/// What it does NOT promise: exactly-once delivery. SMTP cannot offer that and PEMS has no delivery
/// webhook. When the provider's answer is lost the reservation says so, and says so to the user, rather
/// than guessing in either direction.
/// </para>
/// </summary>
public sealed class EmailSendIdempotencyBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEmailSendReservationStore _store;
    private readonly IIdempotencyKeyAccessor _keys;
    private readonly ICurrentUserService _currentUser;
    private readonly EmailSendAttempt _attempt;

    public EmailSendIdempotencyBehaviour(
        IEmailSendReservationStore store,
        IIdempotencyKeyAccessor keys,
        ICurrentUserService currentUser,
        EmailSendAttempt attempt)
    {
        _store = store;
        _keys = keys;
        _currentUser = currentUser;
        _attempt = attempt;
    }

    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        // Opt-in: everything that has not declared itself an idempotent send passes straight through,
        // with no reservation, no header requirement and no behaviour change of any kind.
        if (request is not IIdempotentEmailSend send) return await next(cancellationToken);

        if (!typeof(IEmailSendResult).IsAssignableFrom(typeof(TResponse)))
            throw new InvalidOperationException(
                $"{typeof(TRequest).Name} declares {nameof(IIdempotentEmailSend)} but its response " +
                $"{typeof(TResponse).Name} is not an {nameof(IEmailSendResult)}, so a successful send " +
                "could not be replayed. Fix the response type rather than removing the declaration.");

        // The actor is read here and nowhere else. Taking it from the payload would let a client reserve
        // under somebody else's identity, which is a bigger hole than the one this closes.
        var actorUserId = _currentUser.UserId
            ?? throw new ForbiddenException("Không xác định được người gửi.");

        var keyHash = IdempotencyKey.RequireHash(_keys.CurrentKey);
        var fingerprint = EmailSendFingerprint.Compute(send, actorUserId);

        var reservation = await _store.ReserveAsync(
            actorUserId, send.OperationCode, keyHash, fingerprint, cancellationToken);

        switch (reservation.Outcome)
        {
            case EmailSendReservationOutcome.ReplaySucceeded:
                return Replay(reservation.ResultMessage);

            case EmailSendReservationOutcome.InProgress:
                throw new ConflictException(
                    "Yêu cầu gửi này đang được xử lý. Vui lòng đợi kết quả, đừng gửi lại.",
                    EmailErrorCodes.IdempotencyInProgress);

            case EmailSendReservationOutcome.FingerprintMismatch:
                throw new ConflictException(
                    "Mã chống trùng đã được dùng cho một yêu cầu khác. Hãy thực hiện một lần gửi mới.",
                    EmailErrorCodes.IdempotencyKeyReused);

            case EmailSendReservationOutcome.OutcomeUnknown:
                throw new ConflictException(
                    "Lần gửi trước chưa xác định được kết quả — email có thể đã được gửi. " +
                    "Kiểm tra lịch sử email trước khi quyết định gửi lại.",
                    EmailErrorCodes.IdempotencyOutcomeUnknown);
        }

        _attempt.Begin(reservation.ReservationId);

        TResponse response;
        try
        {
            response = await next(cancellationToken);
        }
        catch (Exception ex)
        {
            // The single question that matters. `DispatchStarted` is set by ReportEmailSender immediately
            // before the provider call, and only after that transition is durable — so "not started" here
            // really does mean nothing left the process.
            await _store.MarkFailedAsync(
                reservation.ReservationId,
                _attempt.DispatchStarted,
                StableCodeOf(ex),
                CancellationToken.None);
            throw;
        }

        var result = (IEmailSendResult)response!;
        await _store.MarkSucceededAsync(
            reservation.ReservationId, result.Message, _attempt.SentEmailId, CancellationToken.None);

        return response;
    }

    /// <summary>
    /// Rebuilds the earlier success. Nothing is re-run: no PDF, no storage write, no history row and no
    /// message — which is the entire point of recognising the replay before the handler.
    /// </summary>
    private static TResponse Replay(string? storedMessage)
    {
        var replay = (IEmailSendResult)Activator.CreateInstance(typeof(TResponse))!;
        replay.Success = true;
        replay.Message = storedMessage ?? "Yêu cầu này đã được gửi trước đó.";
        return (TResponse)replay;
    }

    /// <summary>
    /// The machine-readable code, when the failure has one. Never the message: a message can name a
    /// recipient or an amount, and this value is stored.
    /// </summary>
    private static string? StableCodeOf(Exception ex) => ex switch
    {
        BusinessRuleException business => business.ErrorCode,
        ValidationException validation => validation.ErrorCode,
        ConflictException conflict => conflict.ErrorCode,
        NotFoundException notFound => notFound.ErrorCode,
        _ => null,
    };
}
