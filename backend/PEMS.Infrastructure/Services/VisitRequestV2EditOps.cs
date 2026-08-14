using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using PEMS.Application.Common.DTOs;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Common;
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

        var rows = new List<VisitGuestMember>();
        uint order = 1;
        foreach (var v in visitors ?? new List<VisitorDto>())
            rows.Add(new VisitGuestMember
            {
                FullName = v.FullName, Organization = v.Organization, JobTitle = v.JobTitle,
                Nationality = v.Nationality, MemberType = "GUEST", DisplayOrder = order++,
                CreatedAt = now, CreatedBy = actorId,
            });
        foreach (var m in support ?? new List<SupportTeamMemberDto>())
            rows.Add(new VisitGuestMember
            {
                FullName = m.FullName, Organization = m.Organization, JobTitle = m.JobTitle,
                Nationality = m.Nationality, MemberType = "EXTERNAL_SUPPORT", DisplayOrder = order++,
                CreatedAt = now, CreatedBy = actorId,
            });
        foreach (var r in rows) request.GuestMembers.Add(r);
        return rows;
    }

    /// <summary>Phase 2 — create the composite links for the staged member rows (ids resolved after flush #1).</summary>
    public static void LinkMembers(
        IApplicationDbContext db, VisitRequest request, VisitRequestCampus instance,
        IReadOnlyList<VisitGuestMember> newRows, System.DateTime now, ulong? actorId)
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
        // problem. The contact SNAPSHOT is immutable through this path (EnsureContactSnapshotUnchanged
        // refuses any change to it), so matching it against the new rows re-finds the same person.
        if (instance.FormDetail is not null)
            OperationalContactLink.Resolve(instance.FormDetail, newRows);
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
            OperationalContactEmail = Clean(content.OperationalContact.Email)!,
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
