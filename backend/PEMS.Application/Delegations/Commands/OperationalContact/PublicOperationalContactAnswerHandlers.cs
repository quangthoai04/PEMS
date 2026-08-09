using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Options;
using PEMS.Application.Delegations.Services;
using PEMS.Domain.Constants;

namespace PEMS.Application.Delegations.Commands.OperationalContact;

/// <summary>
/// Answering an operational-contact invitation WITHOUT signing in.
///
/// <para>
/// The invited person is typically an external guest who has never used PEMS. Requiring a Google
/// sign-in before they could answer meant the first thing the system asked of a partner organisation
/// was to create an account in order to say yes or no — so invitations went unanswered, campuses
/// stayed behind the confirmation gate, and nobody could approve anything.
/// </para>
/// <para>
/// What replaces the session is the token, and it is a stronger claim than a session would be for
/// this one act: single-use, short-lived, bound to ONE intended action, and bound to an address the
/// REGISTRANT chose rather than one the caller supplied. Both handlers below therefore do exactly two
/// things of their own — prove the token and turn the invited address into a real account — and then
/// hand the actual business change to the SAME command the authenticated flow runs. There is no
/// second copy of the accept/decline rules.
/// </para>
/// <para>
/// Neither is reachable by GET. The email's buttons open a confirmation page; the page posts. A GET
/// that mutated would be answered by whatever prefetches links — Outlook, Gmail, Defender, corporate
/// mail scanners — instead of by the person the invitation was addressed to.
/// </para>
/// </summary>
public sealed class PublicAcceptOperationalContactConfirmationCommandHandler
    : IRequestHandler<PublicAcceptOperationalContactConfirmationCommand, OperationalContactActionResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ISender _sender;
    private readonly IEmailActionTokenService _tokens;
    private readonly IUserProvisionService _provisioning;
    private readonly IDateTimeService _clock;
    private readonly PerCampusFormV2WriteOptions _writeFlag;

    public PublicAcceptOperationalContactConfirmationCommandHandler(
        IApplicationDbContext db, ISender sender, IEmailActionTokenService tokens,
        IUserProvisionService provisioning, IDateTimeService clock, PerCampusFormV2WriteOptions writeFlag)
    {
        _db = db;
        _sender = sender;
        _tokens = tokens;
        _provisioning = provisioning;
        _clock = clock;
        _writeFlag = writeFlag;
    }

    public async Task<OperationalContactActionResponse> Handle(
        PublicAcceptOperationalContactConfirmationCommand request, CancellationToken cancellationToken)
    {
        if (!_writeFlag.Enabled) throw new NotFoundException("Không tìm thấy.");

        var change = await PublicContactAnswer.LoadInvitationAsync(
            _db, _tokens, request.Token, EmailIntendedActions.Accept, cancellationToken);

        // ── The relation is an ACCOUNT, never an email string, so accepting has to end at a user id.
        //    Reuse the account that already owns this address whatever its role — an FPTU staff member
        //    is allowed to run a campus (the authenticated path allows it too) — and otherwise
        //    provision the same Visitor account the public visit-request flow would create, so a later
        //    Google sign-in with this address lands on this very user rather than a duplicate. ──
        var existing = await _db.Users.AsNoTracking()
            .Where(u => u.Email == change.NewEmailNormalized)
            .Select(u => new { u.UserId, u.Status })
            .FirstOrDefaultAsync(cancellationToken);

        ulong actingUserId;
        if (existing is not null)
        {
            if (!string.Equals(existing.Status, UserStatuses.Active, StringComparison.OrdinalIgnoreCase))
                throw new BusinessRuleException(
                    "Tài khoản của email này đang không hoạt động nên không thể nhận vai trò đầu mối vận hành.",
                    OperationalContactErrorCodes.AccountInactive);
            actingUserId = existing.UserId;
        }
        else
        {
            // The name the registrant wrote on the invitation is the best one available; the account is
            // the person's to correct afterwards.
            var snapshot = Delegations.Common.PendingContactSnapshot.Read(change.PendingSnapshotJson);
            actingUserId = await _provisioning.EnsureVisitorAccountAsync(
                change.NewEmailNormalized!,
                string.IsNullOrWhiteSpace(snapshot?.ResolvedFullName)
                    ? change.NewEmailMasked ?? "Đầu mối vận hành"
                    : snapshot!.ResolvedFullName!,
                snapshot?.ResolvedPhone,
                _clock.VietnamNow,
                cancellationToken);
        }

        // One canonical accept: same lock, same lifecycle guards, same gate recompute, same history.
        return await _sender.Send(
            new AcceptOperationalContactConfirmationCommand(request.Token, actingUserId),
            cancellationToken);
    }
}

/// <inheritdoc cref="PublicAcceptOperationalContactConfirmationCommandHandler"/>
public sealed class PublicDeclineOperationalContactConfirmationCommandHandler
    : IRequestHandler<PublicDeclineOperationalContactConfirmationCommand, OperationalContactActionResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ISender _sender;
    private readonly IEmailActionTokenService _tokens;
    private readonly IDateTimeService _clock;
    private readonly PerCampusFormV2WriteOptions _writeFlag;

    public PublicDeclineOperationalContactConfirmationCommandHandler(
        IApplicationDbContext db, ISender sender, IEmailActionTokenService tokens,
        IDateTimeService clock, PerCampusFormV2WriteOptions writeFlag)
    {
        _db = db;
        _sender = sender;
        _tokens = tokens;
        _clock = clock;
        _writeFlag = writeFlag;
    }

    public async Task<OperationalContactActionResponse> Handle(
        PublicDeclineOperationalContactConfirmationCommand request, CancellationToken cancellationToken)
    {
        if (!_writeFlag.Enabled) throw new NotFoundException("Không tìm thấy.");

        var change = await PublicContactAnswer.LoadInvitationAsync(
            _db, _tokens, request.Token, EmailIntendedActions.Decline, cancellationToken);

        // Declining provisions NOTHING. Somebody who is not taking the role has no reason to acquire
        // an account in order to refuse it, and creating one would leave a Visitor account behind for
        // a person who has just said they want no part in this.
        //
        // The decline still needs an actor id for its history row, so an EXISTING account for the
        // address is used when there is one; otherwise the invitation is settled without one.
        var existingUserId = await _db.Users.AsNoTracking()
            .Where(u => u.Email == change.NewEmailNormalized)
            .Select(u => (ulong?)u.UserId)
            .FirstOrDefaultAsync(cancellationToken);

        if (existingUserId is { } userId)
            return await _sender.Send(
                new DeclineOperationalContactConfirmationCommand(request.Token, request.Reason, userId),
                cancellationToken);

        // The no-account settlement is the one accept/decline path that does not go through the
        // canonical command, so the clock it stamps with has to be handed to it — the same
        // IDateTimeService.VietnamNow the canonical command reads, not this process's wall clock.
        return await PublicContactAnswer.DeclineWithoutAccountAsync(
            _db, _tokens, change, request.Token, request.Reason, _clock.VietnamNow, cancellationToken);
    }
}
