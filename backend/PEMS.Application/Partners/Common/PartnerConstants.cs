namespace PEMS.Application.Partners.Common;

/// <summary>DB enum values of partners.profile_status (labels are UI-only).</summary>
public static class PartnerProfileStatuses
{
    public const string Draft = "DRAFT";
    public const string PendingApproval = "PENDING_APPROVAL";
    public const string Approved = "APPROVED";
    public const string Rejected = "REJECTED";

    public static readonly string[] All = { Draft, PendingApproval, Approved, Rejected };
}

/// <summary>DB enum values of partners.visibility.</summary>
public static class PartnerVisibilities
{
    public const string Private = "PRIVATE";
    public const string Internal = "INTERNAL";
    public const string Public = "PUBLIC";

    public static readonly string[] All = { Private, Internal, Public };
}

/// <summary>DB enum values of partners.partner_type.</summary>
public static class PartnerTypes
{
    public static readonly string[] All = { "UNIVERSITY", "COMPANY", "GOVERNMENT", "NGO", "OTHER" };
}

/// <summary>DB enum values of visit_guest_partner_links.match_status.</summary>
public static class PartnerLinkMatchStatuses
{
    public const string Suggested = "SUGGESTED";
    public const string Confirmed = "CONFIRMED";
    public const string Rejected = "REJECTED";
}

/// <summary>DB enum values of visit_guest_partner_links.match_source.</summary>
public static class PartnerLinkMatchSources
{
    public const string AutoName = "AUTO_NAME";
    public const string AutoEmailDomain = "AUTO_EMAIL_DOMAIN";
    public const string Manual = "MANUAL";
    public const string CreatedFromGuest = "CREATED_FROM_GUEST";
    public const string BusinessCardOcr = "BUSINESS_CARD_OCR";

    public static readonly string[] All = { AutoName, AutoEmailDomain, Manual, CreatedFromGuest, BusinessCardOcr };
}

public static class PartnerErrorCodes
{
    public const string Forbidden = "PARTNER_FORBIDDEN";
    public const string NotFound = "PARTNER_NOT_FOUND";
    public const string CodeDuplicated = "PARTNER_CODE_DUPLICATED";
    public const string NameDuplicated = "PARTNER_NAME_DUPLICATED";
    public const string PublicRequiresApproved = "PARTNER_PUBLIC_REQUIRES_APPROVED";
    public const string NotPending = "PARTNER_NOT_PENDING_APPROVAL";
    public const string RejectReasonRequired = "PARTNER_REJECT_REASON_REQUIRED";
    public const string WrongCampus = "PARTNER_WRONG_CAMPUS";
    public const string ContactNotFound = "PARTNER_CONTACT_NOT_FOUND";
    public const string ContactEmailDuplicated = "PARTNER_CONTACT_EMAIL_DUPLICATED";
    public const string AliasDuplicated = "PARTNER_ALIAS_DUPLICATED";
    public const string AliasNotFound = "PARTNER_ALIAS_NOT_FOUND";
    public const string LinkTargetRequired = "PARTNER_LINK_TARGET_REQUIRED";
    public const string LinkNotFound = "PARTNER_LINK_NOT_FOUND";
}
