namespace PEMS.Domain.Constants;

public static class FaqConstants
{
    public static class Type
    {
        public const string AccountAccess = "ACCOUNT_ACCESS";
        public const string VisitRequest = "VISIT_REQUEST";
        public const string DelegationManagement = "DELEGATION_MANAGEMENT";
        public const string LogisticsResource = "LOGISTICS_RESOURCE";
        public const string DocumentMedia = "DOCUMENT_MEDIA";
        public const string NotificationEmail = "NOTIFICATION_EMAIL";
        public const string Other = "OTHER";

        public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
        {
            AccountAccess,
            VisitRequest,
            DelegationManagement,
            LogisticsResource,
            DocumentMedia,
            NotificationEmail,
            Other
        };
    }

    public static class Status
    {
        public const string Published = "PUBLISHED";
        public const string Hidden = "HIDDEN";
    }

    public static string ToVietnameseTypeLabel(string faqType) => faqType switch
    {
        Type.AccountAccess => "Tài khoản và truy cập",
        Type.VisitRequest => "Đăng ký tham quan",
        Type.DelegationManagement => "Quản lý đoàn tiếp khách",
        Type.LogisticsResource => "Hậu cần và tài nguyên",
        Type.DocumentMedia => "Tài liệu và truyền thông",
        Type.NotificationEmail => "Thông báo và email",
        _ => "Khác"
    };

    public static string ToEnglishTypeLabel(string faqType) => faqType switch
    {
        Type.AccountAccess => "Account & Access",
        Type.VisitRequest => "Visit Registration",
        Type.DelegationManagement => "Delegation Management",
        Type.LogisticsResource => "Logistics & Resources",
        Type.DocumentMedia => "Documents & Media",
        Type.NotificationEmail => "Notifications & Email",
        _ => "Other"
    };

    /// <summary>Returns the localised label for <paramref name="faqType"/> in the requested language.
    /// Defaults to Vietnamese for any unrecognised <paramref name="languageCode"/>.</summary>
    public static string ToTypeLabel(string faqType, string? languageCode) =>
        string.Equals(languageCode, "en", StringComparison.OrdinalIgnoreCase)
            ? ToEnglishTypeLabel(faqType)
            : ToVietnameseTypeLabel(faqType);

    public static string ToVietnameseStatusLabel(string status) => status switch
    {
        Status.Published => "Hiển thị",
        Status.Hidden => "Ẩn",
        _ => status
    };
}
