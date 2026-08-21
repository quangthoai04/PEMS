using System;
using System.Linq;
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
/// The single entry point the contact-management screen saves through, for ONE campus. It classifies
/// the edit and dispatches; it implements nothing.
///
/// <code>
/// Normalize(new) == Normalize(stored)  → UpdateOperationalContactProfile   (no token, no mail)
/// Normalize(new) != Normalize(stored)  → no confirmed holder yet → ReplaceOperationalContact
///                                      → a confirmed holder exists → InitiateOperationalContactTransfer
/// </code>
///
/// <para>
/// Classifying here rather than in the client is what makes the rule enforceable. A UI that decided
/// for itself could send a "profile update" carrying a new address, or a "replace" that only fixed a
/// phone number and re-invited the same person for no reason; both are decided from the STORED address
/// now, which no client can misreport.
/// </para>
/// <para>
/// Normalising both sides is the other half: <c>Anh.Nguyen@FPT.EDU.VN</c> and
/// <c>anh.nguyen@fpt.edu.vn</c> are one person, and a case change must not send them a confirmation
/// email asking them to prove they are themselves.
/// </para>
/// </summary>
public sealed class SaveOperationalContactCommandHandler
    : IRequestHandler<SaveOperationalContactCommand, OperationalContactManageResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ISender _sender;
    private readonly ICurrentUserService _currentUser;
    private readonly PerCampusFormV2WriteOptions _writeFlag;

    public SaveOperationalContactCommandHandler(
        IApplicationDbContext db, ISender sender, ICurrentUserService currentUser,
        PerCampusFormV2WriteOptions writeFlag)
    {
        _db = db;
        _sender = sender;
        _currentUser = currentUser;
        _writeFlag = writeFlag;
    }

    public async Task<OperationalContactManageResponse> Handle(
        SaveOperationalContactCommand request, CancellationToken cancellationToken)
    {
        // The same bar the three destination handlers apply, run first so an unauthenticated caller
        // gets 403 rather than a 404 leaked from the read below.
        OperationalContactGuards.RequireAuthenticated(_writeFlag, _currentUser);

        // Read-only, and only the two facts the branch turns on. The destination handler re-loads the
        // campus tracked, re-proves it belongs to the request and re-runs every guard — nothing here is
        // trusted downstream, which is why a projection is enough.
        var campus = await _db.VisitRequestCampuses.AsNoTracking()
            .Where(c => c.VisitInstanceId == request.VisitInstanceId
                        && c.VisitRequestId == request.VisitRequestId)
            .Select(c => new { c.OperationalContactUserId, Email = c.FormDetail!.OperationalContactEmail })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new ConflictException(
                "Cơ sở này không thuộc đơn đăng ký đã chọn.",
                CampusScopeErrorCodes.InstanceNotInRequest);

        var storedEmail = VisitRequestFingerprintBuilder.NormalizeEmail(campus.Email);
        var incomingEmail = VisitRequestFingerprintBuilder.NormalizeEmail(request.Email);

        if (string.Equals(storedEmail, incomingEmail, StringComparison.Ordinal))
            return await _sender.Send(
                new UpdateOperationalContactProfileCommand(
                    request.VisitRequestId, request.VisitInstanceId,
                    request.FullName, request.Organization, request.JobTitle, request.Phone, request.Email,
                    request.ExpectedRowVersion),
                cancellationToken);

        // ── The address changed, so somebody has to accept it. WHICH handshake is decided by whether
        //    anyone currently holds the campus, not by whether it has been DECIDED: a campus with a
        //    confirmed holder is handover territory even before any Staff Leader has acted on it, and a
        //    campus with nobody confirmed yet is replace territory even if its lifecycle window has
        //    since closed — the destination handler's own guard produces the right refusal for the
        //    latter. Deciding lifecycle here as well would put that rule in two places. ──
        var hasConfirmedHolder = campus.OperationalContactUserId is not null;

        return !hasConfirmedHolder
            ? await _sender.Send(
                new ReplaceOperationalContactCommand(
                    request.VisitRequestId, request.VisitInstanceId,
                    request.FullName, request.Organization, request.JobTitle, request.Phone, request.Email),
                cancellationToken)
            : await _sender.Send(
                new InitiateOperationalContactTransferCommand(
                    request.VisitRequestId, request.VisitInstanceId,
                    request.FullName, request.Organization, request.JobTitle, request.Phone, request.Email,
                    request.Reason),
                cancellationToken);
    }
}
