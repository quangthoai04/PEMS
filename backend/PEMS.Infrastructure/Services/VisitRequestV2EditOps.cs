using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using PEMS.Application.Common.DTOs;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Common;
using PEMS.Application.Delegations.Services;
using PEMS.Domain.Entities.Delegations;
using PEMS.Shared;

namespace PEMS.Infrastructure.Services;

/// <summary>
/// Shared per-instance mutation primitives for the v2 edit paths (pending-edit + resubmit). They keep a
/// request's per-campus members INDEPENDENT and honour copy-on-write: replacing one campus's members never
/// deletes a member row still linked to a sibling campus (only this instance's link is dropped there). New
/// member rows are staged on the request navigation so their ids resolve on the caller's flush #1; the
/// composite links are created after that flush. The caller owns the transaction and both flushes.
/// </summary>
internal static class VisitRequestV2EditOps
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    /// <summary>
    /// Phase 1 — full-replace one instance's guest/support members. Removes THIS instance's links; deletes only
    /// the guest_member rows that no OTHER instance still links (copy-on-write); builds and stages the new rows
    /// on <paramref name="request"/>.GuestMembers (FK filled on insert). Returns the new rows so the caller can
    /// link them after flush #1. A deletion that hits a downstream FK (RESTRICT) rolls the whole edit back —
    /// safe by construction, since pending/rejected instances carry no minutes/feedback/OCR references.
    /// </summary>
    public static List<VisitGuestMember> StageReplaceMembers(
        IApplicationDbContext db,
        VisitRequest request,
        VisitRequestCampus instance,
        IList<VisitorDto> visitors,
        IList<SupportTeamMemberDto> support,
        System.DateTime now,
        ulong? actorId)
    {
        // Patch 4 hardening (H4-3): snapshot this campus's CURRENT member content BEFORE anything below
        // is removed. A row is copy-on-write regardless of WHAT changed on the campus — even a Purpose
        // edit rewrites every member row here — so without this, every save would re-validate every
        // member's nationality even when that specific member's own content never moved. A row
        // recognized as byte-identical (in canonical space) to one that already existed keeps its
        // EXISTING stored nationality untouched instead of being forced through NationalityResolution;
        // see MemberContentIndex for why this is content equality, not identity, and why that is the
        // right (and the only safe, non-fuzzy) boundary without a persisted per-member identity.
        var untouched = MemberContentIndex.Build(V2CanonicalRefresh.MembersOf(request, instance));

        foreach (var link in instance.GuestMemberLinks.ToList())
        {
            db.VisitInstanceGuestMembers.Remove(link);
            instance.GuestMemberLinks.Remove(link);

            var sharedElsewhere = request.CampusInstances
                .Where(ci => ci.VisitInstanceId != instance.VisitInstanceId)
                .SelectMany(ci => ci.GuestMemberLinks)
                .Any(l => l.GuestMemberId == link.GuestMemberId);
            if (!sharedElsewhere)
            {
                var member = request.GuestMembers.FirstOrDefault(m => m.GuestMemberId == link.GuestMemberId);
                if (member is not null)
                {
                    request.GuestMembers.Remove(member);
                    db.VisitGuestMembers.Remove(member);
                }
            }
        }

        // A campus's member set is copy-on-write: whenever this runs, EVERY member row for this
        // instance is brand new (new guest_member_id — never an in-place update, see the class doc).
        // A row whose content is genuinely new-or-changed is resolved to the canonical Vietnamese short
        // name or the whole edit is refused, exactly like create (Patch 4). A row recognized above as
        // content-identical to one that already existed on this campus is exempt — see the
        // MemberContentIndex snapshot and its own doc comment for why that boundary is safe.
        var rows = new List<VisitGuestMember>();
        uint order = 1;
        foreach (var v in visitors ?? new List<VisitorDto>())
            rows.Add(new VisitGuestMember
            {
                FullName = v.FullName, Organization = v.Organization, JobTitle = v.JobTitle,
                OrganizationPartnerId = v.OrganizationPartnerId,
                Nationality = untouched.TryTakeMatch("GUEST", v.FullName, v.Organization, v.JobTitle, v.Nationality, out var keptGuestNat)
                    ? keptGuestNat!
                    : NationalityResolution.ResolveOrThrow(v.Nationality, "Quốc tịch khách không hợp lệ:"),
                MemberType = "GUEST", DisplayOrder = order++,
                CreatedAt = now, CreatedBy = actorId,
            });
        foreach (var m in support ?? new List<SupportTeamMemberDto>())
            rows.Add(new VisitGuestMember
            {
                FullName = m.FullName, Organization = m.Organization, JobTitle = m.JobTitle,
                OrganizationPartnerId = m.OrganizationPartnerId,
                Nationality = untouched.TryTakeMatch("EXTERNAL_SUPPORT", m.FullName, m.Organization, m.JobTitle, m.Nationality, out var keptSupportNat)
                    ? keptSupportNat!
                    : NationalityResolution.ResolveOrThrow(m.Nationality, "Quốc tịch nhân sự hỗ trợ không hợp lệ:"),
                MemberType = "EXTERNAL_SUPPORT", DisplayOrder = order++,
                CreatedAt = now, CreatedBy = actorId,
            });
        foreach (var r in rows) request.GuestMembers.Add(r);
        return rows;
    }

    /// <summary>
    /// The client-minted member keys carried by ONE campus's edit content, in the order
    /// <see cref="StageReplaceMembers"/> builds the rows from it (visitors, then support). Kept beside
    /// that method so the two orderings are read together and cannot drift apart.
    /// </summary>
    public static List<string?> MemberKeys(CampusVisitEditV2Dto content)
    {
        var keys = new List<string?>();
        foreach (var v in content.Visitors ?? new List<VisitorDto>()) keys.Add(v.ClientMemberKey);
        foreach (var m in content.ExternalSupportMembers ?? new List<SupportTeamMemberDto>())
            keys.Add(m.ClientMemberKey);
        return keys;
    }

    /// <summary>Phase 2 — create the composite links for the staged member rows (ids resolved after flush #1).</summary>
    /// <param name="clientMemberKeys">
    /// The keys those rows arrived under, index-aligned with <paramref name="newRows"/>. Null for the
    /// paths that have none — an amendment replays member lists stored days ago, and no key from that
    /// form still exists.
    /// </param>
    /// <param name="pickedClientMemberKey">Which of them the campus's operational contact is.</param>
    public static void LinkMembers(
        IApplicationDbContext db, VisitRequest request, VisitRequestCampus instance,
        IReadOnlyList<VisitGuestMember> newRows, System.DateTime now, ulong? actorId,
        IReadOnlyList<string?>? clientMemberKeys = null, string? pickedClientMemberKey = null)
    {
        uint linkOrder = 0;
        foreach (var m in newRows)
            db.VisitInstanceGuestMembers.Add(new VisitInstanceGuestMember
            {
                VisitRequestId = request.VisitRequestId,
                VisitInstanceId = instance.VisitInstanceId,
                GuestMemberId = m.GuestMemberId,
                DisplayOrder = linkOrder++,
                CreatedAt = now,
                CreatedBy = actorId,
            });

        // Re-point the operational contact at the member row that now represents them (NP-03).
        //
        // StageReplaceMembers has just DELETED every member row this instance had and created fresh
        // ones, so whatever guest_member_id the contact used to hold names a row that no longer
        // exists. Without this the link silently degrades to null on the first content edit — the
        // contact would go back to being a name that has to be string-matched, which is the whole
        // problem.
        //
        // The KEY is what re-finds them, and it is the only thing that can: the contact snapshot is
        // immutable through this path (EnsureContactSnapshotUnchanged refuses any change to it) while
        // the member row is freely editable, so correcting a spelling in the delegation list used to
        // be enough for the snapshot match to find nobody. Re-pointing by key also means editing that
        // member — their name, their job title, the partner behind their organisation — never changes
        // WHO the contact is. Only removing them does, and that is refused rather than absorbed.
        if (instance.FormDetail is not null)
            OperationalContactLink.Resolve(
                instance.FormDetail,
                OperationalContactLink.Pair(newRows, clientMemberKeys),
                pickedClientMemberKey);
    }

    /// <summary>
    /// Overwrites an EXISTING instance's form detail from the edit content and bumps its form_revision.
    ///
    /// <para>
    /// The five <c>operational_contact_*</c> columns are deliberately absent. They belong to the
    /// contact-management workflow, which owns its own endpoint, its own concurrency check and its own
    /// audit entry; a request edit only carries the snapshot so an unchanged payload round-trips, and
    /// <c>EnsureContactSnapshotUnchanged</c> has already refused the call if any of them differs. Not
    /// writing them at all is what makes that guard the only thing standing between this path and the
    /// contact — rather than a guard sitting in front of an assignment that would still be correct if
    /// somebody removed it.
    /// </para>
    /// <para>
    /// Only <see cref="BuildFormDetail"/> writes them, and only for a campus being ADDED, which has no
    /// contact yet and no invitation bound to anything.
    /// </para>
    /// </summary>
    public static void ApplyFormDetail(
        VisitInstanceFormDetail detail, CampusVisitEditV2Dto content, System.DateTime now, ulong? actorId)
    {
        detail.DelegationName = content.DelegationName;
        detail.VisitType = content.VisitType;
        detail.VisitTypeOther = content.VisitType == "OTHER" ? content.VisitTypeOther : null;
        detail.Purpose = content.Purpose;
        detail.WorkingContent = content.WorkingContent;
        detail.WorkingLanguage = content.WorkingLanguage;
        detail.TransportationNote = Clean(content.TransportationNote);
        detail.MediaConsentStatus = content.MediaConsentStatus;
        detail.Notes = Clean(content.Notes);
        detail.FormRevision += 1;
        detail.RowVersion += 1;
        detail.UpdatedAt = now;
        detail.UpdatedBy = actorId;
    }

    /// <summary>Builds a fresh form detail for a newly added campus (form_revision starts at 1).</summary>
    public static VisitInstanceFormDetail BuildFormDetail(
        CampusVisitEditV2Dto content, System.DateTime now, ulong? actorId)
        => new()
        {
            DelegationName = content.DelegationName,
            VisitType = content.VisitType,
            VisitTypeOther = content.VisitType == "OTHER" ? content.VisitTypeOther : null,
            Purpose = content.Purpose,
            WorkingContent = content.WorkingContent,
            OperationalContactFullName = content.OperationalContact.FullName,
            OperationalContactOrganization = Clean(content.OperationalContact.Organization),
            OperationalContactJobTitle = content.OperationalContact.JobTitle.Trim(),
            OperationalContactPhone = PhoneNumber.NormalizeOrOriginal(content.OperationalContact.Phone),
            // Patch 5: normalized (trim + lowercase), not Clean()'s trim-only — matches the value
            // Create/StageReplaceMembers persist for the same column on every other write path.
            OperationalContactEmail = VisitRequestFingerprintBuilder.NormalizeEmail(content.OperationalContact.Email),
            WorkingLanguage = content.WorkingLanguage,
            TransportationNote = Clean(content.TransportationNote),
            MediaConsentStatus = content.MediaConsentStatus,
            Notes = Clean(content.Notes),
            FormRevision = 1,
            ApprovalRevision = 1,
            RowVersion = 0,
            CreatedAt = now,
            CreatedBy = actorId,
        };

    // Revision snapshots are NOT written here any more. Each service used to serialize its own
    // anonymous object and they had drifted; there is now exactly one writer,
    // VisitFormRevisionSnapshotBuilder, and every path goes through it.

    public static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
