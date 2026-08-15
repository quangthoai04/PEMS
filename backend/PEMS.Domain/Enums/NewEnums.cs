namespace PEMS.Shared;

public static class CreatedSource
{
    public const string VisitorSubmitted = "VISITOR_SUBMITTED";
    public const string StaffCreated = "STAFF_CREATED";
}

public static class VisitType
{
    public const string CampusTour = "CAMPUS_TOUR";
    public const string Meeting = "MEETING";
    public const string Workshop = "WORKSHOP";
    public const string SigningCeremony = "SIGNING_CEREMONY";
    public const string Exchange = "EXCHANGE";
    public const string Other = "OTHER";
}

public static class MediaConsentStatus
{
    public const string Agreed = "AGREED";
    public const string Declined = "DECLINED";
}

public static class GuestMemberType
{
    public const string Guest = "GUEST";
    public const string ExternalSupport = "EXTERNAL_SUPPORT";
}

/// <summary>
/// Whether a <c>minute_participants</c> row is part of the biên bản or has been set aside (MIN-03).
///
/// <para>Removing a source-linked person used to delete the row outright, so the next "đồng bộ người
/// mới" saw somebody still on the official delegation/participant list and added them straight back.
/// The Host's decision had nowhere to live. An EXCLUDED row does not appear in the biên bản and is
/// not offered by sync, but it still exists — which is what makes it restorable, and what makes the
/// removal survive a save.</para>
/// </summary>
public static class MinuteParticipantSyncStates
{
    public const string Active = "ACTIVE";
    public const string Excluded = "EXCLUDED";
}

public static class FaqType
{
    // PEMS v10: FAQ grouped by system functional area (Vietnamese-only, no language_code).
    public const string AccountAccess = "ACCOUNT_ACCESS";
    public const string VisitRequest = "VISIT_REQUEST";
    public const string DelegationManagement = "DELEGATION_MANAGEMENT";
    public const string LogisticsResource = "LOGISTICS_RESOURCE";
    public const string DocumentMedia = "DOCUMENT_MEDIA";
    public const string NotificationEmail = "NOTIFICATION_EMAIL";
    public const string Other = "OTHER";

    public static readonly string[] All =
    {
        AccountAccess, VisitRequest, DelegationManagement,
        LogisticsResource, DocumentMedia, NotificationEmail, Other,
    };
}

public static class FaqStatus
{
    public const string Published = "PUBLISHED";
    public const string Hidden = "HIDDEN";
}

public static class LanguageCode
{
    public const string Vi = "vi";
    public const string En = "en";
}

public static class GalleryMediaType
{
    public const string Image = "IMAGE";
    public const string Video = "VIDEO";
}

public static class RecipientType
{
    public const string To = "TO";
    public const string Cc = "CC";
    public const string Bcc = "BCC";
}

public static class DeliveryStatus
{
    public const string Queued = "QUEUED";
    public const string Sent = "SENT";
    public const string Delivered = "DELIVERED";
    public const string Failed = "FAILED";
    public const string Bounced = "BOUNCED";
}

public static class ApiTestStatus
{
    public const string NotTested = "NOT_TESTED";
    public const string Success = "SUCCESS";
    public const string Failed = "FAILED";
}

public static class ReminderType
{
    public const string Popup = "POPUP";
    public const string Email = "EMAIL";
    public const string Notification = "NOTIFICATION";
}

public static class ReminderStatus
{
    public const string Pending = "PENDING";
    public const string Sent = "SENT";
    public const string Failed = "FAILED";
    public const string Cancelled = "CANCELLED";
}

public static class ResponseStatus
{
    public const string NeedsAction = "NEEDS_ACTION";
    public const string Accepted = "ACCEPTED";
    public const string Declined = "DECLINED";
    public const string Tentative = "TENTATIVE";
}
