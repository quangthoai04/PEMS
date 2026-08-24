using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using PEMS.Domain.Entities.Delegations;

namespace PEMS.Infrastructure.Services;

/// <summary>
/// THE writer of revision snapshots. Every path that appends to
/// <c>visit_instance_form_revision_history</c> or <c>visit_request_revision_history</c> — create,
/// pending-edit, resubmit, safe-edit, amendment-applied — serializes through here and nowhere else.
///
/// <para>
/// It exists because they did not. Each service serialized its own anonymous object, and the objects
/// had drifted apart: the create path omitted <c>operationalContactJobTitle</c> entirely, so the very
/// first revision of every request recorded no job title, and the next edit's diff duly reported
/// "(trống) → Trưởng phòng" for a field that had been filled in from the start. A history that invents
/// a change is worse than one that misses it, because people act on it.
/// </para>
/// <para>
/// The shape is deliberately FLAT and camelCase, which is what every row already written contains: a
/// new nesting would have made every existing snapshot legacy overnight for no gain, since the reader
/// has to normalize old shapes either way.
/// </para>
/// </summary>
internal static class VisitFormRevisionSnapshotBuilder
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    /// <summary>One campus's content as it stands after an edit: form detail, schedule and members.</summary>
    public static string Instance(
        VisitRequestCampus instance, VisitInstanceFormDetail d, IEnumerable<VisitGuestMember> members)
        => JsonSerializer.Serialize(new
        {
            d.DelegationName,
            d.VisitType,
            d.VisitTypeOther,
            d.Purpose,
            d.WorkingContent,
            d.WorkingLanguage,
            d.MediaConsentStatus,
            d.TransportationNote,
            d.Notes,
            d.OperationalContactFullName,
            d.OperationalContactOrganization,
            d.OperationalContactJobTitle,
            d.OperationalContactPhone,
            d.OperationalContactEmail,
            // WHICH delegation member (if any) the contact IS — plan CanhIter3FixBug "Đầu mối hiện tại
            // có nằm trong danh sách đoàn không?". Missing from every snapshot before this fix, exactly
            // like the schedule fields below used to be: an amendment could move this relationship (via
            // a member-list replace OR, now, on its own) and the history would show the member list and
            // the free-text profile snapshot but never say who among them was the contact.
            d.OperationalContactGuestMemberId,
            // The schedule belongs to the campus row rather than the form detail, and was missing from
            // every snapshot — so a pending edit that moved a visit by a day left no trace of the day
            // it used to be on.
            instance.PlannedStartAt,
            instance.PlannedEndAt,
            Members = members
                .Select(m => new {
                    m.FullName, m.Organization, m.JobTitle, m.Nationality, m.MemberType, m.DisplayOrder,
                    // WHICH row this is, in the database's own terms — without it, nothing reading this
                    // snapshot back can resolve OperationalContactGuestMemberId to a name: it names a
                    // member by id, and the member list used to have no ids at all to match against.
                    m.GuestMemberId,
                })
                .ToList(),
        }, Json);

    /// <summary>
    /// The request-level block: the registrant, and only the registrant. Each campus's contact is
    /// recorded in that campus's own snapshot above — there is no request-level contact to capture.
    /// </summary>
    public static string Request(VisitRequest r)
        => JsonSerializer.Serialize(new
        {
            r.RegistrantFullName, r.RegistrantOrganization, r.RegistrantJobTitle,
            // Nationality is part of the registrant snapshot an edit may now change, so it has to be
            // recorded or its edits would leave no trace. Rows written before it was added simply do
            // not carry the key, and the differ reports that as "not recorded" rather than as a change
            // from blank — see DiffSnapshots' BeforeUnknown handling.
            r.RegistrantNationality,
            r.RegistrantPhone, r.RegistrantEmail,
        }, Json);
}
