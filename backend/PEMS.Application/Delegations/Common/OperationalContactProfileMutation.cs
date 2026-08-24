using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Services;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Entities.Users;
using PEMS.Shared;

namespace PEMS.Application.Delegations.Common;

/// <summary>
/// The ONE implementation of "correct a same-person operational contact's details" — shared by
/// <c>UpdateOperationalContactProfileCommandHandler</c> (the standalone command) and
/// <c>VisitSafeEditService</c>'s contact branch (plan CanhIter3FixBug), so the two write doors cannot
/// drift apart on normalization, audit-field semantics, or the pending-invitation snapshot refresh.
///
/// <para>
/// Deliberately does NOT touch <c>OperationalContactGuestMemberId</c>, <c>OperationalContactUserId</c>,
/// <c>OperationalContactEmail</c>, or any revision counter — correcting somebody's spelling does not
/// change which person it is, and this type has no opinion on relation or identity at all.
/// </para>
/// </summary>
public static class OperationalContactProfileMutation
{
    private static readonly JsonSerializerOptions Json =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    /// <summary>The four editable fields, trimmed/normalized the same way regardless of caller.</summary>
    public readonly record struct NormalizedProfile(
        string FullName, string Organization, string JobTitle, string? Phone);

    /// <param name="organization">
    /// Required by the validator like FullName/JobTitle — a blank value never reaches here. Not marked
    /// nullable in the signature would force every caller to null-forgive it anyway, so this keeps the
    /// same shape the two existing call sites already use.
    /// </param>
    public static NormalizedProfile Normalize(
        string fullName, string? organization, string jobTitle, string? phone)
        => new(
            fullName.Trim(),
            organization!.Trim(),
            jobTitle.Trim(),
            string.IsNullOrWhiteSpace(phone) ? null : PhoneNumber.NormalizeOrOriginal(phone.Trim()));

    /// <summary>
    /// Field-by-field, and only what actually moved — an audit entry that lists four rows every time
    /// cannot be read for what somebody changed. Returns whether anything did.
    /// </summary>
    public static bool AddProfileChanges(
        AuditLog audit, VisitInstanceFormDetail detail, NormalizedProfile profile, DateTime now)
    {
        var before = audit.Changes.Count;
        AddChange(audit, "operational_contact_full_name", detail.OperationalContactFullName, profile.FullName, now);
        AddChange(audit, "operational_contact_organization", detail.OperationalContactOrganization, profile.Organization, now);
        AddChange(audit, "operational_contact_job_title", detail.OperationalContactJobTitle, profile.JobTitle, now);
        AddChange(audit, "operational_contact_phone", detail.OperationalContactPhone, profile.Phone, now);
        return audit.Changes.Count > before;
    }

    /// <summary>Writes the four fields onto <paramref name="detail"/>. Nothing else.</summary>
    public static void Apply(VisitInstanceFormDetail detail, NormalizedProfile profile)
    {
        detail.OperationalContactFullName = profile.FullName;
        detail.OperationalContactOrganization = profile.Organization;
        detail.OperationalContactJobTitle = profile.JobTitle;
        detail.OperationalContactPhone = profile.Phone;
    }

    /// <summary>
    /// The pending-invitation snapshot payload, built the ONE way both write doors need it — shared so
    /// neither can drift on which fields it carries.
    /// </summary>
    public static string BuildPendingSnapshotJson(VisitInstanceFormDetail detail, string invitedEmailNormalized)
        => JsonSerializer.Serialize(new
        {
            fullName = detail.OperationalContactFullName,
            organization = detail.OperationalContactOrganization,
            jobTitle = detail.OperationalContactJobTitle,
            phone = detail.OperationalContactPhone,
            email = invitedEmailNormalized,
        }, Json);

    /// <summary>
    /// Keeps a PENDING invitation's stored snapshot in step with the campus, without touching anything
    /// that governs its lifetime — the simple case (no response view needed), used by
    /// <c>VisitSafeEditService</c>'s contact branch. <c>UpdateOperationalContactProfileCommandHandler</c>
    /// does its own single lock (it also needs the pending row to build its response's pending-change
    /// view) and calls <see cref="BuildPendingSnapshotJson"/> directly instead of this wrapper, so the
    /// row is locked exactly once per call there.
    ///
    /// <para>
    /// The row is taken FOR UPDATE and re-checked for PENDING, so an invitation that expired or was
    /// answered between the read and here is left alone. <c>expires_at</c>, <c>token_version</c> and
    /// <c>resend_count</c> are not assigned at all — a correction is not a resend.
    /// </para>
    /// </summary>
    public static async Task RefreshPendingInvitationSnapshotAsync(
        IOperationalContactInvitationService invitations,
        VisitRequestCampus instance, VisitInstanceFormDetail detail, CancellationToken ct)
    {
        var pending = await invitations.LockPendingChangeForInstanceAsync(instance.VisitInstanceId, ct);
        if (pending is null || pending.Status != IdentityChangeStatuses.Pending)
            return;

        // Only when the invitation is about THIS address. A pending TRANSFER is an invitation to a
        // DIFFERENT person, whose details are the transfer's own and none of this correction's business.
        var invited = pending.NewEmailNormalized;
        if (string.IsNullOrEmpty(invited)
            || !string.Equals(
                invited,
                VisitRequestFingerprintBuilder.NormalizeEmail(detail.OperationalContactEmail),
                StringComparison.Ordinal))
            return;

        pending.PendingSnapshotJson = BuildPendingSnapshotJson(detail, invited);
    }

    private static void AddChange(
        AuditLog audit, string field, string? oldValue, string? newValue, DateTime now)
    {
        var before = oldValue ?? string.Empty;
        var after = newValue ?? string.Empty;
        if (string.Equals(before, after, StringComparison.Ordinal))
            return;

        audit.Changes.Add(new AuditLogChange
        {
            FieldName = field,
            OldValueText = oldValue,
            NewValueText = newValue,
            CreatedAt = now,
        });
    }
}
