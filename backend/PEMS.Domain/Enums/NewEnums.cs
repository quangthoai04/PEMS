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

public static class TransportationType
{
    public const string SelfArranged = "SELF_ARRANGED";
    public const string FptuSupport = "FPTU_SUPPORT";
    public const string Unknown = "UNKNOWN";
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

public static class FaqType
{
    public const string Program = "PROGRAM";
    public const string TuitionFee = "TUITION_FEE";
    public const string Visa = "VISA";
    public const string Dormitory = "DORMITORY";
    public const string VisitRequest = "VISIT_REQUEST";
    public const string Security = "SECURITY";
    public const string Logistics = "LOGISTICS";
    public const string Other = "OTHER";
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
