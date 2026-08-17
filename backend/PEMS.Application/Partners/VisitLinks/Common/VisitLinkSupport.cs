using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;

namespace PEMS.Application.Partners.VisitLinks.Common;

/// <summary>Access rule for partner-link operations on a visit instance.</summary>
public static class VisitLinkSupport
{
    /// <summary>
    /// SEC-18: ADMIN used to be hardcoded into the allow expression. Fixed with an explicit,
    /// unconditional early-deny BEFORE any relationship check — not merely removing it from the
    /// allow-list, since Admin could otherwise still pass via an unrelated branch (e.g. being a
    /// historical CurrentHostUserId or holding a VisitParticipants row). Host of the instance, any
    /// ACCEPTED/ASSIGNED participant, the campus Staff Leader, and HO may see/manage partner links
    /// of the visit. The public Homepage partner directory is a separate, unauthenticated endpoint
    /// and is untouched by this — Admin has no internal Partner permission but still sees Public
    /// Partner on the Homepage.
    /// </summary>
    public static async Task<VisitRequestCampus> LoadInstanceWithAccessAsync(
        IApplicationDbContext db, ICurrentUserService user, ulong visitInstanceId, CancellationToken ct)
    {
        var instance = await db.VisitRequestCampuses
            .FirstOrDefaultAsync(c => c.VisitInstanceId == visitInstanceId, ct)
            ?? throw new NotFoundException("VisitRequestCampus", visitInstanceId);

        if (user.UserId is not { } userId)
            throw new ForbiddenException();

        string effective;
        try
        {
            effective = EffectiveRole.Resolve(user.RoleCode ?? string.Empty, user.SubRole);
        }
        catch (InvalidOperationException)
        {
            throw new ForbiddenException("Bạn không có quyền thao tác liên kết đối tác của chuyến thăm này.");
        }

        if (effective == EffectiveRole.Admin)
            throw new ForbiddenException("Bạn không có quyền thao tác liên kết đối tác của chuyến thăm này.");

        var allowed =
            effective == EffectiveRole.Ho
            || instance.CurrentHostUserId == userId
            || (effective == EffectiveRole.StaffLeader && user.PrimaryCampusId == instance.CampusId)
            || await db.VisitParticipants.AnyAsync(
                p => p.VisitInstanceId == visitInstanceId && p.UserId == userId
                     && (p.Status == "ACCEPTED" || p.Status == "ASSIGNED"), ct);

        if (!allowed)
            throw new ForbiddenException("Bạn không có quyền thao tác liên kết đối tác của chuyến thăm này.");

        return instance;
    }

    /// <summary>
    /// The guest member must belong to THIS campus instance — proven by the
    /// <c>visit_instance_guest_members</c> row, which is the only record that says which delegation
    /// member attends which campus.
    ///
    /// <para>There is deliberately no "the id exists somewhere" fallback. The previous version ORed one
    /// in, which made every scope check above it dead code: any guest_member_id in the database
    /// satisfied it, so a caller could attach a guest of another visit — another campus, another
    /// customer — to this instance's partner, corrupting that partner's visit history and handing out
    /// an IDOR to anyone who could guess an id (PART-08).</para>
    ///
    /// <para>404, not 403: an id the caller may not touch must not be distinguishable from one that does
    /// not exist, or the endpoint becomes an existence oracle.</para>
    /// </summary>
    public static async Task EnsureGuestMemberInInstanceAsync(
        IApplicationDbContext db, VisitRequestCampus instance, ulong guestMemberId, CancellationToken ct)
    {
        var belongs = await db.VisitInstanceGuestMembers.AnyAsync(
            l => l.VisitInstanceId == instance.VisitInstanceId
                 && l.GuestMemberId == guestMemberId
                 && l.VisitRequestId == instance.VisitRequestId, ct);

        if (!belongs) throw new NotFoundException("VisitGuestMember", guestMemberId);
    }

    /// <summary>
    /// The minute participant must sit in a minutes record of THIS instance. Same reasoning as
    /// <see cref="EnsureGuestMemberInInstanceAsync"/>: the join through <c>minutes</c> is the scope.
    /// </summary>
    public static async Task EnsureMinuteParticipantInInstanceAsync(
        IApplicationDbContext db, VisitRequestCampus instance, ulong minuteParticipantId, CancellationToken ct)
    {
        var belongs = await (
            from mp in db.MinuteParticipants
            join m in db.Minutes on mp.MinutesId equals m.MinutesId
            where mp.MinuteParticipantId == minuteParticipantId
                  && m.VisitInstanceId == instance.VisitInstanceId
            select mp.MinuteParticipantId).AnyAsync(ct);

        if (!belongs) throw new NotFoundException("MinuteParticipant", minuteParticipantId);
    }

    /// <summary>Validates both possible link targets in one call (either may be null).</summary>
    public static async Task EnsureTargetsInInstanceAsync(
        IApplicationDbContext db, VisitRequestCampus instance,
        ulong? guestMemberId, ulong? minuteParticipantId, CancellationToken ct)
    {
        if (guestMemberId is { } gid)
            await EnsureGuestMemberInInstanceAsync(db, instance, gid, ct);
        if (minuteParticipantId is { } mid)
            await EnsureMinuteParticipantInInstanceAsync(db, instance, mid, ct);
    }
}
