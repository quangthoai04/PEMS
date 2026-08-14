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
    public const string University = "UNIVERSITY";
    public const string Company = "COMPANY";
    public const string Government = "GOVERNMENT";
    public const string Ngo = "NGO";
    public const string Other = "OTHER";

    public static readonly string[] All = { University, Company, Government, Ngo, Other };

    public static string ToVietnameseLabel(string partnerType) => partnerType switch
    {
        University => "Trường đại học",
        Company => "Doanh nghiệp",
        Government => "Cơ quan nhà nước",
        Ngo => "Tổ chức phi chính phủ",
        _ => "Khác"
    };

    public static string ToEnglishLabel(string partnerType) => partnerType switch
    {
        University => "University",
        Company => "Company",
        Government => "Government",
        Ngo => "NGO",
        _ => "Other"
    };

    /// <summary>Returns the localised label for <paramref name="partnerType"/> in the requested
    /// language. Defaults to Vietnamese for any unrecognised <paramref name="languageCode"/>
    /// (mirrors <c>FaqConstants.ToTypeLabel</c>).</summary>
    public static string ToLabel(string partnerType, string? languageCode) =>
        string.Equals(languageCode, "en", System.StringComparison.OrdinalIgnoreCase)
            ? ToEnglishLabel(partnerType)
            : ToVietnameseLabel(partnerType);
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

    /// <summary>
    /// The registrant picked the partner themselves in the registration form. Distinct from
    /// <see cref="Manual"/> (staff clicked "link" on the minutes screen) and from
    /// <see cref="AutoName"/> (the system inferred it) — recording all three the same way would lose
    /// the only thing that distinguishes a decision from a guess (PART-01/PART-02).
    /// </summary>
    public const string RegistrationSelected = "REGISTRATION_SELECTED";

    public static readonly string[] All =
        { AutoName, AutoEmailDomain, Manual, CreatedFromGuest, BusinessCardOcr, RegistrationSelected };
}

/// <summary>
/// Why a candidate/partner may NOT be linked to a guest. A blocked candidate is still SHOWN
/// (it stops a duplicate profile being created for the same organization) but carries no link
/// action — see <see cref="PartnerLinkRecommendedActions"/> for what the user should do instead.
/// </summary>
public static class PartnerLinkBlockReasons
{
    /// <summary>Profile was rejected by the campus Staff Leader — edit + resubmit, never re-create.</summary>
    public const string Rejected = "PARTNER_REJECTED";
    /// <summary>Profile is still a draft — it has not been submitted for approval yet.</summary>
    public const string Draft = "PARTNER_DRAFT";
    /// <summary>Pending approval and owned by ANOTHER campus — only the owner campus may link it early.</summary>
    public const string PendingOtherCampus = "PARTNER_PENDING_OTHER_CAMPUS";
    /// <summary>Outside the caller's campus/visibility scope.</summary>
    public const string OutOfScope = "PARTNER_OUT_OF_SCOPE";
}

/// <summary>What the UI should offer instead of "Liên kết" for a blocked candidate.</summary>
public static class PartnerLinkRecommendedActions
{
    public const string Link = "LINK";
    public const string Resubmit = "RESUBMIT";
    public const string None = "NONE";
}

public static class PartnerErrorCodes
{
    public const string Forbidden = "PARTNER_FORBIDDEN";
    public const string NotFound = "PARTNER_NOT_FOUND";
    /// <summary>Caller may VIEW the partner but its profile status forbids linking (rejected/draft/pending).</summary>
    public const string NotLinkable = "PARTNER_NOT_LINKABLE";
    /// <summary>Specifically: the profile was rejected. Kept separate so the UI can offer "resubmit".</summary>
    public const string RejectedCannotLink = "PARTNER_REJECTED_CANNOT_LINK";
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
