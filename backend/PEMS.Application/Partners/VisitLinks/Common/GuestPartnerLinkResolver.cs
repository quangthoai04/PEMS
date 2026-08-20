using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Partners.Common;
using PEMS.Domain.Entities.Partners;

namespace PEMS.Application.Partners.VisitLinks.Common;

/// <summary>
/// Turns "which organization does this delegation member belong to" into confirmed
/// <c>visit_guest_partner_links</c> rows — the ONE place the registration form and the minutes screen
/// both go through, so they can never disagree about who is already linked (PART-02, PART-06).
///
/// <para><b>A suggestion is not a relationship.</b> This resolver only ever writes CONFIRMED rows, and
/// only from evidence that somebody actually decided something:</para>
/// <list type="number">
///   <item>the member carries an <c>OrganizationPartnerId</c> — the registrant picked it themselves;</item>
///   <item>another member of the SAME visit with the EXACT same normalized organization is already
///         confirmed against a partner — the decision has been made for that organization here.</item>
/// </list>
/// <para>Nothing else. Fuzzy and near-name matches stay where they belong, in
/// <see cref="PartnerMatcher"/>, as candidates a human confirms — writing a link for a 78%-similar
/// name would put a guess in the same table as a decision, and no screen downstream could tell them
/// apart.</para>
///
/// <para><b>Idempotent.</b> Running it repeatedly over the same visit produces no duplicate link: one
/// target keeps at most one active row, and an existing CONFIRMED link is left alone rather than
/// rewritten — re-running a resolver is not a decision to change somebody's partner.</para>
/// </summary>
public static class GuestPartnerLinkResolver
{
    /// <summary>
    /// Seeds and propagates links for every guest member of <paramref name="visitRequestId"/>.
    /// Call INSIDE the caller's transaction, after the member rows have ids. Adds entities to the
    /// context; the caller saves.
    /// </summary>
    /// <returns>How many rows were added or removed — 0 means nothing to save.</returns>
    public static async Task<int> ResolveForRequestAsync(
        IApplicationDbContext db,
        ulong visitRequestId,
        DateTime now,
        ulong? actorUserId,
        CancellationToken ct)
    {
        var memberLinks = await db.VisitInstanceGuestMembers.AsNoTracking()
            .Where(l => l.VisitRequestId == visitRequestId)
            .Join(db.VisitGuestMembers.AsNoTracking(),
                l => l.GuestMemberId, g => g.GuestMemberId,
                (l, g) => new
                {
                    l.VisitInstanceId,
                    g.GuestMemberId,
                    g.Organization,
                    g.OrganizationPartnerId,
                })
            .ToListAsync(ct);

        if (memberLinks.Count == 0) return 0;

        // A legacy member can still be shared by more than one instance of the SAME request (the
        // copy-on-write split only happens the first time ONE of the sharing campuses is actually
        // edited — §6.1 of the remediation plan). The join above then yields one row per instance
        // that shares it, all carrying the SAME Organization/OrganizationPartnerId (both are columns
        // of the single underlying VisitGuestMembers row, never of the instance link), so collapsing
        // by GuestMemberId here is lossless. A member tied to exactly one instance keeps that instance
        // on its link; a still-shared member gets a request-wide link (VisitInstanceId = null) instead
        // of an arbitrary pick — the link then stays visible from every instance that shares the
        // member, rather than silently attaching to only one of them (GP-2 — no cross-campus link).
        var members = memberLinks
            .GroupBy(m => m.GuestMemberId)
            .Select(g =>
            {
                var first = g.First();
                var instanceIds = g.Select(x => x.VisitInstanceId).Distinct().ToList();
                return new
                {
                    GuestMemberId = g.Key,
                    first.Organization,
                    first.OrganizationPartnerId,
                    VisitInstanceId = instanceIds.Count == 1 ? instanceIds[0] : (ulong?)null,
                };
            })
            .ToList();

        // Existing links of this request — both the "already done" set and the source of the
        // per-organization decisions we propagate from.
        var existing = await db.VisitGuestPartnerLinks
            .Where(l => l.VisitRequestId == visitRequestId)
            .ToListAsync(ct);

        var linkedGuestIds = existing
            .Where(l => l.GuestMemberId != null && l.MatchStatus != PartnerLinkMatchStatuses.Rejected)
            .Select(l => l.GuestMemberId!.Value)
            .ToHashSet();

        // An edit replaces member rows, and the FK is ON DELETE SET NULL, so the previous links are
        // left pointing at nothing. Such a row names no person and no participant — there is nobody
        // for a reviewer to decide about, so it is removed rather than reported. (Confirmed links to a
        // REJECTED PROFILE are a different matter entirely: those name a real person, and §7.4 of the
        // plan keeps them for a human to decide.)
        var orphans = existing
            .Where(l => l.GuestMemberId is null && l.MinuteParticipantId is null)
            .ToList();
        var removed = 0;
        foreach (var orphan in orphans)
        {
            db.VisitGuestPartnerLinks.Remove(orphan);
            existing.Remove(orphan);
            removed++;
        }

        // ── Step 1: the decisions already on record for this visit, keyed by normalized organization.
        // Built from CONFIRMED links only. A key that resolves to TWO different partners is ambiguous
        // and is dropped: two people writing "ABC" and being confirmed against different profiles is
        // exactly the case where guessing for the third is wrong.
        var orgByGuestId = members.ToDictionary(m => m.GuestMemberId, m => m.Organization);
        var decisions = new Dictionary<string, ulong?>(StringComparer.Ordinal);
        void Record(string key, ulong partnerId)
        {
            if (key.Length == 0) return;
            if (decisions.TryGetValue(key, out var current))
            {
                if (current != partnerId) decisions[key] = null; // ambiguous → propagate nothing
                return;
            }
            decisions[key] = partnerId;
        }

        foreach (var l in existing.Where(l => l.MatchStatus == PartnerLinkMatchStatuses.Confirmed))
        {
            if (l.GuestMemberId is not { } gid) continue;
            if (!orgByGuestId.TryGetValue(gid, out var org)) continue;
            Record(PartnerNormalization.NormalizeKey(org), l.PartnerId);
        }

        // A member's OWN explicit pick is a decision too, and it counts for the whole visit — that is
        // what makes "one person selects ABC, everyone else typing ABC comes along" work on the very
        // first save, before any link row exists.
        foreach (var m in members.Where(m => m.OrganizationPartnerId is not null))
            Record(PartnerNormalization.NormalizeKey(m.Organization), m.OrganizationPartnerId!.Value);

        // ── Step 2: which partners are actually linkable. A profile rejected between the form being
        // filled in and this running must not be seeded (PART-04); it becomes a candidate instead.
        var partnerIds = decisions.Values.Where(v => v.HasValue).Select(v => v!.Value)
            .Concat(members.Where(m => m.OrganizationPartnerId.HasValue).Select(m => m.OrganizationPartnerId!.Value))
            .Distinct().ToList();
        var linkablePartnerIds = partnerIds.Count == 0
            ? new HashSet<ulong>()
            : (await db.Partners.AsNoTracking()
                .Where(p => partnerIds.Contains(p.PartnerId)
                            && (p.ProfileStatus == PartnerProfileStatuses.Approved
                                || p.ProfileStatus == PartnerProfileStatuses.PendingApproval))
                .Select(p => p.PartnerId)
                .ToListAsync(ct)).ToHashSet();

        // ── Step 3: write the links that are missing.
        var changes = removed;
        foreach (var m in members)
        {
            if (linkedGuestIds.Contains(m.GuestMemberId)) continue; // already decided — never overwrite

            ulong? partnerId = null;
            string matchSource;

            if (m.OrganizationPartnerId is { } picked && linkablePartnerIds.Contains(picked))
            {
                partnerId = picked;
                matchSource = PartnerLinkMatchSources.RegistrationSelected;
            }
            else
            {
                var key = PartnerNormalization.NormalizeKey(m.Organization);
                if (key.Length > 0 && decisions.TryGetValue(key, out var byOrg)
                    && byOrg is { } propagated && linkablePartnerIds.Contains(propagated))
                {
                    partnerId = propagated;
                    matchSource = PartnerLinkMatchSources.AutoName;
                }
                else
                {
                    continue; // no stable identity and no exact decision → leave to the matcher
                }
            }

            db.VisitGuestPartnerLinks.Add(new VisitGuestPartnerLink
            {
                VisitRequestId = visitRequestId,
                VisitInstanceId = m.VisitInstanceId,
                GuestMemberId = m.GuestMemberId,
                PartnerId = partnerId!.Value,
                MatchSource = matchSource,
                MatchStatus = PartnerLinkMatchStatuses.Confirmed,
                CreatedAt = now,
                CreatedBy = actorUserId,
            });
            linkedGuestIds.Add(m.GuestMemberId);
            changes++;
        }

        return changes;
    }
}
