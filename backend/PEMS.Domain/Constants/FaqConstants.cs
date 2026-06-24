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
}
